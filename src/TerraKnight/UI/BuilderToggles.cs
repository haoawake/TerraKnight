using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's builder toggles (Main.DrawBuilderAccToggles) and its Ruler (DrawInterface_4_Ruler). Every player has the
    /// Ruler (Player.ResetEffects: rulerLine is always set since the Ruler became a weapon), off at first
    /// (builderAccStatus[0] = 1); its toggle stands at the inventory's left edge (10, 77), a click switching it, kept
    /// in the save (TerraMod.GrantedAbilities). On, while the player isn't going faster than 6 px a tick: a line of cyan
    /// tiles (Extra_2) from the tile at the player's feet to the cursor's tile, across then up or down, fading from
    /// 2 px a tick, and by the cursor the measure "width x height" in tiles. The block swap toggle (DrawBlockReplacementIcon,
    /// builderAccStatus[10]: on unless switched off; Items.BlockSwap) stands above it at the inventory's top left (0, 44),
    /// UI/BlockReplace_0's frame for on or off at 0.9, its outline frame over it while hovered, the Unlock sound as it switches.
    /// </summary>
    internal static class BuilderToggles
    {
        const string RulerFlag = "builder.ruler.on";
        /// <summary>The block swap switched off (kept in the save): on otherwise, as Terraria's starts.</summary>
        const string SwapOffFlag = "builder.blockswap.off";
        /// <summary>The block swap toggle's index (Player.builderAccStatus[10]).</summary>
        public const int BlockSwapIndex = 10;
        /// <summary>Main.OurFavoriteColor: the hovered toggle's highlight.</summary>
        static readonly Color Favorite = new Color(1f, 231f / 255f, 69f / 255f);
        /// <summary>The ruler's colour (0.24, 0.8, 0.9) and its strength (0.8).</summary>
        static readonly Color RulerColor = new Color(0.24f, 0.8f, 0.9f, 1f);

        static GUIStyle style;
        static int hoverWas = -1;
        /// <summary>The ruler as last drawn: its tiles across and down (rulerLineDisplayValues), whether it showed.</summary>
        static int measureX, measureY;
        static bool showing;
        public static int DevDrawn;
        /// <summary>Dev: the cursor's world point for the ruler (the autotest has no mouse).</summary>
        public static Vector2? DevMouse;
        public static Vector2Int DevMeasure => new Vector2Int(Mathf.Abs(measureX) + 1, Mathf.Abs(measureY) + 1);

        public static bool RulerOn
        {
            get
            {
                var mod = TerraMod.Instance;
                return mod != null && mod.GrantedAbilities.Contains(RulerFlag);
            }
        }

        /// <summary>Terraria's block swap (TileReplacementEnabled): on unless switched off.</summary>
        public static bool BlockSwapOn
        {
            get
            {
                var mod = TerraMod.Instance;
                return mod == null || !mod.GrantedAbilities.Contains(SwapOffFlag);
            }
        }

        public static void Toggle(int index)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            if (index == 0 && !mod.GrantedAbilities.Remove(RulerFlag)) mod.GrantedAbilities.Add(RulerFlag);
            if (index == BlockSwapIndex && !mod.GrantedAbilities.Remove(SwapOffFlag)) mod.GrantedAbilities.Add(SwapOffFlag);
        }

        static string T(string key, string zh) => TerrariaText.Get(Lang.Culture, "GameUI." + key) ?? Lang.T(zh);

        /// <summary>
        /// The column of toggles (inventory open): 14×14 icons from UI/BuilderIcons (row 16, the Ruler's at 0), 24 px
        /// apart down from (10, 77), white when on and grey (127) when off, the highlight (InfoIcon_13) over a hovered one
        /// with its state's name; a tick each time the pointer comes onto another. Returns the icons' places with their
        /// indices, and the hovered one's text.
        /// </summary>
        public static List<(int index, Rect rect)> Draw(Rect view, float ui, Vector2 mouse, out string hover)
        {
            hover = null;
            var rects = new List<(int, Rect)>();
            var sheet = TerrariaAssets.Texture("UI/BuilderIcons");
            if (sheet == null) return rects;
            int over = -1;
            var centre = new Vector2(view.x + 10f * ui, view.y + 77f * ui);
            var r = new Rect(centre.x - 7f * ui, centre.y - 7f * ui, 14f * ui, 14f * ui);
            var prev = GUI.color;
            GUI.color = RulerOn ? Color.white : new Color(127f / 255f, 127f / 255f, 127f / 255f);
            GUI.DrawTextureWithTexCoords(r, sheet, new Rect(0f, 1f - (16f + 14f) / sheet.height, 14f / sheet.width, 14f / sheet.height));
            GUI.color = prev;
            if (r.Contains(mouse))
            {
                over = 0;
                hover = RulerOn ? T("RulerOn", "标尺：开") : T("RulerOff", "标尺：关");
                var hl = TerrariaAssets.Texture("UI/InfoIcon_13");
                if (hl != null)
                {
                    GUI.color = Favorite;
                    GUI.DrawTexture(new Rect(centre.x - hl.width * 0.5f * ui, centre.y - hl.height * 0.5f * ui, hl.width * ui, hl.height * ui), hl);
                    GUI.color = prev;
                }
            }
            rects.Add((0, r));
            // The block swap's: at the top left (0, 44), 0.9 of UI/BlockReplace_0's frame (on, off; the third its outline).
            var swap = TerrariaAssets.Texture("UI/BlockReplace_0");
            if (swap != null)
            {
                float fw = swap.width / 3f, s = 0.9f * ui;
                var sr = new Rect(view.x, view.y + 44f * ui, fw * s, swap.height * s);
                bool on = BlockSwapOn;
                GUI.DrawTextureWithTexCoords(sr, swap, new Rect(on ? 0f : 1f / 3f, 0f, 1f / 3f, 1f));
                if (sr.Contains(mouse))
                {
                    over = BlockSwapIndex;
                    hover = on ? T("BlockReplacerOn", "方块替换：开") : T("BlockReplacerOff", "方块替换：关");
                    GUI.color = Favorite;
                    GUI.DrawTextureWithTexCoords(sr, swap, new Rect(2f / 3f, 0f, 1f / 3f, 1f));
                    GUI.color = prev;
                }
                rects.Add((BlockSwapIndex, sr));
            }
            // DoStatefulTickSound.
            if (over >= 0 && over != hoverWas) TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            hoverWas = over;
            return rects;
        }

        /// <summary>Each frame in play: the ruler, when it is on, drawn in the world (FxBatch, over the tiles).</summary>
        public static void Tick(HeroController hero)
        {
            showing = false;
            var fx = FxBatch.Instance;
            if (!RulerOn || hero == null || fx == null || hero.cState.dead || UI.WorldMap.Open || Time.timeScale == 0f) return;
            var rb = hero.GetComponent<Rigidbody2D>();
            float speed = rb != null ? rb.linearVelocity.magnitude / TerraMovement.PxT : 0f;
            Vector2 mouse = default;
            if (speed > 6f || DevMouse == null && !TerraMod.MouseWorld(out mouse)) return;
            if (DevMouse != null) mouse = DevMouse.Value;
            float fade = speed >= 2f ? 1f - (speed - 2f) / 6f : 1f;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            // The player's tile: under its middle, 2 px above its feet; one tile on towards the cursor when the cursor isn't in its column.
            var feet = CellGrid.WorldToCell(new Vector2(box.center.x, box.min.y + 2f * TerraParticles.Px));
            var at = CellGrid.WorldToCell(mouse);
            int x = feet.x, y = -feet.y - 1;
            if (Mathf.Abs(x - at.x) > 0) x += mouse.x < box.center.x ? -1 : 1;
            int across = at.x - x, down = (-at.y - 1) - y;
            measureX = across;
            measureY = down;
            if (across == 0 && down == 0) return;
            showing = true;
            DevDrawn++;
            var colour = RulerColor * 0.8f * fade;
            fx.OnTop = true;
            try
            {
                Tile(fx, x, y, 0, colour);
                if (across != 0)
                {
                    int s = across > 0 ? 1 : -1;
                    while (across != 0)
                    {
                        across -= s;
                        x += s;
                        Tile(fx, x, y, across == 0 ? 0 : s > 0 ? 16 : 32, colour);
                    }
                }
                if (down != 0)
                {
                    int s = down > 0 ? 1 : -1;
                    while (down != 0)
                    {
                        down -= s;
                        y += s;
                        Tile(fx, x, y, down == 0 ? 0 : s > 0 ? 48 : 64, colour);
                    }
                }
            }
            finally { fx.OnTop = false; }
        }

        /// <summary>One of the ruler's tiles: Extra_2's 16×16 frame at a row, on the tile at (x, y) in Terraria's tile numbering (y down).</summary>
        static void Tile(FxBatch fx, int x, int y, int frameY, Color colour)
        {
            var frame = FxBatch.Frame("Extra_2", 0, frameY, 16, 16, 0f, 0f);
            fx.Draw(frame, new Vector2(x * 16f, y * 16f), colour, 0f, 1f);
        }

        /// <summary>The measure by the cursor (Utils.DrawBorderString at the mouse + 16 px, scale 0.8): the tiles across and down, each counted from one.</summary>
        public static void DrawMeasure()
        {
            if (!showing || Event.current.type != EventType.Repaint) return;
            int w = Mathf.Abs(measureX) + 1, h = Mathf.Abs(measureY) + 1;
            if (w <= 1 && h <= 1) return;
            float ui = GameView.Scale;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false };
            style.fontSize = Mathf.RoundToInt(18f * 0.8f * ui);
            var mouse = TerraCursor.GuiMouse();
            var r = new Rect(mouse.x + 16f * ui, mouse.y, 120f * ui, 24f * ui);
            string text = w + "x" + h;
            style.normal.textColor = Color.black;
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), text, style);
            style.normal.textColor = RulerColor;
            GUI.Label(r, text, style);
        }
    }
}
