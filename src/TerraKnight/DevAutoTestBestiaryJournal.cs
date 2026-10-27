using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BestiaryJournalTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int savedStage = Shop.DevStage;
            bool creative = mod.Creative, catalogue = InventoryGui.DevBookCatalogue;
            bool freeMaking = mod.Inventory.FreeMaking, infinite = mod.Inventory.Infinite;
            var savedClears = VanillaBossProgression.DevClears;
            var difficulty = GameDifficulty.Current;
            bool hadMiner = Bestiary.Kills.TryGetValue("ZombieMiner", out int minerKills);
            int checks = 0, failures = 0;
            var evidence = new Dictionary<string, object>();
            void Check(string label, bool pass)
            {
                checks++;
                if (!pass) { failures++; Plugin.Log.LogError($"[AutoTest] 手记 {label} 不通过"); }
            }
            IEnumerator Show(string rec, string query)
            {
                InventoryGui.DevBookSearch = query;
                InventoryGui.DevBestiaryShow(rec);
                yield return new WaitForSecondsRealtime(.25f);
                float deadline = Time.realtimeSinceStartup + 50f;
                while (InventoryGui.DevSearchPending && Time.realtimeSinceStartup < deadline) yield return null;
                Check("搜索在期限内结束/" + rec, !InventoryGui.DevSearchPending);
                InventoryGui.DevBestiaryShow(rec);
                yield return new WaitForSecondsRealtime(.45f);
            }
            try
            {
                yield return GoTo("Town", "left1");
                GameDifficulty.Current = DifficultyLevel.Normal;
                mod.ApplyCreative(false);
                mod.Inventory.FreeMaking = mod.Inventory.Infinite = false;
                Shop.DevStage = 2;
                VanillaBossProgression.DevClears = new HashSet<string>();
                Bestiary.Kills["ZombieMiner"] = 1;
                InventoryGui.DevBookCatalogue = false;
                InventoryGui.Open = true;
                PointerOn(new Vector2(Screen.width * .5f, Screen.height * .4f));
                InventoryGui.DevBestiary(true);
                string hellstone = ItemDb.Get(ItemType.Hellstone).Name;
                yield return Show("ZombieMiner", hellstone);
                Check("狱石未开放时当前模式不误报", !InventoryGui.DevBestiarySearchMatches("ZombieMiner"));
                Check("资料模式有实际点击区域", InventoryGui.DevHitRect("BestiaryEntry", -61).HasValue);
                yield return ClickHit("BestiaryEntry", -61, .2f);
                Check("点击切换全部资料", InventoryGui.DevBookCatalogue);
                yield return Show("ZombieMiner", hellstone);
                Check("资料搜索可找到未开放狱石", InventoryGui.DevBestiarySearchMatches("ZombieMiner"));
                var homes = BestiaryHabitats.Of("ZombieMiner");
                int mines = -1;
                for (int i = 0; i < homes.Count; i++) if (homes[i].Area == "水晶山峰") mines = i;
                Check("矿工有水晶山峰来源", mines >= 0);
                if (mines >= 0) InventoryGui.DevBestiarySelect(-30 - mines);
                yield return new WaitForSecondsRealtime(.3f);
                string locked = InventoryGui.DevJournalRequirement("ZombieMiner", ItemType.Hellstone);
                evidence["HellstoneBefore"] = locked;
                Check("锁定卡写出具体首领", locked != null && locked.Contains(ProgressionInfo.BossName("DungDefender")) && locked.Contains(ProgressionInfo.BossName("FlukeMother")));
                Check("未开放卡不显示假设掉率", InventoryGui.DevJournalChance("ZombieMiner", ItemType.Hellstone) == null);
                Check("七个能力阶段预览按钮保留", Enumerable.Range(10, 7).All(i => InventoryGui.DevHitRect("BestiaryEntry", -i).HasValue));
                Shot("Town", "journal_hellstone_locked");
                yield return new WaitForSecondsRealtime(.2f);
                ulong state = VanillaBossProgression.StateKey;
                var clearsReference = VanillaBossProgression.DevClears;
                int killsCount = Bestiary.Kills.Count;
                BestiaryCatalogue.Of("Mummy", "RestingGrounds_10");
                Check("资料查询不改变战况对象或状态", ReferenceEquals(clearsReference, VanillaBossProgression.DevClears) && state == VanillaBossProgression.StateKey);
                Check("资料查询不解锁发现记录", Bestiary.Kills.Count == killsCount);
                var slyHome = BestiaryHabitats.Of("Nailsage").First(h => h.Area == "神居");
                var ggSources = BestiaryCatalogue.Of("Nailsage", slyHome.Scene);
                Check("神居低探索阶段支援来源不被世界首领阶段隐藏", ggSources.Any(e => e.Type == ItemType.Hellstone));
                InventoryGui.DevBestiarySelect(-13);
                var ggActual = EnemyDrops.At("Nailsage", 2, Bestiary.KillsOf("Nailsage") == 0, false, false, slyHome.Scene);
                Check("神居阶段预览保留实际支援掉落", ggActual.Count > 0 && InventoryGui.DevBookDropsIn("Nailsage", slyHome.Scene).SequenceEqual(ggActual));
                InventoryGui.DevBestiarySelect(-10);
                ProgressionNotice.Reset(); ProgressionNotice.Tick(true);
                int worldNotes = ProgressionNotice.DevWorldNotes;
                VanillaBossProgression.DevClears.Add("DungDefender");
                yield return new WaitForSecondsRealtime(.5f);
                Check("同阶段Boss胜利触发本地提示", ProgressionNotice.DevWorldNotes == worldNotes + 1 && Net.NetChat.DevHas(ProgressionInfo.BossName("DungDefender")));
                Check("狱石卡随战况立刻开放", InventoryGui.DevJournalRequirement("ZombieMiner", ItemType.Hellstone) == "");
                Check("开放后显示实际概率", InventoryGui.DevJournalChance("ZombieMiner", ItemType.Hellstone)?.StartsWith("65%") == true);
                Shot("Town", "journal_hellstone_open");
                yield return new WaitForSecondsRealtime(.2f);
                InventoryGui.DevBookCatalogue = false;
                Check("当前模式同步找到开放狱石", InventoryGui.DevBestiarySearchMatches("ZombieMiner"));
                Shop.DevStage = 5;
                Check("叛徒前灵气受限", ProgressionInfo.Missing(ItemType.Ectoplasm, 5).Contains(ProgressionInfo.BossName("TraitorLord")));
                bool BeforeEctoplasm = EnemyDrops.At("Flukeman", 5, true, true, true, "Waterways_01").Any(d => d.Type == ItemType.Ectoplasm);
                VanillaBossProgression.DevClears.Add("TraitorLord");
                Check("叛徒后灵气开放", ProgressionInfo.Missing(ItemType.Ectoplasm, 5) == "");
                bool afterEctoplasm = EnemyDrops.At("Flukeman", 5, true, true, true, "Waterways_01").Any(d => d.Type == ItemType.Ectoplasm);
                Check("同阶段实际水道灵气表变化", !BeforeEctoplasm && afterEctoplasm);
                evidence["Ectoplasm"] = new { Before = BeforeEctoplasm, After = afterEctoplasm, Stage = Shop.UnlockedStage() };
                Check("前辈前操纵机条件清楚", ProgressionInfo.Missing(ItemType.LunarCraftingStation, 5).Contains(ProgressionInfo.BossName("HollowKnight")));
                VanillaBossProgression.DevClears.Add("HollowKnight");
                Check("前辈后操纵机进度满足", ProgressionInfo.Missing(ItemType.LunarCraftingStation, 5) == "");
                yield return new WaitForSecondsRealtime(.4f);
                int n = ProgressionNotice.DevWorldNotes;
                yield return new WaitForSecondsRealtime(.4f);
                Check("战况提示不重复", ProgressionNotice.DevWorldNotes == n);

                InventoryGui.DevBookCatalogue = true;
                yield return Show("FalseKnight", "");
                InventoryGui.DevBestiarySelect(-65);
                yield return new WaitForSecondsRealtime(.4f);
                Shot("Town", "journal_world_progress");
                yield return new WaitForSecondsRealtime(.2f);
                Check("三页签可点击", new[] { -63, -64, -65 }.All(i => InventoryGui.DevHitRect("BestiaryEntry", i).HasValue));
                yield return ClickHit("BestiaryEntry", -64, .3f);
                Check("点击猎人笔记实际切页", InventoryGui.DevJournalTab == 1);
                Shot("Town", "journal_notes");
                yield return new WaitForSecondsRealtime(.2f);
                yield return ClickHit("BestiaryEntry", -63, .3f);
                Check("点击战利品实际切页", InventoryGui.DevJournalTab == 0);
                Check("窗口在屏幕内", InventoryGui.DevJournalPanel.xMin >= 0 && InventoryGui.DevJournalPanel.yMin >= 0 && InventoryGui.DevJournalPanel.xMax <= Screen.width && InventoryGui.DevJournalPanel.yMax <= Screen.height);

                InventoryGui.DevBestiary(false);
                yield return new WaitForSecondsRealtime(10.5f);
                InventoryGui.DevBestiaryShow("FalseKnight");
                float firstPhase = 1;
                for (int frame = 0; frame < 14; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    if (frame == 0) firstPhase = InventoryGui.DevJournalOpenPhase;
                    Shot("Town", "journal_anim_" + frame.ToString("D2"));
                    yield return new WaitForSecondsRealtime(.045f);
                }
                Check("展开动画实际推进并结束", firstPhase < .99f && InventoryGui.DevJournalOpenPhase > .99f);
                evidence["Opening"] = new { First = firstPhase, Last = InventoryGui.DevJournalOpenPhase };
                InventoryGui.DevBestiaryShow("Hornet");
                yield return new WaitForEndOfFrame();
                float firstPage = InventoryGui.DevJournalPagePhase;
                yield return new WaitForSecondsRealtime(.4f);
                Check("选中切页动画实际推进", firstPage < .99f && InventoryGui.DevJournalPagePhase > .99f);
                Shop.DevStage = 5;
                InventoryGui.DevBestiarySelect(-12);
                Check("预览不改变探索进度或战况", Shop.UnlockedStage() == 5 && VanillaBossProgression.Cleared("HollowKnight"));
                InventoryGui.DevBestiarySelect(-10);
                Screen.SetResolution(1600, 900, false);
                yield return new WaitForSecondsRealtime(1.6f);
                Shot("Town", "journal_1600");
                yield return new WaitForSecondsRealtime(.3f);
                Check("大窗口布局在视图内", InventoryGui.DevJournalPanel.xMin >= 0 && InventoryGui.DevJournalPanel.xMax <= Screen.width && InventoryGui.DevJournalPanel.yMax <= Screen.height);
                Screen.SetResolution(1024, 768, false);
                yield return new WaitForSecondsRealtime(1.6f);
                Shot("Town", "journal_1024");
                yield return new WaitForSecondsRealtime(.3f);
                Check("窄窗口布局在视图内", InventoryGui.DevJournalPanel.xMin >= 0 && InventoryGui.DevJournalPanel.xMax <= Screen.width && InventoryGui.DevJournalPanel.yMax <= Screen.height);
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "bestiary-journal-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Evidence = evidence }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 猎人手记检查 {checks}，失败 {failures}");
            }
            finally
            {
                Shop.DevStage = savedStage;
                VanillaBossProgression.DevClears = savedClears;
                GameDifficulty.Current = difficulty;
                if (hadMiner) Bestiary.Kills["ZombieMiner"] = minerKills; else Bestiary.Kills.Remove("ZombieMiner");
                InventoryGui.DevBookCatalogue = catalogue;
                InventoryGui.DevBookSearch = "";
                InventoryGui.DevBestiarySelect(-63);
                InventoryGui.DevBestiarySelect(-10);
                InventoryGui.DevBestiary(false);
                InventoryGui.Open = false;
                mod.ApplyCreative(creative);
                mod.Inventory.FreeMaking = freeMaking;
                mod.Inventory.Infinite = infinite;
                ProgressionNotice.Reset();
                PointerOff();
            }
        }
    }
}
