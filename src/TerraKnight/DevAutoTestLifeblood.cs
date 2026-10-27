using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "lifeblood"): the game's blue masks given (a lifeblood cocoon's): two silver hearts of shield and two mana
    /// stars; hits take the shield first, a broken shield retains the native collection count; lifeblood again fills it, not more
    /// stars; death takes both. The life column photographed with the shield up.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LifebloodTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 生命血 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", EntryGateOf("Town"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var pd = PlayerData.instance;
            var st = mod.Stats;
            pd.healthBlue = 0;
            Lifeblood.OnDeath(st);
            Lifeblood.Tick(st, pd);
            st.FullHeal();
            int mana0 = st.ManaMax;
            pd.healthBlue = 2;
            yield return new WaitForSeconds(0.3f);
            Check("得到生命血", st.Shield == Lifeblood.ShieldLife && st.TempMana == Lifeblood.ManaStars && st.ManaMax == mana0 + Lifeblood.ManaStars,
                $"护盾 {st.Shield}，临时魔力 {st.TempMana}，魔力上限 {mana0} → {st.ManaMax}");
            st.RestoreMana(999);
            yield return new WaitForSeconds(0.4f);
            Shot("Town", "lifeblood_shield");
            yield return null;
            int life0 = st.Life;
            st.TakeDirect(30 + Mathf.CeilToInt(st.Defense * World.GameDifficulty.DefenseEffectiveness));
            yield return new WaitForSeconds(0.2f);
            Check("护盾先挨打", st.Life == life0 && st.Shield == 10, $"生命 {life0} → {st.Life}，护盾 {st.Shield}");
            st.TakeDirect(30 + Mathf.CeilToInt(st.Defense * World.GameDifficulty.DefenseEffectiveness));
            yield return new WaitForSeconds(0.3f);
            Check("护盾打破后计数保留且正常扣生命", st.Shield == 0 && st.Life == life0 - 20 && pd.healthBlue == 2, $"生命 {st.Life}，护盾 {st.Shield}，蓝面具 {pd.healthBlue}");
            pd.healthBlue = 3;
            yield return new WaitForSeconds(0.3f);
            Check("再打一次生命血", st.Shield == Lifeblood.ShieldLife && st.ManaMax == mana0 + Lifeblood.ManaStars, $"护盾 {st.Shield}，魔力上限 {st.ManaMax}");
            pd.atBench = true;
            pd.MaxHealth();
            Lifeblood.Tick(st, pd);
            pd.atBench = false;
            yield return new WaitForSeconds(.2f);
            Check("原版休息回血入口重置计数", pd.healthBlue == 0, $"计数 {pd.healthBlue}");
            for (int i = 0; i < 15; i++) {
                GameManager.instance.IncrementPlayerDataInt("healthBlue");
                yield return null;
                st.Shield = 0;
            }
            yield return new WaitForSeconds(.2f);
            Check("连续收集十五只且护盾反复破碎仍累计", pd.healthBlue == 15 && st.Shield == 0 && st.TempMana == Lifeblood.ManaStars, $"计数 {pd.healthBlue}，护盾 {st.Shield}");
            int masks = pd.maxHealthBase, pieces = pd.heartPieces, mp = pd.MPReserveMax, fragments = pd.vesselFragments;
            pd.maxHealthBase = 7; pd.heartPieces = 2; pd.MPReserveMax = 66; pd.vesselFragments = 1;
            var report = PlayerStatReport.Capture(4);
            Check("背包显示累计面具和容器碎片", report.Any(r => r.Label == "面具碎片收集" && r.Value == "10 / 16") && report.Any(r => r.Label == "灵魂容器碎片收集" && r.Value == "7 / 9"));
            Check("背包独立显示蓝虫与零护盾", report.Any(r => r.Label == "当前生命血（蓝虫）计数" && r.Value == "15") && report.Any(r => r.Label == "当前生命血银心护盾" && r.Value == "0"));
            Check("背包精华读取实际可用量", report.Any(r => r.Label == "当前可用梦境精华" && r.Value == pd.dreamOrbs.ToString()));
            InventoryGui.Open = true; InventoryGui.DevUtilityClick(2);
            yield return new WaitForSeconds(.4f);
            Shot("Town", "collection_progress_link");
            yield return new WaitForSeconds(.3f);
            InventoryGui.DevCollectionClick();
            Check("圣巢背包直达收集进度", InventoryGui.DevStatsOpen && !InventoryGui.HkBagOpen);
            yield return new WaitForSeconds(.4f);
            Shot("Town", "collection_progress_top");
            yield return new WaitForSeconds(.3f);
            InventoryGui.DevCollectionScroll(12);
            yield return new WaitForSeconds(.3f);
            Shot("Town", "collection_progress_bottom");
            yield return new WaitForSeconds(.3f);
            InventoryGui.Open = false;
            pd.maxHealthBase = masks; pd.heartPieces = pieces; pd.MPReserveMax = mp; pd.vesselFragments = fragments;
            yield return GoTo("Abyss_06_Core", EntryGateOf("Abyss_06_Core"));
            pd.blueVineDoor = false; pd.gotCharm_9 = false;
            var plinth = UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f => f.gameObject.name == "Blue Plinth" && f.FsmName == "Control");
            var door = UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f => f.gameObject.name == "Blue Door" && f.FsmName == "Control");
            Check("找到原版蓝门与祭坛", plinth != null && door != null);
            if (plinth != null && door != null) {
                plinth.SetState("Init"); door.SetState("Init");
                yield return new WaitForSeconds(.3f);
                var hero = HeroController.instance;
                var stand = Clear(hero, (Vector2)plinth.transform.position + Vector2.up);
                float until = Time.realtimeSinceStartup + 45;
                while (!pd.blueVineDoor && Time.realtimeSinceStartup < until) {
                    hero.transform.position = stand;
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    plinth.FsmVariables.FindFsmBool("Hero Range").Value = true;
                    yield return null;
                }
                Check("十五计数零护盾按原版祭坛流程打开蓝门", pd.blueVineDoor && pd.healthBlue == 15 && st.Shield == 0, $"门 {door.ActiveStateName}，祭坛 {plinth.ActiveStateName}，计数 {pd.healthBlue}");
                Shot("Abyss_06_Core", "lifeblood_counter_door_open");
            }
            Lifeblood.OnDeath(st);
            pd.healthBlue = 0;
            yield return new WaitForSeconds(0.3f);
            Check("死亡清空", st.Shield == 0 && st.TempMana == 0 && st.ManaMax == mana0, $"护盾 {st.Shield}，魔力上限 {st.ManaMax}");
            // The Water Candle's tooltip: its use's multiplier said, in English too.
            var tip = Items.ItemDb.Get(Items.ItemType.WaterCandle).Tooltip ?? "";
            var lines = tip.Split('\n');
            string en = lines.Length > 1 ? Lang.Line(lines[1], "en-US") : null;
            Check("水蜡烛说明写了倍率", lines.Length >= 2 && lines[1].Contains("倍率") && en != null && en.Contains("multiplier"), $"{tip.Replace('\n', '|')} / {en}");
            st.FullHeal();
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "lifeblood-counter.json"), JsonConvert.SerializeObject(new { Checks = all, Passed = ok, Failures = all - ok, DoorOpened = pd.blueVineDoor, CollectionRows = report }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 生命血 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
