using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Each Hallownest area's own block and its wall (the art made for the mod: Resources/hk_HkTile_*, hk_Wall_4xx, and their
    /// item pictures). The scene's terrain dug out in an area comes out as that area's block; the block places as a
    /// Terraria block (CellKind.AreaBlockFirst on, framed like stone), crafts into four of its walls at a work bench and four
    /// walls back into one, Terraria's way. The walls are placed walls (Terraria wall ids from 400, past the game's own), so
    /// they make houses; and they are what the area's dug-out terrain shows behind it (Areas.WallFor).
    /// </summary>
    internal static class AreaBlocks
    {
        /// <summary>One area: its key (the art's file names), Areas.AreaName's name for it, its block's and wall's names.</summary>
        public struct Area
        {
            public string Key, Name, Block, Wall;
            public Area(string key, string name, string block, string wall) { Key = key; Name = name; Block = block; Wall = wall; }
        }

        /// <summary>The areas, in the order of CellKind.AreaBlockFirst.. and ItemType.AreaBlockKingsPass.. / AreaWallKingsPass...</summary>
        public static readonly Area[] All =
        {
            new Area("KingsPass", "国王山道", "山道石块", "山道石墙"),
            new Area("Dirtmouth", "德特茅斯", "德特茅斯石块", "德特茅斯石墙"),
            new Area("HowlingCliffs", "呼啸悬崖", "风蚀岩块", "风蚀岩墙"),
            new Area("Crossroads", "遗忘十字路", "十字路鹅卵石块", "十字路鹅卵石墙"),
            new Area("Greenpath", "苍绿之径", "苔石块", "苔石墙"),
            new Area("FungalWastes", "真菌荒地", "菌褶块", "菌褶墙"),
            new Area("FogCanyon", "雾之峡谷", "雾泡块", "雾泡墙"),
            new Area("QueensGardens", "王后花园", "荆棘石块", "荆棘石墙"),
            new Area("CityOfTears", "泪水之城", "泪城砖块", "泪城砖墙"),
            new Area("RoyalWaterways", "皇家水道", "水道砖块", "水道砖墙"),
            new Area("CrystalPeak", "水晶山峰", "水晶岩块", "水晶岩墙"),
            new Area("RestingGrounds", "安息之地", "墓石块", "墓石墙"),
            new Area("Deepnest", "深巢", "深巢岩块", "深巢岩墙"),
            new Area("BeastsDen", "野兽巢穴", "蛛网岩块", "蛛网岩墙"),
            new Area("KingdomsEdge", "王国边缘", "白灰岩块", "白灰岩墙"),
            new Area("Hive", "蜂巢", "蜂蜡块", "蜂蜡墙"),
            new Area("AncientBasin", "远古盆地", "古盆岩块", "古盆岩墙"),
            new Area("Abyss", "深渊", "虚空岩块", "虚空岩墙"),
            new Area("WhitePalace", "白色宫殿", "白宫大理石块", "白宫大理石墙"),
            new Area("BlackEggTemple", "黑卵神殿", "神殿石块", "神殿石墙"),
            new Area("Colosseum", "愚人斗兽场", "斗兽场砖块", "斗兽场砖墙"),
            new Area("GrimmTroupe", "格林剧团", "剧团绒布块", "剧团绒布墙"),
            new Area("Dream", "梦境", "梦境石块", "梦境石墙"),
            new Area("Godhome", "神居", "神居金砖块", "神居金砖墙"),
        };

        const float Tick = 1f / 60f;

        /// <summary>The first area wall's id (Terraria's own walls end at 366).</summary>
        public const int FirstWall = 400;

        static readonly Dictionary<string, int> byName = new Dictionary<string, int>();

        static AreaBlocks()
        {
            for (int i = 0; i < All.Length; i++) byName[All[i].Name] = i;
        }

        public static CellKind Kind(int i) => (CellKind)((int)CellKind.AreaBlockFirst + i);
        public static int IndexOf(CellKind k) => Cells.IsAreaBlock(k) ? (int)k - (int)CellKind.AreaBlockFirst : -1;
        public static ItemType BlockItem(int i) => ItemType.AreaBlockKingsPass + i;
        public static ItemType WallItem(int i) => ItemType.AreaWallKingsPass + i;
        public static int WallId(int i) => FirstWall + i;
        public static bool IsAreaWall(int wall) => wall >= FirstWall && wall < FirstWall + All.Length;

        /// <summary>The block sheet a placed area block is drawn from (Tiles_1's layout).</summary>
        public static string Sheet(CellKind k)
        {
            int i = IndexOf(k);
            return i < 0 ? null : "HkTile_" + All[i].Key;
        }

        /// <summary>The area a scene belongs to (Areas.AreaName), -1 for none of them.</summary>
        public static int IndexOfScene(string scene) => byName.TryGetValue(Areas.AreaName(scene), out int i) ? i : -1;

        /// <summary>The area wall's sheet for a scene (Wall_4xx), or null.</summary>
        public static string WallSheetOfScene(string scene)
        {
            int i = IndexOfScene(scene);
            return i < 0 ? null : "Wall_" + WallId(i);
        }

        /// <summary>What a mined cell gives: the scene's own terrain the area's block (stone outside the areas), the rest their item.</summary>
        public static ItemType DropFor(CellKind kind, Vector2Int cell)
        {
            if (kind == CellKind.Original)
            {
                var region = TerraWorld.RegionAt(cell.x, cell.y);
                int i = region != null ? IndexOfScene(region.SceneName) : -1;
                if (i >= 0) return BlockItem(i);
            }
            if (kind == CellKind.Torch) return RegionalTorches.ItemAt(cell);
            if (kind >= CellKind.AreaPlatformFirst && kind <= CellKind.AreaPlatformLast) return RegionalFurniture.Item((int)kind - (int)CellKind.AreaPlatformFirst, RegionalFurniture.PlatformIndex);
            int a = IndexOf(kind);
            return a >= 0 ? BlockItem(a) : ItemDb.DropFor(kind);
        }

        // Natural terrain is acquired by mining its own region, not by a global
        // stage-0 material roll. Recipes inherit this estimate without changing ingredients.
        public static int AcquisitionStage(int i)
        {
            switch(All[i].Name) {
                case "国王山道":case "德特茅斯":case "遗忘十字路":case "苍绿之径":case "黑卵神殿":return 0;
                case "呼啸悬崖":case "真菌荒地":case "雾之峡谷":return 1;
                case "泪水之城":case "皇家水道":case "水晶山峰":case "安息之地":case "梦境":return 2;
                case "王后花园":case "深巢":case "野兽巢穴":case "远古盆地":return 3;
                case "王国边缘":case "蜂巢":case "深渊":case "愚人斗兽场":return 4;
                default:return 5;
            }
        }

        public static void AddItems(List<ItemDef> list)
        {
            for (int i = 0; i < All.Length; i++)
            {
                var a = All[i];
                list.Add(new ItemDef
                {
                    Type = BlockItem(i), Name = a.Block, Texture = "HkTileItem_" + a.Key, Source = ItemSource.Drop, Stage = AcquisitionStage(i), Places = Kind(i), MaxStack = 999,
                    UseAnimation = 15 * Tick, UseTime = 10 * Tick, Tooltip = "可放置",
                });
                list.Add(new ItemDef
                {
                    Type = WallItem(i), Name = a.Wall, Texture = "HkWallItem_" + a.Key, Source = ItemSource.Craft, Stage = AcquisitionStage(i), PlacesWall = WallId(i), MaxStack = 9999,
                    Consumable = true, UseTime = 7 * Tick, UseAnimation = 15 * Tick, Tooltip = "可放置背景墙\n用锤子拆除",
                });
            }
        }

        /// <summary>Terraria's block and wall pair: one block makes four walls at a work bench, four walls one block.</summary>
        public static IEnumerable<Recipe> Recipes()
        {
            for (int i = 0; i < All.Length; i++)
            {
                yield return new Recipe(WallItem(i), 4, "工作台", 0, (BlockItem(i), 1));
                yield return new Recipe(BlockItem(i), 1, "工作台", 0, (WallItem(i), 4));
            }
        }
    }
}
