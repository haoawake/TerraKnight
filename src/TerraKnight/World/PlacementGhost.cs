using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's placement preview (TileObject.DrawPreview): with a piece of furniture in hand (a crafting station, a
    /// relic, a chest) the thing is drawn where it would go, at half its alpha: white where it fits, red (×0.7) where it
    /// doesn't. Shown while the cursor is within the player's reach (TileReachCheckSettings.Simple) and off the
    /// interface; snapped as the placing itself snaps (Stations.FindSpot, Chests.FindSpot).
    /// </summary>
    internal static class PlacementGhost
    {
        static SpriteRenderer ghost;
        static readonly System.Collections.Generic.Dictionary<int, Sprite> wallSprites = new System.Collections.Generic.Dictionary<int, Sprite>();
        public static bool DevShown { get; private set; }
        public static bool DevValid { get; private set; }
        /// <summary>Dev: the furniture shown faces right.</summary>
        public static bool DevRight { get; private set; }
        /// <summary>Dev: where the ghost stands (its bottom middle; a wall's cell middle).</summary>
        public static Vector2 DevAt { get; private set; }

        /// <summary>Call every frame in gameplay with the item in hand and the cursor in the world.</summary>
        public static void Tick(HeroController hero, Items.ItemDef held, Vector2 mouse, bool overUi)
        {
            Sprite sprite = null;
            Vector2 bottom = default;
            float sink = 0f;
            bool ok = false;
            if (hero != null && held != null && !overUi)
            {
                if (held.PlacesStation != StationKind.None)
                {
                    ok = Stations.FindSpot(held.PlacesStation, hero, mouse, out bottom);
                    if (!ok && !Stations.GuessSpot(held.PlacesStation, hero, mouse, out bottom)) bottom = default;
                    // Facing the way it would go down (TileObject.DrawPreview draws the alternate the player's direction picks).
                    bool right = Stations.FacesRight(held.PlacesStation, hero);
                    if (bottom != default) { sprite = Stations.GhostSprite(held.PlacesStation, right); sink = Stations.GhostSink(held.PlacesStation); }
                    DevRight = right;
                }
                else if (held.PlacesChest)
                {
                    ok = Chests.FindSpot(hero, mouse, out bottom, held.ChestStyle);
                    if (!ok && !Chests.GuessSpot(hero, mouse, out bottom, held.ChestStyle)) bottom = default;
                    if (bottom != default) { sprite = Chests.GhostSprite(held.ChestStyle); sink = RegionalFurniture.IsDresser(held.ChestStyle) ? 0 : 2f * Effects.TerraParticles.Px; }
                }
                else if (held.PlacesWall > 0)
                {
                    var cell = CellGrid.WorldToCell(mouse);
                    // With the smart cursor on, where the smart cursor puts the wall (SmartCursorHelper.Step_Walls).
                    var mod = TerraMod.Instance;
                    if (mod != null && mod.SmartCursor && mod.TargetFor(held, mouse, out var smartCell, out bool smart) && smart) cell = smartCell;
                    if (TerraMod.InReach(hero.GetComponent<BoxCollider2D>().bounds, held, cell))
                    {
                        bottom = CellGrid.CellCenter(cell.x, cell.y);
                        ok = BuildingTools.CanPlaceWall(cell);
                        if (!wallSprites.TryGetValue(held.PlacesWall, out sprite))
                        {
                            sprite = Terraria.TerrariaAssets.Sprite("Wall_" + held.PlacesWall, 324, 108, 32, 32,
                                CellVisuals.PixelsPerUnit, new Vector2(0.5f, 0.5f));
                            wallSprites[held.PlacesWall] = sprite;
                        }
                    }
                }
            }
            DevShown = sprite != null;
            DevValid = ok && sprite != null;
            DevAt = bottom;
            if (sprite == null)
            {
                if (ghost != null && ghost.enabled) ghost.enabled = false;
                return;
            }
            if (ghost == null)
            {
                var go = new GameObject("Terra Placement Ghost");
                Object.DontDestroyOnLoad(go);
                ghost = go.AddComponent<SpriteRenderer>();
                ghost.sortingOrder = 5;
            }
            ghost.enabled = true;
            ghost.sprite = sprite;
            ghost.flipX = held != null && RegionalFurniture.IsRegional(held.PlacesStation) && DevRight;
            ghost.transform.position = new Vector3(bottom.x, bottom.y - sink, 0.003f);
            ghost.color = ok ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0f, 0f, 0.35f);
        }

        public static void Hide()
        {
            if (ghost != null) ghost.enabled = false;
            DevShown = false;
        }
    }

    internal sealed partial class Stations
    {
        public static Sprite GhostSprite(StationKind kind, bool right = false) => SpriteOf(kind, right);

        /// <summary>The station standing under a point (none: StationKind.None).</summary>
        public static StationKind KindAt(Vector2 p)
        {
            foreach (var s in stations) if (s.Region != null && s.Box.Contains(p)) return s.Kind;
            return StationKind.None;
        }

        /// <summary>How far the station's picture sinks below its bottom (a bottom row 18 px tall, DrawYOffset).</summary>
        public static float GhostSink(StationKind kind)
        {
            var spec = specs[kind];
            return ((spec.Heights[spec.Heights.Length - 1] - 16) + spec.DrawY) * Px;
        }

        /// <summary>Where a station would stand at the cursor if it fitted (snapped as FindSpot snaps, on the ground under it), within reach.</summary>
        public static bool GuessSpot(StationKind kind, HeroController hero, Vector2 aim, out Vector2 bottom)
        {
            var spec = specs[kind];
            const float cell = CellGrid.CellSize;
            float x = spec.W % 2 == 1 ? (Mathf.Floor(aim.x / cell) + 0.5f) * cell : Mathf.Round(aim.x / cell) * cell;
            var down = Physics2D.Raycast(new Vector2(x, aim.y + 0.25f), Vector2.down, 1.5f, 1 << ChunkColliders.TerrainLayer);
            float y = down.collider != null ? down.point.y : Mathf.Floor(aim.y / cell) * cell;
            bottom = new Vector2(x, y);
            var box = new Rect(x - spec.W * cell / 2f, y, spec.W * cell, spec.H * cell);
            return Reach(hero).Overlaps(box);
        }
    }

    internal sealed partial class Chests
    {
        public static Sprite GhostSprite(int style = 0) => RegionalChests.FrameSprite(style,0) ?? FrameSprite(0);

        /// <summary>A chest stands under a point.</summary>
        public static bool At(Vector2 p)
        {
            foreach (var c in chests) if (c.Region != null && c.Box.Contains(p)) return true;
            return false;
        }

        /// <summary>Where a chest would stand at the cursor if it fitted, within reach.</summary>
        public static bool GuessSpot(HeroController hero, Vector2 aim, out Vector2 bottom, int style = 0)
        {
            const float cell = CellGrid.CellSize;
            float w=WidthOf(style);
            float x = RegionalFurniture.IsDresser(style) ? (Mathf.Floor(aim.x/cell)+.5f)*cell : Mathf.Round(aim.x / cell) * cell;
            var down = Physics2D.Raycast(new Vector2(x, aim.y + 0.25f), Vector2.down, 1.5f, 1 << ChunkColliders.TerrainLayer);
            float y = down.collider != null ? down.point.y : Mathf.Floor(aim.y / cell) * cell;
            bottom = new Vector2(x, y);
            return InReach(hero, new Rect(x - w / 2f, y, w, Height));
        }
    }
}
