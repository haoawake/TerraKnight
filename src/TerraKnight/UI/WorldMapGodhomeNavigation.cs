using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class WorldMap
    {
        static Rect godhomeTab;

        // A chart-navigation shortcut, available only once the save can actually see the region.
        static Room GodhomeMapTarget()
        {
            foreach (string name in new[] { "GG_Atrium", "GG_Workshop", "GG_Atrium_Roof" })
                if (rooms.TryGetValue(name, out var room) && room.Captured && room.AnyShown) return room;
            foreach (var room in rooms.Values)
                if (room.Area == "神居" && room.Captured && room.AnyShown) return room;
            return null;
        }

        internal static bool JumpToGodhome()
        {
            var room = GodhomeMapTarget();
            if (room == null) return false;
            layerChoice = MapLayer.Deep;
            pan = new Vector2(room.At.x + room.DrawnW / 2f, room.At.y + room.DrawnH / 2f);
            zoom = Mathf.Clamp(Mathf.Min(Screen.width / (room.W + 80f), Screen.height / (room.H + 80f)), .3f, 4f);
            TerraMod.Instance?.PlaySound("Menu_Tick", .7f);
            return true;
        }

        internal static int DevAtlasRoomCount => atlas.Count;
        internal static bool DevGodhomePlayerPoint(Vector2 at) => ScenePoint("GG_Atrium", at, out _);
        internal static Vector2Int DevLayoutOf(string scene) => layout[scene];
        internal static string DevGodhomeGateTarget(string scene, string name) =>
            Gates.TryGetValue(scene, out var list) ? list.Find(g => g.Name == name)?.Partner?.Scene : null;
    }
}
