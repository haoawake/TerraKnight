using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The frame a scene's room on the map stands for. The game's compass stretches the scene's
    /// tilemap over its room, unless the scene says otherwise: some scenes' state machines call
    /// GameMap.SetManualTilemap with a frame of their own (Cliffs_02: origin offset 40,0 and
    /// 170x185 instead of its 240x85 tilemap), and the compass then puts the player at
    /// ((x + offsetX) / width, (y + offsetY) / height) of the room. The call is caught as it
    /// happens, by the scene of the state machine making it; a seamless neighbour making it
    /// behind the player keeps it to itself (the compass stays the current scene's) and has it
    /// applied when it becomes the current scene.
    /// </summary>
    [HarmonyPatch(typeof(GameMap), nameof(GameMap.SetManualTilemap))]
    internal static class MapFrames
    {
        /// <summary>By scene: origin offset x, y and width, height (x, y, z, w), as seen this session.</summary>
        static readonly Dictionary<string, Vector4> frames = new Dictionary<string, Vector4>();

        /// <summary>
        /// The frames every scene sets as it loads (dev suite "blockers" loads each one and lists
        /// them), for a scene not loaded this session: a pylon or the shade there.
        /// </summary>
        static readonly Dictionary<string, Vector4> known = new Dictionary<string, Vector4>
        {
            ["Town"] = new Vector4(0f, 18f, 263f, 110f), ["Crossroads_35"] = new Vector4(0f, 0f, 70f, 50f), ["Crossroads_46"] = new Vector4(0f, 0f, 110f, 25f),
            ["Crossroads_46b"] = new Vector4(30f, 0f, 79f, 25f), ["Crossroads_49"] = new Vector4(0f, 5f, 30f, 180f), ["Crossroads_49b"] = new Vector4(0f, 0f, 30f, 86f),
            ["Ruins1_02"] = new Vector4(0f, 0f, 60f, 76f), ["Ruins1_09"] = new Vector4(0f, 0f, 40f, 30f), ["Ruins1_27"] = new Vector4(0f, 8f, 80f, 80f),
            ["Ruins2_04"] = new Vector4(0f, 8f, 150f, 100f), ["Ruins2_08"] = new Vector4(0f, 8f, 50f, 60f), ["Ruins2_09"] = new Vector4(0f, 5f, 80f, 30f),
            ["Ruins2_10b"] = new Vector4(0f, -5f, 30f, 205f), ["Ruins2_Watcher_Room"] = new Vector4(0f, 5f, 80f, 160f), ["Fungus1_17"] = new Vector4(0f, 0f, 80f, 20f),
            ["Fungus1_26"] = new Vector4(-10f, 0f, 110f, 65f), ["Fungus2_07"] = new Vector4(0f, 5f, 70f, 30f), ["Fungus2_13"] = new Vector4(0f, 0f, 41f, 116f),
            ["Fungus3_22"] = new Vector4(0f, 0f, 31f, 128f), ["Fungus3_50"] = new Vector4(-15f, -65f, 20f, 133f), ["Cliffs_01"] = new Vector4(40f, 0f, 170f, 185f),
            ["Cliffs_02"] = new Vector4(0f, 82f, 240f, 160f), ["RestingGrounds_08"] = new Vector4(0f, 0f, 145f, 70f), ["RestingGrounds_10"] = new Vector4(0f, 0f, 160f, 30f),
            ["Mines_13"] = new Vector4(0f, 0f, 100f, 45f), ["Mines_24"] = new Vector4(0f, 0f, 60f, 20f), ["Mines_28"] = new Vector4(0f, 0f, 100f, 61f),
            ["Mines_34"] = new Vector4(-10f, 10f, 210f, 110f), ["Deepnest_East_13"] = new Vector4(0f, 5f, 60f, 30f), ["Deepnest_East_14"] = new Vector4(0f, 0f, 160f, 80f),
            ["Deepnest_East_14b"] = new Vector4(0f, 0f, 160f, 80f), ["Abyss_03"] = new Vector4(15f, 0f, 220f, 25f), ["Abyss_03_b"] = new Vector4(0f, 0f, 350f, 25f),
            ["Abyss_03_c"] = new Vector4(100f, 0f, 260f, 25f), ["Abyss_12"] = new Vector4(-30f, 0f, 160f, 30f), ["Abyss_17"] = new Vector4(0f, 0f, 170f, 60f),
            ["Hive_01"] = new Vector4(0f, 0f, 120f, 62f), ["Hive_02"] = new Vector4(-103f, 0f, 95f, 86f), ["Hive_03"] = new Vector4(0f, -63f, 140f, 90f),
            ["Hive_03_c"] = new Vector4(0f, -63f, 140f, 90f), ["Hive_04"] = new Vector4(-105f, -80f, 110f, 70f),
        };

        public static bool TryGet(string scene, out Vector4 frame)
        {
            frame = default;
            if (scene == null) return false;
            if (!frames.TryGetValue(scene, out frame) && !known.TryGetValue(scene, out frame)) return false;
            return frame.z > 0f && frame.w > 0f;
        }

        static bool Prefix(float offsetX, float offsetY, float width, float height)
        {
            var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
            var go = fsm?.GameObject;
            string scene = go != null ? go.scene.name : GameManager.UnsafeInstance?.sceneName;
            if (string.IsNullOrEmpty(scene)) return true;
            if (!frames.ContainsKey(scene)) Plugin.Log.LogInfo($"[地图] {scene} 用自己的地图框：原点偏移 {offsetX},{offsetY}，宽高 {width}x{height}");
            frames[scene] = new Vector4(offsetX, offsetY, width, height);
            var live = World.LiveWorld.Instance;
            return live == null || !live.IsNeighbour(scene);
        }

        /// <summary>Dev: every frame seen.</summary>
        public static string DevAll()
        {
            var sb = new StringBuilder();
            foreach (var kv in frames) sb.Append($" {kv.Key}=({kv.Value.x},{kv.Value.y},{kv.Value.z},{kv.Value.w})");
            return $"{frames.Count} 个：{sb}";
        }
    }
}
