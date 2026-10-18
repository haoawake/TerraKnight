using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "melee2f", sweep S1.F): the boomerangs thrown at an enemy held in place and
    /// back (the Trimarang three out, the Wrench home off its first hit, the shield out of
    /// targets), the Vampire Knives' spread and healing orbs, the Shadowflame Knife's spring
    /// back, the Flying Knife steered and let go, and the Flint charged and released.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Melee2fTest(string scene, HeroController hero)
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
            var tp = TerraProjectiles.Instance;
            void Hold(Vector2 at)
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 3000);
                Pin(target, trb, at);
            }
            var far = C() + Vector2.right * side * 3f;
            // The boomerangs: one throw each (the Trimarang held for three), until they're back.
            foreach (var (item, kind, uses, pic) in new[]
            {
                (ItemType.IceBoomerang, ProjKind.IceBoomerangProj, 1, "m2f_ice"), (ItemType.FruitcakeChakram, ProjKind.FruitcakeChakramProj, 1, (string)null),
                (ItemType.BloodyMachete, ProjKind.BloodyMacheteProj, 1, "m2f_machete"), (ItemType.Shroomerang, ProjKind.ShroomerangProj, 1, null),
                (ItemType.Trimarang, ProjKind.TrimarangProj, 4, "m2f_trimarang"), (ItemType.Axearang, ProjKind.AxearangProj, 1, null),
                (ItemType.CombatWrench, ProjKind.CombatWrenchProj, 1, "m2f_wrench"), (ItemType.BouncingShield, ProjKind.BouncingShieldProj, 1, "m2f_shield"),
            })
            {
                Take(item);
                tp.DevClear();
                mod.DevHold = false;
                while (!mod.DevReady) yield return null;
                int h0 = TerraProjectiles.DevHit(kind), m0 = TerraProjectiles.DevMade(kind), l0 = TerraProjectiles.DevShieldLeaps;
                mod.DevAim = far;
                int thrown = 0, most = 0;
                bool pictured = pic == null;
                float t = 0f;
                for (; t < 4f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (thrown < uses && mod.DevReady)
                    {
                        mod.DevUseSelected(far);
                        thrown++;
                    }
                    most = Mathf.Max(most, tp.DevCount(kind));
                    if (!pictured && t > 0.15f && tp.DevCount(kind) > 0)
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 130, 2);
                    }
                    if (thrown >= uses && t > 0.3f && tp.DevCount(kind) == 0) break;
                    yield return null;
                }
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 近战2f {ItemDb.Get(item).Name}：扔 {thrown} 次 生成 {TerraProjectiles.DevMade(kind) - m0}，同时最多 {most}，命中 {TerraProjectiles.DevHit(kind) - h0}，" +
                    $"{t:F2} 秒后全部回来={tp.DevCount(kind) == 0}" + (item == ItemType.BouncingShield ? $"，跳向下一个 {TerraProjectiles.DevShieldLeaps - l0} 次" : "") +
                    (item == ItemType.IceBoomerang && target != null ? $"，霜火={EnemyDebuffs.Has(target, EnemyDebuffs.Frostburn)}" : "") +
                    (item == ItemType.BloodyMachete && target != null ? $"，流血={EnemyDebuffs.Has(target, EnemyDebuffs.Bleeding)}" : ""));
            }
            // The Sergeant United Shield raised on the right button while it's in hand and not out.
            Take(ItemType.BouncingShield);
            for (int f = 0; f < 4; f++) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 近战2f 中士联盾：手持未扔出时可举盾={MeleeSpecials.BouncingShieldInHand}");
            // The knives.
            foreach (var (item, kind, pic) in new[]
            {
                (ItemType.VampireKnives, ProjKind.VampireKnife, "m2f_vampire"), (ItemType.ShadowFlameKnife, ProjKind.ShadowFlameKnifeProj, "m2f_shadowflame"),
            })
            {
                Take(item);
                tp.DevClear();
                mod.DevHold = false;
                while (!mod.DevReady) yield return null;
                int h0 = TerraProjectiles.DevHit(kind), m0 = TerraProjectiles.DevMade(kind), o0 = TerraProjectiles.DevVampireOrbs, b0 = TerraProjectiles.DevKnifeBounces;
                TerraProjectiles.DevVampireHealed = 0;
                mod.DevAim = far;
                int uses = 0;
                bool pictured = false;
                for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (uses < 3 && mod.DevReady)
                    {
                        mod.DevUseSelected(far);
                        uses++;
                    }
                    if (!pictured && t > 0.1f && tp.DevCount(kind) > 0)
                    {
                        pictured = true;
                        yield return CloseUpAt(scene, pic, (C() + far) / 2f, 130, 2);
                    }
                    yield return null;
                }
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 近战2f {ItemDb.Get(item).Name}：用 {uses} 次 飞刀 {TerraProjectiles.DevMade(kind) - m0}（吸血鬼刀每次 4-8），命中 {TerraProjectiles.DevHit(kind) - h0}，" +
                    $"吸血球 {TerraProjectiles.DevVampireOrbs - o0} 回血 {TerraProjectiles.DevVampireHealed}，弹回 {TerraProjectiles.DevKnifeBounces - b0}" +
                    (item == ItemType.ShadowFlameKnife && target != null ? $"，暗影焰={EnemyDebuffs.Has(target, EnemyDebuffs.Shadowflame)}" : ""));
            }
            // The Flying Knife: held at the enemy 1.2 s, then let go.
            Take(ItemType.FlyingKnife);
            tp.DevClear();
            while (!mod.DevReady) yield return null;
            int fk0 = TerraProjectiles.DevHit(ProjKind.FlyingKnifeProj);
            mod.DevAim = far;
            mod.DevHold = true;
            mod.DevUseSelected(far);
            bool fkPic = false;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                Hold(far);
                if (!fkPic && t > 0.5f)
                {
                    fkPic = true;
                    yield return CloseUpAt(scene, "m2f_flyingknife", far, 120, 2);
                }
                yield return null;
            }
            int heldHits = TerraProjectiles.DevHit(ProjKind.FlyingKnifeProj) - fk0;
            mod.DevHold = false;
            float back = 0f;
            for (; back < 3f && tp.DevCount(ProjKind.FlyingKnifeProj) > 0; back += Time.deltaTime)
            {
                Hold(far);
                yield return null;
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 近战2f 飞刀：按住 1.2 秒命中 {heldHits}，松开时 {TerraProjectiles.DevFlyingKnife}，{back:F2} 秒后飞回手里={tp.DevCount(ProjKind.FlyingKnifeProj) == 0}");
            // The Flint: a quick tap (a cut, no spikes), then charged a second (a cut and the spikes).
            Take(ItemType.DeadCellsFlint);
            var near = C() + Vector2.right * side * 1.4f;
            foreach (var (charge, pic) in new[] { (0.1f, (string)null), (1f, "m2f_flint") })
            {
                tp.DevClear();
                while (!mod.DevReady || tp.DevCount(ProjKind.FlintHold) > 0) yield return null;
                int c0 = TerraProjectiles.DevFlintCuts, s0 = TerraProjectiles.DevFlintSpikes, sl0 = SwordFx.DevFlintSlashes;
                int sh0 = TerraProjectiles.DevHit(ProjKind.FlintSpike);
                mod.DevAim = near;
                mod.DevHold = true;
                mod.DevUseSelected(near);
                for (float t = 0f; t < charge; t += Time.deltaTime)
                {
                    Hold(near);
                    yield return null;
                }
                mod.DevHold = false;
                bool pictured = pic == null;
                for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                {
                    Hold(near);
                    if (!pictured && t > 0.12f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 150, 2);
                    }
                    yield return null;
                }
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 近战2f 燧石（蓄力 {charge:F1} 秒）：斩击 {TerraProjectiles.DevFlintCuts - c0}/{SwordFx.DevFlintSlashes - sl0}，地刺 {TerraProjectiles.DevFlintSpikes - s0}（满 0.5 秒才出），地刺命中 {TerraProjectiles.DevHit(ProjKind.FlintSpike) - sh0}");
            }
            mod.DevHold = null;
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
