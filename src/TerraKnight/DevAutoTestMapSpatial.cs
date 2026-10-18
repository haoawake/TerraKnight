using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator MapSpatialTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            mod.ApplyCreative(true);
            UI.WorldMap.RevealWorld();
            int checks = 0, failures = 0;
            var rows = new List<object>();
            void Check(string label, bool pass)
            {
                checks++;
                if (!pass) { failures++; Plugin.Log.LogError($"[AutoTest] 地图空间分层 {label} 不通过"); }
            }
            foreach (string room in UI.WorldMap.DevSceneNames().Where(s => s.StartsWith("Room_")))
                Check("室内场景/" + room, UI.WorldMap.DevDeep(room));
            foreach (string room in new[] { "Cliffs_01", "Cliffs_02" })
            {
                var counts = UI.WorldMap.DevLayerPixels(room, DevCopy.SaveDir);
                Check("双层实际纹理/" + room, counts.Shallow > 0 && counts.Deep > 0 && counts.Overlap == 0 && counts.Wrong == 0);
                rows.Add(new { Scene=room,Shallow=counts.Shallow,Deep=counts.Deep,Overlap=counts.Overlap,Wrong=counts.Wrong });
            }
            foreach (string room in new[] { "Cliffs_03", "Cliffs_04", "Cliffs_05", "Cliffs_06", "Room_nailmaster", "Room_Mask_Maker", "Room_Colosseum_01", "Room_Wyrm" })
            {
                var counts = UI.WorldMap.DevLayerPixels(room, DevCopy.SaveDir);
                Check("整间室内实际纹理/" + room, counts.Shallow == 0 && counts.Deep > 0 && counts.Overlap == 0 && counts.Wrong == 0);
            }
            Check("暗道入口标记在内层", UI.WorldMap.DevGateDeep("Cliffs_02", "bot1"));
            Check("室外德特茅斯入口在外层", !UI.WorldMap.DevGateDeep("Cliffs_02", "right1"));
            Check("悬崖洞窟入口在内层", UI.WorldMap.DevGateDeep("Cliffs_01", "right2"));
            yield return SealTravel("Cliffs_02", "bot1");
            UI.FullMap.DevSetOpen(false);
            var hero = HeroController.instance;
            foreach (var sample in new[]
            {
                (new Vector2(195f,6f),true), (new Vector2(190f,20f),true),
                (new Vector2(175f,13.5f),true), (new Vector2(166f,13.5f),true),
                (new Vector2(56f,41f),false), (new Vector2(150f,45f),false),
            })
            {
                Vector2 at = sample.Item1 + (Vector2)TerraWorld.Live.Root.position;
                hero.transform.position = new Vector3(at.x,at.y,hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().position=at;
                hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                yield return new WaitForSecondsRealtime(.1f);
                Check("同房间位置分类/"+sample.Item1,UI.WorldMap.DevDeepAt("Cliffs_02",sample.Item1)==sample.Item2);
                Check("同房间玩家当前层/"+sample.Item1,UI.WorldMap.DevViewDeep==sample.Item2);
                rows.Add(new {Scene="Cliffs_02",X=sample.Item1.x,Y=sample.Item1.y,ExpectedDeep=sample.Item2,ActualDeep=UI.WorldMap.DevViewDeep});
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"map-spatial-result.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Rows=rows},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 地图空间分层 {checks}，失败 {failures}");
        }
    }
}
