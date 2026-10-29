using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "zenitharts", Items.TerraProjectilesMonkArts / ZenithArts / BladeSkillsZenith, the user's 10-26 "继续升级剩下的
    /// 这些…特效，动画，粒子效果，新颖，多样"): in Dirtmouth by its Target Dummy. Each monk staff used again and again for 5 s: all
    /// four of its techniques come in turn, the dummy is struck, a remade technique's head reaches at least 25% further than
    /// the game's whirl; its damage a second against the game's (the option off) within 0.85 to 1.45; its right-click art
    /// (the Octopod's march of slams, the Glaive's ghasts, the Fury's dash and bolts). The Zenith: five uses fly its five
    /// patterns, each striking; its damage a second against the game's; the right button held gathers sixteen swords, let go
    /// they strike for 1.05 to 1.3 times its damage a second over the charge and the flight. Shots Town_zenitharts_*. TK_ZENITH_ONLY narrows it (MonkStaffT1, MonkStaffT2, MonkStaffT3, Zenith).
    /// Lines "[AutoTest] 天顶 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ZenithArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 天顶 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            // The area's title gone from over the shots.
            yield return new WaitForSeconds(6f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Check(false, "德特茅斯的训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var aim = at.Value + new Vector2(0f, 0.8f);
            var inv = mod.Inventory;
            var saved = inv.Slots[0];
            int savedSel = inv.Selected;
            var only = new List<string>((System.Environment.GetEnvironmentVariable("TK_ZENITH_ONLY") ?? "").Split(','));
            bool Wanted(ItemType t) => only.Count == 0 || only[0].Length == 0 || only.Contains(t.ToString());

            void Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
            }

            void Hand(ItemType type)
            {
                inv.Slots[0] = ItemStack.Of(type);
                inv.Selected = 0;
            }

            // Used over and over for that long (each use as the last ends), the button held; the techniques named as they begin.
            IEnumerator Keep(ItemType type, float seconds, List<string> names, string shots)
            {
                Hand(type);
                mod.DevAim = aim;
                mod.DevHold = true;
                float start = Time.time, nextShot = start + 0.3f;
                int seen = TerraProjectiles.DevStaffMoves, patterns = ZenithSwords.DevPatterns, shot = 0;
                while (Time.time - start < seconds)
                {
                    if (!mod.IsUsingItem && mod.DevReady) mod.DevUse(type, aim);
                    if (names != null && TerraProjectiles.DevStaffMoves != seen) { seen = TerraProjectiles.DevStaffMoves; names.Add(TerraProjectiles.DevStaffMove); }
                    if (names != null && ZenithSwords.DevPatterns != patterns) { patterns = ZenithSwords.DevPatterns; names.Add(ZenithSwords.DevPattern); }
                    if (shots != null && Time.time >= nextShot && shot < 12) { Shot("Town", $"zenitharts_{shots}_{shot++:00}"); nextShot += 0.33f; }
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
                yield return new WaitForSeconds(1.2f);
            }

            IEnumerator Dps(ItemType type, float[] dps)
            {
                for (int mode = 0; mode < 2; mode++)
                {
                    BladeArts.Set(mode == 1);
                    TerraProjectiles.DevStaffReset();
                    ZenithSwords.DevPatternReset();
                    Stand(2f);
                    yield return new WaitForSeconds(0.8f);
                    int dmg0 = TargetDummy.DevDamage;
                    float start = Time.time;
                    yield return Keep(type, 5f, null, null);
                    dps[mode] = (TargetDummy.DevDamage - dmg0) / (Time.time - start);
                }
                BladeArts.Set(true);
            }

            foreach (var type in new[] { ItemType.MonkStaffT1, ItemType.MonkStaffT2, ItemType.MonkStaffT3 })
            {
                if (!Wanted(type)) continue;
                var def = ItemDb.Get(type);
                TerraProjectiles.DevStaffReset();
                Stand(2f);
                yield return new WaitForSeconds(0.8f);
                var names = new List<string>();
                int hits0 = TargetDummy.DevHits;
                yield return Keep(type, 5f, names, type.ToString());
                int hits = TargetDummy.DevHits - hits0;
                var distinct = new HashSet<string>(names);
                float far = TerraProjectiles.DevStaffFar, vanilla = type == ItemType.MonkStaffT1 ? 73f : type == ItemType.MonkStaffT2 ? 95f : 110f;
                Check(distinct.Count >= 4 && hits >= 6 && far >= vanilla * 1.25f, $"{def.Name}：技法 {string.Join(" → ", names)}（{distinct.Count} 种），命中 {hits}，最远 {far:F0} px（原版 {vanilla:F0}）");

                float[] dps = new float[2];
                yield return Dps(type, dps);
                float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                Check(ratio >= 0.85f && ratio <= 1.45f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新版 {dps[1]:F0}（×{ratio:F2}）");

                // Its right-click art.
                Hand(type);
                bool charged = type != ItemType.MonkStaffT2;
                Stand(type == ItemType.MonkStaffT3 ? 4.5f : 2.5f);
                mod.DevAim = aim;
                yield return new WaitForSeconds(1f);
                int dmgA = TargetDummy.DevDamage, slams0 = TerraProjectiles.DevQuakeSlams, ghasts0 = TerraProjectiles.DevGhasts, bolts0 = TerraProjectiles.DevSkyBolts, dashes0 = TerraProjectiles.DevStaffDashes;
                float x0 = hero.transform.position.x;
                BladeSkills.DevLastArt = "";
                BladeSkills.DevHeld = true;
                BladeSkills.DevPress = true;
                yield return new WaitForSeconds(charged ? 1.15f : 0.1f);
                BladeSkills.DevHeld = false;
                yield return new WaitForSeconds(0.25f);
                Shot("Town", $"zenitharts_art_{type}_1");
                yield return new WaitForSeconds(0.3f);
                Shot("Town", $"zenitharts_art_{type}_2");
                yield return new WaitForSeconds(1.2f);
                BladeSkills.DevHeld = null;
                mod.DevAim = null;
                int artDmg = TargetDummy.DevDamage - dmgA;
                float moved = hero.transform.position.x - x0;
                int slams = TerraProjectiles.DevQuakeSlams - slams0, ghasts = TerraProjectiles.DevGhasts - ghasts0, bolts = TerraProjectiles.DevSkyBolts - bolts0;
                bool shape = type == ItemType.MonkStaffT1 ? slams >= 6 : type == ItemType.MonkStaffT2 ? ghasts >= 4 : TerraProjectiles.DevStaffDashes > dashes0 && moved > 2f && bolts >= 3;
                Check(BladeSkills.DevLastArt.Length > 0 && artDmg > 0 && shape,
                    $"右键 {def.Name}：{BladeSkills.DevLastArt}，假人受到 {artDmg}，地裂 {slams}，鬼魂 {ghasts}，龙息弹 {bolts}，人移动 {moved:F1}");
                yield return new WaitForSeconds(2.5f);
            }

            if (Wanted(ItemType.Zenith))
            {
                var def = ItemDb.Get(ItemType.Zenith);
                ZenithSwords.DevPatternReset();
                ZenithSwords.DevHitsBy.Clear();
                Stand(5f);
                yield return new WaitForSeconds(0.8f);
                var names = new List<string>();
                int hits0 = TargetDummy.DevHits;
                yield return Keep(ItemType.Zenith, 3.2f, names, "Zenith");
                var distinct = new HashSet<string>(names);
                int hits = TargetDummy.DevHits - hits0;
                Check(distinct.Count >= 5 && hits >= 10, $"天顶剑：飞法 {string.Join(" → ", names)}（{distinct.Count} 种），命中 {hits}");
                ZenithSwords.DevHitsBy.Clear();

                float[] dps = new float[2];
                yield return Dps(ItemType.Zenith, dps);
                float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                var by = new List<string>();
                foreach (var kv in ZenithSwords.DevHitsBy) by.Add($"{kv.Key} {kv.Value}");
                Check(ratio >= 0.85f && ratio <= 1.3f, $"天顶剑 每秒伤害：原版 {dps[0]:F0}，新版 {dps[1]:F0}（×{ratio:F2}）；命中按飞法（两轮合计）：{string.Join("，", by)}");

                Hand(ItemType.Zenith);
                Stand(5f);
                mod.DevAim = aim;
                yield return new WaitForSeconds(1f);
                int dmgA = TargetDummy.DevDamage, gathered0 = ZenithSwords.DevGathered, loosed0 = ZenithSwords.DevLoosed;
                BladeSkills.DevHeld = true;
                BladeSkills.DevPress = true;
                yield return new WaitForSeconds(1.3f);
                Shot("Town", "zenitharts_art_Zenith_gathered");
                yield return null;
                BladeSkills.DevHeld = false;
                float loosedAt = Time.time;
                // The volley converging, a shot every 0.12 s.
                for (int k = 1; k <= 6; k++)
                {
                    yield return new WaitForSeconds(0.12f);
                    Shot("Town", $"zenitharts_art_Zenith_{k}");
                }
                yield return new WaitForSeconds(1.08f);
                BladeSkills.DevHeld = null;
                mod.DevAim = null;
                int artDmg = TargetDummy.DevDamage - dmgA;
                int gathered = ZenithSwords.DevGathered - gathered0, loosed = ZenithSwords.DevLoosed - loosed0;
                float artRate = artDmg / (Time.time - loosedAt + 1.3f);
                float artRatio = dps[0] > 0f ? artRate / dps[0] : 0f;
                Check(gathered >= 16 && loosed >= 16 && artRatio >= 1.05f && artRatio <= 1.3f,
                    $"右键 天顶剑：{BladeSkills.DevLastArt}，聚剑 {gathered}，射出 {loosed}，假人受到 {artDmg}（蓄力加飞行每秒 {artRate:F0}，原版每秒 {dps[0]:F0}，×{artRatio:F2}）");
            }
            inv.Slots[0] = saved;
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 天顶 合计 {ok}/{all}");
        }
    }
}
