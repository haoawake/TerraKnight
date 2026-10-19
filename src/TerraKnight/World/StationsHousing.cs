using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The furniture a house is built with besides the chair (Housing): Terraria's Wooden Door and Wooden Table, and
    /// the furniture as tiles for the housing check. The door stands between a floor and a ceiling (TileObjectData for
    /// tile 10: 1x3, AnchorTop and AnchorBottom SolidTile), closed it is a solid tile the Knight and everything else
    /// walk into (a box on the terrain layer, as the grid's blocks are), and a right click opens it (WorldGen.OpenDoor:
    /// tile 11, 2x3, swung the way the player faces, else the other way, if that column is clear) or shuts it
    /// (WorldGen.CloseDoor: not on whoever stands in its doorway). Walking into a shut door pushes it open, and it
    /// shuts again behind the player (DoorOpeningHelper, PushDoors); a townsperson walking into one opens it and shuts
    /// it once a few tiles past (NPC.AI_007's doorX / closeDoor). The table (tile 14, Style3x2) is solid on top
    /// (Main.tileSolidTop): stood on, and other furniture goes on it.
    /// </summary>
    internal sealed partial class Stations
    {
        /// <summary>Tiles_10 (closed, CoordinateHeights {16, 16, 16}), Tiles_11 (open: frameX 0/18 swung right, 36/54 swung left), Tiles_14 ({16, 18}).</summary>
        static void AddHousingSpecs()
        {
            specs[StationKind.Door] = new Spec { Texture = "Tiles_10", W = 1, H = 3, Heights = new[] { 16, 16, 16 }, Item = ItemType.WoodenDoor, Counts = new string[0] };
            specs[StationKind.DoorOpenRight] = new Spec { Texture = "Tiles_11", W = 2, H = 3, Heights = new[] { 16, 16, 16 }, Item = ItemType.WoodenDoor, Counts = new string[0] };
            specs[StationKind.DoorOpenLeft] = new Spec { Texture = "Tiles_11", W = 2, H = 3, StyleX = 36, Heights = new[] { 16, 16, 16 }, Item = ItemType.WoodenDoor, Counts = new string[0] };
            specs[StationKind.Table] = new Spec { Texture = "Tiles_14", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.WoodenTable, Counts = new string[0], SolidTop = true };
        }

        /// <summary>A Wooden Door, shut or open.</summary>
        public static bool IsDoor(StationKind k) => RegionalFurniture.BaseKind(k) == StationKind.Door || RegionalFurniture.BaseKind(k) == StationKind.DoorOpenRight || RegionalFurniture.BaseKind(k) == StationKind.DoorOpenLeft;

        /// <summary>The door's hinge column (the one it stands in shut): the left one swung right, the right one swung left.</summary>
        static int HingeColumn(StationKind k) => RegionalFurniture.BaseKind(k) == StationKind.DoorOpenLeft ? 1 : 0;

        /// <summary>Terraria's tile a station is: its sheet's number (Tiles_N), the relics' Master Mode trophy base 617, the door 10 shut and 11 open.</summary>
        static int TileTypeOf(Station s)
        {
            if (RegionalFurniture.BaseKind(s.Kind) == StationKind.Door) return 10;
            if (RegionalFurniture.BaseKind(s.Kind) == StationKind.DoorOpenRight || RegionalFurniture.BaseKind(s.Kind) == StationKind.DoorOpenLeft) return 11;
            if (RegionalFurniture.IsRegional(s.Kind)) return RegionalFurniture.TileOf(s.Kind);
            var tex = s.Spec.Texture;
            if (tex != null && tex.StartsWith("Tiles_") && int.TryParse(tex.Substring(6), out int n)) return n;
            return 617;
        }

        /// <summary>
        /// Every loaded station cell by cell, as Terraria's tiles (type, frameX: a column's place on its sheet, the
        /// right-facing alternate's for one placed facing right), for the housing check: a cell's middle decides which
        /// cell each of the station's tiles is in.
        /// </summary>
        internal static void HousingTiles(System.Action<int, int, int, int> add)
        {
            const float c = CellGrid.CellSize;
            foreach (var s in stations)
            {
                if (s.Region == null) continue;
                int type = TileTypeOf(s);
                var b = s.Box;
                int x0 = s.Spec.StyleX + (s.Right ? s.Spec.RightX : 0);
                for (int col = 0; col < s.Spec.W; col++)
                    for (int row = 0; row < s.Spec.H; row++)
                    {
                        var cell = CellGrid.WorldToCell(new Vector2(b.xMin + (col + 0.5f) * c, b.yMin + (row + 0.5f) * c));
                        add(cell.x, cell.y, type, x0 + col * 18);
                    }
            }
        }

        /// <summary>
        /// A solid block against a surface (above it, up; else below): the grid's (not a platform: AnchorType.SolidTile),
        /// or the scene's own ground the grid doesn't hold, read with a short ray from just inside.
        /// </summary>
        static bool SolidTileAt(float x, float surface, bool up)
        {
            const float c = CellGrid.CellSize;
            var cell = CellGrid.WorldToCell(new Vector2(x, surface + (up ? c * 0.5f : -c * 0.5f)));
            if (TerraWorld.RegionAt(cell.x, cell.y) != null && TerraWorld.IsSolid(cell.x, cell.y)) return true;
            var from = new Vector2(x, surface + (up ? -0.05f : 0.05f));
            int n = Physics2D.RaycastNonAlloc(from, up ? Vector2.up : Vector2.down, hits, 0.15f, 1 << ChunkColliders.TerrainLayer);
            for (int i = 0; i < n; i++)
                if (hits[i].collider != null && !hits[i].collider.usedByEffector && hits[i].collider.GetComponentInParent<TerraOwned>() == null) return true;
            return false;
        }

        /// <summary>TileObjectData for doors: a solid tile over the hinge column's top and one under its foot.</summary>
        static bool DoorAnchored(Rect box, int hingeColumn)
        {
            const float c = CellGrid.CellSize;
            float x = box.xMin + (hingeColumn + 0.5f) * c;
            return SolidTileAt(x, box.yMin, false) && SolidTileAt(x, box.yMax, true);
        }

        /// <summary>Whether a door set at this box would hold (FindSpot): floor and ceiling.</summary>
        static bool DoorFits(StationKind kind, Rect box) => !IsDoor(kind) || DoorAnchored(box, HingeColumn(kind));

        /// <summary>A door whose ceiling or floor is gone falls into its item as other furniture does (WorldGen.CheckDoorClosed / CheckDoorOpen).</summary>
        static bool DoorHeld(Station s) => DoorAnchored(s.Box, HingeColumn(s.Kind));

        /// <summary>Anything in the cells of a column of a door's height (an active tile: WorldGen.OpenDoor lets only cut plants through).</summary>
        static bool ColumnTaken(float x, float bottom, Station except)
        {
            const float c = CellGrid.CellSize;
            for (int row = 0; row < 3; row++)
            {
                var p = new Vector2(x, bottom + (row + 0.5f) * c);
                var cell = CellGrid.WorldToCell(p);
                if (TerraWorld.RegionAt(cell.x, cell.y) == null || TerraWorld.IsOccupied(cell.x, cell.y)) return true;
                foreach (var o in stations)
                    if (o != except && o.Region != null && o.Box.Contains(p)) return true;
                foreach (var b in Chests.Boxes())
                    if (b.Contains(p)) return true;
            }
            return false;
        }

        /// <summary>A door's doorway: its hinge column (where it stands shut), three cells high.</summary>
        static Rect Doorway(Station s)
        {
            const float c = CellGrid.CellSize;
            var b = s.Box;
            return new Rect(b.xMin + (RegionalFurniture.BaseKind(s.Kind) == StationKind.Door ? 0 : HingeColumn(s.Kind)) * c, b.yMin, c, 3 * c);
        }

        /// <summary>WorldGen.OpenDoor: swung to that side (1 right, -1 left) when the column there is clear.</summary>
        static bool OpenDoor(Station s, int direction)
        {
            if (RegionalFurniture.BaseKind(s.Kind) != StationKind.Door) return false;
            const float c = CellGrid.CellSize;
            var b = s.Box;
            float swing = direction == -1 ? b.xMin - c * 0.5f : b.xMax + c * 0.5f;
            if (ColumnTaken(swing, b.yMin, s)) return false;
            s.Kind = RegionalFurniture.WithPose(s.Kind, direction == -1 ? StationKind.DoorOpenLeft : StationKind.DoorOpenRight);
            s.Local.x += direction * c * 0.5f;
            TerraMod.Instance?.PlaySound("Door_Opened");
            // Its body goes at once, not with the next look over the stations: the one walking in is already moving on.
            DoorBody(s);
            return true;
        }

        /// <summary>
        /// WorldGen.CloseDoor: back into its hinge column, unless somebody stands in it (Collision.EmptyTile: the player
        /// or a townsperson).
        /// </summary>
        static bool CloseDoor(Station s)
        {
            if (RegionalFurniture.BaseKind(s.Kind) != StationKind.DoorOpenRight && RegionalFurniture.BaseKind(s.Kind) != StationKind.DoorOpenLeft) return false;
            const float c = CellGrid.CellSize;
            int hinge = HingeColumn(s.Kind);
            var doorway = Doorway(s);
            var hero = HeroController.SilentInstance;
            if (hero != null)
            {
                var h = hero.GetComponent<BoxCollider2D>().bounds;
                if (doorway.Overlaps(new Rect(h.min.x, h.min.y, h.size.x, h.size.y))) return false;
            }
            if (Town.TownNpcs.AnyIn(doorway)) return false;
            s.Local.x += (hinge == 0 ? -1f : 1f) * c * 0.5f;
            s.Kind = RegionalFurniture.WithPose(s.Kind, StationKind.Door);
            TerraMod.Instance?.PlaySound("Door_Closed");
            return true;
        }

        /// <summary>Player.TileInteractionsUse on a door: open it the way the player faces, else the other way; shut an open one.</summary>
        static bool ToggleDoor(Station s, HeroController hero)
        {
            int dir = hero != null && !hero.cState.facingRight ? -1 : 1;
            // In a LAN game the host's world decides (NetToggleDoor), as Terraria's NetMessage 19 has the server do it.
            if (Net.WorldSync.Guest)
            {
                Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "door", Key = Net.WorldSync.Key("s", s.Scene, s.Local), Kind = dir });
                return true;
            }
            return Toggle(s, dir);
        }

        static bool Toggle(Station s, int dir)
        {
            if (RegionalFurniture.BaseKind(s.Kind) == StationKind.Door)
            {
                bool opened = OpenDoor(s, dir) || OpenDoor(s, -dir);
                if (opened) DevDoorToggles++;
                return opened;
            }
            bool shut = CloseDoor(s);
            if (shut) DevDoorToggles++;
            return shut;
        }

        /// <summary>The host's side of a guest's right click on a door (the way that guest faces, Kind).</summary>
        internal static bool NetToggleDoor(Net.WorldSync.Action a)
        {
            foreach (var s in stations)
                if (IsDoor(s.Kind) && !s.Fixed && Net.WorldSync.Key("s", s.Scene, s.Local) == a.Key) return Toggle(s, a.Kind < 0 ? -1 : 1);
            return false;
        }

        /// <summary>A shut door's body: a solid box over it on the terrain layer, gone while it is open.</summary>
        static void DoorBody(Station s)
        {
            if (RegionalFurniture.BaseKind(s.Kind) != StationKind.Door)
            {
                if (s.Top != null)
                {
                    // Out of the physics world now (a ray cast this frame mustn't still meet it), destroyed at the frame's end.
                    s.Top.SetActive(false);
                    Object.Destroy(s.Top);
                    s.Top = null;
                }
                return;
            }
            if (s.Renderer == null) return;
            if (s.Top == null)
            {
                var go = new GameObject("Terra Door") { layer = ChunkColliders.TerrainLayer };
                go.transform.SetParent(s.Renderer.transform, false);
                go.AddComponent<TerraOwned>();
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;
                var box = go.AddComponent<BoxCollider2D>();
                box.sharedMaterial = s.Region != null ? s.Region.Material : null;
                s.Top = go;
            }
            var bb = s.Box;
            s.Top.transform.position = new Vector3(bb.center.x, bb.center.y, s.Renderer.transform.position.z);
            var col = s.Top.GetComponent<BoxCollider2D>();
            col.size = bb.size;
            col.offset = Vector2.zero;
            if (!s.Top.activeSelf) s.Top.SetActive(true);
        }

        // ------------------------------------------------------------------ the doors and the smart cursor

        /// <summary>
        /// The cells the loaded doors stand in, for the smart cursor's sight (BuildingTools.SmartWall): a shut door's three
        /// (tile 10 is a solid tile) and an open one's hinge column (DelegateMethods.NotDoorStand: the open door's frames
        /// 0 and 54, where it is hung, stop the line; its swung leaf doesn't).
        /// </summary>
        internal static void DoorCells(HashSet<Vector2Int> shut, HashSet<Vector2Int> hinges)
        {
            shut.Clear();
            hinges.Clear();
            const float c = CellGrid.CellSize;
            foreach (var s in stations)
            {
                if (s.Region == null || !IsDoor(s.Kind)) continue;
                var d = Doorway(s);
                for (int row = 0; row < 3; row++)
                {
                    var cell = CellGrid.WorldToCell(new Vector2(d.xMin + 0.5f * c, d.yMin + (row + 0.5f) * c));
                    if (RegionalFurniture.BaseKind(s.Kind) == StationKind.Door) shut.Add(cell);
                    else hinges.Add(cell);
                }
            }
        }

        /// <summary>TileID.Sets.DisableSmartCursor among the stations: what a right click works on (doors, beds, the banks, the buff stations, a placed bottle, tombstones).</summary>
        static readonly HashSet<int> disableSmartCursor = new HashSet<int> { 10, 11, 13, 29, 79, 85, 88, 97, 125, 287, 354, 463, 464, 491, 621 };

        /// <summary>A station whose tile turns the smart cursor off under the mouse (TileID.Sets.DisableSmartCursor) covers this cell.</summary>
        internal static bool DisablesSmartCursorAt(Vector2Int cell)
        {
            var p = CellGrid.CellCenter(cell.x, cell.y);
            foreach (var s in stations)
                if (s.Region != null && s.Box.Contains(p) && disableSmartCursor.Contains(TileTypeOf(s))) return true;
            return false;
        }

        // ------------------------------------------------------------------ pushed open

        /// <summary>The doors the player pushed open (DoorOpeningHelper._ongoingOpenDoors), each with when a guest's request for it was sent.</summary>
        static readonly List<Station> pushedOpen = new List<Station>();
        static readonly Dictionary<Station, float> askedAt = new Dictionary<Station, float>();

        /// <summary>Dev: doors are only opened by a right click (Terraria's "auto open doors" off).</summary>
        public static bool DevNoAutoDoors;
        public static int DevPushedOpen, DevPushedShut, DevNpcDoorsOpened, DevNpcDoorsShut;

        /// <summary>
        /// DoorOpeningHelper.Update (PreferenceSettings EnabledForEverything), each frame. Where the player heads: the keys,
        /// plus his speed while they're held or while he dashes (GetAutoDoorVelocityContribution), ceil'd to whole pixels;
        /// his body, a pixel in at the top and the bottom, moved on by that is the push. First the doors he pushed open
        /// shut once neither his body nor the push is in their doorway any more (the hinge column, a pixel wider either
        /// side; CloseDoor still waits for anyone standing in it). Then a shut door the push meets, with the tile under
        /// it, opens when his body lies between the door's top and that tile's bottom: the way he heads, else the other
        /// way (WorldGen.OpenDoor). In a LAN game a guest's push and shut are asked of the host, a request at a time for
        /// each door.
        /// </summary>
        static void PushDoors()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || DevNoAutoDoors || !Net.WorldSync.CanInteract)
            {
                pushedOpen.Clear();
                return;
            }
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            if (col == null || rb == null) return;
            const float c = CellGrid.CellSize;
            float slack = Px + Physics2D.defaultContactOffset;
            var h = col.bounds;
            var body = new Rect(h.min.x, h.min.y, h.size.x, h.size.y);
            bool guest = Net.WorldSync.Guest;
            if (askedAt.Count > 64) askedAt.Clear();
            // Where he heads: the keys (the autotest's held direction too), not while the game holds him.
            int keys = 0;
            if (hero.acceptingInput)
            {
                if (Player.TerraMovement.DevMove != 0) keys = Player.TerraMovement.DevMove;
                else if (GameHooks.Input != null)
                    keys = (GameHooks.Input.inputActions.right.IsPressed ? 1 : 0) - (GameHooks.Input.inputActions.left.IsPressed ? 1 : 0);
            }
            bool dashing = hero.cState.dashing || hero.cState.shadowDashing || Player.TerraMovement.EocDash > 0f;
            float speed = keys != 0 || dashing ? rb.linearVelocity.x / Player.TerraMovement.PxT : 0f;
            float value = keys + speed;
            int shift = Mathf.Abs(value) < 1e-4f ? 0 : (int)Mathf.Sign(value) * Mathf.CeilToInt(Mathf.Abs(value));
            int dir = keys != 0 ? keys : shift > 0 ? 1 : shift < 0 ? -1 : 0;
            var push = new Rect(body.xMin + shift * Px + dir * Physics2D.defaultContactOffset, body.yMin + Px, body.width, body.height - 2f * Px);
            for (int i = pushedOpen.Count - 1; i >= 0; i--)
            {
                var s = pushedOpen[i];
                bool waiting = guest && askedAt.TryGetValue(s, out float at) && Time.time - at < 1f;
                bool open = s.Region != null && stations.Contains(s) && (RegionalFurniture.BaseKind(s.Kind) == StationKind.DoorOpenLeft || RegionalFurniture.BaseKind(s.Kind) == StationKind.DoorOpenRight);
                if (!open)
                {
                    if (!waiting) pushedOpen.RemoveAt(i);
                    continue;
                }
                var doorway = Doorway(s);
                doorway = new Rect(doorway.xMin - slack, doorway.yMin, doorway.width + 2f * slack, doorway.height);
                // Still in the door's way: his body, or still pressing into it (the step that takes him in is yet to come).
                if (doorway.Overlaps(body) || (dir != 0 && doorway.Overlaps(push)) || waiting) continue;
                pushedOpen.RemoveAt(i);
                if (guest)
                {
                    Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "door", Key = Net.WorldSync.Key("s", s.Scene, s.Local), Kind = 1 });
                    askedAt[s] = Time.time;
                }
                else if (CloseDoor(s))
                {
                    DevDoorToggles++;
                    DevPushedShut++;
                }
            }
            if (dir == 0) return;
            bool flipped = Player.GravityGlobe.Flipped;
            foreach (var s in stations)
            {
                if (RegionalFurniture.BaseKind(s.Kind) != StationKind.Door || s.Region == null || s.Fixed) continue;
                var d = s.Box;
                // The door and the tile it stands on (the one it hangs from, turned over by the Gravity Globe).
                var area = flipped ? new Rect(d.xMin, d.yMin, d.width, d.height + c) : new Rect(d.xMin, d.yMin - c, d.width, d.height + c);
                if (!area.Overlaps(push) || push.yMax > area.yMax || push.yMin < area.yMin) continue;
                if (guest)
                {
                    if (askedAt.TryGetValue(s, out float at) && Time.time - at < 1f) continue;
                    Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "door", Key = Net.WorldSync.Key("s", s.Scene, s.Local), Kind = dir });
                    askedAt[s] = Time.time;
                    if (!pushedOpen.Contains(s)) pushedOpen.Add(s);
                    continue;
                }
                if (OpenDoor(s, dir) || OpenDoor(s, -dir))
                {
                    DevDoorToggles++;
                    DevPushedOpen++;
                    if (!pushedOpen.Contains(s)) pushedOpen.Add(s);
                }
            }
        }

        // ------------------------------------------------------------------ townsfolk at a door

        /// <summary>
        /// NPC.AI_007 at a shut door (tile 10 at head height ahead): a townsperson walking into one opens it the way it
        /// walks, else the other way. True when the collider was a shut door's body and it opened; hinge: where its
        /// doorway is, for shutting it again (NpcShutDoor). A LAN guest's townsfolk leave doors to the host.
        /// </summary>
        public static bool NpcOpenDoor(Collider2D body, int dir, out Vector2 hinge)
        {
            hinge = default;
            if (body == null || Net.WorldSync.Guest) return false;
            foreach (var s in stations)
            {
                if (RegionalFurniture.BaseKind(s.Kind) != StationKind.Door || s.Top == null || s.Top != body.gameObject) continue;
                if (!OpenDoor(s, dir) && !OpenDoor(s, -dir)) return false;
                hinge = Doorway(s).center;
                DevDoorToggles++;
                DevNpcDoorsOpened++;
                return true;
            }
            return false;
        }

        /// <summary>The townsperson that opened the door at hinge is two tiles past it: it shuts (WorldGen.CloseDoor). True when it did, or when that door isn't open any more.</summary>
        public static bool NpcShutDoor(Vector2 hinge)
        {
            foreach (var s in stations)
            {
                if ((RegionalFurniture.BaseKind(s.Kind) != StationKind.DoorOpenLeft && RegionalFurniture.BaseKind(s.Kind) != StationKind.DoorOpenRight) || s.Region == null) continue;
                if ((Doorway(s).center - hinge).sqrMagnitude > 0.01f) continue;
                if (Net.WorldSync.Guest || !CloseDoor(s)) return false;
                DevDoorToggles++;
                DevNpcDoorsShut++;
                return true;
            }
            return true;
        }

        public static int DevDoorToggles;

        /// <summary>Dev: the door nearest a point in a loaded scene: its kind and box.</summary>
        public static StationKind DevDoorNear(Vector2 p, out Rect box)
        {
            Station best = null;
            foreach (var s in stations)
                if (IsDoor(s.Kind) && s.Region != null && (best == null || Vector2.Distance(s.Box.center, p) < Vector2.Distance(best.Box.center, p))) best = s;
            box = best != null ? best.Box : default;
            return best != null ? best.Kind : StationKind.None;
        }

        /// <summary>Dev: the right click on the door nearest a point (in the hero's reach or not).</summary>
        public static bool DevToggleDoor(Vector2 p)
        {
            Station best = null;
            foreach (var s in stations)
                if (IsDoor(s.Kind) && s.Region != null && (best == null || Vector2.Distance(s.Box.center, p) < Vector2.Distance(best.Box.center, p))) best = s;
            return best != null && ToggleDoor(best, HeroController.SilentInstance);
        }

        /// <summary>Dev: takes away the station nearest a point, as its item knocked out would (no drop).</summary>
        public static bool DevRemoveNear(Vector2 p, StationKind kind)
        {
            int best = -1;
            for (int i = 0; i < stations.Count; i++)
                if (stations[i].Kind == kind && stations[i].Region != null && (best < 0 || Vector2.Distance(stations[i].Box.center, p) < Vector2.Distance(stations[best].Box.center, p))) best = i;
            if (best < 0) return false;
            Remove(best);
            return true;
        }

        /// <summary>Dev: which way the station of a kind nearest a point faces (1 right, -1 left; 0 none there).</summary>
        public static int DevFacingNear(Vector2 p, StationKind kind)
        {
            Station best = null;
            foreach (var s in stations)
                if (s.Kind == kind && s.Region != null && (best == null || Vector2.Distance(s.Box.center, p) < Vector2.Distance(best.Box.center, p))) best = s;
            return best == null ? 0 : best.Right ? 1 : -1;
        }

        /// <summary>Dev: the sheet column the station of a kind nearest a point is drawn from (its style and its facing).</summary>
        public static int DevFrameXNear(Vector2 p, StationKind kind)
        {
            Station best = null;
            foreach (var s in stations)
                if (s.Kind == kind && s.Region != null && (best == null || Vector2.Distance(s.Box.center, p) < Vector2.Distance(best.Box.center, p))) best = s;
            return best == null ? -1 : best.Spec.StyleX + (best.Right ? best.Spec.RightX : 0);
        }

        /// <summary>Dev: the z the stations are drawn at.</summary>
        public static float DevZ => Z;
    }
}
