using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "melee2d", sweep S1.D): the shortswords' stabs and the rapiers' bursts (three,
    /// four and five stabs a use, scattered) on an enemy held in place; close-ups of a few.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Melee2dTest(string scene, HeroController hero)
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
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            Wear();
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            void Take(ItemType t)
            {
                inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(t);
                inv.Selected = Inventory.HotbarSize - 1;
            }
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float dd = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (dd < best) { best = dd; target = hm; }
            }
            var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            var near = C() + Vector2.right * side * 1.2f;
            foreach (var (item, type, pic) in new[]
            {
                (ItemType.IronShortsword, 940, (string)null), (ItemType.TinShortsword, 939, null), (ItemType.LeadShortsword, 941, null), (ItemType.SilverShortsword, 942, null),
                (ItemType.TungstenShortsword, 943, null), (ItemType.GoldShortsword, 944, null), (ItemType.PlatinumShortsword, 945, "m2d_platinum"),
                (ItemType.Gladius, 802, "m2d_gladius"), (ItemType.Ruler, 842, "m2d_ruler"), (ItemType.ChlorophyteSaber, 1127, "m2d_saber"),
            })
            {
                Take(item);
                mod.DevHold = false;
                SwordFx.DevStabs.Clear();
                SwordFx.DevHits.Clear();
                while (!mod.DevReady) yield return null;
                // One use, the button let go: a shortsword stabs once, the rapiers once every useTime of it.
                mod.DevAim = near;
                mod.DevUseSelected(near);
                bool pictured = pic == null;
                for (int f = 0; f < 3; f++) yield return null;
                if (pic != null)
                    Plugin.Log.LogInfo($"[AutoTest] 近战2d 朝向检查：敌人在 {(side > 0f ? "右" : "左")}，使用方向 {mod.UseDirection}，使用中={mod.IsUsingItem}，刺的方向 {SwordFx.Instance.StabDirection}，主角面朝右={hero.cState.facingRight}");
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    if (target != null) { target.hp = Mathf.Max(target.hp, 3000); Pin(target, trb, near); }
                    if (!pictured && t > 0.12f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 70, 3);
                    }
                    yield return null;
                }
                mod.DevAim = null;
                int stabs = SwordFx.DevStabs.TryGetValue(type, out int s) ? s : 0, hits = SwordFx.DevHits.TryGetValue(type, out int h) ? h : 0;
                Plugin.Log.LogInfo($"[AutoTest] 近战2d {ItemDb.Get(item).Name}：一次使用刺 {stabs}（原版 {Mathf.Max(1, Mathf.RoundToInt(ItemDb.Get(item).UseAnimation / ItemDb.Get(item).UseTime))}），命中 {hits}");
            }
            // The held blades: held 1.5 s at the enemy.
            var tp = TerraProjectiles.Instance;
            foreach (var (item, kind, pic) in new[]
            {
                (ItemType.Arkhalis, ProjKind.ArkhalisSlash, "m2d_arkhalis"), (ItemType.Terragrim, ProjKind.TerragrimSlash, "m2d_terragrim"),
                (ItemType.PiercingStarlight, ProjKind.PiercingStarlight, "m2d_starlight"),
            })
            {
                Take(item);
                tp.DevClear();
                int h0 = TerraProjectiles.DevHit(kind);
                while (!mod.DevReady) yield return null;
                mod.DevAim = near;
                mod.DevHold = true;
                mod.DevUseSelected(near);
                bool pictured = false;
                int most = 0;
                for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                {
                    if (target != null) { target.hp = Mathf.Max(target.hp, 3000); Pin(target, trb, near); }
                    most = Mathf.Max(most, tp.DevCount(kind));
                    if (!pictured && t > 0.5f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 110, 2);
                    }
                    yield return null;
                }
                mod.DevHold = false;
                for (int f = 0; f < 5; f++) yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 近战2d {ItemDb.Get(item).Name}：按住 1.5 秒 同时最多 {most} 个，命中 {TerraProjectiles.DevHit(kind) - h0}（Arkhalis/泰拉魔刃每 5 帧一下，星光每 4 帧），松开后剩 {tp.DevCount(kind)}");
                mod.DevAim = null;
            }
            // The True Copper Shortsword: three copper shortswords thrown a use.
            Take(ItemType.TrueCopperShortsword);
            while (!mod.DevReady) yield return null;
            mod.DevAim = near;
            mod.DevHold = false;
            mod.DevUseSelected(near);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                if (target != null) { target.hp = Mathf.Max(target.hp, 3000); Pin(target, trb, near); }
                yield return null;
            }
            yield return CloseUp(scene, "m2d_truecopper", hero, 160, 2);
            mod.DevAim = null;
            mod.DevHold = null;
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
