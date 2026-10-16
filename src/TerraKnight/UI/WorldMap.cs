using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's fullscreen map, drawn from the world itself: every cell of every scene the
    /// player has been through, a pixel each, as the terrain grid has it (the scene's ground, its
    /// hand-placed structures, the player's blocks, platforms, torches, rope, dug-out walls), with
    /// water, spikes, acid and lava over the open cells, and the things worth knowing about as
    /// coloured blocks (benches, lamps, doors, levers, lifts, chests, the stag, grubs, totems...),
    /// named under the mouse. Scenes sit where the seamless world joins them (WorldMapLayout).
    /// Cells are explored near the player with terrain occlusion and kept with the save
    /// (TerraKnight_slotN.map). Purchased charts show unexplored cells at 30% opacity.
    /// </summary>
    internal static partial class WorldMap
    {
        public static bool Enabled = true;
        const float C = CellGrid.CellSize;
        const int MapVersion = 2;

        enum Tile : byte { None, Air, Terrain, Structure, Platform, Dirt, Stone, Wood, Sand, Ice, Torch, Rope, Dug, Meteorite }
        enum Wet : byte { None, Water, Spikes, Acid, Lava }
        enum Obj : byte { Bench, Lamp, Door, Lever, Lift, Chest, Stag, Toll, Grub, GeoRock, Totem, Item, BreakableWall, Spring, Npc, Pylon, TerraChest, LifeCrystal }

        sealed class Thing
        {
            public Obj Kind;
            public string Name;
            public RectInt Cells;
        }

        sealed class Room
        {
            public string Name, Area;
            public int W, H;
            public Vector2Int At;
            public byte[] Tiles, Wets, Seen;
            public readonly List<Thing> Things = new List<Thing>();
            public bool Captured, RevealAll, AnySeen, Dirty = true;
            /// <summary>Filled from the world atlas (creative), not from the scene itself: not kept in the save.</summary>
            public bool FromAtlas;
            /// <summary>Its area's map bought: unexplored cells drawn at 30% opacity, Life Crystals still hidden.</summary>
            public bool Charted;
            public float LastBuilt;
            public Texture2D Solid, Open;
            public Color Hue;

            public bool IsSeen(int x, int y) { int i = y * W + x; return (Seen[i >> 3] & (1 << (i & 7))) != 0; }
            /// <summary>Drawn on the map: seen, or its area's map bought.</summary>
            public bool Shows(int x, int y) => Charted || IsSeen(x, y);
            public bool AnyShown => AnySeen || Charted;
            public bool SetSeen(int x, int y)
            {
                int i = y * W + x;
                byte bit = (byte)(1 << (i & 7));
                if ((Seen[i >> 3] & bit) != 0) return false;
                Seen[i >> 3] |= bit;
                return true;
            }
        }

        static readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        static readonly HashSet<CellGrid> watched = new HashSet<CellGrid>();
        static readonly Queue<string> toCapture = new Queue<string>();
        static float nextReveal, nextCapture, nextChart;
        static Vector2 lastHead;
        static bool haveHead;

        public static bool Open { get; private set; }
        static Vector2 pan;      // the view's centre, in global cells
        static float zoom = 4f;  // screen pixels per cell
        static bool dragging;
        static Vector2 lastMouse;
        public static int DevCaptures, DevRevealed;

        // ---------------------------------------------------------------- the world as it is met

        /// <summary>Call every frame in gameplay: scenes taken onto the map as they load, cells revealed round the player.</summary>
        public static void Tick()
        {
            if (!Enabled) return;
            float now = Time.unscaledTime;
            if (now >= nextCapture)
            {
                nextCapture = now + 0.25f;
                foreach (var ts in TerrainSystem.All)
                {
                    var r = ts.Region;
                    if (r == null || r.SceneName == null || !layout.ContainsKey(r.SceneName)) continue;
                    if (!rooms.TryGetValue(r.SceneName, out var room) || !room.Captured || !watched.Contains(r.Grid))
                        if (!toCapture.Contains(r.SceneName)) toCapture.Enqueue(r.SceneName);
                }
                // One scene a turn: a capture walks every cell and object of the scene.
                if (toCapture.Count > 0)
                {
                    string scene = toCapture.Dequeue();
                    var region = TerrainSystem.Find(scene);
                    if (region != null) Capture(region);
                }
            }
            if (now >= nextReveal)
            {
                nextReveal = now + 0.15f;
                Reveal();
            }
            if (now >= nextChart)
            {
                nextChart = now + 1f;
                ChartBought();
            }
        }

        static Room RoomFor(string scene, int w, int h)
        {
            if (rooms.TryGetValue(scene, out var room) && room.W == w && room.H == h && room.Tiles != null) return room;
            bool revealAll = room != null && room.RevealAll;
            room = new Room { Name = scene, W = w, H = h, At = layout[scene], Tiles = new byte[w * h], Wets = new byte[w * h], Seen = new byte[(w * h + 7) / 8], RevealAll = revealAll };
            room.Area = Areas.AreaName(scene);
            room.Hue = Player.OpeningPortal.AreaColor(room.Area);
            rooms[scene] = room;
            return room;
        }

        static Tile TileOf(CellGrid g, int x, int y)
        {
            switch (g.Get(x, y))
            {
                case CellKind.Original: return g.IsArt(x, y) ? Tile.Structure : Tile.Terrain;
                case CellKind.Dirt: return Tile.Dirt;
                case CellKind.Stone: return Tile.Stone;
                case CellKind.Wood: return Tile.Wood;
                case CellKind.Sand: return Tile.Sand;
                case CellKind.MagicIce: return Tile.Ice;
                case CellKind.Platform: return Tile.Platform;
                case CellKind.Torch: return Tile.Torch;
                case CellKind.Rope: return Tile.Rope;
                case CellKind.Meteorite: return Tile.Meteorite;
                default: return g.IsDug(x, y) ? Tile.Dug : Tile.Air;
            }
        }

        static void Capture(TerrainRegion region)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var grid = region.Grid;
            var room = RoomFor(region.SceneName, grid.Width, grid.Height);
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    room.Tiles[y * grid.Width + x] = (byte)TileOf(grid, x, y);
            if (watched.Add(grid))
            {
                var r = room;
                grid.CellChanged += (x, y) =>
                {
                    if (!grid.InBounds(x, y) || r.Tiles == null || r.W != grid.Width) return;
                    r.Tiles[y * r.W + x] = (byte)TileOf(grid, x, y);
                    r.Dirty = true;
                };
            }
            var scene = USM.GetSceneByName(region.SceneName);
            CaptureThings(room, region.CellOffset, scene);
            room.FromAtlas = false;
            if (room.RevealAll)
            {
                for (int i = 0; i < room.Seen.Length; i++) room.Seen[i] = 0xFF;
                room.AnySeen = true;
                room.RevealAll = false;
            }
            room.Captured = true;
            room.Dirty = true;
            DevCaptures++;
            Plugin.Log.LogInfo($"[地图] 记下 {region.SceneName}：{grid.Width}x{grid.Height} 格，{room.Things.Count} 个物件，{sw.ElapsedMilliseconds} ms");
        }

        /// <summary>A scene's water, hazards and things into its room, its grid's origin at offset (global cells).</summary>
        static void CaptureThings(Room room, Vector2Int offset, UnityEngine.SceneManagement.Scene scene)
        {
            Array.Clear(room.Wets, 0, room.Wets.Length);
            room.Things.Clear();
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                CaptureObjects(root.transform, offset, room);
        }

        static RectInt CellsOf(Bounds b, Vector2Int offset, int limit) => CellsOf(b, offset, limit, limit);

        /// <summary>A world rectangle in the region's own cells, at least a cell, at most the limits round its centre.</summary>
        static RectInt CellsOf(Bounds b, Vector2Int offset, int limitW, int limitH)
        {
            int x0 = Mathf.FloorToInt(b.min.x / C) - offset.x, y0 = Mathf.FloorToInt(b.min.y / C) - offset.y;
            int x1 = Mathf.CeilToInt(b.max.x / C) - offset.x, y1 = Mathf.CeilToInt(b.max.y / C) - offset.y;
            if (x1 <= x0) x1 = x0 + 1;
            if (y1 <= y0) y1 = y0 + 1;
            if (x1 - x0 > limitW) { int cx = (x0 + x1) / 2; x0 = cx - limitW / 2; x1 = x0 + limitW; }
            if (y1 - y0 > limitH) { int cy = (y0 + y1) / 2; y0 = cy - limitH / 2; y1 = y0 + limitH; }
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        static void CaptureObjects(Transform root, Vector2Int region, Room room)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(false))
            {
                var go = t.gameObject;
                // Hazards and water over the open cells.
                var dh = go.GetComponent<DamageHero>();
                if (dh != null && dh.hazardType >= 2 && dh.hazardType <= 4)
                {
                    var col = go.GetComponent<Collider2D>();
                    if (col != null) Wetten(room, CellsOf(col.bounds, region, 400), dh.hazardType == 2 ? Wet.Spikes : dh.hazardType == 3 ? Wet.Acid : Wet.Lava);
                    continue;
                }
                if (go.layer == 4)
                {
                    var col = go.GetComponent<Collider2D>();
                    if (col != null) Wetten(room, CellsOf(col.bounds, region, 400), Wet.Water);
                    continue;
                }
                if (!Classify(go, out var kind, out string name)) continue;
                var c2 = go.GetComponent<Collider2D>();
                var rend = go.GetComponent<Renderer>();
                Bounds b;
                if (kind == Obj.Door || kind == Obj.Spring || kind == Obj.BreakableWall) { if (c2 == null) continue; b = c2.bounds; }
                else if (rend != null && rend.bounds.size.sqrMagnitude > 0.01f) b = rend.bounds;
                else if (c2 != null) b = c2.bounds;
                else b = new Bounds(t.position, Vector3.one * 0.5f);
                var (lw, lh) = SizeLimit(kind);
                var cells = CellsOf(b, region, lw, lh);
                // Copies of one thing on top of each other (a lamp and its bug) are one block.
                bool dup = false;
                foreach (var o in room.Things)
                    if (o.Kind == kind && o.Cells.Overlaps(cells)) { dup = true; break; }
                if (!dup) room.Things.Add(new Thing { Kind = kind, Name = name, Cells = cells });
            }
        }

        /// <summary>The biggest block a kind of thing is drawn as, in cells (its sprite with glows and shadows is bigger than the thing).</summary>
        static (int, int) SizeLimit(Obj kind)
        {
            switch (kind)
            {
                case Obj.Lamp: return (3, 8);
                case Obj.Bench: return (8, 3);
                case Obj.Lever: return (2, 4);
                case Obj.Lift: return (10, 3);
                case Obj.Chest: return (4, 3);
                case Obj.Stag: return (8, 6);
                case Obj.Toll: return (4, 5);
                case Obj.Grub: return (3, 4);
                case Obj.GeoRock: return (4, 4);
                case Obj.Totem: return (4, 6);
                case Obj.Item: return (2, 2);
                case Obj.Npc: return (3, 5);
                case Obj.Door: return (6, 8);
                case Obj.BreakableWall: return (30, 30);
                case Obj.Spring: return (120, 60);
                default: return (8, 8);
            }
        }

        static void Wetten(Room room, RectInt r, Wet wet)
        {
            for (int y = Mathf.Max(0, r.yMin); y < Mathf.Min(room.H, r.yMax); y++)
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(room.W, r.xMax); x++)
                    room.Wets[y * room.W + x] = (byte)wet;
        }

        /// <summary>What a scene object is on the map, if anything (names as the game's scenes call them).</summary>
        static bool Classify(GameObject go, out Obj kind, out string name)
        {
            kind = default;
            name = null;
            string n = go.name;
            var tp = go.GetComponent<TransitionPoint>();
            if (tp != null)
            {
                if (!tp.isADoor || n.StartsWith("door_dreamReturn")) return false;
                kind = Obj.Door;
                name = "门" + (RoomNames.TryGetValue(tp.targetScene ?? "", out var rn) || DoorNames.TryGetValue(n, out rn) ? "：" + rn : "");
                return true;
            }
            foreach (var f in go.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Bench Control") { kind = Obj.Bench; name = "长椅"; return true; }
            if (go.GetComponent<PylonObject>() != null) return false; // drawn from the unlocked list
            string low = n.ToLowerInvariant();
            if (n.StartsWith("Lamp_Bug") || n.StartsWith("lamp_bug") && !low.Contains("escape") || (low.Contains("lamp") && !low.Contains("glow") && !low.Contains("light") && !low.Contains("escape") && go.GetComponent<SpriteRenderer>() != null))
            { kind = Obj.Lamp; name = "路灯"; return true; }
            if (n.Contains("Lever") && !low.Contains("base")) { kind = Obj.Lever; name = "拉杆"; return true; }
            if (n.StartsWith("Ruins Lift") || n.StartsWith("plat_lift_0") || n == "White Palace Lift" || n == "Toll Machine Lift" || n == "Stag Lift")
            { kind = Obj.Lift; name = "升降梯"; return true; }
            if (n == "Chest" || n == "Mantis Chest" || n == "Grimm Chest") { kind = Obj.Chest; name = "宝箱"; return true; }
            if (n == "Stag" || n.StartsWith("Station Bell")) { kind = Obj.Stag; name = "鹿角虫车站"; return true; }
            if (n.StartsWith("Toll Gate Machine") || n == "Toll Machine Bench") { kind = Obj.Toll; name = "收费机关"; return true; }
            if (n.StartsWith("Grub Bottle")) { kind = Obj.Grub; name = "被困的幼虫"; return true; }
            if (n.StartsWith("Geo Rock")) { kind = Obj.GeoRock; name = "吉欧石"; return true; }
            if (n.StartsWith("Soul Totem")) { kind = Obj.Totem; name = "灵魂图腾"; return true; }
            if (n.StartsWith("Shiny Item")) { kind = Obj.Item; name = "物品"; return true; }
            if (n.StartsWith("Breakable Wall")) { kind = Obj.BreakableWall; name = "可打碎的墙"; return true; }
            if (n == "Spa Region") { kind = Obj.Spring; name = "温泉"; return true; }
            if (n.EndsWith(" NPC") && !n.StartsWith("Colosseum Crowd") && !n.StartsWith("CamLock") && go.GetComponent<PlayMakerFSM>() != null)
            { kind = Obj.Npc; name = "NPC"; return true; }
            return false;
        }

        /// <summary>Doors whose way the game sets as they are used, by the gate's own name.</summary>
        static readonly Dictionary<string, string> DoorNames = new Dictionary<string, string>
        {
            ["door_sly"] = "斯莱的商店", ["door_mapper"] = "地图店", ["door_bretta"] = "布雷塔的家", ["door_station"] = "德特茅斯车站",
            ["door_jiji"] = "杰吉的小屋", ["room_grimm"] = "格林剧团帐篷", ["room_divine"] = "迪瓦恩的帐篷", ["door_charmshop"] = "萨鲁巴的护符店",
            ["door_mender"] = "修补匠的家", ["door_stagExit"] = "鹿角虫车站",
        };

        static readonly Dictionary<string, string> RoomNames = new Dictionary<string, string>
        {
            ["Room_shop"] = "斯莱的商店", ["Room_mapper"] = "地图店", ["Room_Bretta"] = "布雷塔的家", ["Room_Charm_Shop"] = "萨鲁巴的护符店",
            ["Room_Mender_House"] = "修补匠的家", ["Room_temple"] = "黑卵神殿", ["Room_Town_Stag_Station"] = "德特茅斯车站", ["Grimm_Main_Tent"] = "格林剧团帐篷",
            ["Grimm_Divine"] = "迪瓦恩的帐篷", ["Room_Jinn"] = "钢魂吉恩", ["Room_Ouiji"] = "杰吉的小屋", ["Room_nailsmith"] = "骨钉匠的铺子",
            ["Room_nailmaster"] = "骨钉大师马托的小屋", ["Room_nailmaster_02"] = "骨钉大师谢欧的小屋", ["Room_nailmaster_03"] = "骨钉大师奥罗的小屋",
            ["Room_Queen"] = "白色夫人", ["Room_Fungus_Shaman"] = "萨满", ["Room_Slug_Shrine"] = "大鼻涕虫圣地", ["Room_Mansion"] = "灰色哀悼者的宅邸",
            ["Room_spider_small"] = "野兽巢穴的小屋", ["Room_ruinhouse"] = "废墟小屋", ["Crossroads_ShamanTemple"] = "先祖之丘", ["Room_Wyrm"] = "王国边缘的骸骨",
            ["Room_Mask_Maker"] = "面具制作者", ["Room_Colosseum_01"] = "愚人斗兽场", ["Fungus3_archive"] = "教师档案馆", ["Room_GG_Shortcut"] = "神居捷径",
            ["Deepnest_Spider_Town"] = "野兽巢穴", ["Room_Final_Boss_Atrium"] = "黑卵神殿",
        };

        static void Reveal()
        {
            var hero = HeroController.SilentInstance;
            var r = TerraWorld.Live;
            if (hero == null || r == null || !GameHooks.PlayerHasControl() || Open) return;
            if (r.SceneName == null || !rooms.TryGetValue(r.SceneName, out var room) || !room.Captured || room.W != r.Grid.Width || room.H != r.Grid.Height) return;
            var body = hero.GetComponent<Collider2D>();
            Vector2 centre = body != null ? (Vector2)body.bounds.center : (Vector2)hero.transform.position;
            var cell = CellGrid.WorldToCell(centre) - r.CellOffset;
            int changed = MapExploration.Reveal(room.W, room.H, cell.x, cell.y,
                (x, y) => Cells.Blocks(r.Grid.Get(x, y)), room.SetSeen);
            if (changed > 0) { room.AnySeen = true; room.Dirty = true; DevRevealed += changed; }
        }

        // ---------------------------------------------------------------- where things are on the map

        /// <summary>The player's place on the map, in global cells.</summary>
        static Vector2 HeadCell(out bool found)
        {
            found = false;
            var hero = HeroController.SilentInstance;
            var live = TerraWorld.Live;
            if (hero != null && live != null && live.SceneName != null && layout.TryGetValue(live.SceneName, out var at))
            {
                // The body's middle (Terraria's map puts the head on the player's centre), as the cells round it are revealed.
                var body = hero.GetComponent<Collider2D>();
                Vector2 p = body != null ? (Vector2)body.bounds.center : (Vector2)hero.transform.position;
                lastHead = new Vector2(at.x + p.x / C - live.CellOffset.x, at.y + p.y / C - live.CellOffset.y);
                haveHead = true;
                found = true;
            }
            return haveHead ? lastHead : Vector2.zero;
        }

        /// <summary>A point in a scene's own coordinates (units, as it loads unmoved) on the map.</summary>
        static bool ScenePoint(string scene, Vector2 local, out Vector2 cell)
        {
            cell = default;
            if (scene == null || !layout.TryGetValue(scene, out var at)) return false;
            cell = new Vector2(at.x + local.x / C, at.y + local.y / C);
            return true;
        }

        static Vector2 ToScreen(Vector2 cell) => new Vector2(Screen.width / 2f + (cell.x - pan.x) * zoom, Screen.height / 2f - (cell.y - pan.y) * zoom);
        static Vector2 ToCell(Vector2 gui) => new Vector2(pan.x + (gui.x - Screen.width / 2f) / zoom, pan.y - (gui.y - Screen.height / 2f) / zoom);

        // ---------------------------------------------------------------- colours

        static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        static Color TileColor(Room room, Tile t)
        {
            switch (t)
            {
                case Tile.Terrain: return Color.Lerp(Rgb(108, 108, 116), room.Hue, 0.3f);
                case Tile.Structure: return Color.Lerp(Rgb(150, 138, 118), room.Hue, 0.2f);
                case Tile.Air: return Color.Lerp(Rgb(18, 18, 24), room.Hue, 0.14f);
                case Tile.Dug: return Rgb(66, 52, 42);
                case Tile.Dirt: return Rgb(151, 107, 75);
                case Tile.Stone: return Rgb(128, 128, 128);
                case Tile.Wood: return Rgb(191, 142, 111);
                case Tile.Sand: return Rgb(186, 168, 84);
                case Tile.Ice: return Rgb(144, 195, 232);
                case Tile.Platform: return Rgb(210, 160, 120);
                case Tile.Torch: return Rgb(253, 221, 3);
                case Tile.Rope: return Rgb(137, 120, 67);
                // Bright on the map (MapHelper's 104, 86, 84 lifted to a glow): a meteorite is found by it.
                case Tile.Meteorite: return Rgb(255, 112, 40);
                default: return Color.clear;
            }
        }

        static Color WetColor(Wet w) =>
            w == Wet.Water ? Rgb(9, 61, 191) : w == Wet.Spikes ? Rgb(196, 64, 64) : w == Wet.Acid ? Rgb(96, 200, 60) : w == Wet.Lava ? Rgb(253, 32, 3) : Color.clear;

        static Color ObjColor(Obj o)
        {
            switch (o)
            {
                case Obj.Bench: return Rgb(232, 178, 92);
                case Obj.Lamp: return Rgb(253, 221, 3);
                case Obj.Door: return Rgb(168, 120, 70);
                case Obj.Lever: return Rgb(190, 190, 190);
                case Obj.Lift: return Rgb(150, 150, 175);
                case Obj.Chest: return Rgb(233, 207, 94);
                case Obj.Stag: return Rgb(200, 160, 255);
                case Obj.Toll: return Rgb(210, 210, 120);
                case Obj.Grub: return Rgb(120, 225, 120);
                case Obj.GeoRock: return Rgb(90, 150, 215);
                case Obj.Totem: return Rgb(235, 235, 255);
                case Obj.Item: return Rgb(255, 255, 255);
                case Obj.BreakableWall: return Rgb(160, 115, 90);
                case Obj.Spring: return Rgb(120, 200, 235);
                case Obj.Npc: return Rgb(255, 150, 205);
                case Obj.Pylon: return Rgb(100, 235, 205);
                case Obj.TerraChest: return Rgb(174, 129, 92);
                // MapHelper's colour for tile 12.
                case Obj.LifeCrystal: return Rgb(174, 24, 69);
                default: return Color.magenta;
            }
        }

        static bool IsSolid(Tile t) => t == Tile.Terrain || t == Tile.Structure || t == Tile.Dirt || t == Tile.Stone || t == Tile.Wood || t == Tile.Sand || t == Tile.Ice;

        /// <summary>
        /// Two pictures a room: its solid cells, then its open ones (air, water and hazards, the
        /// things). All rooms' solids are drawn before any room's open cells, so where Hollow
        /// Knight's rooms overlap (they don't fit together exactly) the open spaces of both show.
        /// </summary>
        static void Build(Room room)
        {
            if (room.Solid == null)
            {
                room.Solid = new Texture2D(room.W, room.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Map " + room.Name };
                room.Open = new Texture2D(room.W, room.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Map open " + room.Name };
            }
            var solid = new Color32[room.W * room.H];
            var open = new Color32[room.W * room.H];
            var clear = new Color32(0, 0, 0, 0);
            var colors = new Color32[16];
            for (int i = 0; i < colors.Length; i++) colors[i] = TileColor(room, (Tile)i);
            var wets = new Color32[8];
            for (int i = 0; i < wets.Length; i++) wets[i] = WetColor((Wet)i);
            for (int y = 0; y < room.H; y++)
                for (int x = 0; x < room.W; x++)
                {
                    int i = y * room.W + x;
                    solid[i] = clear;
                    open[i] = clear;
                    if (!room.Shows(x, y)) continue;
                    var t = (Tile)room.Tiles[i];
                    if (IsSolid(t)) solid[i] = ExplorationColor(room, x, y, colors[(int)t]);
                    else if (room.Wets[i] != 0) open[i] = ExplorationColor(room, x, y, wets[room.Wets[i]]);
                    else open[i] = ExplorationColor(room, x, y, colors[(int)t]);
                }
            foreach (var o in room.Things)
            {
                Color32 c = ObjColor(o.Kind);
                for (int y = Mathf.Max(0, o.Cells.yMin); y < Mathf.Min(room.H, o.Cells.yMax); y++)
                    for (int x = Mathf.Max(0, o.Cells.xMin); x < Mathf.Min(room.W, o.Cells.xMax); x++)
                        if (room.Shows(x, y)) open[y * room.W + x] = ExplorationColor(room, x, y, c);
            }
            // The Life Crystals lying here (two cells on two, their bottom middle at the spot), once their cells are seen
            // (a bought map doesn't show them: they are found by exploring).
            Color32 crystal = ObjColor(Obj.LifeCrystal);
            foreach (var (scene, local) in LifeCrystals.Standing())
            {
                if (scene != room.Name) continue;
                int x0 = Mathf.FloorToInt(local.x / C + 0.5f) - 1, y0 = Mathf.FloorToInt(local.y / C + 0.5f);
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(room.H, y0 + 2); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(room.W, x0 + 2); x++)
                        if (room.IsSeen(x, y)) open[y * room.W + x] = crystal;
            }
            room.Solid.SetPixels32(solid);
            room.Solid.Apply(false);
            room.Open.SetPixels32(open);
            room.Open.Apply(false);
            room.Dirty = false;
            room.LastBuilt = Time.unscaledTime;
        }

        static Color32 ExplorationColor(Room room, int x, int y, Color32 color)
        {
            color.a = (byte)((color.a * MapExploration.Alpha(room.IsSeen(x, y), room.Charted) + 127) / 255);
            return color;
        }

        // ---------------------------------------------------------------- open, close, input

        public static void Toggle()
        {
            if (Open) Close();
            else TryOpen();
        }

        public static void TryOpen()
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null) return;
            Open = true;
            dragging = false;
            // Terraria opens the map centred on the player, on the player's layer.
            pan = HeadCell(out _);
            layerChoice = MapLayer.Current;
            hero.RelinquishControl();
            TerraMod.Instance?.PlaySound("Menu_Open", 0.7f);
        }

        public static void Close()
        {
            if (!Open) return;
            Open = false;
            dragging = false;
            var hero = HeroController.SilentInstance;
            if (hero != null) hero.RegainControl();
            TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>Input while open; call every frame.</summary>
        public static void Update()
        {
            var kb = Net.NetChat.BlocksKeys ? null : Keyboard.current;
            // Tests open and close the map themselves: keys pressed in their window don't count.
            if (kb != null && Player.TerraKeys.Pressed(Player.TerraKeys.Action.FullMap) && TerraMod.Instance?.AutoTest == null) Toggle();
            if (!Open) return;
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.IsInSceneTransition || hero.cState.dead || gm.isPaused || kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 m = TerraCursor.ScreenMouse(mouse);
            var gui = new Vector2(m.x, Screen.height - m.y);
            // The map keys zoom about the middle, 1.05 a tick (Player.cs's full map).
            float keyZoom = (Player.TerraKeys.Held(Player.TerraKeys.Action.MapZoomIn) ? 1f : 0f) - (Player.TerraKeys.Held(Player.TerraKeys.Action.MapZoomOut) ? 1f : 0f);
            if (keyZoom != 0f) zoom = Mathf.Clamp(zoom * Mathf.Pow(keyZoom > 0f ? 1.05f : 0.95f, Time.unscaledDeltaTime * 60f), 0.3f, 32f);
            // The wheel zooms about the cursor: the cell under it stays put.
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.1f)
            {
                var before = ToCell(gui);
                zoom = Mathf.Clamp(zoom * (wheel > 0f ? 1.25f : 0.8f), 0.3f, 32f);
                var after = ToCell(gui);
                pan += before - after;
            }
            bool pressed = TerraCursor.LeftPressed(mouse);
            var pylon = pressed ? PylonAt(gui) : null;
            if (pylon != null)
            {
                Close();
                Pylons.Teleport(pylon);
                return;
            }
            if (pressed && LayerTabAt(gui, out var layer))
            {
                layerChoice = layer;
                TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
                return;
            }
            if (pressed && FullMap.CloseButton().Contains(gui))
            {
                Close();
                return;
            }
            // A right click flips the layer drawn: the shallow world and the deep rooms.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                FlipLayer();
                return;
            }
            if (pressed) dragging = true;
            if (!TerraCursor.LeftHeld(mouse)) dragging = false;
            if (dragging) pan -= new Vector2(m.x - lastMouse.x, m.y - lastMouse.y) / zoom;
            lastMouse = m;
            // Player.cs: the movement keys pan the fullscreen map, 16 screen pixels a tick.
            if (kb != null)
            {
                float step = 16f * Time.unscaledDeltaTime * 60f / zoom;
                var dir = Vector2.zero;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Up)) dir.y += 1f;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Down)) dir.y -= 1f;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Left)) dir.x -= 1f;
                if (Player.TerraKeys.Held(Player.TerraKeys.Action.Right)) dir.x += 1f;
                pan += dir * step;
            }
        }

        static Pylons.Pylon PylonAt(Vector2 gui)
        {
            foreach (var p in Pylons.Unlocked)
            {
                if (!Shown(p.Scene) || !ScenePoint(p.Scene, p.Local + new Vector2(0.75f, 1f), out var at)) continue;
                if (FullMap.PylonIconRect(ToScreen(at)).Contains(gui)) return p;
            }
            return null;
        }

        // ---------------------------------------------------------------- drawing

        static GUIStyle style;

        public static void Draw()
        {
            if (!Open || Event.current.type != EventType.Repaint) return;
            float ui = GameView.Scale;
            var prev = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            var visible = new List<(Room room, Rect rect)>();
            foreach (var room in rooms.Values)
            {
                if (!room.Captured || !room.AnyShown || !Shown(room)) continue;
                var a = ToScreen(new Vector2(room.At.x, room.At.y + room.H));
                var b = ToScreen(new Vector2(room.At.x + room.W, room.At.y));
                var rect = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
                if (rect.xMax < 0 || rect.yMax < 0 || rect.xMin > Screen.width || rect.yMin > Screen.height) continue;
                if (room.Dirty || room.Solid == null) Build(room);
                visible.Add((room, rect));
            }
            LiveOnTop(visible);
            foreach (var (room, rect) in visible) GUI.DrawTexture(rect, room.Solid);
            foreach (var (room, rect) in visible) GUI.DrawTexture(rect, room.Open);

            // The player's own chests and the pylons found, as blocks and icons (those of the layer drawn).
            foreach (var c in Chests.Save())
                if (Shown(c.Scene) && ScenePoint(c.Scene, c.Local, out var at) && Seen(at)) Block(at, 2, 2, ObjColor(Obj.TerraChest));
            var mp = devMouse ?? TerraCursor.GuiMouse();
            Pylons.Pylon hoveredPylon = null;
            var icons = TerrariaAssets.Texture("Extra_182");
            foreach (var p in Pylons.Unlocked)
            {
                if (!Shown(p.Scene) || !ScenePoint(p.Scene, p.Local + new Vector2(0.75f, 1f), out var at)) continue;
                var r = FullMap.PylonIconRect(ToScreen(at));
                if (r.Contains(mp))
                {
                    hoveredPylon = p;
                    r = new Rect(r.center.x - r.width * 0.65f, r.center.y - r.height * 0.65f, r.width * 1.3f, r.height * 1.3f);
                }
                GUI.DrawTextureWithTexCoords(r, icons, new Rect(p.Style * 30f / icons.width, 0f, 30f / icons.width, 1f));
            }
            // The shade, with Terraria's death marker.
            var pd = PlayerData.instance;
            if (pd != null && !string.IsNullOrEmpty(pd.shadeScene) && pd.shadeScene != "None" && Shown(pd.shadeScene) && ScenePoint(pd.shadeScene, new Vector2(pd.shadePositionX, pd.shadePositionY), out var shade))
            {
                var tex = TerrariaAssets.Texture("MapDeath");
                var s = ToScreen(shade);
                GUI.DrawTexture(new Rect(s.x - tex.width * ui / 2f, s.y - tex.height * ui / 2f, tex.width * ui, tex.height * ui), tex);
            }
            // The player on their own layer.
            if (PlayerDeep == ViewDeep) FullMap.DrawHead(ToScreen(HeadCell(out _)), ui);
            // The other players of a LAN game where they are, their names under them.
            DrawOthers(ToScreen, ui, true, null);

            // What is under the cursor, as Terraria names it.
            string label = hoveredPylon != null ? Lang.T(hoveredPylon.Name) + "（" + Areas.Display(Areas.AreaName(hoveredPylon.Scene)) + "）　" + Lang.T("点击传送") : Show(NameAt(ToCell(mp)));
            if (!string.IsNullOrEmpty(label))
                FullMap.Outlined(new Rect(mp.x + 26f * ui, mp.y - 10f * ui, 600f * ui, 24f * ui), label, hoveredPylon != null ? new Color(1f, 0.9f, 0.5f) : Color.white);
            var close = FullMap.CloseButton();
            GUI.DrawTexture(close, TerrariaAssets.Texture(close.Contains(mp) ? "Map_4" : "Map_0"));
            FullMap.Outlined(new Rect(close.xMax + 10f * ui, close.y + 4f * ui, 700f * ui, 24f * ui), Lang.T("滚轮缩放，拖动平移，鼠标指着看是什么，M 关闭"), new Color(1f, 1f, 1f, 0.7f));
            DrawLayerTabs(mp);
        }

        // ---------------------------------------------------------------- layers

        /// <summary>
        /// The map's two layers: the world the seamless gates join (shallow) and the rooms only a door, a dream or a
        /// tram leads into (deep, WorldMapLayers), which sit beside their door, over the world outside it. One layer is
        /// drawn at a time: the corner maps the player's own, the full map the one picked on its tabs (the player's own at first).
        /// </summary>
        enum MapLayer : byte { Current, Shallow, Deep }

        static MapLayer layerChoice;
        static string lastLaidOut;

        static bool IsDeep(string scene) => scene != null && deepRooms.Contains(scene);

        /// <summary>The player's layer: the scene being played's (the last one with a place on the map, for one without).</summary>
        static bool PlayerDeep
        {
            get
            {
                string scene = TerraWorld.Live?.SceneName;
                if (scene != null && layout.ContainsKey(scene)) lastLaidOut = scene;
                return IsDeep(lastLaidOut);
            }
        }

        /// <summary>The layer drawn: the one picked on the full map, else the player's.</summary>
        static bool ViewDeep => Open && layerChoice != MapLayer.Current ? layerChoice == MapLayer.Deep : PlayerDeep;

        static bool Shown(Room room) => IsDeep(room.Name) == ViewDeep;

        static bool Shown(string scene) => IsDeep(scene) == ViewDeep;

        /// <summary>The room being played drawn last, over a room of its layer beside the same door (the Colosseum's trials).</summary>
        static void LiveOnTop(List<(Room room, Rect rect)> visible)
        {
            string live = TerraWorld.Live?.SceneName;
            int i = visible.FindIndex(v => v.room.Name == live);
            if (i < 0 || i == visible.Count - 1) return;
            var top = visible[i];
            visible.RemoveAt(i);
            visible.Add(top);
        }

        static readonly Rect[] layerTabs = new Rect[3];
        static GUIStyle tabStyle;

        /// <summary>The full map's layer tabs over its close button (as last drawn): the player's layer, the shallow, the deep.</summary>
        static bool LayerTabAt(Vector2 gui, out MapLayer layer)
        {
            layer = MapLayer.Current;
            for (int i = 0; i < layerTabs.Length; i++)
                if (layerTabs[i].Contains(gui)) { layer = (MapLayer)i; return true; }
            return false;
        }

        static void DrawLayerTabs(Vector2 mp)
        {
            float ui = GameView.Scale;
            var close = FullMap.CloseButton();
            if (tabStyle == null) tabStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            tabStyle.fontSize = Mathf.RoundToInt(18 * ui);
            float x = close.x;
            for (int i = 0; i < layerTabs.Length; i++)
            {
                string text = i == 0 ? Lang.F("当前层：{0}", Lang.T(PlayerDeep ? "深层" : "浅层")) : Lang.T(i == 1 ? "浅层" : "深层");
                var r = new Rect(x, close.y - 36f * ui, tabStyle.CalcSize(new GUIContent(text)).x + 18f * ui, 28f * ui);
                layerTabs[i] = r;
                x = r.xMax + 6f * ui;
                bool picked = (int)layerChoice == i, hover = r.Contains(mp);
                // Terraria's panel blue, brighter for the tab picked and the one under the mouse.
                var prev = GUI.color;
                GUI.color = picked ? new Color(73 / 255f, 94 / 255f, 171 / 255f, 0.95f) : new Color(63 / 255f, 82 / 255f, 151 / 255f, hover ? 0.85f : 0.6f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = prev;
                FullMap.Outlined(new Rect(r.x + 9f * ui, r.y + 2f * ui, r.width, r.height), text, picked ? new Color(1f, 0.9f, 0.5f) : hover ? Color.white : new Color(1f, 1f, 1f, 0.75f));
            }
            // The right click's hint after the tabs.
            string hint = Lang.T("右键切换");
            FullMap.Outlined(new Rect(x + 6f * ui, close.y - 34f * ui, tabStyle.CalcSize(new GUIContent(hint)).x + 12f * ui, 28f * ui), hint, new Color(1f, 1f, 1f, 0.85f));
        }

        static GUIStyle otherStyle;

        /// <summary>The other players' heads (Main.DrawPlayerHeads on the map): their look, a team's colour round them, the name on the big map.</summary>
        static void DrawOthers(System.Func<Vector2, Vector2> cellToScreen, float scale, bool names, Rect? clip)
        {
            if (Net.NetSession.State == Net.NetSession.Phase.Off) return;
            foreach (var p in Net.NetSession.Players.Values)
            {
                if (!p.HasState || !p.Latest.Shown || !Shown(p.Latest.Scene) || !ScenePoint(p.Latest.Scene, p.Latest.BoxCenter, out var cell)) continue;
                var at = cellToScreen(cell);
                if (clip.HasValue && !clip.Value.Contains(at)) continue;
                var s = p.Latest;
                var look = Player.TerraPlayerSprite.Looks[Mathf.Clamp(p.Look, 0, Player.TerraPlayerSprite.Looks.Length - 1)];
                bool hair = s.Eq.Head == 0 || s.Eq.HairUnderHead;
                FullMap.DrawHead(at, scale, s.Eq.Head, hair, look.Skin, look.Hair, look.Eye, p.Team == 0 ? Color.black : (Color)p.TeamColor);
                if (!names) continue;
                if (otherStyle == null) otherStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
                otherStyle.fontSize = Mathf.RoundToInt(16 * scale);
                Net.RemotePlayer.Outlined(new Rect(at.x - 100f * scale, at.y + 22f * scale, 200f * scale, 22f * scale), p.Name, otherStyle, p.Team == 0 ? Color.white : (Color)p.TeamColor);
            }
        }

        static void Block(Vector2 cell, int w, int h, Color c)
        {
            var a = ToScreen(new Vector2(cell.x, cell.y + h));
            var b = ToScreen(new Vector2(cell.x + w, cell.y));
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(Rect.MinMaxRect(a.x, a.y, b.x, b.y), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static bool Seen(Vector2 cell)
        {
            foreach (var room in rooms.Values)
            {
                if (!Shown(room)) continue;
                int x = Mathf.FloorToInt(cell.x) - room.At.x, y = Mathf.FloorToInt(cell.y) - room.At.y;
                if (room.Captured && x >= 0 && y >= 0 && x < room.W && y < room.H && room.IsSeen(x, y)) return true;
            }
            return false;
        }

        /// <summary>A stored thing's name (kept in Chinese, as the map's save has it) shown in the language now.</summary>
        static string Show(string name)
        {
            if (string.IsNullOrEmpty(name) || Lang.IsSource) return name;
            if (name.StartsWith("门：")) return Lang.F("门：{0}", Lang.T(name.Substring(2)));
            return Lang.T(name);
        }

        static string TileName(Tile t, Wet w, string area)
        {
            area = Areas.Display(area);
            switch (w)
            {
                case Wet.Water: return "水";
                case Wet.Spikes: return "尖刺";
                case Wet.Acid: return "酸水";
                case Wet.Lava: return "熔岩";
            }
            switch (t)
            {
                case Tile.Terrain: return Lang.F("地面（{0}）", area);
                case Tile.Structure: return Lang.F("建筑（{0}）", area);
                case Tile.Platform: return "平台";
                case Tile.Dirt: return "土块";
                case Tile.Stone: return "石块";
                case Tile.Wood: return "木材";
                case Tile.Sand: return "沙块";
                case Tile.Ice: return "冰块";
                case Tile.Torch: return "火把";
                case Tile.Rope: return "绳";
                case Tile.Meteorite: return "陨石";
                case Tile.Dug: return Lang.F("挖开的地方（{0}）", area);
                default: return null;
            }
        }

        /// <summary>The name of what is at a cell of the map: a thing first, then the block (an open cell of any room over a solid one).</summary>
        static string NameAt(Vector2 cell)
        {
            int gx = Mathf.FloorToInt(cell.x), gy = Mathf.FloorToInt(cell.y);
            string solidName = null;
            foreach (var room in rooms.Values)
            {
                if (!room.Captured || !room.AnyShown || !Shown(room)) continue;
                int x = gx - room.At.x, y = gy - room.At.y;
                if (x < 0 || y < 0 || x >= room.W || y >= room.H || !room.Shows(x, y)) continue;
                foreach (var o in room.Things)
                    if (o.Cells.Contains(new Vector2Int(x, y))) return o.Name;
                int i = y * room.W + x;
                var t = (Tile)room.Tiles[i];
                string n = TileName(t, (Wet)room.Wets[i], room.Area);
                if (IsSolid(t)) solidName = solidName ?? n;
                else if (n != null) return n;
            }
            foreach (var c in Chests.Save())
                if (Shown(c.Scene) && ScenePoint(c.Scene, c.Local, out var at) && gx >= Mathf.FloorToInt(at.x) && gx < Mathf.FloorToInt(at.x) + 2 && gy >= Mathf.FloorToInt(at.y) && gy < Mathf.FloorToInt(at.y) + 2)
                    return "箱子";
            foreach (var (scene, local) in LifeCrystals.Standing())
                if (Shown(scene) && ScenePoint(scene, local, out var at) && gx >= Mathf.FloorToInt(at.x) - 1 && gx < Mathf.FloorToInt(at.x) + 1 && gy >= Mathf.FloorToInt(at.y) && gy < Mathf.FloorToInt(at.y) + 2 && Seen(at + new Vector2(0f, 0.5f)))
                    return "生命水晶";
            return solidName;
        }

        // ---------------------------------------------------------------- the save

        static string PathFor(int slot) => Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.map");

        public static void Delete(int slot)
        {
            try
            {
                string path = PathFor(slot);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[地图] 删除地图存档失败：{e.Message}"); }
        }

        static void WriteRle(BinaryWriter w, byte[] data)
        {
            w.Write(data.Length);
            int i = 0;
            while (i < data.Length)
            {
                byte v = data[i];
                int run = 1;
                while (i + run < data.Length && data[i + run] == v && run < 65535) run++;
                w.Write((ushort)run);
                w.Write(v);
                i += run;
            }
        }

        static byte[] ReadRle(BinaryReader r)
        {
            var data = new byte[r.ReadInt32()];
            int i = 0;
            while (i < data.Length)
            {
                int run = r.ReadUInt16();
                byte v = r.ReadByte();
                for (int k = 0; k < run && i < data.Length; k++) data[i++] = v;
            }
            return data;
        }

        public static void Save(int slot)
        {
            if (slot < 0 || !Enabled) return;
            try
            {
                string path = PathFor(slot);
                using (var fs = File.Create(path + ".new"))
                using (var gz = new GZipStream(fs, CompressionMode.Compress))
                using (var w = new BinaryWriter(gz))
                {
                    w.Write("TKMAP");
                    w.Write(MapVersion);
                    var kept = new List<Room>();
                    foreach (var room in rooms.Values) if (!room.FromAtlas) kept.Add(room);
                    w.Write(kept.Count);
                    foreach (var room in kept)
                    {
                        w.Write(room.Name);
                        w.Write(room.W);
                        w.Write(room.H);
                        w.Write(room.Captured);
                        w.Write(room.RevealAll);
                        if (!room.Captured) continue;
                        WriteRle(w, room.Tiles);
                        WriteRle(w, room.Wets);
                        WriteRle(w, room.Seen);
                        w.Write(room.Things.Count);
                        foreach (var o in room.Things)
                        {
                            w.Write((byte)o.Kind);
                            w.Write(o.Name ?? "");
                            w.Write(o.Cells.x); w.Write(o.Cells.y); w.Write(o.Cells.width); w.Write(o.Cells.height);
                        }
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".new", path);
            }
            catch (Exception e) { Plugin.Log.LogError($"[地图] 地图存档写入失败：{e}"); }
        }

        /// <summary>Nothing known: a new game (the last save's map mustn't stay).</summary>
        public static void Reset()
        {
            foreach (var room in rooms.Values)
            {
                if (room.Solid != null) UnityEngine.Object.Destroy(room.Solid);
                if (room.Open != null) UnityEngine.Object.Destroy(room.Open);
            }
            rooms.Clear();
            watched.Clear();
            toCapture.Clear();
            // The bought maps' rooms (from the atlas, not kept) are charted again for the save now loaded.
            chartedKey = "";
            nextReveal = nextCapture = nextChart = 0f;
            haveHead = false;
            Open = false;
        }

        public static void Load(int slot)
        {
            Reset();
            string path = PathFor(slot);
            if (slot >= 0 && File.Exists(path))
            {
                try
                {
                    int version;
                    using (var fs = File.OpenRead(path))
                    using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (var r = new BinaryReader(gz))
                    {
                        if (r.ReadString() != "TKMAP") throw new InvalidDataException("not a map file");
                        version = r.ReadInt32();
                        if (version < 1 || version > MapVersion) throw new InvalidDataException("unsupported map version " + version);
                        int count = r.ReadInt32();
                        for (int k = 0; k < count; k++)
                        {
                            string name = r.ReadString();
                            int w = r.ReadInt32(), h = r.ReadInt32();
                            bool captured = r.ReadBoolean(), revealAll = r.ReadBoolean();
                            var room = new Room { Name = name, W = w, H = h, Captured = captured, RevealAll = revealAll };
                            room.Area = Areas.AreaName(name);
                            room.Hue = Player.OpeningPortal.AreaColor(room.Area);
                            if (captured)
                            {
                                room.Tiles = ReadRle(r);
                                room.Wets = ReadRle(r);
                                room.Seen = ReadRle(r);
                                int things = r.ReadInt32();
                                for (int i = 0; i < things; i++)
                                {
                                    var kind = (Obj)r.ReadByte();
                                    string tn = r.ReadString();
                                    int x = r.ReadInt32(), y = r.ReadInt32(), tw = r.ReadInt32(), th = r.ReadInt32();
                                    room.Things.Add(new Thing { Kind = kind, Name = tn, Cells = new RectInt(x, y, tw, th) });
                                }
                                foreach (var b in room.Seen) if (b != 0) { room.AnySeen = true; break; }
                            }
                            if (!layout.TryGetValue(name, out room.At)) continue;
                            rooms[name] = room;
                        }
                    }
                    if (version < MapVersion)
                    {
                        // Old Seen bits mixed real exploration with camera-wide and legacy room reveals.
                        // Preserve the original map before clearing only those bits. Purchased maps
                        // remain in PlayerData and ChartBought reconstructs their dimmed outlines.
                        string backup = path + ".before-exploration-v2.bak";
                        if (File.Exists(backup)) backup += "." + DateTime.UtcNow.Ticks;
                        try { File.Copy(path, backup); }
                        catch (Exception e)
                        {
                            Plugin.Log.LogError($"[地图] 旧探索记录备份失败，保留原记录：{e.Message}");
                            return;
                        }
                        foreach (var room in rooms.Values)
                        {
                            if (room.Seen != null) Array.Clear(room.Seen, 0, room.Seen.Length);
                            room.AnySeen = room.RevealAll = false;
                            room.Dirty = true;
                        }
                        Save(slot);
                        Plugin.Log.LogInfo($"[地图] 已备份旧地图到 {backup}，重置探索；已购买地图保留");
                    }
                    Plugin.Log.LogInfo($"[地图] 读入地图存档：{rooms.Count} 个场景");
                    return;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"[地图] 地图存档读不了，重新开始记：{e.Message}");
                    rooms.Clear();
                }
            }
            // A visited scene is not a walked cell. With no cell map, begin exploring locally;
            // purchased charts are reconstructed separately without setting Seen bits.
        }

        // ---------------------------------------------------------------- dev

        /// <summary>Dev: what the map would name under a world point (current frame's coordinates).</summary>
        public static string DevNameAtWorld(Vector2 world)
        {
            var live = TerraWorld.Live;
            if (live == null) return "无";
            foreach (var ts in TerrainSystem.All)
            {
                var r = ts.Region;
                if (r == null || r.SceneName == null || !layout.TryGetValue(r.SceneName, out var at)) continue;
                int x = Mathf.FloorToInt(world.x / C) - r.CellOffset.x, y = Mathf.FloorToInt(world.y / C) - r.CellOffset.y;
                if (x < 0 || y < 0 || x >= r.Grid.Width || y >= r.Grid.Height) continue;
                return NameAt(new Vector2(at.x + x + 0.5f, at.y + y + 0.5f)) ?? "（空）";
            }
            return "不在地图上";
        }

        /// <summary>Dev: every scene placed on the world map.</summary>
        public static List<string> DevSceneNames() => new List<string>(layout.Keys);

        /// <summary>A room's picture made again (a Life Crystal placed or taken in it).</summary>
        public static void Refresh(string scene)
        {
            if (scene != null && rooms.TryGetValue(scene, out var room)) room.Dirty = true;
        }

        public static string DevReport()
        {
            int captured = 0, seenRooms = 0, things = 0;
            long seenCells = 0;
            foreach (var room in rooms.Values)
            {
                if (room.Captured) captured++;
                if (room.AnySeen) seenRooms++;
                things += room.Things.Count;
                if (room.Seen != null) foreach (var b in room.Seen) seenCells += CountBits(b);
            }
            var head = HeadCell(out bool found);
            return $"场景 {rooms.Count} 个（已采集 {captured}，看到过 {seenRooms}），物件 {things} 个，揭开格子 {seenCells}，头像 {head:F1}（{(found ? "定位到" : "沿用上次")}），缩放 {zoom:F2}";
        }

        static int CountBits(byte b) { int n = 0; while (b != 0) { n += b & 1; b >>= 1; } return n; }

        public static string DevThings(string scene)
        {
            if (!rooms.TryGetValue(scene, out var room)) return "没有";
            var counts = new Dictionary<string, int>();
            foreach (var o in room.Things) counts[o.Name] = counts.TryGetValue(o.Name, out int k) ? k + 1 : 1;
            var sb = new System.Text.StringBuilder();
            foreach (var kv in counts) sb.Append($" {kv.Key}×{kv.Value}");
            return sb.ToString();
        }

        static Vector2? devMouse;

        /// <summary>Dev: the hover label as if the cursor were over a world point (null: the real cursor). The real cursor isn't moved.</summary>
        public static void DevPointAt(Vector2? world)
        {
            devMouse = null;
            TerraCursor.DevAt = null;
            var live = TerraWorld.Live;
            if (world == null || live == null || !layout.TryGetValue(live.SceneName, out var at)) return;
            var cell = new Vector2(at.x + world.Value.x / C - live.CellOffset.x, at.y + world.Value.y / C - live.CellOffset.y);
            devMouse = ToScreen(cell);
            TerraCursor.DevAt = devMouse;
        }

        /// <summary>Dev: where a pylon's icon is on the open map (GUI point), if it's drawn.</summary>
        public static Vector2? DevPylonScreen(Pylons.Pylon p)
        {
            if (!Open || !ScenePoint(p.Scene, p.Local + new Vector2(0.75f, 1f), out var at)) return null;
            return FullMap.PylonIconRect(ToScreen(at)).center;
        }

        /// <summary>Dev: centre the open map on a pylon.</summary>
        public static void DevCentrePylon(Pylons.Pylon p)
        {
            if (ScenePoint(p.Scene, p.Local + new Vector2(0.75f, 1f), out var at)) pan = at;
        }

        /// <summary>Dev: centre the open map on a world point.</summary>
        public static void DevCentre(Vector2 world)
        {
            var live = TerraWorld.Live;
            if (live == null || !layout.TryGetValue(live.SceneName, out var at)) return;
            pan = new Vector2(at.x + world.x / C - live.CellOffset.x, at.y + world.y / C - live.CellOffset.y);
        }

        /// <summary>Dev: write the map save and read it back; what is kept, before and after.</summary>
        public static string DevRoundTrip(int slot)
        {
            string before = DevReport();
            Save(slot);
            Load(slot);
            return $"存前：{before}\n    读回：{DevReport()}";
        }

        public static void DevZoom(float z) => zoom = z;

        public static bool DevDoorRoom(string scene) => doorRooms.Contains(scene);

        /// <summary>Whether the map draws a room now (a room behind a door only while the player is in it).</summary>
        public static bool DevShown(string scene) => rooms.TryGetValue(scene, out var room) && Shown(room);

        /// <summary>Dev: a scene in the deep layer; the layer drawn now; pick the full map's layer (0 the player's, 1 shallow, 2 deep).</summary>
        public static bool DevDeep(string scene) => IsDeep(scene);
        public static bool DevViewDeep => ViewDeep;
        public static void DevPickLayer(int layer) => layerChoice = (MapLayer)layer;

        /// <summary>The full map's right click: the other layer drawn (shallow from deep, deep from shallow).</summary>
        public static void FlipLayer()
        {
            layerChoice = ViewDeep ? MapLayer.Shallow : MapLayer.Deep;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }
        public static Rect DevLayerTab(int i) => layerTabs[i];

        /// <summary>Dev: the player's place on the map, in global cells.</summary>
        public static Vector2 DevHeadCell => HeadCell(out _);

        public static void DevSetOpen(bool open)
        {
            if (open && !Open) TryOpen();
            else if (!open && Open) Close();
        }
    }
}
