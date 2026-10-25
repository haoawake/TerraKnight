using System.Collections.Generic;
namespace TerraKnight.Items
{
    /// <summary>Append-only item IDs: the old HallownestShield remains crystal + shade.</summary>
    internal static class AbilityShields
    {
        public const int Crystal = 1, Shade = 2, Dive = 4;
        internal sealed class Def
        {
            public ItemType Type; public string Name, Icon; public int Mask, Level;
            public Def(ItemType t, string n, string icon, int mask, int level = 0)
            { Type = t; Name = n; Icon = icon; Mask = mask; Level = level; }
            public int Stage => (Mask & Shade) != 0 || Level == 2 ? 5 : (Mask & Crystal) != 0 ? 3 : 2;
        }
        public static readonly Def[] All = {
            new Def(ItemType.CrystalShield, "晶心之盾", "crystal_shield", Crystal),
            new Def(ItemType.ShadeShield, "影帷之盾", "shade_shield", Shade),
            new Def(ItemType.DiveShield, "荒芜之盾", "dive_shield", Dive, 1),
            new Def(ItemType.DarkShield, "暗渊之盾", "dark_shield", Dive, 2),
            new Def(ItemType.HallownestShield, "晶影之盾", "crystal_shade_shield", Crystal | Shade),
            new Def(ItemType.CrystalDiveShield, "晶陨之盾", "crystal_dive_shield", Crystal | Dive, 1),
            new Def(ItemType.ShadeDiveShield, "影陨之盾", "shade_dive_shield", Shade | Dive, 1),
            new Def(ItemType.CrystalDarkShield, "晶渊之盾", "crystal_dark_shield", Crystal | Dive, 2),
            new Def(ItemType.ShadeDarkShield, "影渊之盾", "shade_dark_shield", Shade | Dive, 2),
            new Def(ItemType.HallownestDiveShield, "圣巢之盾", "hallownest_shield", Crystal | Shade | Dive, 1),
            new Def(ItemType.VoidHallownestShield, "虚空圣巢之盾", "void_hallownest_shield", Crystal | Shade | Dive, 2),
        };
        public static Def Get(ItemType t) { foreach (var d in All) if (d.Type == t) return d; return null; }
        public static bool Is(ItemType t) => Get(t) != null;
        public static bool Includes(ItemType t, ItemType component)
        {
            var d = Get(t);
            if (d == null) return false;
            return component == ItemType.ShieldOfCthulhu
                || component == ItemType.CrystalHeart && (d.Mask & Crystal) != 0
                || component == ItemType.ShadeCloak && (d.Mask & Shade) != 0
                || component == ItemType.DeadCellsRamRune && d.Level > 0
                || component == ItemType.DescendingDarkRune && d.Level == 2;
        }
        public static string Tip(Def d)
        {
            string t = "点按冲刺键：盾冲并撞击敌人";
            if ((d.Mask & Crystal) != 0) t += "\n长按冲刺键蓄力，松开超级冲刺（空中也可使用）";
            if ((d.Mask & Shade) != 0) t += "\n按暗影冲刺键或双击方向键：暗影冲刺，穿过敌人、攻击和暗影之门（冷却中双击是盾冲）";
            if (d.Level > 0) t += d.Level == 2 ? "\n空中按住下并跳跃：黑砸，双倍猛击伤害，落地短暂无敌" : "\n空中按住下并跳跃：普通下砸，砸开脆弱地板";
            return t;
        }
        public static IEnumerable<Recipe> Recipes()
        {
            var components = new[] { ItemType.CrystalHeart, ItemType.ShadeCloak, ItemType.DeadCellsRamRune, ItemType.DescendingDarkRune };
            foreach (var d in All)
                foreach (var c in components)
                {
                    int bit = c == ItemType.CrystalHeart ? Crystal : c == ItemType.ShadeCloak ? Shade : Dive;
                    int level = c == ItemType.DescendingDarkRune ? 2 : c == ItemType.DeadCellsRamRune ? 1 : d.Level;
                    if ((d.Mask & bit) == 0)
                    {
                        var to = Find(d.Mask | bit, level);
                        yield return new Recipe(to.Type, 1, "工匠作坊", 2, (d.Type, 1), (c, 1));
                    }
                    else if (c == ItemType.DescendingDarkRune && d.Level == 1)
                        yield return new Recipe(Find(d.Mask, 2).Type, 1, "工匠作坊", 2, (d.Type, 1), (c, 1));
                }
            foreach (var c in components)
            {
                int mask = c == ItemType.CrystalHeart ? Crystal : c == ItemType.ShadeCloak ? Shade : Dive;
                int level = c == ItemType.DescendingDarkRune ? 2 : c == ItemType.DeadCellsRamRune ? 1 : 0;
                yield return new Recipe(Find(mask, level).Type, 1, "工匠作坊", 2, (ItemType.ShieldOfCthulhu, 1), (c, 1));
            }
        }
        static Def Find(int mask, int level)
        { foreach (var d in All) if (d.Mask == mask && d.Level == level) return d; throw new System.InvalidOperationException("Missing ability shield"); }
    }
}

