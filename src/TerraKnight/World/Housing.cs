using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's housing check, 1.4.5's WorldGen.StartRoomCheck / CheckRoom / RoomNeeds / ScoreRoom / MoveTownNPC /
    /// QuickFindHome / CheckSpecialTownNPCSpawningConditions, read on Hallownest's world. It works in Terraria's own
    /// tile coordinates, y growing downwards: Terraria's tile (i, j) is the grid's cell (i, -j - 1) (the same mapping
    /// TerraWorld.LineClearPx uses), so "j - 1" is the cell above as it is in Terraria. What a tile is (TileAt): the
    /// player's blocks, platforms, torches and rope as their tiles; Hollow Knight's own terrain, and everything outside
    /// the loaded scenes (which TerraWorld reads as undiggable rock), as solid ground; the furniture standing there
    /// (Stations.HousingTiles: doors, tables, chairs, benches, beds...) and the chests as their own tiles, frames and all.
    /// A wall is the player's (Main.wallHouse decides as in Terraria); a dug cell's backdrop is the scene's natural wall,
    /// never a house wall (Terraria's unsafe walls). Hallownest has no world edge (the loaded world ends in rock), no
    /// world surface, no corruption, crimson or hallow, no stinkbugs, trap doors or tall gates: those rules are kept and
    /// simply never fire.
    /// </summary>
    internal static class Housing
    {
        /// <summary>WorldGen.maxRoomTiles, maxRoomSize, and StartRoomCheck's smallest room.</summary>
        public const int MaxRoomTiles = 750, MaxRoomSize = 100, MinRoomTiles = 60;

        /// <summary>TownNPCRoomCheckFailureReason.</summary>
        public enum Failure : byte { None, TooCloseToWorldEdge, RoomIsTooBig, RoomIsTooSmall, HoleInWallIsTooBig, RoomCheckStartedInASolidTile, TooManyUnsafeWalls }

        /// <summary>One tile as WorldGen reads it: active(), type, frameX, wall.</summary>
        public struct Tile
        {
            public bool Active;
            public int Type;
            public int FrameX;
            public int Wall;
        }

        /// <summary>A townsperson with a home: its home tile (homeTileX, homeTileY: the floor it stands on) and NPC.housingCategory.</summary>
        public struct Occupant
        {
            public string Who;
            public int X, Y;
            public int Category;
        }

        // ------------------------------------------------------------------ Terraria's tables

        /// <summary>Main.wallHouse (Main.Initialize: every wall the player can place, none of the natural ones), as id ranges.</summary>
        static readonly int[] wallHouse =
        {
            1, 1, 4, 6, 10, 12, 16, 27, 29, 39, 41, 47, 60, 60, 66, 68, 72, 78, 82, 82, 84, 85, 88, 93, 100, 169, 172, 177, 179, 179,
            181, 184, 186, 186, 224, 243, 245, 348, 351, 366,
        };

        public static bool WallHouse(int wall)
        {
            // The areas' own walls (World/AreaBlocks.cs) are placed walls, as Terraria's crafted ones.
            if (AreaBlocks.IsAreaWall(wall)) return true;
            for (int i = 0; i < wallHouse.Length; i += 2)
                if (wall >= wallHouse[i] && wall <= wallHouse[i + 1]) return true;
            return false;
        }

        /// <summary>
        /// The backdrop of a cell dug out of the scene's own ground: the natural wall drawn there (Areas.WallFor's
        /// sheet), which Terraria would count as an unsafe wall. WallID 2, the natural dirt wall, stands for it: a wall,
        /// never Main.wallHouse.
        /// </summary>
        public const int NaturalWall = 2;

        /// <summary>Main.tileSolid for the tiles this world has: the blocks (dirt 0, stone 1, wood 30, sand 53, meteorite 37, magic ice 127, mushroom grass 70), platforms 19 and the closed door 10.</summary>
        static bool Solid(int type) => type == 0 || type == 1 || type == 10 || type == 19 || type == 30 || type == 37 || type == 53 || type == 70 || type == 127;

        /// <summary>A tile Main.tileSolid holds solid (active and of a solid type).</summary>
        public static bool SolidTile(Tile t) => t.Active && Solid(t.Type);

        /// <summary>Main.tileSolidTop among the solid ones: the platform (Collision.SolidTiles lets it through).</summary>
        static bool SolidTop(int type) => type == 19 || type == 14 || type == 16 || type == 18 || type == 114 || type == 134;

        /// <summary>TileID.Sets.RoomNeeds (CountsAsChairTypes, CountsAsTableTypes, CountsAsTorchTypes, CountsAsDoorTypes).</summary>
        static readonly int[] countsAsChair = { 15, 79, 89, 102, 487, 497 };
        static readonly int[] countsAsTable = { 14, 18, 87, 88, 90, 101, 354, 355, 464, 469, 487, 699 };
        static readonly int[] countsAsTorch = { 4, 33, 34, 35, 42, 49, 93, 95, 98, 100, 149, 173, 174, 270, 271, 316, 317, 318, 92, 372, 646, 405, 592, 572, 581, 660 };
        static readonly int[] countsAsDoor = { 10, 11, 19, 387, 386, 388, 389, 436, 435, 438, 427, 439, 437 };

        /// <summary>TileID.Sets.HousingWalls (11 the open door, 389 the open tall gate, 386 the open trap door).</summary>
        static bool HousingWalls(int type) => type == 11 || type == 389 || type == 386;
        /// <summary>TileID.Sets.IgnoredInHouseScore (torches, plants, herbs, the open trap door).</summary>
        static bool IgnoredInHouseScore(int type) => type == 4 || type == 3 || type == 73 || type == 82 || type == 83 || type == 84 || type == 386;
        /// <summary>TileID.Sets.BasicChest.</summary>
        static bool BasicChest(int type) => type == 21 || type == 467;

        // ------------------------------------------------------------------ the world as tiles

        static readonly Dictionary<long, (int Type, int FrameX)> furniture = new Dictionary<long, (int, int)>();
        static long Key(int gx, int gy) => ((long)gx << 32) ^ (uint)gy;

        /// <summary>Reads the furniture afresh (each check does), for TileAt on its own.</summary>
        public static void Refresh() => Snapshot();

        /// <summary>The furniture and chests standing now, cell by cell: read afresh before each check.</summary>
        static void Snapshot()
        {
            furniture.Clear();
            Stations.HousingTiles((gx, gy, type, frameX) => furniture[Key(gx, gy)] = (type, frameX));
            foreach (var pair in Chests.HousingBoxes())
            {
                var b = pair.Box;
                var lo = CellGrid.WorldToCell(new Vector2(b.xMin + 0.01f, b.yMin + 0.01f));
                var hi = CellGrid.WorldToCell(new Vector2(b.xMax - 0.01f, b.yMax - 0.01f));
                for (int x = lo.x; x <= hi.x; x++)
                    for (int y = lo.y; y <= hi.y; y++)
                        furniture[Key(x, y)] = (pair.Type, (x - lo.x) * 18);
            }
        }

        /// <summary>
        /// Main.tile[i, j]. The grid's cells by their tiles: Hollow Knight's own ground is solid (in the Fungal Wastes,
        /// the mod's mushroom biome, mushroom grass 70; elsewhere stone 1), and so is everything outside the loaded scenes.
        /// </summary>
        public static Tile TileAt(int i, int j)
        {
            int gx = i, gy = -j - 1;
            var t = new Tile();
            var region = TerraWorld.RegionAt(gx, gy);
            if (region == null)
            {
                t.Active = true;
                t.Type = 1;
                return t;
            }
            int lx = gx - region.CellOffset.x, ly = gy - region.CellOffset.y;
            int wall = region.Grid.GetWall(lx, ly);
            t.Wall = wall > 0 ? wall : wall < 0 && region.Grid.IsDug(lx, ly) ? NaturalWall : 0;
            var kind = region.Grid.Get(lx, ly);
            if (kind != CellKind.Empty)
            {
                t.Active = true;
                t.Type = TypeOf(kind, region);
                return t;
            }
            if (furniture.TryGetValue(Key(gx, gy), out var f))
            {
                t.Active = true;
                t.Type = f.Type;
                t.FrameX = f.FrameX;
            }
            return t;
        }

        static int TypeOf(CellKind kind, TerrainRegion region)
        {
            switch (kind)
            {
                case CellKind.Original: return Pylons.StyleFor(region.SceneName) == 7 ? 70 : 1;
                case CellKind.Dirt: return 0;
                case CellKind.Stone: return 1;
                case CellKind.Wood: return 30;
                case CellKind.Platform: return 19;
                case CellKind.Sand: return 53;
                case CellKind.Torch: return 4;
                case CellKind.Rope: return 213;
                case CellKind.MagicIce: return 127;
                case CellKind.Meteorite: return 37;
                case CellKind.Obsidian: return 56;
                default: return Cells.IsPlatform(kind) ? 19 : 1;
            }
        }

        /// <summary>Terraria's tile under a world point (Main.MouseWorld / 16).</summary>
        public static Vector2Int TileOf(Vector2 world)
        {
            var c = CellGrid.WorldToCell(world);
            return new Vector2Int(c.x, -c.y - 1);
        }

        /// <summary>The grid cell of a Terraria tile.</summary>
        public static Vector2Int CellOf(int i, int j) => new Vector2Int(i, -j - 1);

        /// <summary>WorldGen.IsOpenDoorAnchorFrame: the open door's hinge column (frameX % 72 below 18 or from 54).</summary>
        static bool IsOpenDoorAnchorFrame(Tile t)
        {
            if (!t.Active || t.Type != 11) return false;
            int n = t.FrameX % 72;
            return n < 18 || n >= 54;
        }

        // ------------------------------------------------------------------ WorldGen's state

        public static int RoomX1, RoomX2, RoomY1, RoomY2, NumRoomTiles, BestX, BestY, HiScore;
        public static bool CanSpawn;
        public static Failure Reason;
        static bool roomTorch, roomDoor, roomChair, roomTable, roomOccupied, roomEvil, roomHasStandingSpace;
        static int sharedRoomX;
        static readonly HashSet<int> houseTile = new HashSet<int>();
        static readonly Stack<Vector2Int> roomCheckStack = new Stack<Vector2Int>();

        /// <summary>WorldGen.roomTiles (BitSet2D): the tiles the last check spread over, MaxRoomSize either way of its start.</summary>
        static readonly bool[] roomBits = new bool[(MaxRoomSize * 2 + 1) * (MaxRoomSize * 2 + 1)];
        static int roomOffX, roomOffY;
        const int RoomBitsSize = MaxRoomSize * 2 + 1;

        static void ResetRoomTiles(int x, int y)
        {
            roomOffX = x - MaxRoomSize;
            roomOffY = y - MaxRoomSize;
            System.Array.Clear(roomBits, 0, roomBits.Length);
        }

        static bool RoomInBounds(int x, int y)
        {
            int a = x - roomOffX, b = y - roomOffY;
            return a >= 0 && a < RoomBitsSize && b >= 0 && b < RoomBitsSize;
        }

        static bool RoomAdd(int x, int y)
        {
            int i = (y - roomOffY) * RoomBitsSize + (x - roomOffX);
            if (roomBits[i]) return false;
            roomBits[i] = true;
            return true;
        }

        /// <summary>WorldGen.Housing_CheckIfInRoom.</summary>
        public static bool InRoom(int i, int j) => RoomInBounds(i, j) && roomBits[(j - roomOffY) * RoomBitsSize + (i - roomOffX)];

        // ------------------------------------------------------------------ the check

        /// <summary>
        /// WorldGen.StartRoomCheck: a flood from the tile, eight ways, over every tile that isn't solid, each needing a
        /// house wall or a block within two tiles across and within two tiles up or down (CheckRoom); the blocks it meets
        /// are counted with the room, and every kind of tile it touched goes into houseTile (what RoomNeeds reads).
        /// stopOnFail: NoRoomCheckFeedback's (every check that decides anything).
        /// </summary>
        public static bool StartRoomCheck(int x, int y, bool stopOnFail = true)
        {
            Snapshot();
            return RoomCheck(x, y, stopOnFail);
        }

        static bool RoomCheck(int x, int y, bool stopOnFail)
        {
            RoomX1 = x;
            RoomX2 = x;
            RoomY1 = y;
            RoomY2 = y;
            ResetRoomTiles(x, y);
            NumRoomTiles = 0;
            houseTile.Clear();
            CanSpawn = true;
            // (x < 10 || y < 10 || ... maxTilesX - 10: TooCloseToWorldEdge) Hallownest's world has no edge to come near.
            var start = TileAt(x, y);
            if (start.Active && Solid(start.Type))
            {
                Reason = Failure.RoomCheckStartedInASolidTile;
                CanSpawn = false;
                return false;
            }
            Reason = Failure.None;
            roomCheckStack.Clear();
            roomCheckStack.Push(new Vector2Int(x, y));
            while (roomCheckStack.Count > 0 && (CanSpawn || !stopOnFail))
            {
                var p = roomCheckStack.Pop();
                if (!CheckRoom(p.x, p.y, stopOnFail)) continue;
                for (int j = -1; j <= 1; j++)
                    for (int k = -1; k <= 1; k++)
                        if (j != 0 || k != 0) roomCheckStack.Push(new Vector2Int(p.x + j, p.y + k));
            }
            if (!CanSpawn) return false;
            if (NumRoomTiles < MinRoomTiles)
            {
                Reason = Failure.RoomIsTooSmall;
                CanSpawn = false;
                return false;
            }
            return CanSpawn;
        }

        /// <summary>WorldGen.CheckRoom.</summary>
        static bool CheckRoom(int x, int y, bool stopOnFail)
        {
            if (!RoomInBounds(x, y))
            {
                Reason = Failure.RoomIsTooBig;
                CanSpawn = false;
                return false;
            }
            if (!RoomAdd(x, y)) return false;
            NumRoomTiles++;
            if (stopOnFail && NumRoomTiles >= MaxRoomTiles)
            {
                Reason = Failure.RoomIsTooBig;
                CanSpawn = false;
                return false;
            }
            if (x < RoomX1) RoomX1 = x;
            if (x > RoomX2) RoomX2 = x;
            if (y < RoomY1) RoomY1 = y;
            if (y > RoomY2) RoomY2 = y;
            if (stopOnFail && (RoomX2 - RoomX1 >= MaxRoomSize || RoomY2 - RoomY1 >= MaxRoomSize))
            {
                Reason = Failure.RoomIsTooBig;
                CanSpawn = false;
                return false;
            }
            var t = TileAt(x, y);
            if (t.Active)
            {
                houseTile.Add(t.Type);
                if (Solid(t.Type)) return false;
                // The open door's hinge column stands as a wall would.
                if (t.Type == 11 && (t.FrameX == 0 || t.FrameX == 54 || t.FrameX == 72 || t.FrameX == 126)) return false;
                // 389 and 386 (an open tall gate, an open trap door) and 630, 631 (stinkbugs): none in this world.
            }
            bool flag = false, flag2 = false;
            for (int i = -2; i < 3; i++)
            {
                var h = TileAt(x + i, y);
                if (WallHouse(h.Wall)) flag = true;
                if (h.Active && (Solid(h.Type) || HousingWalls(h.Type))) flag = true;
                var v = TileAt(x, y + i);
                if (WallHouse(v.Wall)) flag2 = true;
                if (v.Active && (Solid(v.Type) || HousingWalls(v.Type))) flag2 = true;
            }
            if (!flag || !flag2)
            {
                Reason = t.Wall > 0 ? Failure.TooManyUnsafeWalls : Failure.HoleInWallIsTooBig;
                CanSpawn = false;
                return false;
            }
            return true;
        }

        /// <summary>WorldGen.RoomNeeds: a chair, a table, a light and a door (or a platform) among the tiles the room touched.</summary>
        public static bool RoomNeeds()
        {
            roomChair = Any(countsAsChair);
            roomTable = Any(countsAsTable);
            roomTorch = Any(countsAsTorch);
            roomDoor = Any(countsAsDoor);
            CanSpawn = roomChair && roomTable && roomDoor && roomTorch;
            return CanSpawn;
        }

        static bool Any(int[] types)
        {
            foreach (int t in types) if (houseTile.Contains(t)) return true;
            return false;
        }

        /// <summary>
        /// NPC.housingCategory: 1 for the town pets and the town slimes, 0 for everyone else, and two of a category
        /// don't share a room (TownRoomManager.CanNPCsLiveWithEachOther).
        /// </summary>
        static bool CanLiveWith(int category, Occupant other) => category != other.Category;

        /// <summary>WorldGen.ScoreRoom_IsThisRoomOccupiedBySomeone.</summary>
        static bool OccupiedBySomeone(string ignore, int category, List<Occupant> occupants)
        {
            if (occupants == null) return false;
            foreach (var o in occupants)
            {
                if (o.Who == ignore || !InRoom(o.X, o.Y) || !InRoom(o.X, o.Y - 1)) continue;
                if (!CanLiveWith(category, o)) return true;
                sharedRoomX = o.X;
            }
            return false;
        }

        /// <summary>WorldGen.Housing_GetTestedRoomBounds (no world edge to clamp to here).</summary>
        static void TestedRoomBounds(out int startX, out int endX, out int startY, out int endY)
        {
            const int num = 40, num2 = 5, num3 = 3;
            startX = RoomX1 - num2 - 1 - num;
            endX = RoomX2 + num2 + 1 + num;
            startY = RoomY1 - num3 - 1 - num;
            endY = RoomY2 + num3 + 1 + num;
        }

        /// <summary>WorldGen.GetTileTypeCountByCategory(TotalGoodEvil): the hallow's tiles less the corruption's and the crimson's.</summary>
        static int TotalGoodEvil(Dictionary<int, int> counts)
        {
            int C(int t) => counts.TryGetValue(t, out int n) ? n : 0;
            int hallow = C(109) + C(110) + C(113) + C(117) + C(116) + C(164) + C(403) + C(402);
            int corruption = C(23) + C(24) + C(25) + C(32) + C(112) + C(163) + C(400) + C(398) + -5 * C(27);
            int crimson = C(199) + C(203) + C(200) + C(401) + C(399) + C(234) + C(352) - 5 * C(27);
            return hallow - (corruption + crimson + 5 * C(27));
        }

        /// <summary>
        /// WorldGen.ScoreRoom: every floor tile of the room with solid tiles either side and three free tiles over it
        /// (Collision.SolidTiles) scores 50, less the evil about, +5 for each piece of furniture in the five by four over
        /// it, -5 for each block, -20 for a door, a chest's -30 and anything right over it -15; the best tile whose three
        /// tiles above lie in the room is the home (bestX, bestY). Someone of the same housing category living there
        /// already (other than ignore) makes it occupied (hiScore -1).
        /// </summary>
        public static void ScoreRoom(string ignore, int category, List<Occupant> occupants)
        {
            roomOccupied = false;
            roomHasStandingSpace = false;
            roomEvil = false;
            sharedRoomX = -1;
            if (OccupiedBySomeone(ignore, category, occupants))
            {
                roomOccupied = true;
                HiScore = -1;
                return;
            }
            HiScore = 0;
            int num2 = 50;
            TestedRoomBounds(out int startX, out int endX, out int startY, out int endY);
            var counts = new Dictionary<int, int>();
            for (int i = startX + 1; i <= endX - 1; i++)
                for (int j = startY + 2; j <= endY + 1; j++)
                {
                    var t = TileAt(i, j);
                    if (!t.Active) continue;
                    counts.TryGetValue(t.Type, out int n);
                    counts[t.Type] = n + 1;
                }
            int num3 = -TotalGoodEvil(counts);
            if (num3 < 50) num3 = 0;
            num2 -= num3;
            if (num3 > 0) roomEvil = true;
            if (num2 <= -250)
            {
                HiScore = num2;
                return;
            }
            for (int i = RoomX1 + 1; i < RoomX2; i++)
                for (int j = RoomY1 + 2; j < RoomY2 + 2; j++)
                {
                    var floor = TileAt(i, j);
                    // ScoreRoom_CanBeHomeSpot: not a bubble (379; none here).
                    if (!floor.Active || floor.Type == 379 || !Solid(floor.Type) || SolidTiles(i - 1, i + 1, j - 3, j - 1)) continue;
                    var left = TileAt(i - 1, j);
                    var right = TileAt(i + 1, j);
                    if (!left.Active || !Solid(left.Type) || !right.Active || !Solid(right.Type)) continue;
                    int num = num2, num4 = 0, num5 = 0;
                    for (int k = i - 2; k < i + 3; k++)
                        for (int l = j - 4; l < j; l++)
                        {
                            var tile = TileAt(k, l);
                            if (!tile.Active || IgnoredInHouseScore(tile.Type) || (tile.Type == 11 && !IsOpenDoorAnchorFrame(tile))) continue;
                            if (k == i) num4++;
                            else if (BasicChest(tile.Type)) num5++;
                            else if (tile.Type == 10 || tile.Type == 388) num -= 20;
                            else if (IsOpenDoorAnchorFrame(tile) || tile.Type == 389) num -= 20;
                            else if (!Solid(tile.Type)) num += 5;
                            else num -= 5;
                        }
                    if (num > 0) roomHasStandingSpace = true;
                    if (sharedRoomX >= 0 && num >= 1 && Mathf.Abs(sharedRoomX - i) < 3) num = 1;
                    if (num > 0 && num5 > 0)
                    {
                        num -= 30 * num5;
                        if (num < 1) num = 1;
                    }
                    if (num > 0 && num4 > 0)
                    {
                        num -= 15 * num4;
                        if (num < 1) num = 1;
                    }
                    if (num <= HiScore) continue;
                    bool flag = InRoom(i, j);
                    for (int m = 1; m <= 3; m++)
                    {
                        var above = TileAt(i, j - m);
                        bool free = !above.Active || !Solid(above.Type);
                        if (!InRoom(i, j - m)) free = false;
                        if (!free) flag = false;
                    }
                    if (flag && InRoom(i, j - 1))
                    {
                        HiScore = num;
                        BestX = i;
                        BestY = j;
                    }
                }
        }

        /// <summary>Collision.SolidTiles: a solid tile that isn't solid only on top (a platform) anywhere in the box.</summary>
        static bool SolidTiles(int startX, int endX, int startY, int endY)
        {
            for (int i = startX; i < endX + 1; i++)
                for (int j = startY; j < endY + 1; j++)
                {
                    var t = TileAt(i, j);
                    if (t.Active && Solid(t.Type) && !SolidTop(t.Type)) return true;
                }
            return false;
        }

        /// <summary>
        /// WorldGen.CheckSpecialTownNPCSpawningConditions: the Truffle (160) lives only where mushroom tiles (70, 71, 72,
        /// 528) number SceneMetrics.MushroomTileThreshold (100) round the room — in Hallownest, the Fungal Wastes' ground.
        /// (Its other condition, a room below the world's surface before he first came, has no surface here to be under.)
        /// </summary>
        public static bool CheckSpecialTownNPCSpawningConditions(int type)
        {
            if (type != 160) return true;
            TestedRoomBounds(out int startX, out int endX, out int startY, out int endY);
            int num = 0;
            for (int i = startX + 1; i < endX; i++)
                for (int j = startY + 2; j < endY + 2; j++)
                {
                    var t = TileAt(i, j);
                    if (t.Active && (t.Type == 70 || t.Type == 71 || t.Type == 72 || t.Type == 528)) num++;
                }
            return num >= 100;
        }

        // ------------------------------------------------------------------ the verdicts

        /// <summary>
        /// WorldGen.MoveTownNPC with NoRoomCheckFeedback.WithText: whether the room at (x, y) is a house for a
        /// townsperson of that type and housing category (npcType -1: the housing query, Main.PerformHousingCheck), and
        /// Terraria's own words for what is wrong (Main.NewText's lines, in the language now). ignore: one whose own
        /// home doesn't count (the eviction's diagnosis of its own room); Terraria's MoveTownNPC ignores nobody.
        /// </summary>
        public static bool MoveTownNPC(int x, int y, int npcType, int category, string npcTypeName, List<Occupant> occupants, List<string> said, string ignore = null)
        {
            bool ok = true;
            if (!StartRoomCheck(x, y))
            {
                string text = Text("LegacyInterface.40");
                switch (Reason)
                {
                    case Failure.TooManyUnsafeWalls: text = Text("TownNPCHousingFailureReasons.RoomHasUnsafeWalls"); break;
                    case Failure.HoleInWallIsTooBig: text = Text("TownNPCHousingFailureReasons.HoleInWallIsTooBig"); break;
                    case Failure.RoomCheckStartedInASolidTile: text = Text("TownNPCHousingFailureReasons.RoomCheckStartedInASolidTile"); break;
                    case Failure.RoomIsTooBig: text = Text("TownNPCHousingFailureReasons.RoomIsTooBig"); break;
                    case Failure.RoomIsTooSmall: text = Text("TownNPCHousingFailureReasons.RoomIsTooSmall"); break;
                    case Failure.TooCloseToWorldEdge: text = Text("TownNPCHousingFailureReasons.TooCloseToWorldEdge"); break;
                }
                said?.Add(text);
                return false;
            }
            if (!RoomNeeds())
            {
                var missing = new List<string>();
                if (!roomTorch) missing.Add(Text("Game.HouseLightSource"));
                if (!roomDoor) missing.Add(Text("Game.HouseDoor"));
                if (!roomTable) missing.Add(Text("Game.HouseTable"));
                if (!roomChair) missing.Add(Text("Game.HouseChair"));
                string format = Text("Game.HouseMissing_" + missing.Count);
                string line;
                try { line = string.Format(format, missing.ToArray()); }
                catch (System.FormatException) { line = format + " " + string.Join(", ", missing); }
                said?.Add(line);
                return false;
            }
            // RoomHasAStinkbug / RoomHasAnEchoStinkbug: no stinkbugs in Hallownest.
            ScoreRoom(ignore, category, occupants);
            if (HiScore <= 0)
            {
                if (roomOccupied) said?.Add(Text("LegacyInterface.41"));
                else if (roomEvil) said?.Add(Text("LegacyInterface.42"));
                else if (!roomHasStandingSpace) said?.Add(Text("TownNPCHousingFailureReasons.RoomHasNoStandingSpace"));
                else said?.Add(Text("LegacyInterface.40"));
                return false;
            }
            if (npcType >= 0 && !CheckSpecialTownNPCSpawningConditions(npcType))
            {
                said?.Add(Text("LegacyInterface.55") + " " + npcTypeName);
                ok = false;
            }
            return ok;
        }

        /// <summary>
        /// WorldGen.QuickFindHome, what Main.UpdateTime_SpawnTownNPCs runs for every townsperson with a home each 7200
        /// ticks: the room is looked for again from over its home tile, then a tile either way round it, then every
        /// second tile ten either way (each column's search stopping at its first room, as Terraria's loops do); its
        /// needs, its score (the townsperson's own home not counting) and whether another of its category has the same
        /// home tile. True: still housed, at (BestX, BestY), which may have moved within the room.
        /// </summary>
        public static bool QuickFindHome(int homeX, int homeY, int npcType, int category, string who, List<Occupant> occupants)
        {
            Snapshot();
            CanSpawn = false;
            RoomCheck(homeX, homeY - 1, true);
            if (!CanSpawn)
                for (int i = homeX - 1; i < homeX + 2; i++)
                    for (int j = homeY - 1; j < homeY + 2 && !RoomCheck(i, j, true); j++) { }
            if (!CanSpawn)
            {
                const int num = 10;
                for (int k = homeX - num; k <= homeX + num; k += 2)
                    for (int l = homeY - num; l <= homeY + num && !RoomCheck(k, l, true); l += 2) { }
            }
            if (!CheckSpecialTownNPCSpawningConditions(npcType)) CanSpawn = false;
            if (!CanSpawn) return false;
            RoomNeeds();
            if (CanSpawn) ScoreRoom(who, category, occupants);
            if (CanSpawn && HiScore > 0) CanSpawn = RoomConsideredFree(who, category, occupants);
            return CanSpawn && HiScore > 0;
        }

        /// <summary>WorldGen.IsRoomConsideredOccupiedForNPCIndex (true: free): no one else of the category has (BestX, BestY) for its home.</summary>
        static bool RoomConsideredFree(string who, int category, List<Occupant> occupants)
        {
            if (occupants == null) return true;
            foreach (var o in occupants)
                if (o.Who != who && o.X == BestX && o.Y == BestY && !CanLiveWith(category, o)) return false;
            return true;
        }

        /// <summary>
        /// The width a townsperson walks in its room: the run of the home's floor either side of the home tile whose
        /// tiles above lie in the room and are free (the open door's hinge, the closed door and the frame are where it
        /// stops). Read right after a check of this room. (left, right) in tiles.
        /// </summary>
        public static void WalkSpan(int bestX, int bestY, out int left, out int right)
        {
            left = right = bestX;
            bool Walkable(int i)
            {
                var floor = TileAt(i, bestY);
                if (!floor.Active || !Solid(floor.Type)) return false;
                for (int m = 1; m <= 2; m++)
                {
                    if (!InRoom(i, bestY - m)) return false;
                    var t = TileAt(i, bestY - m);
                    if (t.Active && (Solid(t.Type) || IsOpenDoorAnchorFrame(t))) return false;
                }
                return true;
            }
            if (!InRoom(bestX, bestY - 1)) return;
            while (left - 1 > bestX - MaxRoomSize && Walkable(left - 1)) left--;
            while (right + 1 < bestX + MaxRoomSize && Walkable(right + 1)) right++;
        }

        /// <summary>
        /// Whether a click is in something the player built to live in rather than out in the open (the mod's own rule,
        /// which decides between Terraria's housing and the panel's chair and bench): a spread like CheckRoom's from the
        /// tile, through every tile that isn't solid, that never gets MaxRoomSize tiles away.
        /// </summary>
        public static bool Enclosed(int x, int y)
        {
            Snapshot();
            ResetRoomTiles(x, y);
            var start = TileAt(x, y);
            if (start.Active && Solid(start.Type)) return false;
            roomCheckStack.Clear();
            roomCheckStack.Push(new Vector2Int(x, y));
            int seen = 0;
            while (roomCheckStack.Count > 0)
            {
                var p = roomCheckStack.Pop();
                if (!RoomInBounds(p.x, p.y)) return false;
                if (!RoomAdd(p.x, p.y)) continue;
                if (++seen > RoomBitsSize * RoomBitsSize) return false;
                var t = TileAt(p.x, p.y);
                if (t.Active && (Solid(t.Type) || (t.Type == 11 && IsOpenDoorAnchorFrame(t)))) continue;
                for (int j = -1; j <= 1; j++)
                    for (int k = -1; k <= 1; k++)
                        if (j != 0 || k != 0) roomCheckStack.Push(new Vector2Int(p.x + j, p.y + k));
            }
            return true;
        }

        /// <summary>A tile the click is on: a solid one, and whether the player made it (not Hollow Knight's own ground).</summary>
        public static bool SolidAt(int i, int j, out bool playerMade)
        {
            Snapshot();
            var t = TileAt(i, j);
            var cell = CellOf(i, j);
            playerMade = TerraWorld.Get(cell.x, cell.y) != CellKind.Original && TerraWorld.InWorld(cell.x, cell.y);
            return t.Active && Solid(t.Type);
        }

        /// <summary>Terraria's own text for a key in the language now (English if that language hasn't it).</summary>
        public static string Text(string key) => TerrariaText.Get(Lang.Culture, key) ?? TerrariaText.Get("en-US", key) ?? key;
    }
}
