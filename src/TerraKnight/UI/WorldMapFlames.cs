using System;
using System.IO;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class WorldMap
    {
        internal sealed class GrimmFlame
        {
            public readonly string Scene;
            public readonly int Level;
            public readonly Vector2 Local;
            public readonly bool Brumm;
            public GrimmFlame(string scene, int level, float x, float y, bool brumm = false) { Scene = scene; Level = level; Local = new Vector2(x, y); Brumm = brumm; }
        }

        // Native FlamePin levels + the scenes' Flamebearer Spawn positions.
        internal static readonly GrimmFlame[] GrimmFlames =
        {
            new GrimmFlame("Fungus1_10", 1, 70.39f, 8.9f),
            new GrimmFlame("Mines_10", 1, 110.63f, 6.9f),
            new GrimmFlame("Ruins1_28", 1, 71.09f, 22.22f),
            new GrimmFlame("Tutorial_01", 2, 146.47f, 5.97f),
            new GrimmFlame("RestingGrounds_06", 2, 31.05f, 19.96f),
            new GrimmFlame("Deepnest_East_03", 2, 41.5108f, 72.6165f),
            new GrimmFlame("Fungus2_30", 3, 27.65f, 34.01f),
            new GrimmFlame("Abyss_02", 3, 100.37f, 17.94f),
            new GrimmFlame("Hive_03", 3, 86.0804f, 123.6802f),
            new GrimmFlame("Room_spider_small", 3, 25.48f, 14.5804f, true),
        };

        internal static bool FlameAvailable(GrimmFlame flame, PlayerData pd) => pd != null
            && pd.GetBool("equippedCharm_40") && pd.grimmChildLevel == flame.Level
            && pd.flamesCollected < pd.flamesRequired
            && (flame.Brumm ? !pd.gotBrummsFlame : !pd.scenesFlameCollected.Contains(flame.Scene));

        static Texture2D grimmFlameIcon;
        static Texture2D GrimmFlameIcon()
        {
            if (grimmFlameIcon != null) return grimmFlameIcon;
            using (var input = typeof(WorldMap).Assembly.GetManifestResourceStream("TerraKnight.GrimmFlameMap.png"))
            using (var bytes = new MemoryStream())
            {
                if (input == null) return null;
                input.CopyTo(bytes);
                grimmFlameIcon = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                grimmFlameIcon.LoadImage(bytes.ToArray());
            }
            return grimmFlameIcon;
        }

        static string DrawGrimmFlames(Func<Vector2, Vector2> toScreen, float size, Rect? clip, Vector2? mouse)
        {
            var pd = PlayerData.instance;
            string hovered = null;
            var tex = GrimmFlameIcon();
            if (tex == null) return null;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, prev.a);
            foreach (var flame in GrimmFlames)
            {
                if (!FlameAvailable(flame, pd) || !Shown(flame.Scene, flame.Local) || !ScenePoint(flame.Scene, flame.Local, out var cell)) continue;
                var at = toScreen(cell);
                if (clip.HasValue && !clip.Value.Contains(at)) continue;
                var rect = new Rect(at.x - size / 2f, at.y - size / 2f, size, size);
                GUI.DrawTexture(rect, tex);
                if (mouse.HasValue && rect.Contains(mouse.Value)) hovered = Lang.T(flame.Brumm ? "布鲁姆的火焰" : "格林火焰") + "（" + Areas.Display(Areas.AreaName(flame.Scene)) + "）";
            }
            GUI.color = prev;
            return hovered;
        }
    }
}
