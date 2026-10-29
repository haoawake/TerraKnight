using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "compass"): the Wayward Compass worn (UI.WorldMap's compass). Every fight's room is on the map with gates, and
    /// a way to it is found from Dirtmouth; with no fight won and stage 0, it points to the nearest of the Crossroads' three;
    /// that one won, to the nearer of the other two; all three won, to stage 1's. The way's rooms follow the gates (each next
    /// room a gate's partner), end in the fight's room, and from the next room along it the way is shorter. Screenshots of the
    /// minimap and the full map in Dirtmouth and along the way; off, nothing is pointed to.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CompassTest()
        {
            int pass = 0, all = 0;
            void Check(string name, bool ok, string detail = "")
            {
                all++;
                if (ok) pass++;
                Plugin.Log.LogInfo($"[AutoTest] 指南针 {name} {(ok ? "通过" : "不通过")} {detail}");
            }
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            Charms.DevSetPage(true);
            Charms.DevAnywhere = true;
            pd.SetBool("gotCharm_2", true);
            if (!Charms.Worn(2)) Charms.Click(2);
            Check("戴上指南针", Charms.Worn(2), $"已装备 {string.Join(",", Charms.WornList ?? new List<int>())}");

            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;

            // Every fight's room on the map, and a way there from Dirtmouth.
            var noRoom = new List<string>();
            var noWay = new List<string>();
            int fights = 0;
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            {
                if (!Relics.Is(t)) continue;
                fights++;
                string room = UI.WorldMap.DevCompassRoomOf(t, "Town");
                if (!UI.WorldMap.DevLaidOut(room)) { noRoom.Add($"{t}:{room}"); continue; }
                if (UI.WorldMap.DevCostFromHere(room) >= float.MaxValue) noWay.Add($"{t}:{room}");
            }
            Check("每个首领的房间都在地图上", noRoom.Count == 0, $"{fights} 个首领；没有房间的：{string.Join("、", noRoom)}");
            Check("从德特茅斯都找得到路", noWay.Count == 0, $"找不到路的：{string.Join("、", noWay)}");
            bool oldDead = pd.zoteDead, oldBuzzer = pd.zoteRescuedBuzzer, oldDeepnest = pd.zoteRescuedDeepnest, oldBretta = pd.brettaRescued, oldDefeated = pd.zoteDefeated;
            try
            {
                pd.zoteDead = pd.zoteRescuedBuzzer = pd.zoteRescuedDeepnest = pd.brettaRescued = pd.zoteDefeated = false;
                Check("苍绿救援记录缺失也固定指向雕像", UI.WorldMap.DevCompassRoomOf(ItemType.RelicGreyPrince, "Town") == "Room_Bretta_Basement", UI.WorldMap.DevCompassHintOf(ItemType.RelicGreyPrince));
                pd.zoteRescuedBuzzer = true;
                Check("深巢救援记录缺失也固定指向雕像", UI.WorldMap.DevCompassRoomOf(ItemType.RelicGreyPrince, "Town") == "Room_Bretta_Basement", UI.WorldMap.DevCompassHintOf(ItemType.RelicGreyPrince));
                pd.zoteRescuedDeepnest = true;
                Check("布蕾塔救援记录缺失也固定指向雕像", UI.WorldMap.DevCompassRoomOf(ItemType.RelicGreyPrince, "Town") == "Room_Bretta_Basement", UI.WorldMap.DevCompassHintOf(ItemType.RelicGreyPrince));
                pd.brettaRescued = true;
                Check("竞技场记录缺失也固定指向雕像", UI.WorldMap.DevCompassRoomOf(ItemType.RelicGreyPrince, "Town") == "Room_Bretta_Basement", UI.WorldMap.DevCompassHintOf(ItemType.RelicGreyPrince));
                pd.zoteDefeated = true;
                Check("灰王入口精确到地下室雕像", UI.WorldMap.DevCompassRoomOf(ItemType.RelicGreyPrince, "Town") == "Room_Bretta_Basement" && UI.WorldMap.DevCompassHintOf(ItemType.RelicGreyPrince).Contains("雕像"), UI.WorldMap.DevCompassHintOf(ItemType.RelicGreyPrince));
                pd.zoteDead = true;
                Check("左特死亡记录不替换导航终点", UI.WorldMap.DevCompassRoomOf(ItemType.RelicGreyPrince, "Town") == "Room_Bretta_Basement", UI.WorldMap.DevCompassHintOf(ItemType.RelicGreyPrince));
            }
            finally { pd.zoteDead = oldDead; pd.zoteRescuedBuzzer = oldBuzzer; pd.zoteRescuedDeepnest = oldDeepnest; pd.brettaRescued = oldBretta; pd.zoteDefeated = oldDefeated; }

            // A save with nothing won, at stage 0.
            var mod = TerraMod.Instance;
            var kept = new List<string>();
            foreach (var f in mod.GrantedAbilities) if (f.StartsWith(BossMastery.Prefix)) kept.Add(f);
            foreach (var f in kept) mod.GrantedAbilities.Remove(f);
            mod.GrantedAbilities.Add(BossMastery.Initialized);
            int stage0 = Shop.DevStage;
            Shop.DevStage = 0;
            try
            {
                UI.WorldMap.DevCompassForget();
                yield return new WaitForSeconds(0.5f);
                string first = UI.WorldMap.DevCompass();
                var pointed = UI.WorldMap.DevCompassFight;
                var costs = new Dictionary<ItemType, float>();
                foreach (var t in new[] { ItemType.RelicFalseKnight, ItemType.RelicBigFly, ItemType.RelicMawlek })
                    costs[t] = UI.WorldMap.DevCostFromHere(UI.WorldMap.DevCompassRoomOf(t, "Town"));
                var nearest = ItemType.RelicFalseKnight;
                foreach (var kv in costs) if (kv.Value < costs[nearest]) nearest = kv.Key;
                Check("阶段 0 指向最近的一个", pointed == nearest, $"{first}；三个的路程 {string.Join("，", Costs(costs))}");
                CheckWay(Check, "德特茅斯");
                Shot("Town", "compass_town_minimap");
                yield return null;
                UI.FullMap.DevSetOpen(true);
                yield return new WaitForSeconds(0.4f);
                Check("全屏地图实际绘制导航路线", UI.WorldMap.DevFullCompassDots > 0, $"绘制 {UI.WorldMap.DevFullCompassDots} 个路线点");
                Shot("Town", "compass_town_fullmap");
                yield return new WaitForSeconds(0.2f);
                UI.FullMap.DevSetOpen(false);
                yield return new WaitForSeconds(0.3f);

                // Along the way: the next room's way is shorter and starts there.
                var scenes = new List<string>(UI.WorldMap.DevCompassScenes);
                if (scenes.Count >= 2)
                {
                    string next = scenes[1];
                    float before = UI.WorldMap.DevCostFromHere(UI.WorldMap.DevCompassRoom);
                    yield return GoTo(next, EntryGateOf(next));
                    yield return new WaitForSeconds(1.5f);
                    hero = HeroController.instance;
                    string there = UI.WorldMap.DevCompass();
                    float after = UI.WorldMap.DevCostFromHere(UI.WorldMap.DevCompassRoom);
                    var now = UI.WorldMap.DevCompassScenes;
                    Check("走到路线上的下一个房间，路线从那里开始、变短", now.Count > 0 && now[0] == next && after < before, $"{next}：{there}；路程 {before:F0} → {after:F0}");
                    CheckWay(Check, next);
                    Shot(next, "compass_next_minimap");
                    yield return null;
                    UI.FullMap.DevSetOpen(true);
                    yield return new WaitForSeconds(0.4f);
                    Shot(next, "compass_next_fullmap");
                    yield return new WaitForSeconds(0.2f);
                    UI.FullMap.DevSetOpen(false);
                    yield return new WaitForSeconds(0.3f);
                }
                else Check("走到路线上的下一个房间", false, "路线只有一个房间");

                // The one pointed to won: the nearer of the other two; all three: stage 1's.
                mod.GrantedAbilities.Add(BossMastery.Prefix + nearest);
                UI.WorldMap.DevCompassForget();
                yield return new WaitForSeconds(0.3f);
                string second = UI.WorldMap.DevCompass();
                var pointed2 = UI.WorldMap.DevCompassFight;
                Check("打过最近的那个后，指向剩下两个之一", pointed2.HasValue && pointed2 != nearest && costs.ContainsKey(pointed2.Value), second);
                foreach (var t in costs.Keys) mod.GrantedAbilities.Add(BossMastery.Prefix + t);
                UI.WorldMap.DevCompassForget();
                yield return new WaitForSeconds(0.3f);
                string third = UI.WorldMap.DevCompass();
                var pointed3 = UI.WorldMap.DevCompassFight;
                var stage1 = new HashSet<ItemType> { ItemType.RelicHornet, ItemType.RelicMegaMossCharger, ItemType.RelicBigBuzzer, ItemType.RelicGhostNoEyes, ItemType.RelicGhostAladar };
                Check("阶段 0 打完，指向阶段 1 的", pointed3.HasValue && stage1.Contains(pointed3.Value), third);
                CheckWay(Check, "阶段 1");
                Shot(GameManager.instance.sceneName, "compass_stage1_minimap");
                yield return null;
                UI.FullMap.DevSetOpen(true);
                yield return new WaitForSeconds(0.4f);
                Shot(GameManager.instance.sceneName, "compass_stage1_fullmap");
                yield return new WaitForSeconds(0.2f);
                UI.FullMap.DevSetOpen(false);
                yield return new WaitForSeconds(0.3f);

                // The native dream entrance, rather than Zote standing outside in Dirtmouth.
                try
                {
                    foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
                        if (Relics.Is(t) && t != ItemType.RelicGreyPrince) mod.GrantedAbilities.Add(BossMastery.Prefix + t);
                    mod.GrantedAbilities.Remove(BossMastery.Prefix + ItemType.RelicGreyPrince);
                    pd.zoteDead = false;
                    pd.zoteRescuedBuzzer = pd.zoteRescuedDeepnest = pd.brettaRescued = pd.zoteDefeated = true;
                    Shop.DevStage = 5;
                    yield return GoTo("Room_Bretta", "right1");
                    UI.WorldMap.DevCompassForget();
                    string grey = UI.WorldMap.DevCompass();
                    Check("灰王追踪不止于布蕾塔客厅", UI.WorldMap.DevCompassFight == ItemType.RelicGreyPrince && UI.WorldMap.DevCompassRoom == "Room_Bretta_Basement", grey);
                    CheckWay(Check, "布蕾塔家");
                    yield return GoTo("Room_Bretta_Basement", "top1");
                    UI.WorldMap.DevCompassForget();
                    grey = UI.WorldMap.DevCompass();
                    Check("灰王路线终点为原生入梦雕像", Vector2.Distance(UI.WorldMap.DevCompassEnd, new Vector2(698.58f, -22.94f)) < .1f, grey);
                    UI.FullMap.DevSetOpen(true);
                    yield return new WaitForSeconds(.5f);
                    Check("灰王地下室全屏显示路线", UI.WorldMap.DevFullCompassDots > 0, grey);
                    Shot("Room_Bretta_Basement", "compass_grey_prince_statue");
                    yield return new WaitForSeconds(.2f);
                    // Zote dead: his fights pass over to the next (here none is left).
                    pd.zoteDead = true;
                    UI.WorldMap.DevCompassForget();
                    string blocked = UI.WorldMap.DevCompass();
                    Check("左特死后不再指向他的战斗", UI.WorldMap.DevCompassFight != ItemType.RelicGreyPrince && UI.WorldMap.DevCompassFight != ItemType.RelicZote
                        && !BossGuide.NextFights(mod).Contains(ItemType.RelicGreyPrince), blocked);
                    mod.GrantedAbilities.Remove(BossMastery.Prefix + ItemType.RelicZote);
                    mod.GrantedAbilities.Remove(BossMastery.Prefix + ItemType.RelicOblobble);
                    Shop.DevStage = 4;
                    var stage4 = BossGuide.NextFights(mod);
                    Check("左特死后斗兽场指向下一个首领", !stage4.Contains(ItemType.RelicZote) && stage4.Contains(ItemType.RelicOblobble), string.Join("、", stage4));
                    // The voice's way on (BossGuide.Compose) passes over him the same way.
                    var wayOn = BossGuide.DevWayOn(mod, ItemType.RelicLobsterLancer);
                    Check("左特死后指引指向下一个首领", wayOn == ItemType.RelicOblobble, wayOn?.ToString() ?? "无");
                    mod.GrantedAbilities.Add(BossMastery.Prefix + ItemType.RelicOblobble);
                    wayOn = BossGuide.DevWayOn(mod, ItemType.RelicLobsterLancer);
                    Check("左特死后指引跳过灰色王子左特", wayOn != ItemType.RelicZote && wayOn != ItemType.RelicGreyPrince, wayOn?.ToString() ?? "无（收束对白）");
                    mod.GrantedAbilities.Remove(BossMastery.Prefix + ItemType.RelicOblobble);
                    pd.zoteDead = false;
                    Check("左特活着时仍指向他", BossGuide.NextFights(mod).Contains(ItemType.RelicZote), string.Join("、", BossGuide.NextFights(mod)));
                    wayOn = BossGuide.DevWayOn(mod, ItemType.RelicLobsterLancer);
                    Check("左特活着时指引仍指向他", wayOn == ItemType.RelicZote, wayOn?.ToString() ?? "无");
                    UI.FullMap.DevSetOpen(false);
                }
                finally { pd.zoteDead = oldDead; pd.zoteRescuedBuzzer = oldBuzzer; pd.zoteRescuedDeepnest = oldDeepnest; pd.brettaRescued = oldBretta; pd.zoteDefeated = oldDefeated; }

                // Off: nothing pointed to.
                Charms.Click(2);
                UI.WorldMap.DevCompassForget();
                yield return null;
                string off = UI.WorldMap.DevCompass();
                Check("摘下后不再指", !Charms.Worn(2) && UI.WorldMap.DevCompassPoints == 0, off);
            }
            finally
            {
                foreach (var f in new List<string>(mod.GrantedAbilities)) if (f.StartsWith(BossMastery.Prefix)) mod.GrantedAbilities.Remove(f);
                foreach (var f in kept) mod.GrantedAbilities.Add(f);
                Shop.DevStage = stage0;
                Charms.DevAnywhere = false;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 指南针 结果 {pass}/{all}");
        }

        static IEnumerable<string> Costs(Dictionary<ItemType, float> costs)
        {
            foreach (var kv in costs) yield return $"{kv.Key} {(kv.Value < float.MaxValue ? kv.Value.ToString("F0") : "无路")}";
        }

        /// <summary>The way drawn: from the room being played to the fight's room.</summary>
        static void CheckWay(System.Action<string, bool, string> check, string where)
        {
            var scenes = UI.WorldMap.DevCompassScenes;
            string room = UI.WorldMap.DevCompassRoom;
            check($"{where}的路线从当前房间到首领房间", scenes.Count > 0 && scenes[0] == GameManager.instance.sceneName && scenes[scenes.Count - 1] == room && UI.WorldMap.DevCompassPoints >= 2,
                $"{string.Join(" → ", scenes)}（目标 {room}）");
        }
    }
}
