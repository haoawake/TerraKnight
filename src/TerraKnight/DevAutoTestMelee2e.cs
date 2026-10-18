using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "melee2e", sweep S1.E): the plain spears' thrusts, the Scourge of the
    /// Corruptor's javelin bursting into tiny eaters, and Daybreak: javelins stuck in an enemy
    /// held in place (eight at most, the oldest going off in a solar blast), Daybroken burning.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Melee2eTest(string scene, HeroController hero)
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
            // The plain spears: one thrust each at an enemy just in reach.
            var near = C() + Vector2.right * side * 1.6f;
            foreach (var (item, kind, pic) in new[]
            {
                (ItemType.Spear, ProjKind.SpearThrust, "m2e_spear"), (ItemType.TheRottedFork, ProjKind.RottedForkThrust, "m2e_fork"),
                (ItemType.Swordfish, ProjKind.SwordfishThrust, (string)null), (ItemType.ObsidianSwordfish, ProjKind.ObsidianSwordfishThrust, "m2e_obsidian"),
                (ItemType.SlimeSpear, ProjKind.SlimeSpearThrust, "m2e_slime"),
            })
            {
                Take(item);
                mod.DevHold = false;
                while (!mod.DevReady) yield return null;
                int h0 = TerraProjectiles.DevHit(kind);
                mod.DevAim = near;
                mod.DevUseSelected(near);
                bool pictured = pic == null;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    Hold(near);
                    if (!pictured && t > 0.15f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 80, 3);
                    }
                    yield return null;
                }
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 近战2e {ItemDb.Get(item).Name}：一次刺出 生成 {TerraProjectiles.DevMade(kind)} 命中 {TerraProjectiles.DevHit(kind) - h0}" +
                    (item == ItemType.SlimeSpear && target != null ? $"，敌人涂黏={EnemyDebuffs.Has(target, EnemyDebuffs.Slimed)}" : ""));
            }
            // The Scourge of the Corruptor: one javelin at an enemy a little way off, then the eaters' hunt.
            var far = C() + Vector2.right * side * 4f;
            Take(ItemType.ScourgeoftheCorruptor);
            while (!mod.DevReady) yield return null;
            TerraProjectiles.DevEaters = 0;
            int j0 = TerraProjectiles.DevHit(ProjKind.ScourgeJavelin), e0 = TerraProjectiles.DevHit(ProjKind.TinyEater);
            mod.DevAim = far;
            mod.DevUseSelected(far);
            bool shot = false, flying = false;
            int mostEaters = 0;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                Hold(far);
                mostEaters = Mathf.Max(mostEaters, tp.DevCount(ProjKind.TinyEater));
                if (!flying && tp.DevCount(ProjKind.ScourgeJavelin) > 0 && t > 0.04f)
                {
                    flying = true;
                    yield return CloseUpAt(scene, "m2e_scourge_fly", (C() + far) / 2f, 110, 2);
                }
                if (!shot && tp.DevCount(ProjKind.TinyEater) > 0 && t > 0.25f)
                {
                    shot = true;
                    yield return CloseUpAt(scene, "m2e_scourge", far + Vector2.down * 0.6f, 160, 2);
                }
                yield return null;
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 近战2e 腐化者之戟：标枪命中 {TerraProjectiles.DevHit(ProjKind.ScourgeJavelin) - j0}，放出小吞噬怪 {TerraProjectiles.DevEaters}（原版 2-4，百分之一 15），同时最多 {mostEaters}，吞噬怪命中 {TerraProjectiles.DevHit(ProjKind.TinyEater) - e0}");
            // Daybreak: twelve throws at an enemy held a few tiles off.
            Take(ItemType.DayBreak);
            tp.DevClear();
            TerraProjectiles.DevDaybreakStuck = 0;
            TerraProjectiles.DevBlasts = 0;
            int d0 = TerraProjectiles.DevHit(ProjKind.Daybreak), b0 = TerraProjectiles.DevHit(ProjKind.DaybreakBlast);
            int mostStuck = 0, throws = 0;
            bool burning = false, stuckPic = false, blastPic = false;
            mod.DevAim = far;
            for (float t = 0f; t < 4.5f; t += Time.deltaTime)
            {
                Hold(far);
                if (throws < 12 && mod.DevReady)
                {
                    mod.DevUseSelected(far);
                    throws++;
                }
                if (target != null)
                {
                    mostStuck = Mathf.Max(mostStuck, tp.StuckDaybreaks(target));
                    burning |= EnemyDebuffs.Has(target, EnemyDebuffs.Daybroken);
                }
                if (!stuckPic && target != null && tp.StuckDaybreaks(target) >= 5)
                {
                    stuckPic = true;
                    yield return CloseUpAt(scene, "m2e_daybreak", far + Vector2.down * 0.5f, 140, 2);
                }
                if (!blastPic && tp.DevCount(ProjKind.DaybreakBlast) > 0)
                {
                    blastPic = true;
                    yield return CloseUpAt(scene, "m2e_blast", far + Vector2.down * 0.5f, 140, 2);
                }
                yield return null;
            }
            int left = target != null ? tp.StuckDaybreaks(target) : 0;
            // Five seconds after the last went in, every one has come out.
            for (float t = 0f; t < 5.5f; t += Time.deltaTime)
            {
                Hold(far);
                yield return null;
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 近战2e 破晓之光：投出 {throws}，钉入 {TerraProjectiles.DevDaybreakStuck}，同时钉住最多 {mostStuck}（原版至多 8），破晓燃烧={burning}，" +
                $"直接命中 {TerraProjectiles.DevHit(ProjKind.Daybreak) - d0}，爆炸 {TerraProjectiles.DevBlasts} 次 命中 {TerraProjectiles.DevHit(ProjKind.DaybreakBlast) - b0}；投完时钉着 {left}，5.5 秒后剩 {(target != null ? tp.StuckDaybreaks(target) : 0)}");
            // The monk staffs: the whirls held through, the thrusts one use each, the Fury's thrust on the right button.
            var close = C() + Vector2.right * side * 2f;
            foreach (var (item, kind, alt, hold, pic) in new[]
            {
                (ItemType.MonkStaffT1, ProjKind.MonkStaffT1Spin, false, true, "m2e_octopod"), (ItemType.MonkStaffT2, ProjKind.MonkStaffT2, false, false, "m2e_glaive"),
                (ItemType.MonkStaffT3, ProjKind.MonkStaffT3Spin, false, true, "m2e_fury"), (ItemType.MonkStaffT3, ProjKind.MonkStaffT3Thrust, true, false, "m2e_furythrust"),
            })
            {
                Take(item);
                tp.DevClear();
                TerraProjectiles.DevMonkSlams = TerraProjectiles.DevGhasts = TerraProjectiles.DevSkyBolts = TerraProjectiles.DevSkyFields = 0;
                while (!mod.DevReady) yield return null;
                var counted = new[] { kind, ProjKind.MonkStaffT1Slam, ProjKind.MonkStaffT2Ghast, ProjKind.SkyDragonBolt, ProjKind.SkyDragonField };
                var h0 = new int[counted.Length];
                for (int k = 0; k < counted.Length; k++) h0[k] = TerraProjectiles.DevHit(counted[k]);
                mod.DevAim = close;
                mod.DevHold = hold;
                mod.DevUseSelected(close, alt);
                bool pictured = false, extraPic = false;
                float life = 0f;
                for (float t = 0f; t < 1.6f; t += Time.deltaTime)
                {
                    Hold(close);
                    if (tp.DevCount(kind) > 0) life = t;
                    if (!pictured && t > (hold ? 0.3f : 0.2f))
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 130, 2);
                    }
                    if (!extraPic && (tp.DevCount(ProjKind.MonkStaffT2Ghast) > 0 || tp.DevCount(ProjKind.SkyDragonField) > 0) && t > 0.35f)
                    {
                        extraPic = true;
                        yield return CloseUpAt(scene, pic + "_fx", close, 110, 2);
                    }
                    yield return null;
                }
                mod.DevHold = false;
                mod.DevAim = null;
                for (int f = 0; f < 5; f++) yield return null;
                var hits = new int[counted.Length];
                for (int k = 0; k < counted.Length; k++) hits[k] = TerraProjectiles.DevHit(counted[k]) - h0[k];
                Plugin.Log.LogInfo($"[AutoTest] 近战2e {ItemDb.Get(item).Name}{(alt ? "（右键）" : "")}：持续 {life:F2} 秒，本体命中 {hits[0]}，砸地 {TerraProjectiles.DevMonkSlams} 次 命中 {hits[1]}，" +
                    $"鬼魂 {TerraProjectiles.DevGhasts} 命中 {hits[2]}，龙弹 {TerraProjectiles.DevSkyBolts} 命中 {hits[3]}，电场 {TerraProjectiles.DevSkyFields} 命中 {hits[4]}，结束后剩 {tp.DevCount(kind)}");
            }
            mod.DevHold = null;
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
