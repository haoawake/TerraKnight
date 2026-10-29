using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "compassparts", the user's 10-27 "指南针…应该根据当前的时期进行指引…毛里克的指引…指到了科尼法旁边"): the
    /// Wayward Compass's way through what the save has opened (UI.WorldMapCompassParts). To the Brooding Mawlek from
    /// Dirtmouth with its wall unbroken: in by Crossroads_36 and the arena's left gate, never through Cornifer's room
    /// (Crossroads_33, whose left gate the wall shuts); the wall broken (crossroadsMawlekWall): Cornifer's room's way may
    /// be taken; standing in Cornifer's room with the wall shut: not out by its left gate. The way's end on the Mawlek's
    /// arena floor. Then every fight from Dirtmouth: a way drawn, whether it needed the old way over every gate, and how
    /// long a pick takes. Lines "[AutoTest] 指南针分区 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CompassPartsTest()
        {
            int pass = 0, all = 0;
            void Check(string name, bool ok, string detail = "")
            {
                all++;
                if (ok) pass++;
                Plugin.Log.LogInfo($"[AutoTest] 指南针分区 {name} {(ok ? "通过" : "不通过")} {detail}");
            }
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            Charms.DevSetPage(true);
            Charms.DevAnywhere = true;
            pd.SetBool("gotCharm_2", true);
            if (!Charms.Worn(2)) Charms.Click(2);
            bool wall0 = pd.GetBool("crossroadsMawlekWall");
            try
            {
                UI.WorldMap.DevCompassOnly = ItemType.RelicMawlek;
                pd.SetBool("crossroadsMawlekWall", false);
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(2f);
                UI.WorldMap.DevCompassForget();
                string shut = UI.WorldMap.DevCompass();
                var scenes = new List<string>(UI.WorldMap.DevCompassScenes);
                int at09 = scenes.IndexOf("Crossroads_09");
                Check("墙没破：不经科尼法的房间进毛里克", at09 > 0 && scenes[at09 - 1] != "Crossroads_33" && !UI.WorldMap.DevIgnoringShutters, shut);
                var end = UI.WorldMap.DevCompassEnd;
                var feet = UI.WorldMap.DevSceneCell("Crossroads_09", new Vector2(68f, 4.1f));
                Check("终点在毛里克的战场上", Vector2.Distance(end, feet) < 4f, $"终点 {end}，战场落点 {feet}");
                Plugin.Log.LogInfo($"[AutoTest] 指南针分区 {UI.WorldMap.DevParts("Crossroads_33")}");

                pd.SetBool("crossroadsMawlekWall", true);
                UI.WorldMap.DevCompassForget();
                string open = UI.WorldMap.DevCompass();
                Check("墙破了：科尼法房间那条路可以走", UI.WorldMap.DevCompassScenes.Contains("Crossroads_33") && UI.WorldMap.DevCompassScenes.LastOrDefault() == "Crossroads_09", open);
                Plugin.Log.LogInfo($"[AutoTest] 指南针分区 {UI.WorldMap.DevParts("Crossroads_33")}");

                pd.SetBool("crossroadsMawlekWall", false);
                yield return GoTo("Crossroads_33", "right2");
                yield return new WaitForSeconds(2f);
                UI.WorldMap.DevCompassForget();
                string inRoom = UI.WorldMap.DevCompass();
                var here = UI.WorldMap.DevCompassScenes;
                Check("站在科尼法的房间、墙没破：不从左门出", here.Count >= 2 && here[1] != "Crossroads_09" && !UI.WorldMap.DevIgnoringShutters, $"{inRoom}；{UI.WorldMap.DevParts("Crossroads_33")}");
                Shot("Crossroads_33", "compassparts_cornifer");

                // Every fight from Dirtmouth.
                UI.WorldMap.DevCompassOnly = null;
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(2f);
                var none = new List<string>();
                var fallback = new List<string>();
                var watch = new System.Diagnostics.Stopwatch();
                long slowest = 0;
                foreach (var fight in UI.WorldMap.DevCompassRooms.Keys)
                {
                    UI.WorldMap.DevCompassOnly = fight;
                    UI.WorldMap.DevCompassForget();
                    watch.Restart();
                    string way = UI.WorldMap.DevCompass();
                    watch.Stop();
                    slowest = System.Math.Max(slowest, watch.ElapsedMilliseconds);
                    if (UI.WorldMap.DevCompassPoints < 2) none.Add(fight.ToString());
                    else if (UI.WorldMap.DevIgnoringShutters) fallback.Add(fight.ToString());
                    Plugin.Log.LogInfo($"[AutoTest] 指南针分区 {fight}：{way}{(UI.WorldMap.DevIgnoringShutters ? "（按已打开的路走不到）" : "")}，{watch.ElapsedMilliseconds} ms");
                    yield return null;
                }
                Check("每个首领都画得出路线", none.Count == 0, $"没有路线的：{string.Join("、", none)}");
                Plugin.Log.LogInfo($"[AutoTest] 指南针分区 新存档按已打开的路走不到、改走全部门的：{fallback.Count} 个 {string.Join("、", fallback)}");
                Check("找一次路不卡", slowest < 200, $"最慢 {slowest} ms");
            }
            finally
            {
                UI.WorldMap.DevCompassOnly = null;
                pd.SetBool("crossroadsMawlekWall", wall0);
                Charms.DevAnywhere = false;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 指南针分区 结果 {pass}/{all}");
        }
    }
}
