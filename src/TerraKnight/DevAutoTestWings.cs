using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "wings"): every new pair of wings flown from the ground for a moment (rise,
    /// top climb speed against the slot's cap, flight time left, a close-up in the air); the
    /// hover (down + jump) of the Hoverboard and the Starboard; the jetpack's UP; the Soaring
    /// Insignia's endless flight; the new boots' rockets and run trails.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WingsTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedAcc = (ItemStack[])inv.Accessories.Clone();

            IEnumerator Fly(ItemType[] wear, string name, float seconds, bool down = false, bool up = false)
            {
                Wear(wear);
                yield return Settle(hero, home);
                var w = TerraMovement.Wing;
                float start = col.bounds.min.y, top = start, maxVy = 0f, minVy = 0f;
                TerraMovement.DevJump = true;
                TerraMovement.DevDown = down;
                TerraMovement.DevUp = up;
                TerraMovement.DevGroundJump(hero);
                bool pictured = false;
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    top = Mathf.Max(top, col.bounds.min.y);
                    float vy = rb.linearVelocity.y / TerraMovement.PxT;
                    if (t > 0.3f) { maxVy = Mathf.Max(maxVy, vy); minVy = Mathf.Min(minVy, vy); }
                    if (!pictured && t > seconds * 0.6f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, "w_" + name, hero, 110, 3);
                    }
                    yield return null;
                }
                float left = TerraMovement.WingTimeLeft;
                TerraMovement.DevJump = false;
                TerraMovement.DevDown = false;
                TerraMovement.DevUp = false;
                Plugin.Log.LogInfo($"[AutoTest] 翅膀 {name}：槽 {(w != null ? w.Slot : 0)}，飞行 {seconds:F1}s 升高 {(top - start) / Pxu:F0}px，最快上升 {maxVy:F2}（上限 {(w != null ? w.Num3 * 5.01f : 0f):F2}），最快下落 {-minVy:F2}，剩余飞行时间 {left:F0}/{(w != null ? w.FlyTime : 0)}，帧 {TerraMovement.WingFrame}");
                yield return Land(hero);
            }

            foreach (var w in new[]
            {
                ItemType.ButterflyWings, ItemType.FairyWings, ItemType.FlameWings, ItemType.FrozenWings, ItemType.GhostWings, ItemType.SteampunkWings,
                ItemType.FestiveWings, ItemType.BeetleWings, ItemType.FishronWings, ItemType.MothronWings, ItemType.WingsSolar, ItemType.WingsVortex,
                ItemType.WingsNebula, ItemType.WingsStardust, ItemType.JimsWings, ItemType.LokisWings, ItemType.BetsyWings, ItemType.ArkhalisWings,
                ItemType.GroxTheGreatWings, ItemType.RainbowWings, ItemType.ChickenBonesWings, ItemType.KazzymodusWings, ItemType.LunasWings,
                ItemType.GhostarsWings, ItemType.LeinforsWings, ItemType.SafemanWings, ItemType.Jetpack, ItemType.BejeweledValkyrieWing, ItemType.Hoverboard,
                ItemType.LongRainbowTrailWings,
            })
                yield return Fly(new[] { w }, w.ToString(), 0.9f);

            // The hover: down + jump flies at once and holds the height, at half the time standing still.
            yield return Fly(new[] { ItemType.Hoverboard }, "Hoverboard_hover", 1.2f, down: true);
            yield return Fly(new[] { ItemType.LongRainbowTrailWings }, "Starboard_hover", 1.2f, down: true);
            // The jetpack's UP: three times jump speed at most, twice the time.
            yield return Fly(new[] { ItemType.Jetpack }, "Jetpack_up", 1f, up: true);
            // The Soaring Insignia: flight never runs out.
            yield return Fly(new[] { ItemType.AngelWings, ItemType.EmpressFlightBooster }, "Angel_insignia", 1.5f);

            // The new boots: their rocket flight and the run trail.
            // The run on a long open stretch somewhere in the scene, so the trail shows.
            bool open = OpenRun(hero, 22f, out var runAt, out float runDir, (Vector2)home);
            foreach (var boots in new[] { ItemType.FrostsparkBoots, ItemType.TerrasparkBoots, ItemType.HellfireTreads, ItemType.FairyBoots })
            {
                Wear(boots);
                yield return Settle(hero, home);
                if (open)
                {
                    hero.transform.position = Clear(hero, runAt);
                    rb.linearVelocity = Vector2.zero;
                    for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null;
                }
                // A head start past the trail's speed ((accRunSpeed + maxRunSpeed) / 2), which the boots' slow last stretch takes seconds to reach.
                rb.linearVelocity = new Vector2((open ? runDir : 1f) * 5.5f * TerraMovement.PxT, 0f);
                TerraMovement.DevMove = open ? (int)runDir : 1;
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) yield return null;
                yield return CloseUp(scene, "b_run_" + boots, hero, 120, 3);
                TerraMovement.DevMove = 0;
                yield return Settle(hero, home);
                TerraMovement.DevJump = true;
                TerraMovement.DevGroundJump(hero);
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
                yield return CloseUp(scene, "b_rocket_" + boots, hero, 120, 3);
                TerraMovement.DevJump = false;
                Plugin.Log.LogInfo($"[AutoTest] 靴子 {ItemDb.Get(boots).Name}：火箭种类 {TerraMovement.RocketKind}，跑动尘 {TerraMovement.RunDust}，跑速上限 {TerraMovement.AccRunSpeed}");
                yield return Land(hero);
            }

            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            mod.DevNoFallDamage = false;
        }
    }
}
