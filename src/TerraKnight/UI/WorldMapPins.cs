using System;
using System.Linq;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The game's own map pins on the world map (the user's 10-28 "让收藏家的地图有用一点吧…高亮所有毛毛虫和座椅"): with the
    /// Collector's Map (hasPinGrub) every grub not yet freed, in the game's grub pin; with Iselda's Bench Pin (hasPinBench), or
    /// the Collector's Map, every bench, in its bench pin. Where they are, read from the game's own scenes (each grub bottle's
    /// and bench's place). Drawn on the full map, the minimap and the overlay, on the layer they stand on; named on hover.
    /// </summary>
    internal static partial class WorldMap
    {
        internal readonly struct MapPin
        {
            public readonly string Scene;
            public readonly Vector2 Local;
            public MapPin(string scene, float x, float y) { Scene = scene; Local = new Vector2(x, y); }
        }

        internal static readonly MapPin[] GrubPins =
        {
            new MapPin("Crossroads_03", 4.34f, 41.57f),
            new MapPin("Crossroads_05", 56.17f, 16.6f),
            new MapPin("Crossroads_31", 19.34f, 13.54f),
            new MapPin("Crossroads_35", 3.99f, 44.48f),
            new MapPin("Crossroads_48", 49.82f, 3.48f),
            new MapPin("Ruins_House_01", 32.5f, 6.59f),
            new MapPin("Ruins1_05", 50.89f, 142.44f),
            new MapPin("Ruins1_32", 5.4f, 24.52f),
            new MapPin("Ruins2_03", 23.18f, 50.56f),
            new MapPin("Ruins2_07", 70.29f, 5.55f),
            new MapPin("Ruins2_11", 64.14f, 135.61f),
            new MapPin("Ruins2_11", 38.24f, 135.61f),
            new MapPin("Ruins2_11", 55.17f, 135.61f),
            new MapPin("Fungus1_06", 157.52f, 21.56f),
            new MapPin("Fungus1_07", 55.06f, 12.47f),
            new MapPin("Fungus1_13", 135.62f, 24.41f),
            new MapPin("Fungus1_21", 85.66f, 24.49f),
            new MapPin("Fungus1_28", 87.01f, 52.54f),
            new MapPin("Fungus2_18", 142.77f, 50.66f),
            new MapPin("Fungus2_20", 7.82f, 42.51f),
            new MapPin("Fungus3_10", 11.83f, 35.5f),
            new MapPin("Fungus3_22", 21.29f, 134.45f),
            new MapPin("Fungus3_47", 6.11f, 22.53f),
            new MapPin("Fungus3_48", 16.18f, 94.47f),
            new MapPin("RestingGrounds_10", 117.52f, 17.45f),
            new MapPin("Mines_03", 24.47f, 75.45f),
            new MapPin("Mines_04", 15.14f, 77.56f),
            new MapPin("Mines_16", 54.57f, 22.59f),
            new MapPin("Mines_19", 38.85f, 26.51f),
            new MapPin("Mines_24", 51.14f, 6.61f),
            new MapPin("Mines_31", 29.48f, 33.6f),
            new MapPin("Mines_35", 107.02f, 69.47f),
            new MapPin("Deepnest_03", 14.23f, 103.51f),
            new MapPin("Deepnest_31", 100.17f, 36.5f),
            new MapPin("Deepnest_36", 65.83f, 4.5f),
            new MapPin("Deepnest_39", 122.55f, 46.57f),
            new MapPin("Deepnest_Spider_Town", 99.98f, 119.41f),
            new MapPin("Deepnest_East_11", 62.85f, 63.59f),
            new MapPin("Deepnest_East_14", 154.35f, 22.6f),
            new MapPin("Abyss_17", 152.45f, 4.52f),
            new MapPin("Abyss_19", 82.52f, 85.48f),
            new MapPin("Waterways_04", 126.39f, 8.49f),
            new MapPin("Waterways_13", 108.82f, 53.52f),
            new MapPin("Waterways_14", 186.67f, 37.51f),
            new MapPin("Hive_03", 20.37f, 139.47f),
            new MapPin("Hive_04", 209.39f, 126.47f),
        };

        internal static readonly MapPin[] BenchPins =
        {
            new MapPin("Town", 132.38f, 10.7f),
            new MapPin("Room_nailmaster", 19.69f, 3.59f),
            new MapPin("Room_Tram", 22.92f, 8.66f),
            new MapPin("Room_Tram_RG", 22.92f, 8.66f),
            new MapPin("Room_Colosseum_02", 106.31f, 41.0f),
            new MapPin("Room_Slug_Shrine", 21.09f, 5.73f),
            new MapPin("Crossroads_04", 149.8f, 10.71f),
            new MapPin("Crossroads_30", 37.94f, 4.67f),
            new MapPin("Crossroads_47", 29.34f, 5.66f),
            new MapPin("Ruins_Bathhouse", 68.49f, 31.68f),
            new MapPin("Ruins1_02", 31.66f, 11.63f),
            new MapPin("Ruins1_18", 77.05f, 9.64f),
            new MapPin("Ruins1_29", 14.63f, 5.65f),
            new MapPin("Ruins1_31", 22.6f, 15.8f),
            new MapPin("Ruins1_31", 22.66f, 16.15f),
            new MapPin("Ruins2_08", 14.63f, 5.65f),
            new MapPin("Fungus1_01b", 23.32f, 30.68f),
            new MapPin("Fungus1_15", 41.47f, 33.58f),
            new MapPin("Fungus1_16_alt", 30.97f, 5.67f),
            new MapPin("Fungus1_24", 16.79f, 20.7f),
            new MapPin("Fungus1_31", 27.34f, 50.64f),
            new MapPin("Fungus1_37", 23.36f, 5.77f),
            new MapPin("Fungus2_02", 27.91f, 5.68f),
            new MapPin("Fungus2_13", 31.41f, 6.66f),
            new MapPin("Fungus2_26", 38.75f, 4.39f),
            new MapPin("Fungus2_31", 69.22f, 2.73f),
            new MapPin("Fungus3_40", 171.76f, 12.68f),
            new MapPin("Fungus3_50", 24.44f, 115.83f),
            new MapPin("Fungus3_50", 24.39f, 116.18f),
            new MapPin("Fungus3_archive", 67.96f, 22.76f),
            new MapPin("RestingGrounds_09", 13.77f, 5.68f),
            new MapPin("RestingGrounds_12", 67.73f, 2.66f),
            new MapPin("Mines_18", 30.03f, 10.7f),
            new MapPin("Mines_29", 36.34f, 2.61f),
            new MapPin("Deepnest_14", 59.15f, 43.76f),
            new MapPin("Deepnest_30", 38.58f, 4.71f),
            new MapPin("Deepnest_Spider_Town", 35.48f, 80.9f),
            new MapPin("Deepnest_Spider_Town", 47.02f, 57.73f),
            new MapPin("Deepnest_East_06", 334.87f, 14.65f),
            new MapPin("Deepnest_East_13", 32.6f, 2.67f),
            new MapPin("Abyss_18", 181.55f, 13.82f),
            new MapPin("Abyss_18", 181.6f, 14.17f),
            new MapPin("Abyss_22", 62.05f, 5.72f),
            new MapPin("Waterways_02", 11.38f, 20.86f),
            new MapPin("White_Palace_06", 35.1f, 62.22f),
            new MapPin("Hive_01", 44.62f, 7.75f),
            new MapPin("Room_Final_Boss_Atrium", 196.0f, 5.64f),
        };

        public static bool GrubsMarked(PlayerData pd) => pd != null && pd.hasPinGrub;
        public static bool BenchesMarked(PlayerData pd) => pd != null && (pd.hasPinBench || pd.hasPinGrub);

        /// <summary>Dev: pins drawn the last frame; why the rest weren't (not on the layer shown, not on the map, off the view), sprites found.</summary>
        public static int DevGrubPins, DevBenchPins, DevPinsOffLayer, DevPinsOffMap, DevPinsClipped;
        public static bool DevPinSprites;

        static Sprite grubPin, benchPin;
        static float pinsLookedAt = -10f;

        /// <summary>The game's grub and bench pins (its map's atlas, loaded with the game), looked for now and then until found.</summary>
        static bool PinSprites()
        {
            if (grubPin != null && benchPin != null) return true;
            if (Time.realtimeSinceStartup - pinsLookedAt < 5f) return false;
            pinsLookedAt = Time.realtimeSinceStartup;
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null) continue;
                if (s.name == "pin_grub_location") grubPin = s;
                else if (s.name == "pin_bench") benchPin = s;
            }
            return grubPin != null && benchPin != null;
        }

        internal static void DrawSprite(Sprite s, Rect rect)
        {
            var tex = s.texture;
            var r = s.textureRect;
            GUI.DrawTextureWithTexCoords(rect, tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height));
        }

        /// <summary>A grub freed: the scene's freed grubs counted (the Tower of Love has three, freed one by one).</summary>
        static bool GrubFreed(PlayerData pd, MapPin pin, int nth)
        {
            var freed = pd.scenesGrubRescued;
            if (freed == null) return false;
            int n = 0;
            foreach (var s in freed) if (s == pin.Scene) n++;
            return n > nth;
        }

        static string DrawMapPins(Func<Vector2, Vector2> toScreen, float size, Rect? clip, Vector2? mouse)
        {
            var pd = PlayerData.instance;
            DevGrubPins = DevBenchPins = DevPinsOffLayer = DevPinsOffMap = DevPinsClipped = 0;
            bool grubs = GrubsMarked(pd), benches = BenchesMarked(pd);
            if (!grubs && !benches) return null;
            DevPinSprites = PinSprites();
            if (!DevPinSprites) return null;
            string hovered = null;
            void One(Sprite s, MapPin pin, string name, ref int count)
            {
                if (!Shown(pin.Scene, pin.Local)) { DevPinsOffLayer++; return; }
                if (!ScenePoint(pin.Scene, pin.Local, out var cell)) { DevPinsOffMap++; return; }
                var at = toScreen(cell);
                if (clip.HasValue && !clip.Value.Contains(at)) { DevPinsClipped++; return; }
                var rect = new Rect(at.x - size / 2f, at.y - size / 2f, size, size);
                DrawSprite(s, rect);
                count++;
                if (mouse.HasValue && rect.Contains(mouse.Value)) hovered = Lang.T(name) + "（" + Areas.Display(Areas.AreaName(pin.Scene)) + "）";
            }
            if (benches)
                foreach (var pin in BenchPins) One(benchPin, pin, "长椅", ref DevBenchPins);
            if (grubs)
            {
                string last = null;
                int nth = 0;
                foreach (var pin in GrubPins)
                {
                    nth = pin.Scene == last ? nth + 1 : 0;
                    last = pin.Scene;
                    if (!GrubFreed(pd, pin, nth)) One(grubPin, pin, "被困的幼虫", ref DevGrubPins);
                }
            }
            return hovered;
        }
    }
}
