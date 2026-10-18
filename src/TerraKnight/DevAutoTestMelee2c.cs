using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "melee2c", sweep S1.C): the swords that shoot, on an enemy held in place: the
    /// Frostbrand's bolts and Frostbite, the Beam Sword's beams (and their bounces), the Ice and
    /// Death Sickles' scythes, the Chlorophyte Claymore's slash, slams, spores and ground bursts,
    /// the Flying Dragon's sonic booms; close-ups of each.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Melee2cTest(string scene, HeroController hero)
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
            var tp = TerraProjectiles.Instance;
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
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 近战2c：场景里没有敌人，跳过");
            }
            else
            {
                var trb = target.GetComponent<Rigidbody2D>();
                var near = C() + Vector2.right * side * 3f;
                void Hold() { target.hp = Mathf.Max(target.hp, 3000); Pin(target, trb, near); }
                IEnumerator SwingFor(ItemType item, float seconds, Vector2 aim, string pic = null, float picAt = 0.3f)
                {
                    Take(item);
                    mod.DevHold = false;
                    bool pictured = pic == null;
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        if (target == null || target.isDead) yield break;
                        Hold();
                        if (mod.DevReady) mod.DevUseSelected(aim);
                        if (!pictured && t >= picAt)
                        {
                            pictured = true;
                            yield return CloseUp(scene, pic, hero, 150, 2);
                        }
                        yield return null;
                    }
                }
                void Reset()
                {
                    tp.DevClear();
                    EnemyDebuffs.Clear();
                    MeleeSpecials.DevHits.Clear();
                }
                int Swung(ItemType t) => MeleeSpecials.DevHits.TryGetValue(t, out int n) ? n : 0;
                string Shots(ProjKind k, int m0, int h0) => $"射出 {TerraProjectiles.DevMade(k) - m0}，命中 {TerraProjectiles.DevHit(k) - h0}";

                foreach (var (item, kind, label, pic) in new[]
                {
                    (ItemType.Frostbrand, ProjKind.FrostbrandBolt, "寒霜剑", "m2c_frostbrand"),
                    (ItemType.BeamSword, ProjKind.BeamSwordBeam, "光束剑", "m2c_beamsword"),
                    (ItemType.IceSickle, ProjKind.IceSickleShot, "冰雪镰刀", "m2c_icesickle"),
                    (ItemType.DeathSickle, ProjKind.DeathSickleShot, "死神镰刀", "m2c_deathsickle"),
                    (ItemType.DD2SquireBetsySword, ProjKind.SonicBoom, "飞龙", "m2c_dragon"),
                })
                {
                    Reset();
                    int m0 = TerraProjectiles.DevMade(kind), h0 = TerraProjectiles.DevHit(kind);
                    yield return SwingFor(item, 2.2f, near, pic, 0.25f);
                    Plugin.Log.LogInfo($"[AutoTest] 近战2c {label}：挥砍命中 {Swung(item)}，{Shots(kind, m0, h0)}，冻伤={EnemyDebuffs.Has(target, EnemyDebuffs.Frostbite)}");
                }

                // The Beam Sword's beam off a wall: aimed away from the enemy, it comes back after bouncing.
                Reset();
                int b0 = TerraProjectiles.DevMade(ProjKind.BeamSwordBeam);
                Take(ItemType.BeamSword);
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(C() - Vector2.right * side * 4f);
                float alive = 0f;
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                {
                    if (tp.DevCount(ProjKind.BeamSwordBeam) > 0) alive = t;
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 近战2c 光束剑 反弹：射出 {TerraProjectiles.DevMade(ProjKind.BeamSwordBeam) - b0}，存活约 {alive:F2} 秒（撞墙反弹 3 次，第 4 次消失）");

                // The Chlorophyte Claymore: the slash, a slam every three uses' worth of time, spores, the ground burst.
                Reset();
                SwordFx.ClaymoreCounter = 0;
                int slams0 = SwordFx.DevSlams, bursts0 = SwordFx.DevSporeBursts;
                int s0 = TerraProjectiles.DevMade(ProjKind.ClaymoreSpore), sh0 = TerraProjectiles.DevHit(ProjKind.ClaymoreSpore), k0 = TerraProjectiles.DevHit(ProjKind.ClaymoreSlam);
                yield return SwingFor(ItemType.ChlorophyteClaymore, 4f, near, "m2c_claymore", 0.4f);
                Plugin.Log.LogInfo($"[AutoTest] 近战2c 叶绿双刃刀：猛击 {SwordFx.DevSlams - slams0}（每 3 次挥动的时间一次），地面爆发 {SwordFx.DevSporeBursts - bursts0}，" +
                                   $"孢子 {TerraProjectiles.DevMade(ProjKind.ClaymoreSpore) - s0}（地面孢子命中 {TerraProjectiles.DevHit(ProjKind.ClaymoreSpore) - sh0}），猛击冲击命中 {TerraProjectiles.DevHit(ProjKind.ClaymoreSlam) - k0}");
                yield return CloseUp(scene, "m2c_spores", hero, 150, 2);
            }

            mod.DevHold = null;
            tp.DevClear();
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
