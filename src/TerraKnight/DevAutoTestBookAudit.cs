using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bookaudit"): the monster book against the kills (the user's, 2026-10-20: Grimm's page listed the Last
    /// Prism, which the tent's Grimm never dropped). Normal difficulty, at each stage: a boss's page (the area the book
    /// opens it on) its world fight's (the area of its arena), the same drops as a kill in its arena; then kills rolled into
    /// EnemyDrops.DevSink there, a boss's in its arena and every other enemy's in each of its areas: an item the book gives
    /// at 0.5% or more that never came, or came at under a third of it, and an item that came at 2% or more the book lacks.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BookAuditTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int checks = 0, failures = 0, savedStage = Shop.DevStage;
            var difficulty = GameDifficulty.Current;
            var sink = EnemyDrops.DevSink;
            var random = UnityEngine.Random.state;
            string search = InventoryGui.DevBookSearch;
            var came = new Dictionary<ItemType, int>();
            bool manipulatorGiven=EssentialDrops.ManipulatorGiven;
            void Fail(string what)
            {
                failures++;
                if (failures <= 400) Plugin.Log.LogError("[AutoTest] 图鉴对照 " + what + " 不通过");
            }
            // A page's items by how often the book says one comes (a kill's chance of it: its rows' together).
            Dictionary<ItemType, float> Listed(IReadOnlyList<EnemyDrops.Drop> rows, int stage, bool boss)
            {
                var d = new Dictionary<ItemType, float>();
                foreach (var r in rows)
                {
                    if (r.Master || EnemyDrops.Locked(r, stage)) continue;
                    // A boss's first-kill crystal is paid apart, after the fight (EnemyDrops.Roll leaves it); the Ancient
                    // Manipulator comes once a save (the first kill of the rolls takes it).
                    if (boss && r.Type == ItemType.LifeCrystal || r.Type == ItemType.LunarCraftingStation) continue;
                    if(EnemyDrops.SingleQuantity(ItemDb.Get(r.Type)))d[r.Type]=EnemyDrops.ItemChance(rows,r.Type,stage);
                    else{
                        float p = EnemyDrops.MarginalChance(rows, r, stage);
                        d[r.Type] = (d.TryGetValue(r.Type, out var q) ? q : 0f) + p;
                    }
                }
                return d;
            }
            // Kills there against the page: what never came or came far less, what came that the page lacks.
            IEnumerator Compare(string what, string rec, string scene, IReadOnlyList<EnemyDrops.Drop> page, int stage, bool boss, int kills)
            {
                var listed = Listed(page, stage, boss);
                if(listed.Any(kv=>kv.Value>=.005f && kv.Value<.04f))kills=Math.Max(kills,1000);
                came.Clear();
                bool first = Bestiary.KillsOf(rec) == 0;
                for (int i = 0; i < kills; i++)
                {
                    EnemyDrops.Roll(rec, Vector2.zero, true, first, scene: scene);
                    if (i % 200 == 199) yield return null;
                }
                // A few hundred trials give unstable tails for rare items. Confirm any
                // suspicious result with a larger sample before reporting a real mismatch.
                bool suspicious=listed.Any(kv=>{
                    int got=came.TryGetValue(kv.Key,out var value)?value:0;
                    float expected=kv.Value*kills,p=Mathf.Min(1f,kv.Value);
                    float spread=5f*Mathf.Sqrt(kills*p*(1f-p))+3f;
                    return (kv.Value>=.005f || got>expected+spread)
                        && (got==0&&expected>=3f || Mathf.Abs(got-expected)>spread);
                });
                if(suspicious && kills<4000){
                    Plugin.Log.LogInfo("[AutoTest] 图鉴对照 扩大抽样确认 "+what+" 至4000次");
                    for(int i=kills;i<4000;i++){
                        EnemyDrops.Roll(rec,Vector2.zero,true,first,scene:scene);
                        if(i%200==199)yield return null;
                    }
                    kills=4000;
                }
                foreach (var kv in listed)
                {
                    int got = came.TryGetValue(kv.Key, out var g) ? g : 0;
                    float expected = kv.Value * kills, p = Mathf.Min(1f, kv.Value);
                    // Never coming, or off by more than five sigmas either way (a sure drop: every kill, give or take two).
                    float spread = 5f * Mathf.Sqrt(kills * p * (1f - p)) + 3f;
                    if (kv.Value < 0.005f && got <= expected + spread) continue;
                    checks++;
                    if (got == 0 && expected >= 3f || Mathf.Abs(got - expected) > spread)
                        Fail($"{what} {ItemDb.Get(kv.Key).Name} 图鉴 {kv.Value * 100f:0.##}% 实际 {got}/{kills}");
                }
                foreach (var kv in came)
                {
                    // A boss's own geo, which its page shows apart (EnemyDrops.BossGeo).
                    if (boss && kv.Key == ItemType.Geo && EnemyDrops.BossGeo(rec, scene) > 0) continue;
                    if (listed.ContainsKey(kv.Key) || kv.Value < kills * 0.02f) continue;
                    checks++;
                    Fail($"{what} {ItemDb.Get(kv.Key).Name} 图鉴没写 实际 {kv.Value}/{kills}");
                }
            }
            try
            {
                GameDifficulty.Current = DifficultyLevel.Normal;
                InventoryGui.DevBookSearch = "";
                UnityEngine.Random.InitState(20261020);
                EnemyDrops.DevSink = (t, n) => came[t] = (came.TryGetValue(t, out var c) ? c : 0) + 1;
                var records = EnemyDrops.DevRecords.Where(r => EnemyDrops.KindOf(r) != "non_enemy" && EnemyDrops.KindOf(r) != "player_shade").OrderBy(r => r, StringComparer.Ordinal).ToList();
                for (int stage = 0; stage <= World.Areas.MaxLevel; stage++)
                {
                    Shop.DevStage = stage;
                    int before = failures;
                    foreach (var rec in records)
                    {
                        var homes = BestiaryHabitats.Of(rec);
                        if (EnemyDrops.IsBoss(rec))
                        {
                            string arena = BossArena.SceneOf(rec), world = BestiaryHabitats.WorldScene(rec);
                            string fight = arena != null && !World.Areas.GodhomeRoom(arena) ? arena : world ?? arena ?? EnemyDrops.SceneOf(rec);
                            string home = InventoryGui.DevBookHome(rec);
                            checks++;
                            if (world != null && World.Areas.AreaName(home) != World.Areas.AreaName(fight))
                                Fail($"阶段{stage} {Bestiary.NameOf(rec)} 图鉴默认 {World.Areas.AreaName(home)}（{home}），实战在 {World.Areas.AreaName(fight)}（{fight}）");
                            var page = InventoryGui.DevBestiaryDrops(rec);
                            var real = EnemyDrops.In(rec, fight);
                            var a = Listed(page, stage, true);
                            var b = Listed(real, stage, true);
                            checks++;
                            var onlyBook = a.Keys.Where(t => !b.ContainsKey(t)).Select(t => ItemDb.Get(t).Name).ToList();
                            var onlyReal = b.Keys.Where(t => !a.ContainsKey(t)).Select(t => ItemDb.Get(t).Name).ToList();
                            if (onlyBook.Count > 0 || onlyReal.Count > 0)
                                Fail($"阶段{stage} {Bestiary.NameOf(rec)} 图鉴多 [{string.Join("、", onlyBook)}] 少 [{string.Join("、", onlyReal)}]");
                            // A Godhome-only boss's kills there bring Godhome's bonus too (GodhomeRewards, the page's note).
                            if (!World.Areas.GodhomeRoom(fight))
                                yield return Compare($"阶段{stage} {Bestiary.NameOf(rec)} @{fight}", rec, fight, page, stage, true, 400);
                            // Its Godhome page: the lunar weapons once at the last stage, none before.
                            foreach (var h in homes)
                            {
                                if (!World.Areas.GodhomeRoom(h.Scene)) continue;
                                var gg = Listed(InventoryGui.DevBookDropsIn(rec, h.Scene), stage, true);
                                bool lunar = ProgressionBalance.LunarWeapons.Any(gg.ContainsKey);
                                if (rec != "Zote" && ProgressionBalance.FormalBoss(rec))
                                {
                                    checks++;
                                    if (lunar != GodhomeRewards.LunarFight(rec,true,stage)) Fail($"阶段{stage} {Bestiary.NameOf(rec)} 神居页 月总武器 {(lunar ? "有" : "没有")}");
                                }
                            }
                        }
                        else
                        {
                            foreach (var h in homes)
                                yield return Compare($"阶段{stage} {Bestiary.NameOf(rec)} @{h.Area}", rec, h.Scene, InventoryGui.DevBookDropsIn(rec, h.Scene), stage, false, 250);
                        }
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 图鉴对照 阶段{stage} 完成，累计检查 {checks}，本阶段失败数 {failures - before}");
                }
                Shop.DevStage = 5;
                Plugin.Log.LogInfo($"[AutoTest] 图鉴对照 格林 阶段5：默认 {World.Areas.AreaName(InventoryGui.DevBookHome("Grimm"))}，"
                    + $"终极棱镜 {(InventoryGui.DevBestiaryDrops("Grimm").Any(d => d.Type == ItemType.LastPrism) ? "有" : "没有")}；地区 {string.Join("、", BestiaryHabitats.Of("Grimm").Select(h => h.Area + "/" + h.Scene))}");
            }
            finally
            {
                Shop.DevStage = savedStage;
                GameDifficulty.Current = difficulty;
                EnemyDrops.DevSink = sink;
                UnityEngine.Random.state = random;
                InventoryGui.DevBookSearch = search;
                if(manipulatorGiven)mod.GrantedAbilities.Add(EssentialDrops.ManipulatorKey);else mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"book-drop-audit.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{Checks=checks,Failures=failures,Scope="All records/areas and stages 0-5, normal difficulty; statistical tests cover listed rates >=0.5%, >=1000 trials for rare-item tables and 4000-trial confirmation for suspicious results. Once-only manipulator tested separately."},Newtonsoft.Json.Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 图鉴对照 检查 {checks}，失败数 {failures} {(failures == 0 ? "通过" : "不通过")}");
        }
    }
}
