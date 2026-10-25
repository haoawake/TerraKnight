using TerraKnight.Items;

namespace TerraKnight.World
{
    // Full building tiles from Terraria 1.4.5.8 createTile and native tile flags.
    // IDs are persisted as bytes and sent over the network: append entries, never reorder.
    // Seeds and furniture need their own placement rules and are deliberately excluded.
    internal static class VanillaBlocks
    {
        public readonly struct Entry
        {
            public readonly CellKind Kind;
            public readonly ItemType Item;
            public readonly int Tile;
            public Entry(int kind, ItemType item, int tile) { Kind = (CellKind)kind; Item = item; Tile = tile; }
        }
        public static readonly Entry[] All =
        {
            new Entry(88, ItemType.Pumpkin, 251),
            new Entry(89, ItemType.Cog, 272),
            new Entry(90, ItemType.SpookyWood, 253),
            new Entry(91, ItemType.Ebonwood, 157),
            new Entry(92, ItemType.RichMahogany, 158),
            new Entry(93, ItemType.Pearlwood, 159),
            new Entry(94, ItemType.Shadewood, 208),
            new Entry(95, ItemType.PalmWood, 322),
            new Entry(96, ItemType.BorealWood, 321),
            new Entry(97, ItemType.AshWood, 635),
            new Entry(98, ItemType.Cactus, 188),
            new Entry(99, ItemType.RainbowBrick, 160),
            new Entry(100, ItemType.GlowingMushroom, 190),
            new Entry(101, ItemType.ClayBlock, 40),
            new Entry(102, ItemType.Glass, 54),
            new Entry(103, ItemType.IronOre, 6),
            new Entry(104, ItemType.GoldOre, 8),
            new Entry(105, ItemType.Hellstone, 58),
            new Entry(106, ItemType.CrispyHoneyBlock, 230),
            new Entry(107, ItemType.SturdyFossil, 407),
            new Entry(108, ItemType.MudBlock, 59),
            new Entry(109, ItemType.EbonsandBlock, 112),
            new Entry(110, ItemType.CrimsandBlock, 234),
            new Entry(111, ItemType.PearlsandBlock, 116),
            new Entry(112, ItemType.CopperOre, 7),
            new Entry(113, ItemType.TinOre, 166),
            new Entry(114, ItemType.LeadOre, 167),
            new Entry(115, ItemType.SilverOre, 9),
            new Entry(116, ItemType.TungstenOre, 168),
            new Entry(117, ItemType.PlatinumOre, 169),
            new Entry(118, ItemType.DemoniteOre, 22),
            new Entry(119, ItemType.CrimtaneOre, 204),
            new Entry(120, ItemType.CobaltOre, 107),
            new Entry(121, ItemType.PalladiumOre, 221),
            new Entry(122, ItemType.MythrilOre, 108),
            new Entry(123, ItemType.OrichalcumOre, 222),
            new Entry(124, ItemType.AdamantiteOre, 111),
            new Entry(125, ItemType.TitaniumOre, 223),
            new Entry(126, ItemType.ChlorophyteOre, 211),
            new Entry(127, ItemType.LunarOre, 408),
        };
        public static bool Contains(CellKind kind) => (int)kind >= 88 && (int)kind < 88 + All.Length;
        public static int Tile(CellKind kind) => Contains(kind) ? All[(int)kind - 88].Tile : -1;
        public static string Sheet(CellKind kind) => Contains(kind) ? "Tiles_" + Tile(kind) : null;
        public static ItemType Drop(CellKind kind) => Contains(kind) ? All[(int)kind - 88].Item : ItemType.None;
        public static bool IsSand(CellKind kind) => kind == CellKind.Sand || Tile(kind) == 112 || Tile(kind) == 234 || Tile(kind) == 116;
        public static int PickRequired(CellKind kind)
        {
            switch (Tile(kind))
            {
                case 58: return 65;
                case 22: case 204: return 55;
                case 107: case 221: return 100;
                case 108: case 222: return 110;
                case 111: case 223: return 150;
                case 211: case 408: return 200;
                default: return kind == CellKind.Meteorite ? 50 : kind == CellKind.Obsidian ? 55 : 0;
            }
        }
        public static void Apply(ItemDef[] defs)
        {
            foreach (var e in All)
            {
                var d = defs[(int)e.Item];
                d.Places = e.Kind;
                d.UseTime = 10f / 60f;
                d.UseAnimation = 15f / 60f;
                d.Consumable = true;
                if (d.Tooltip == null || !d.Tooltip.Contains("可放置")) d.Tooltip = "可放置\n" + d.Tooltip;
            }
            var obsidian = defs[(int)ItemType.Obsidian];
            obsidian.Places = CellKind.Obsidian;
            obsidian.UseTime = 10f / 60f;
            obsidian.UseAnimation = 15f / 60f;
            obsidian.Consumable = true;
            obsidian.Tooltip = "可放置\n" + obsidian.Tooltip;
        }
    }
}
