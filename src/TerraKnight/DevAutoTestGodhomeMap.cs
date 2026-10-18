using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeMapVerify()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            int checks = 0, failures = 0;
            var evidence = new List<object>();
            void Check(string name, bool pass)
            {
                checks++; evidence.Add(new { Name = name, Passed = pass });
                if (!pass) { failures++; Plugin.Log.LogError("[AutoTest] 神居地图验证/" + name + " 不通过"); }
            }
            Check("未探索普通存档不显示神居导航", !UI.WorldMap.JumpToGodhome());
            mod.ApplyCreative(true);
            UI.WorldMap.RevealWorld();
            var names = UI.WorldMap.DevSceneNames().Where(s => s.StartsWith("GG_")).ToArray();
            Check("72个原生地形场景布局", names.Length == 72);
            Check("保留原图集并补齐神居", UI.WorldMap.DevAtlasRoomCount == 451);
            foreach (var name in names)
            {
                Check("图集包含/" + name, UI.WorldMap.DevAtlasContains(name));
                var pixels = UI.WorldMap.DevLayerPixels(name, DevCopy.SaveDir);
                bool local = name.StartsWith("GG_Pipeway") || name.StartsWith("GG_Waterways") || name.StartsWith("GG_Lurker");
                Check("图层及实际像素/" + name, pixels.Overlap == 0 && pixels.Wrong == 0 &&
                    (local ? pixels.Shallow > 0 && pixels.Deep == 0 : pixels.Deep > 0 && pixels.Shallow == 0));
            }
            Check("中庭与众神堂入口匹配", UI.WorldMap.DevGodhomeGateTarget("GG_Atrium", "Door_Workshop") == "GG_Workshop");
            Check("众神堂返回中庭", UI.WorldMap.DevGodhomeGateTarget("GG_Workshop", "left1") == "GG_Atrium");
            Check("垃圾坑接水道", UI.WorldMap.DevGodhomeGateTarget("GG_Pipeway", "right1") == "Waterways_08");
            Check("垃圾坑接捷径", UI.WorldMap.DevGodhomeGateTarget("GG_Waterways", "door1") == "Room_GG_Shortcut");
            var pipe = UI.WorldMap.DevLayoutOf("GG_Pipeway");
            var water = UI.WorldMap.DevLayoutOf("Waterways_08");
            Check("水道与通道主入口对齐", pipe + new Vector2Int(298, 10) == water + new Vector2Int(0, 110));
            var atrium = UI.WorldMap.DevLayoutOf("GG_Atrium");
            var roof = UI.WorldMap.DevLayoutOf("GG_Atrium_Roof");
            Check("中庭顶层主入口对齐", atrium + new Vector2Int(161, 170) == roof + new Vector2Int(95, 12));
            UI.WorldMap.DevSetOpen(true);
            Check("神居导航可用", UI.WorldMap.JumpToGodhome() && UI.WorldMap.DevViewDeep);
            UI.WorldMap.DevSetOpen(false);
            yield return GoTo("GG_Atrium", "door_dreamEnter");
            yield return new WaitForSeconds(1f);
            Check("实际神居场景可显示地图", UI.WorldMap.DevGodhomePlayerPoint(HeroController.instance.transform.position));
            Check("实际神居在深层", UI.WorldMap.DevDeep("GG_Atrium"));
            var oldDifficulty = GameDifficulty.Current;
            GameDifficulty.Current = DifficultyLevel.Master;
            Check("大师46防御实际承伤", new Player.TerraStats { Life = 1000, Defense = 46 }.TakeMasks(1, 317) == 271);
            Check("大师74防御实际承伤", new Player.TerraStats { Life = 1000, Defense = 74 }.TakeMasks(1, 317) == 243);
            GameDifficulty.Current = oldDifficulty;
            yield return GoTo("Town", "left1");
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "godhome-map-verification.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Rooms = names.Length, AtlasRooms = UI.WorldMap.DevAtlasRoomCount, VisualWindowVerified = false, Evidence = evidence }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 神居地图验证 {checks} 项，失败 {failures}");
        }

        IEnumerator GodhomeMapSurvey()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            UI.WorldMap.DevExtendAtlas(); // Preserve the existing world atlas while adding the omitted DLC scenes.
            var names = new List<string>();
            for (int i = 0; i < USM.sceneCountInBuildSettings; i++)
            {
                string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (name.StartsWith("GG_")) names.Add(name);
            }
            names.Sort(StringComparer.Ordinal);
            var rows = new List<object>();
            var skipped = new List<object>();
            var gateRows = new List<string>();
            int x = 3600, y = 1200, rowHeight = 0, checks = 0, failures = 0;
            foreach (var name in names)
            {
                var scene = USM.GetSceneByName(name);
                bool loadedHere = !scene.isLoaded;
                if (loadedHere)
                {
                    var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    while (load != null && !load.isDone) yield return null;
                }
                yield return null;
                scene = USM.GetSceneByName(name);
                try
                {
                    var map = TerrainSystem.SceneTileMap(scene);
                    SceneManager manager = null;
                    foreach (var root in scene.GetRootGameObjects())
                        if (manager == null) manager = root.GetComponentInChildren<SceneManager>(true);
                    if (map?.data == null || manager == null || manager.sceneType != GlobalEnums.SceneType.GAMEPLAY)
                        skipped.Add(new { Scene = name, Reason = map?.data == null ? "No terrain tilemap" : "Not a gameplay scene" });
                    else
                    {
                        var grid = TerrainSystem.BuildGrid(map, TerrainSystem.TerrainOutlines(map), TerrainSystem.HandPlacedTerrain(map), name, out _, out _);
                        var offset = TerrainSystem.CellOffsetOf(map);
                        if (x + grid.Width > 5600 && x > 3600) { x = 3600; y += rowHeight + 60; rowHeight = 0; }
                        var at = new Vector2Int(x, y);
                        UI.WorldMap.DevPlaceAtlasRoom(name, at);
                        UI.WorldMap.BakeScene(name, grid, offset, scene);
                        var gates = new List<object>();
                        foreach (var root in scene.GetRootGameObjects())
                            foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                            {
                                var col = tp.GetComponent<Collider2D>();
                                Vector2 centre = (col != null ? (Vector2)col.bounds.center : (Vector2)tp.transform.position) / CellGrid.CellSize - (Vector2)offset;
                                int gx = Mathf.RoundToInt(centre.x), gy = Mathf.RoundToInt(centre.y);
                                string side = tp.name.Contains("left") ? "l" : tp.name.Contains("right") ? "r" : tp.name.Contains("top") ? "t" : tp.name.Contains("bot") ? "b" : "d";
                                gateRows.Add($"{name}|{tp.name}|{side}|{gx},{gy}|{gx},{gy}|{tp.targetScene}|{tp.entryPoint}");
                                gates.Add(new { Name = tp.name, To = tp.targetScene, Entry = tp.entryPoint, X = gx, Y = gy, Side = side });
                            }
                        rows.Add(new { Scene = name, X = x, Y = y, Width = grid.Width, Height = grid.Height, Gates = gates });
                        checks++;
                        if (!UI.WorldMap.DevAtlasContains(name)) { failures++; Plugin.Log.LogError("[AutoTest] 神居地图/未烘焙 " + name + " 不通过"); }
                        Plugin.Log.LogInfo($"[AutoTest] 神居地图 {rows.Count}/{names.Count} {name} {grid.Width}x{grid.Height} 地图 {x},{y}");
                        x += grid.Width + 60;
                        rowHeight = Mathf.Max(rowHeight, grid.Height);
                    }
                }
                catch (Exception e)
                {
                    failures++;
                    Plugin.Log.LogError("[AutoTest] 神居地图/" + name + " 不通过：" + e);
                }
                if (loadedHere)
                {
                    var unload = USM.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
                PlayerData.instance.isInvincible = true;
                if (rows.Count % 8 == 0) { var gc = Resources.UnloadUnusedAssets(); while (!gc.isDone) yield return null; }
            }
            string atlas = UI.WorldMap.WriteAtlas();
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "godhome-map-survey.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Rooms = rows, Skipped = skipped, GateRows = gateRows, Atlas = atlas }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 神居地图 {checks} 场景，失败 {failures}；{atlas}");
        }
    }
}
