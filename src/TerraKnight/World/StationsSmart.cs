using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The stations in SmartInteract (Terraria's TileSmartInteractCandidateProvider): which of them it weighs, the pick's
    /// use on a right click anywhere, and TileDrawing's outline on the ones in reach.
    /// </summary>
    internal sealed partial class Stations
    {
        /// <summary>
        /// TileSmartInteractCandidateProvider.FillPotentialTargetTiles' own list of tile types (a door, a bed, a bank, a
        /// tombstone, the clock, the buff stations, a lever...). The chairs, sofas, toilets and thrones it has too are for
        /// sitting on, which nobody does here.
        /// </summary>
        static readonly HashSet<int> interactTiles = new HashSet<int>
        {
            10, 11, 21, 29, 55, 79, 85, 88, 97, 104, 125, 132, 136, 139, 144, 207, 209, 216, 287, 335, 338, 354, 377, 386, 387,
            388, 389, 410, 411, 425, 441, 455, 463, 464, 467, 468, 470, 475, 480, 487, 491, 494, 509, 510, 511, 573, 597, 621,
            657, 658, 699, 720, 721, 725, 733,
        };

        /// <summary>TileID.Sets.Campfires.</summary>
        const int CampfireTile = 215;

        /// <summary>
        /// A station SmartInteract weighs: one of the list above, a crafting station (Recipe.TileUsedInRecipes: it gets
        /// the outline though a right click does nothing there), a campfire.
        /// </summary>
        static bool SmartTile(Station s)
        {
            if (TerraKnight.Items.Casino.IsMachine(s.Kind)) return true;
            if (s.Spec.Counts != null && s.Spec.Counts.Length > 0) return true;
            int type = TileTypeOf(s);
            return type == CampfireTile || interactTiles.Contains(type);
        }

        /// <summary>TileID.Sets.DisableSmartInteract among the stations: a candle (33), a weapon rack (334), the monoliths...</summary>
        static readonly HashSet<int> disableSmartInteract = new HashSet<int> { 4, 410, 480, 509, 657, 658, 720, 721, 725, 733, 395, 698, 334, 33, 455, 471, 520 };

        internal static bool DisablesSmartInteractAt(Vector2Int cell)
        {
            var p = CellGrid.CellCenter(cell.x, cell.y);
            foreach (var s in stations)
                if (s.Region != null && s.Box.Contains(p) && disableSmartInteract.Contains(TileTypeOf(s))) return true;
            return false;
        }

        static Station smartPick, smartBest;
        public static int DevSmartUsed;

        /// <summary>Of the stations it weighs within reach, the one whose cell is nearest the mouse (SmartInteract.NearestCell).</summary>
        public static bool SmartCandidate(HeroController hero, Vector2 mouse, out float centre, out float rect)
        {
            smartBest = null;
            centre = rect = float.MaxValue;
            if (hero == null) return false;
            var reach = Reach(hero);
            foreach (var s in stations)
            {
                if (s.Region == null || !reach.Overlaps(s.Box) || !SmartTile(s)) continue;
                SmartInteract.NearestCell(s.Box, mouse, out float c, out float r);
                if (c < centre) { centre = c; rect = r; smartBest = s; }
            }
            return smartBest != null;
        }

        public static void SmartWin(bool win) => smartPick = win ? smartBest : null;

        /// <summary>A right click anywhere with a station picked: what a right click on it does (a crafting station: nothing).</summary>
        public static bool TrySmartUse()
        {
            var hero = HeroController.SilentInstance;
            var s = smartPick;
            if (hero == null || s == null || s.Region == null || !Reach(hero).Overlaps(s.Box)) return false;
            bool used = Use(s, hero);
            if (used) DevSmartUsed++;
            return used;
        }

        /// <summary>Dev: the picked station's kind, or None.</summary>
        public static StationKind DevSmartPicked => smartPick != null ? smartPick.Kind : StationKind.None;

        // ------------------------------------------------------------------ the outline

        static readonly Dictionary<(StationKind, bool), Sprite[]> outlineSprites = new Dictionary<(StationKind, bool), Sprite[]>();

        /// <summary>
        /// TileDrawing's smart interact highlight on a station SmartInteract weighs within reach: grey, gold on the picked
        /// one, breathing with the mouse text's pulse; none when the query didn't run (the smart cursor off, a torch under
        /// the mouse).
        /// </summary>
        static void SmartOutline(Station s, HeroController hero)
        {
            bool show = hero != null && SmartInteract.Showing && s.Renderer != null && s.Renderer.enabled && SmartTile(s) && Reach(hero).Overlaps(s.Box);
            var sprite = show ? OutlineOf(s, FrameOf(s.Spec)) : null;
            if (sprite == null)
            {
                if (s.Outline != null) s.Outline.enabled = false;
                return;
            }
            if (s.Outline == null)
            {
                var go = new GameObject("Terra Station Outline");
                go.transform.SetParent(s.Renderer.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.0001f);
                s.Outline = go.AddComponent<SpriteRenderer>();
            }
            s.Outline.enabled = true;
            s.Outline.sprite = sprite;
            s.Outline.flipX = s.Renderer.flipX;
            s.Outline.sortingLayerID = s.Renderer.sortingLayerID;
            s.Outline.sortingOrder = s.Renderer.sortingOrder + 1;
            s.Outline.color = SmartInteract.OutlineColor(s == smartPick);
        }

        /// <summary>
        /// A station's outline: Terraria's own (Images/Misc/TileOutlines/Tiles_N, laid out as the tile sheet, put together
        /// as SpriteOf puts the station together, an animated one's frame by frame) where there is one; else a 2 px rim cut
        /// from the station's picture (as the placing ghost has it).
        /// </summary>
        static Sprite OutlineOf(Station s, int frame)
        {
            var spec = s.Spec;
            bool right = s.Right && spec.RightX > 0 && spec.Custom == null && spec.Native == null;
            var key = (s.Kind, right);
            if (!outlineSprites.TryGetValue(key, out var sp))
            {
                if (spec.Native == null && spec.Custom == null && spec.Texture != null && spec.Texture.StartsWith("Tiles_"))
                    sp = FromTileOutline(spec, right);
                var whole = sp == null ? SpriteOf(s.Kind, s.Right) : null;
                var rim = whole != null ? Rim(whole, spec.Texture + "#rim" + (right ? "R" : "")) : null;
                if (rim != null) sp = new[] { rim };
                outlineSprites[key] = sp;
            }
            return sp != null ? sp[frame % sp.Length] : null;
        }

        static Sprite[] FromTileOutline(Spec spec, bool right)
        {
            Color32[] src;
            int sw, sh;
            try { src = TerrariaAssets.Pixels("Misc/TileOutlines/" + spec.Texture, out sw, out sh); }
            catch (System.Exception) { return null; }
            if (src == null) return null;
            TerrariaAssets.Pixels(spec.Texture, out int tw, out int th0);
            if (tw != sw || th0 != sh) return null;
            int x0 = spec.StyleX + (right ? spec.RightX : 0);
            return FrameSprites(src, sw, sh, spec, x0, spec.Frames, spec.FrameStep, spec.Texture + "#outline" + x0);
        }

        /// <summary>A sprite's 2 px inner rim, white (its opaque pixels within 2 px of a clear one or the edge); null if it can't be read.</summary>
        static Sprite Rim(Sprite sprite, string name)
        {
            var tex = sprite.texture;
            if (tex == null || !tex.isReadable) return null;
            var r = sprite.rect;
            int w = (int)r.width, h = (int)r.height, ox = (int)r.x, oy = (int)r.y;
            var all = tex.GetPixels32();
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && all[(oy + y) * tex.width + ox + x].a > 40;
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Solid(x, y)) continue;
                    bool edge = false;
                    for (int dy = -2; dy <= 2 && !edge; dy++)
                        for (int dx = -2; dx <= 2 && !edge; dx++)
                            if (!Solid(x + dx, y + dy)) edge = true;
                    if (edge) dst[y * w + x] = new Color32(255, 255, 255, 255);
                }
            var made = TerrariaAssets.Make(name, dst, w, h);
            return Sprite.Create(made, new Rect(0, 0, w, h), new Vector2(sprite.pivot.x / w, sprite.pivot.y / h), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
