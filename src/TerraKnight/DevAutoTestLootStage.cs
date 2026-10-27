using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TerraKnight.Items;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator LootStageTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("isolated saves required");
            int checks = 0, failures = 0, originalStage = Shop.DevStage;
            var clears = VanillaBossProgression.DevClears;
            var sink = EnemyDrops.DevSink;
            int selection = LootStage.Selection;
            void Check(string name, bool pass)
            {
                checks++; if (!pass) failures++;
                Plugin.Log.LogInfo($"[AutoTest] 掉落阶段 {name} {(pass ? "通过" : "不通过")}");
            }
            string Rows(IEnumerable<EnemyDrops.Drop> rows) => JsonConvert.SerializeObject(rows);
            try
            {
                yield return GoTo("Town", "left1");
                mod.ApplyCreative(false);
                Shop.DevStage = 5;
                VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Keys);
                Bestiary.Kills["ZombieMiner"] = 1;
                var progression = VanillaBossProgression.StateKey;
                var stats = mod.Stats;
                int life = stats.LifeMax, mana = stats.ManaMax;
                var beforeEquipment = JsonConvert.SerializeObject(Equipment.Current);
                var enemy = new EnemyStats.Info { MaxHp = 40, Level = 0, Scene = "Crossroads_01" };
                int enemyLife = EnemyStats.LifeWhole(enemy);
                string record = "ZombieBarger", scene = EnemyDrops.SceneOf(record);
                if (!EnemyDrops.Known(record)) { record = "ZombieMiner"; scene = EnemyDrops.SceneOf(record); }
                Check("有可核验的敌人", EnemyDrops.Known(record));
                var rolls = new Dictionary<int, string[]>();
                for (int stage = 0; stage <= 5; stage++)
                {
                    Check("可选择已到达阶段/" + stage, LootStage.Choose(stage) && LootStage.Current == stage);
                    var expected = EnemyDrops.At(record, stage, Bestiary.KillsOf(record) == 0, ProgressionBalance.EntryClear, ProgressionBalance.FinalClear, scene);
                    Check("实际来源采用所选阶段/" + stage, Rows(EnemyDrops.In(record, scene)) == Rows(expected));
                    Check("不回退人物敌人与能力数值/" + stage, Shop.UnlockedStage() == 5 && stats.LifeMax == life && stats.ManaMax == mana
                        && JsonConvert.SerializeObject(Equipment.Current) == beforeEquipment && EnemyStats.LifeWhole(enemy) == enemyLife && VanillaBossProgression.StateKey == progression);
                    var paid = new HashSet<ItemType>();
                    EnemyDrops.DevSink = (type, count) => paid.Add(type);
                    UnityEngine.Random.InitState(510 + stage);
                    for (int kill = 0; kill < 256; kill++) EnemyDrops.Roll(record, Vector2.zero, direct: true, firstKill: false, scene: scene);
                    EnemyDrops.DevSink = sink;
                    var legal = new HashSet<ItemType>(EnemyDrops.In(record, scene).Where(d => !EnemyDrops.Locked(d)).Select(d => d.Type));
                    foreach (var type in legal.ToArray()) foreach (var mate in VanityDrops.Mates(type)) legal.Add(mate);
                    Check("击杀实掷只产生当前表中装备/" + stage, paid.Where(t => ItemDb.Get(t).IsWeapon || ItemDb.Get(t).Armor != ArmorSlot.None).All(legal.Contains));
                    rolls[stage] = paid.Select(t => t.ToString()).OrderBy(t => t).ToArray();
                }
                Check("恢复跟随当前进度", LootStage.Choose(-1) && LootStage.FollowsProgress && LootStage.Current == 5);
                Shop.DevStage = 2;
                Check("拒绝选择未来阶段", !LootStage.Choose(3) && LootStage.Current == 2);
                InventoryGui.Open = true;
                PointerOn(new Vector2(Screen.width * .5f, Screen.height * .4f));
                InventoryGui.DevBestiaryShow("ZombieMiner");
                yield return new WaitForSecondsRealtime(.8f);
                Check("只显示可点击的已解锁阶段", Enumerable.Range(70, 4).All(i => InventoryGui.DevHitRect("BestiaryEntry", -i).HasValue)
                    && !InventoryGui.DevHitRect("BestiaryEntry", -74).HasValue);
                yield return ClickHit("BestiaryEntry", -72, .25f);
                Check("实际点击切换到阶段一", LootStage.Selection == 1 && LootStage.Current == 1 && Shop.UnlockedStage() == 2);
                Check("点击后保留当前预览与角色数值", InventoryGui.DevBestiaryDropStage == -1 && stats.LifeMax == life && stats.ManaMax == mana);
                Check("切换后图鉴保持打开", InventoryGui.BestiaryOpen);
                Shot("Town", "loot_stage_1");
                yield return new WaitForEndOfFrame();
                yield return null;
                int slot = Save.ModSave.DevStateSlot;
                string path = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json");
                var file = JObject.Parse(File.ReadAllText(path));
                Check("按存档保存阶段选择", file.Value<int>("LootStageSelection") == 1);
                InventoryGui.DevBestiary(false);
                LootStage.Reset();
                Save.ModSave.Read(slot, mod);
                Check("重新读档恢复阶段选择", LootStage.Selection == 1 && LootStage.Current == 1);
                file.Remove("LootStageSelection");
                File.WriteAllText(path, file.ToString());
                Save.ModSave.Read(slot, mod);
                Check("旧存档默认跟随当前阶段", LootStage.Selection == -1);
                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) { writer.Write((byte)255); writer.Write((byte)4); writer.Write("loot-stage"); writer.Write("1"); }
                    stream.Position = 0;
                    using (var reader = new BinaryReader(stream)) Net.WorldSync.ReceiveState(reader);
                }
                Check("同步包不改变实际探索进度", Shop.UnlockedStage() == 2 && VanillaBossProgression.StateKey == progression);
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "loot-stage-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Rolls = rolls }, Formatting.Indented));
            }
            finally
            {
                EnemyDrops.DevSink = sink;
                LootStage.Load(selection); LootStage.ClearHost();
                Shop.DevStage = originalStage;
                VanillaBossProgression.DevClears = clears;
                PointerOff();
            }
        }
    }
}
