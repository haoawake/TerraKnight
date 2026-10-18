using HarmonyLib;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's bed as the spawn point (Player.TileInteractionsUse for tile 79): a right click on a bed sets the player's
    /// spawn there ("Game.SpawnPointSet"), a right click on the bed that already is it takes it off again
    /// ("Game.SpawnPointRemoved"). The newest one set wins, a bench rested on after it too (the game's own respawn point).
    /// After a death the player comes back beside the bed, standing; a bed knocked down (or fallen) is no spawn point
    /// any more, and the one there was before it (the bench) is back. The point is the game's own record: the respawn
    /// scene is the bed's, the marker a name no object has (<see cref="Prefix"/>, the bed's place and the point before),
    /// which <see cref="AtTheBed"/> finds.
    /// </summary>
    [HarmonyPatch]
    internal static class Beds
    {
        /// <summary>The respawn marker's name for a bed: "TerraKnight Bed@x,y|scene before|marker before|type before".</summary>
        public const string Prefix = "TerraKnight Bed@";

        public static int DevSet, DevRemoved, DevSpawned;
        public static string DevLast = "";

        static readonly Color ServerMessage = new Color(255 / 255f, 240 / 255f, 20 / 255f);

        static string Key(Vector2 local) => local.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + local.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The bed the spawn is at (its scene and bottom middle, scene coordinates), and what it replaced; false if none.</summary>
        public static bool Current(out string scene, out Vector2 local, out string prevScene, out string prevMarker, out int prevType)
        {
            scene = prevScene = prevMarker = null;
            local = default;
            prevType = 0;
            var pd = PlayerData.instance;
            if (pd == null || string.IsNullOrEmpty(pd.respawnMarkerName) || !pd.respawnMarkerName.StartsWith(Prefix)) return false;
            var parts = pd.respawnMarkerName.Substring(Prefix.Length).Split('|');
            var xy = parts[0].Split(',');
            if (xy.Length != 2 || !float.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)) return false;
            scene = pd.respawnScene;
            local = new Vector2(x, y);
            prevScene = parts.Length > 1 ? parts[1] : null;
            prevMarker = parts.Length > 2 ? parts[2] : null;
            prevType = parts.Length > 3 && int.TryParse(parts[3], out int t) ? t : 0;
            return true;
        }

        static void Say(string key, string zh) =>
            Net.NetChat.Add(Terraria.TerrariaText.Get(Lang.Culture, key) ?? Lang.T(zh), ServerMessage);

        /// <summary>A right click on a bed: the spawn here, or (the spawn already here) none.</summary>
        public static void Toggle(string scene, Vector2 local)
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            if (Current(out var s, out var at, out var prevScene, out var prevMarker, out int prevType) && s == scene && Key(at) == Key(local))
            {
                Restore(prevScene, prevMarker, prevType);
                DevRemoved++;
                DevLast = $"取消 {scene} {Key(local)}";
                Say("Game.SpawnPointRemoved", "已移除重生点！");
                return;
            }
            // What it replaces: the point before the beds (another bed's own "before" carried on).
            string beforeScene = pd.respawnScene, beforeMarker = pd.respawnMarkerName;
            int beforeType = pd.respawnType;
            if (Current(out _, out _, out var ps, out var pm, out int pt)) { beforeScene = ps; beforeMarker = pm; beforeType = pt; }
            pd.respawnScene = scene;
            pd.respawnMarkerName = Prefix + Key(local) + "|" + beforeScene + "|" + beforeMarker + "|" + beforeType;
            pd.respawnType = 0;
            DevSet++;
            DevLast = $"设在 {scene} {Key(local)}";
            Say("Game.SpawnPointSet", "已设置重生点！");
        }

        /// <summary>A bed knocked down or fallen: if it was the spawn, the point before it is back.</summary>
        public static void Gone(string scene, Vector2 local)
        {
            if (!Current(out var s, out var at, out var prevScene, out var prevMarker, out int prevType) || s != scene || Key(at) != Key(local)) return;
            Restore(prevScene, prevMarker, prevType);
            DevRemoved++;
            DevLast = $"床没了 {scene} {Key(local)}";
            Say("Game.SpawnPointRemoved", "已移除重生点！");
        }

        static void Restore(string scene, string marker, int type)
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            if (string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(marker) || marker.StartsWith(Prefix))
            {
                // Nothing before it: the world's spawn (Pylons: King's Pass under its top entrance).
                pd.respawnScene = "Tutorial_01";
                pd.respawnMarkerName = Pylons.WorldSpawnMarker;
                pd.respawnType = 0;
                return;
            }
            pd.respawnScene = scene;
            pd.respawnMarkerName = marker;
            pd.respawnType = type;
        }

        static Transform spot;

        /// <summary>
        /// HeroController.LocateSpawnPoint finds no object named for a bed: the bed's own place, standing on the floor in
        /// front of it. Before the other fallbacks (Pylons.SpawnPointFallback), which leave a found point alone.
        /// </summary>
        [HarmonyPostfix, HarmonyPriority(Priority.High)]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.LocateSpawnPoint))]
        static void AtTheBed(ref Transform __result)
        {
            if (__result != null || !Current(out var scene, out var local, out var prevScene, out var prevMarker, out int prevType)) return;
            var active = USM.GetActiveScene();
            if (!active.IsValid() || active.name != scene) return;
            if (!Stations.BedAt(scene, local, out var world))
            {
                // Gone while its scene was away: the point before it, the game's own fallbacks for this time.
                Restore(prevScene, prevMarker, prevType);
                Plugin.Log.LogWarning($"[床] {scene} 的床不在了，重生点退回 {PlayerData.instance.respawnScene} 的 {PlayerData.instance.respawnMarkerName}");
                return;
            }
            if (spot == null) spot = new GameObject("TerraKnight Bed Respawn").transform;
            spot.position = new Vector3(world.x, world.y + 1f, 0f);
            __result = spot;
            DevSpawned++;
            DevLast = $"在 {scene} 的床边复活 {(Vector2)spot.position:F1}";
            Plugin.Log.LogInfo($"[床] {DevLast}");
        }
    }
}
