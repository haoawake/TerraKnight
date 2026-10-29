using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "airarts", the user's 10-26 "那些右键效果改一下能在空中生效，不要只在地面"): in Dirtmouth by its Target Dummy,
    /// the arts that went by the ground, used off it. The greatswords charged on the ground and let go a few tiles up: a plunge,
    /// the blow on landing striking the dummy; the ice spikes and thorns pressed a few tiles up: out of the ground under him,
    /// striking it; the Frostbrand's far above any ground in reach: fanned out from him; the Flying Dragon's fire let go in the
    /// air: sent off level. Lines "[AutoTest] 空中右键 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AirArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 空中右键 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Check(false, "德特茅斯的训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var inv = mod.Inventory;
            IEnumerator Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x + dx, at.Value.y - bottom + 0.05f));
                rb.linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSeconds(0.6f);
            }
            void Lift(float up)
            {
                var p = hero.transform.position + Vector3.up * up;
                hero.transform.position = p;
                rb.position = p;
                rb.linearVelocity = Vector2.zero;
            }
            mod.DevAim = at.Value + new Vector2(0f, 0.8f);

            // The greatswords: charged on the ground, let go three tiles up.
            foreach (var type in new[] { ItemType.BreakerBlade, ItemType.FieryGreatsword, ItemType.ChlorophyteClaymore })
            {
                inv.Slots[0] = ItemStack.Of(type);
                inv.Selected = 0;
                yield return Stand(-2.5f);
                int plunges0 = BladeSkills.DevPlunges, hits0 = TargetDummy.DevHits;
                BladeSkills.DevHeld = true;
                BladeSkills.DevPress = true;
                yield return new WaitForSeconds(1.2f);
                Lift(1.5f);
                // The game marks him off the ground on its next physics step.
                for (float t = Time.time + 0.3f; Time.time < t && hero.cState.onGround;) yield return null;
                bool airborne = !hero.cState.onGround;
                BladeSkills.DevHeld = false;
                yield return new WaitForSeconds(1.6f);
                BladeSkills.DevHeld = null;
                Check(BladeSkills.DevPlunges > plunges0 && TargetDummy.DevHits > hits0,
                    $"{ItemDb.Get(type).Name} 空中松开：下砸 {BladeSkills.DevPlunges - plunges0}，假人挨了 {TargetDummy.DevHits - hits0} 下（松开时离地 {airborne}）");
            }

            // The spikes and thorns three tiles up: out of the ground under him.
            foreach (var type in new[] { ItemType.IceBlade, ItemType.BladeofGrass })
            {
                inv.Slots[0] = ItemStack.Of(type);
                yield return Stand(-3f);
                int hits0 = TargetDummy.DevHits, uses0 = BladeSkills.DevUses;
                Lift(1.5f);
                yield return null;
                BladeSkills.DevPress = true;
                yield return new WaitForSeconds(1.2f);
                Check(BladeSkills.DevUses > uses0 && TargetDummy.DevHits > hits0, $"{ItemDb.Get(type).Name} 空中：用出 {BladeSkills.DevUses - uses0}，假人挨了 {TargetDummy.DevHits - hits0} 下");
            }

            // Far above any ground in reach: fanned out from him.
            inv.Slots[0] = ItemStack.Of(ItemType.Frostbrand);
            yield return Stand(-3f);
            int air0 = BladeSkills.DevAirPosts, used0 = BladeSkills.DevUses;
            Lift(12f);
            yield return null;
            BladeSkills.DevPress = true;
            yield return new WaitForSeconds(0.25f);
            Shot("Town", "airarts_frostbrand_high");
            yield return new WaitForSeconds(0.5f);
            Check(BladeSkills.DevUses > used0 && BladeSkills.DevAirPosts > air0, $"寒霜剑高空：用出 {BladeSkills.DevUses - used0}，从身上扇形放出 {BladeSkills.DevAirPosts - air0} 根冰刺");
            for (float t = Time.time + 6f; Time.time < t && !hero.cState.onGround;) yield return null;

            // The dragon fire let go in the air.
            inv.Slots[0] = ItemStack.Of(ItemType.DD2SquireBetsySword);
            yield return Stand(-4f);
            int waves0 = BladeSkills.DevAirWaves;
            BladeSkills.DevHeld = true;
            BladeSkills.DevPress = true;
            yield return new WaitForSeconds(1.3f);
            Lift(3f);
            for (float t = Time.time + 0.3f; Time.time < t && hero.cState.onGround;) yield return null;
            BladeSkills.DevHeld = false;
            yield return new WaitForSeconds(0.3f);
            Shot("Town", "airarts_dragon");
            yield return new WaitForSeconds(0.6f);
            BladeSkills.DevHeld = null;
            Check(BladeSkills.DevAirWaves > waves0, $"飞龙空中松开：平飞的龙焰 {BladeSkills.DevAirWaves - waves0}");
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 空中右键 合计 {ok}/{all}");
        }
    }
}
