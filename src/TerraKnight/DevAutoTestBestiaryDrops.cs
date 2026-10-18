using System;
using System.Collections;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BestiaryDropStageTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int savedStage = Shop.DevStage, savedFilter = InventoryGui.DevBestiaryDropStage;
            bool creative = mod.Creative;
            bool immortal = Player.TerraStats.DevImmortal, invincible = PlayerData.instance.isInvincible, noFall = mod.DevNoFallDamage;
            int checks = 0, failures = 0;
            void Check(string label, bool pass)
            {
                checks++;
                if (!pass) { failures++; Plugin.Log.LogError($"[AutoTest] 掉落阶段 {label} 不通过"); }
            }
            try
            {
                // Leave the opening sequence before testing rendered controls.
                Player.TerraStats.DevImmortal = true;
                PlayerData.instance.isInvincible = true;
                mod.DevNoFallDamage = true;
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(1f);
                mod.ApplyCreative(true);
                InventoryGui.DevBookSearch = "";
                InventoryGui.Open = true;
                InventoryGui.DevBestiaryShow("FlameBearerMed");
                foreach (int actual in new[] { 0, 5 })
                {
                    Shop.DevStage = actual;
                    for (int stage = 0; stage <= 5; stage++)
                    {
                        InventoryGui.DevBestiarySelect(-11 - stage);
                        foreach (var record in EnemyDrops.DevRecords)
                        {
                            bool futureBoss = ProgressionBalance.FormalBoss(record)
                                && ProgressionBalance.BossRewards.TryGetValue(record, out var boss) && boss.Stage > stage;
                            var expected = futureBoss ? Array.Empty<EnemyDrops.Drop>()
                                : EnemyDrops.At(record, stage, Bestiary.KillsOf(record) == 0, stage >= 4, stage >= 5);
                            var rows = InventoryGui.DevBestiaryDrops(record);
                            Check($"实际进度{actual}/预览{stage}/{record}", rows.SequenceEqual(expected));
                            foreach (var group in rows.Where(d => d.Group > 0 && !EnemyDrops.Locked(d, stage)).GroupBy(d => d.Group))
                                Check($"概率合计/{record}/{stage}/{group.Key}", Math.Abs(group.Sum(d => EnemyDrops.MarginalChance(rows, d, stage)) - EnemyDrops.EffectiveChance(group.First())) < .0001f);
                        }
                        Check("不改变玩家进度", Shop.DevStage == actual);
                    }
                }
                Shop.DevStage = 5;
                InventoryGui.DevBestiarySelect(-13); // Stage 2 in a full-progress creative save.
                Check("阶段2格林大师无泰拉刃", !InventoryGui.DevBestiaryDrops("FlameBearerMed").Any(d => d.Type == ItemType.TerraBlade));
                InventoryGui.DevBookSearch = ItemDb.Get(ItemType.TerraBlade).Name;
                yield return new WaitForSeconds(.4f);
                Check("重新搜索清除旧掉落阶段", InventoryGui.DevBestiaryDropStage == -1);
                Check("重新搜索能找到当前阶段掉落", InventoryGui.DevBestiarySearchMatches("FlameBearerMed"));
                InventoryGui.DevBestiarySelect(-13);
                Check("阶段2搜索不串入阶段5掉落", !InventoryGui.DevBestiarySearchMatches("FlameBearerMed"));
                InventoryGui.DevBookSearch = "";
                Check("清空搜索清除旧掉落阶段", InventoryGui.DevBestiaryDropStage == -1);
                InventoryGui.DevBestiarySelect(-13);
                InventoryGui.DevBookType(true);
                Check("重新聚焦搜索清除旧掉落阶段", InventoryGui.DevBestiaryDropStage == -1);
                InventoryGui.DevBookType(false);
                InventoryGui.DevBestiarySelect(-13);
                InventoryGui.DevBestiaryShow("FlameBearerMed");
                yield return new WaitForSeconds(.4f);
                bool rendered = InventoryGui.DevBookFrames > 0;
                if (rendered)
                {
                    Check("绘制七个阶段按钮", Enumerable.Range(10, 7).All(i => InventoryGui.DevHitRect("BestiaryEntry", -i).HasValue));
                    Shot(GameManager.instance.sceneName, "bestiary_stage2");
                    yield return new WaitForSeconds(.3f);
                    yield return ClickHit("BestiaryEntry", -16, .3f);
                }
                else
                {
                    Plugin.Log.LogWarning("[AutoTest] BESTIARY_UI_UNVERIFIED: hidden window has no GUI repaint; testing the shared click handler directly.");
                    InventoryGui.DevBestiarySelect(-16);
                }
                Check("点击切换阶段5", InventoryGui.DevBestiaryDropStage == 5);
                Check("阶段5格林大师有泰拉刃", InventoryGui.DevBestiaryDrops("FlameBearerMed").Any(d => d.Type == ItemType.TerraBlade));
                if (rendered) Shot(GameManager.instance.sceneName, "bestiary_stage5");
                yield return new WaitForSeconds(.3f);
                InventoryGui.DevBookSearch = ItemDb.Get(ItemType.TerraBlade).Name;
                yield return new WaitForSeconds(.4f);
                Check("阶段5搜索刷新", InventoryGui.DevBestiarySearchMatches("FlameBearerMed"));
                InventoryGui.DevBookSearch = "";
                InventoryGui.DevBestiarySelect(-10);
                Check("当前模式恢复实际掉落", InventoryGui.DevBestiaryDrops("FlameBearerMed").SequenceEqual(EnemyDrops.Of("FlameBearerMed")));
                var locked = new EnemyDrops.Drop { Type = ItemType.TerraBlade, Group = 100, Num = 1, Den = 1, Weight = 1, LockStage = 5 };
                var open = locked; open.LockStage = 0;
                var synthetic = new[] { locked, open };
                Check("概率使用预览阶段", EnemyDrops.GroupSize(synthetic, 100, 2) == 1 && EnemyDrops.MarginalChance(synthetic, open, 2) == 1f);
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "bestiarydrops-result.json"), $"{{\"Checks\":{checks},\"Failures\":{failures},\"UiRendered\":{rendered.ToString().ToLowerInvariant()}}}");
                Plugin.Log.LogInfo($"[AutoTest] 掉落阶段检查 {checks}，失败 {failures}");
            }
            finally
            {
                InventoryGui.DevBookSearch = "";
                InventoryGui.DevBestiarySelect(-11 - savedFilter);
                InventoryGui.DevBestiary(false);
                InventoryGui.Open = false;
                Shop.DevStage = savedStage;
                mod.ApplyCreative(creative);
                Player.TerraStats.DevImmortal = immortal;
                PlayerData.instance.isInvincible = invincible;
                mod.DevNoFallDamage = noFall;
            }
        }
    }
}
