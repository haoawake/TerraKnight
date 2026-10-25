using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// An item's glowmask (ItemDef.Glow) and its colour, as Terraria draws it in the inventory (ItemSlot.Draw), on the
    /// ground (Main.DrawItem) and in hand (PlayerDrawLayers): white, the rainbow phaseblade and phasesaber their running
    /// rainbow (Item.GetPhaseColor), drawn twice, once at half alpha. Their blades are the glowmask: without it only the
    /// hilt shows.
    /// </summary>
    internal static class ItemGlows
    {
        static readonly Color Plain = new Color(250f / 255f, 250f / 255f, 250f / 255f, 1f);

        public static bool Rainbow(ItemDef def) => def != null && (def.Type == ItemType.RainbowPhaseblade || def.Type == ItemType.RainbowPhasesaber);

        /// <summary>The glowmask's colour now.</summary>
        public static Color Tint(ItemDef def)
        {
            if (!Rainbow(def)) return Plain;
            var c = TerraProjectiles.PhaseColor(def.Type == ItemType.RainbowPhaseblade ? ProjKind.PhaseRainbow : ProjKind.SaberRainbow);
            c.a = 1f;
            return c;
        }

        /// <summary>The inventory's icon: the glowmask over it in its colour (the rainbow ones twice, the second at half alpha).</summary>
        public static void DrawIcon(Rect at, ItemDef def, Rect coords)
        {
            if (def?.Glow == null) return;
            Texture2D glow;
            try { glow = Terraria.TerrariaAssets.Texture(def.Glow); }
            catch (System.Exception) { return; }
            if (glow == null) return;
            var prev = GUI.color;
            var c = Tint(def);
            GUI.color = c;
            GUI.DrawTextureWithTexCoords(at, glow, coords);
            if (Rainbow(def))
            {
                GUI.color = new Color(c.r, c.g, c.b, 0.5f);
                GUI.DrawTextureWithTexCoords(at, glow, coords);
            }
            GUI.color = prev;
        }
    }
}
