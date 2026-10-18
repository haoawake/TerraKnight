using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Hallownest's regional torches (空洞骑士地区火把素材包): 48 torches, one for each of its areas and the parts of them
    /// with a look of their own, Terraria's torch (Tiles_4's outline and layout, Flame_0's flame layer, Item_8's icon) in
    /// each area's colours. A placed one is a torch cell (CellKind.Torch) whose style, 1..48 in this table's order, the
    /// grid keeps beside its cells (CellGrid.TorchStyleAt; none, 0, is the plain Terraria torch); the style is kept here
    /// by scene for the save (Save.ModSave) and passed on in a LAN game (Net.TileSync, Net.WorldSync). It is drawn from
    /// its row of <see cref="TileSheet"/> (22 px a row, Tiles_4's six columns), its flame layer from the same place in
    /// <see cref="FlameSheet"/> drawn over it Terraria's way (<see cref="RegionalTorchFlames"/>), and it lights in its own
    /// colour; mined, it gives its own item back (AreaBlocks.DropFor). Breaking a scene's breakables drops the torch of
    /// the area the scene is in (Items.BreakableDrops, World.RegionalTorchAreas).
    /// </summary>
    internal static class RegionalTorches
    {
        /// <summary>One torch: its key (the item pictures' names), its area's name and its own (the pack's list), a scene of its area, and its light (the pack's suggested RGB).</summary>
        public readonly struct Style
        {
            public readonly string Key, Area, Name, Scene;
            public readonly Color Light;

            public Style(string key, string area, string name, string scene, float r, float g, float b)
            {
                Key = key; Area = area; Name = name; Scene = scene; Light = new Color(r, g, b);
            }
        }

        /// <summary>The torches in the pack's order (火把名称清单.csv): style n is All[n - 1], and ItemType.TorchKingsPass + (n - 1).</summary>
        public static readonly Style[] All =
        {
            new Style("KingsPass", "国王山道", "风蚀石火把", "Tutorial_01", 0.2549f, 0.6157f, 1f),
            new Style("Dirtmouth", "德特茅斯", "暖灯火把", "Town", 1f, 0.7176f, 0.2314f),
            new Style("Crossroads", "遗忘十字路", "旧巢火把", "Crossroads_01", 0.3922f, 0.851f, 0.9451f),
            new Style("InfectedCrossroads", "感染十字路", "感染火把", "Crossroads_01", 1f, 0.4667f, 0.1098f),
            new Style("Greenpath", "苍绿之径", "苔芽火把", "Fungus1_01", 0.4118f, 0.8549f, 0.1922f),
            new Style("LakeOfUnn", "乌恩之湖", "乌恩火把", "Fungus1_26", 0.7059f, 0.9137f, 0.4235f),
            new Style("FogCanyon", "雾之峡谷", "雾泡火把", "Fungus3_01", 0.7412f, 0.4549f, 0.9294f),
            new Style("Archives", "教师档案馆", "档案火把", "Fungus3_archive", 0.9294f, 0.6078f, 0.7922f),
            new Style("QueensStation", "王后驿站", "驿站火把", "Fungus2_01", 1f, 0.8157f, 0.4039f),
            new Style("FungalWastes", "真菌荒地", "菌孢火把", "Fungus2_03", 0.9137f, 0.7216f, 0.3059f),
            new Style("MantisVillage", "螳螂村", "螳刃火把", "Fungus2_14", 0.7255f, 0.4588f, 0.9098f),
            new Style("CityOfTears", "泪水之城", "泪雨火把", "Ruins1_01", 0.2627f, 0.6235f, 0.902f),
            new Style("SoulSanctum", "灵魂圣所", "灵魂火把", "Ruins1_23", 0.7137f, 0.9451f, 0.9608f),
            new Style("WatchersSpire", "守望者尖塔", "守望火把", "Ruins2_Watcher_Room", 0.3059f, 0.6706f, 0.9373f),
            new Style("PleasureHouse", "欢乐之屋", "绯帘火把", "Ruins_Bathhouse", 0.9098f, 0.4275f, 0.8f),
            new Style("TowerOfLove", "爱之塔", "琉璃火把", "Ruins2_11", 0.6275f, 0.3922f, 0.902f),
            new Style("RoyalWaterways", "皇家水道", "锈渠火把", "Waterways_01", 0.3451f, 0.8431f, 0.6118f),
            new Style("IsmasGrove", "伊思玛的树林", "酸蕨火把", "Waterways_13", 0.7137f, 0.9137f, 0.2902f),
            new Style("JunkPit", "垃圾坑", "废铁火把", "GG_Waterways", 0.9333f, 0.7059f, 0.3608f),
            new Style("CrystalPeak", "水晶山峰", "晶簇火把", "Mines_01", 0.9294f, 0.4353f, 0.8392f),
            new Style("HallownestsCrown", "圣巢之冠", "王冠火把", "Mines_34", 0.9373f, 0.6f, 0.9216f),
            new Style("RestingGrounds", "安息之地", "安息火把", "RestingGrounds_02", 0.549f, 0.6588f, 0.9333f),
            new Style("SpiritsGlade", "灵魂沼地", "幽魂火把", "RestingGrounds_08", 0.6863f, 0.9137f, 0.8667f),
            new Style("BlueLake", "蓝湖", "静湖火把", "Crossroads_50", 0.1961f, 0.5137f, 0.9255f),
            new Style("HowlingCliffs", "呼啸悬崖", "啸风火把", "Cliffs_01", 0.8588f, 0.9333f, 0.9725f),
            new Style("JonisRepose", "乔尼的安息处", "蓝蝶火把", "Cliffs_05", 0.1882f, 0.5569f, 0.9725f),
            new Style("Deepnest", "深邃巢穴", "蛛丝火把", "Deepnest_01b", 0.6667f, 0.5333f, 0.8902f),
            new Style("BeastsDen", "野兽巢穴", "兽巢火把", "Deepnest_Spider_Town", 0.8627f, 0.7725f, 0.5608f),
            new Style("WeaversDen", "编织者巢穴", "织梦火把", "Deepnest_45_v02", 0.7843f, 0.6078f, 0.9137f),
            new Style("DistantVillage", "遥远的村庄", "空茧火把", "Deepnest_10", 0.6824f, 0.5843f, 0.8353f),
            new Style("FailedTramway", "废弃电车道", "残轨火把", "Deepnest_26", 0.9059f, 0.6824f, 0.3647f),
            new Style("KingdomsEdge", "王国边缘", "白灰火把", "Deepnest_East_01", 0.8824f, 0.9098f, 0.9373f),
            new Style("CastOffShell", "遗弃外壳", "古蜕火把", "Room_Wyrm", 0.9176f, 0.7843f, 0.6f),
            new Style("Colosseum", "愚人斗兽场", "斗兽火把", "Room_Colosseum_01", 0.9843f, 0.4706f, 0.2078f),
            new Style("Hive", "蜂巢", "蜂蜡火把", "Hive_01", 1f, 0.7412f, 0.1843f),
            new Style("QueensGardens", "王后花园", "荆花火把", "Fungus3_04", 0.6f, 0.9373f, 0.7961f),
            new Style("StoneSanctuary", "石之庇护所", "石祠火把", "Fungus1_35", 0.8f, 0.8588f, 0.9098f),
            new Style("OvergrownMound", "茂生的山丘", "蔓生火把", "Room_Fungus_Shaman", 0.5176f, 0.8471f, 0.2863f),
            new Style("AncestralMound", "祖先山丘", "先祖火把", "Crossroads_ShamanTemple", 0.902f, 0.8392f, 0.6784f),
            new Style("AncientBasin", "古老盆地", "沉岩火把", "Abyss_04", 0.5451f, 0.6078f, 0.7373f),
            new Style("WhitePalace", "白色宫殿", "白宫火把", "White_Palace_01", 1f, 0.9412f, 0.7765f),
            new Style("PathOfPain", "苦痛之路", "锯棘火把", "White_Palace_18", 0.9412f, 0.698f, 0.7569f),
            new Style("Abyss", "深渊", "虚空火把", "Abyss_09", 0.2902f, 0.5216f, 0.5961f),
            new Style("Birthplace", "出生地", "初生火把", "Abyss_15", 0.7804f, 0.8745f, 0.8902f),
            new Style("BlackEggTemple", "黑卵神殿", "封印火把", "Room_temple", 0.9412f, 0.7333f, 0.451f),
            new Style("Dream", "梦境", "梦光火把", "Dream_Nailcollection", 0.898f, 0.6392f, 0.8431f),
            new Style("Godhome", "神居", "神辉火把", "GG_Atrium", 1f, 0.8549f, 0.3922f),
            new Style("GrimmTroupe", "格林剧团", "梦魇火把", "Grimm_Main_Tent", 0.9373f, 0.251f, 0.3333f),
        };

        public static int Count => All.Length;

        /// <summary>The placed torches' sheet (Resources/hk_HkTorchTiles.png) and their flame layers' (hk_HkTorchFlames.png): a row a style.</summary>
        public const string TileSheet = "HkTorchTiles", FlameSheet = "HkTorchFlames";

        /// <summary>A style's item picture (Resources/hk_HkTorchItem_&lt;key&gt;.png, 14×16 as Item_8).</summary>
        public static string ItemTexture(int style) => "HkTorchItem_" + All[style - 1].Key;

        public static bool Valid(int style) => style >= 1 && style <= All.Length;

        /// <summary>A style's item: its regional torch, the plain Torch for none.</summary>
        public static ItemType ItemOf(int style) => Valid(style) ? ItemType.TorchKingsPass + (style - 1) : ItemType.Torch;

        /// <summary>The style an item places: a regional torch's own, none (0) for anything else.</summary>
        public static int StyleOf(ItemType t) => t >= ItemType.TorchKingsPass && t <= ItemType.TorchKingsPass + (All.Length - 1) ? t - ItemType.TorchKingsPass + 1 : 0;

        /// <summary>Its name as the mod writes it (the language's, from the item).</summary>
        public static string NameOf(int style) => Valid(style) ? ItemDb.Get(ItemOf(style)).Name : ItemDb.Get(ItemType.Torch).Name;

        /// <summary>The light a torch of the style gives (LightMap's colour): the pack's suggested colour, the plain torch's for none.</summary>
        public static Color LightRgb(int style) => Valid(style) ? All[style - 1].Light : LightMap.TorchRgb;

        /// <summary>The glow drawn round a placed one (Glow.CreateLight): its light at the plain torch's strength.</summary>
        public static Color GlowColor(int style)
        {
            if (!Valid(style)) return CellVisuals.TorchLight;
            var c = All[style - 1].Light;
            return new Color(c.r, c.g, c.b, CellVisuals.TorchLight.a);
        }

        // --- The styles kept by scene (the save's and the network's), as World.WorldWalls keeps the walls. ---

        static readonly Dictionary<string, Dictionary<int, byte>> scenes = new Dictionary<string, Dictionary<int, byte>>();

        public static IEnumerable<KeyValuePair<string, Dictionary<int, byte>>> AllSaved => scenes;

        public static Dictionary<int, byte> For(string scene)
        {
            scene = scene ?? "";
            if (!scenes.TryGetValue(scene, out var map)) scenes[scene] = map = new Dictionary<int, byte>();
            return map;
        }

        public static void Clear() => scenes.Clear();

        public static void ClearInPlace()
        {
            foreach (var map in scenes.Values) map.Clear();
        }

        /// <summary>A region's cell changed (TerrainRegion.OnCellChanged): a styled torch there is kept, anything else forgotten.</summary>
        public static void Record(string scene, CellGrid grid, int index)
        {
            int style = grid.GetAt(index) == CellKind.Torch ? grid.TorchStyleAt(index) : 0;
            if (style > 0) For(scene)[index] = (byte)style;
            else if (scenes.TryGetValue(scene ?? "", out var map)) map.Remove(index);
        }

        /// <summary>The scene's kept styles onto a grid rebuilt from its saved edits (TerrainRegion.ReplayEdits), on its torch cells.</summary>
        public static void Replay(CellGrid grid, string scene)
        {
            if (!scenes.TryGetValue(scene ?? "", out var map)) return;
            int n = grid.Width * grid.Height;
            foreach (var kv in map)
                if (kv.Key >= 0 && kv.Key < n && grid.GetAt(kv.Key) == CellKind.Torch) grid.RestoreTorchStyle(kv.Key, kv.Value);
        }

        /// <summary>The style of the torch cell at a global cell (or the last torch's there, once it is mined), 0 for none.</summary>
        public static int StyleAt(Vector2Int cell)
        {
            var r = TerraWorld.RegionAt(cell.x, cell.y);
            return r == null ? 0 : r.Grid.GetTorchStyle(cell.x - r.CellOffset.x, cell.y - r.CellOffset.y);
        }

        /// <summary>A torch item set down on a cell (TerraMod's placing): the cell takes its style (none for the plain torch).</summary>
        public static void Placed(Vector2Int cell, ItemType item)
        {
            var r = TerraWorld.RegionAt(cell.x, cell.y);
            r?.Grid.SetTorchStyle(cell.x - r.CellOffset.x, cell.y - r.CellOffset.y, StyleOf(item));
        }

        /// <summary>What a torch cell gives when mined or blown up: its own regional torch, or the plain one.</summary>
        public static ItemType ItemAt(Vector2Int cell) => ItemOf(StyleAt(cell));

        // --- The save (Save.ModSave): scene -> index << 8 | style, the torch cells of the save's edits only. ---

        public static Dictionary<string, List<long>> Pack(IEnumerable<KeyValuePair<string, Dictionary<int, byte>>> styles, System.Func<string, int, bool> isTorch)
        {
            var file = new Dictionary<string, List<long>>();
            foreach (var scene in styles)
            {
                List<long> cells = null;
                foreach (var kv in scene.Value)
                {
                    if (!Valid(kv.Value) || !isTorch(scene.Key, kv.Key)) continue;
                    if (cells == null) file[scene.Key] = cells = new List<long>();
                    cells.Add(((long)kv.Key << 8) | kv.Value);
                }
                cells?.Sort();
            }
            return file;
        }

        public static void Load(Dictionary<string, List<long>> file)
        {
            Clear();
            if (file == null) return;
            foreach (var scene in file)
            {
                if (scene.Value == null) continue;
                var map = For(scene.Key);
                foreach (long packed in scene.Value)
                {
                    int style = (int)(packed & 0xFF);
                    if (Valid(style)) map[(int)(packed >> 8)] = (byte)style;
                }
            }
        }

        // --- A LAN game (Net.WorldSync's "r:" records, Net.TileSync's styles after the cells). ---

        /// <summary>The host's styles as world records: a record a scene's 512 cells, index &lt;&lt; 8 | style.</summary>
        public static IEnumerable<(string Key, string Scene, long[] Torches)> NetRecords()
        {
            foreach (var scene in scenes)
            {
                var chunks = new Dictionary<int, List<long>>();
                foreach (var kv in scene.Value)
                {
                    int chunk = kv.Key / 512;
                    if (!chunks.TryGetValue(chunk, out var cells)) chunks[chunk] = cells = new List<long>();
                    cells.Add(((long)kv.Key << 8) | kv.Value);
                }
                foreach (var chunk in chunks)
                {
                    chunk.Value.Sort();
                    yield return ("r:" + scene.Key + ":" + chunk.Key, scene.Key, chunk.Value.ToArray());
                }
            }
        }

        /// <summary>A record's styles: the chunk's cells take them, its others lose theirs.</summary>
        public static void NetApply(string key, string scene, long[] torches)
        {
            if (!ChunkOf(key, out int chunk)) return;
            var keep = new Dictionary<int, int>();
            if (torches != null)
                foreach (long packed in torches) keep[(int)(packed >> 8)] = (int)(packed & 0xFF);
            var map = For(scene);
            var stale = new List<int>();
            foreach (int i in map.Keys) if (i / 512 == chunk && !keep.ContainsKey(i)) stale.Add(i);
            foreach (int i in stale) SetNet(scene, i, 0);
            foreach (var kv in keep) SetNet(scene, kv.Key, kv.Value);
        }

        /// <summary>A record gone (every styled torch of that chunk mined): its cells lose their styles.</summary>
        public static void NetRemove(string key)
        {
            if (key == null || !key.StartsWith("r:", System.StringComparison.Ordinal) || !ChunkOf(key, out int chunk)) return;
            string scene = key.Substring(2, key.LastIndexOf(':') - 2);
            if (!scenes.TryGetValue(scene, out var map)) return;
            var stale = new List<int>();
            foreach (int i in map.Keys) if (i / 512 == chunk) stale.Add(i);
            foreach (int i in stale) SetNet(scene, i, 0);
        }

        static bool ChunkOf(string key, out int chunk)
        {
            chunk = 0;
            int at = key != null ? key.LastIndexOf(':') : -1;
            return at > 1 && int.TryParse(key.Substring(at + 1), out chunk);
        }

        /// <summary>A style from another player: on the loaded scene's grid (drawn again), else kept for when the scene loads.</summary>
        public static void SetNet(string scene, int index, int style)
        {
            var region = TerrainSystem.Find(scene);
            if (region != null && index >= 0 && index < region.Grid.Width * region.Grid.Height)
                region.Grid.SetTorchStyle(index % region.Grid.Width, index / region.Grid.Width, Valid(style) ? style : 0);
            if (Valid(style)) For(scene)[index] = (byte)style;
            else if (scenes.TryGetValue(scene ?? "", out var map)) map.Remove(index);
        }

        // --- Drawing (CellVisuals). ---

        static readonly Dictionary<(int, int, bool), Sprite> sprites = new Dictionary<(int, int, bool), Sprite>();

        /// <summary>A style's frame (fx: Tiles_4's column, 0 standing, 22 / 44 on a wall), its torch or its flame layer, standing on the cell's floor as the plain torch's does.</summary>
        public static Sprite Sprite(int style, int fx, bool flame)
        {
            var key = (style, fx, flame);
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            s = TerrariaAssets.Sprite(flame ? FlameSheet : TileSheet, fx, (style - 1) * 22, 20, 20, CellVisuals.PixelsPerUnit, new Vector2(0.5f, 8f / 20f));
            sprites[key] = s;
            return s;
        }
    }
}
