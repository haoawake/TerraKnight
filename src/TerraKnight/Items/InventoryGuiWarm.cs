using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The inventory's first open used to cost one long frame: every icon on it (Terraria's item
    /// sprites, decoded out of the player's own .xnb files) and every Chinese glyph of the names it
    /// shows get built the first time they are drawn. This does that work in small slices on the first
    /// frames in play instead, behind the loading screen, so opening the bag costs nothing.
    /// </summary>
    internal static partial class InventoryGui
    {
        /// <summary>Item sprites decoded, and glyphs laid out, a frame while warming up.</summary>
        const int IconsPerFrame = 24, TextsPerFrame = 40;
        /// <summary>How many of the first strings also warm the other two text sizes the panel uses.</summary>
        const int ShortTexts = 60;
        /// <summary>How far into the recipe table the icons are warmed: the top of the crafting column.</summary>
        const int WarmRecipes = 120;

        static bool iconsWarm, textWarm;
        static int iconStep, textStep;
        static readonly List<string> warmIcons = new List<string>();
        static readonly List<string> warmTexts = new List<string>();
        static readonly HashSet<string> seenIcons = new HashSet<string>(), seenTexts = new HashSet<string>();

        /// <summary>Called each frame in play while warming up: decodes the next slice of the sprites.</summary>
        public static void WarmIcons()
        {
            if (iconsWarm) return;
            if (iconStep == 0) CollectIcons();
            for (int n = 0; n < IconsPerFrame && iconStep < warmIcons.Count; n++, iconStep++)
            {
                // A sprite this Terraria hasn't got (Hallownest's own pictures, a mod-made one) is
                // skipped: warming up must never be what breaks the game.
                try { TerrariaAssets.Texture(warmIcons[iconStep]); }
                catch (System.Exception) { }
            }
            if (iconStep >= warmIcons.Count) { iconsWarm = true; warmIcons.Clear(); }
        }

        /// <summary>Called each frame the interface draws: lays out the next slice of the text.</summary>
        public static void WarmText()
        {
            if (textWarm || Event.current.type != EventType.Repaint) return;
            // The styles are built with the panel's first draw otherwise, and every line laid out with
            // them would be the first of its size (the atlas builds a set of glyphs per size).
            EnsureStyles(UI.GameView.Scale);
            if (textStep == 0) CollectTexts();
            if (labelStyle == null) return;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0f);
            for (int n = 0; n < TextsPerFrame && textStep < warmTexts.Count; n++, textStep++)
            {
                var rect = new Rect(0f, 0f, 600f, 20f);
                GUI.Label(rect, warmTexts[textStep], labelStyle);
                // The panel's own lines and the filter names also draw at the two other sizes.
                if (textStep < ShortTexts)
                {
                    GUI.Label(rect, warmTexts[textStep], nameStyle);
                    GUI.Label(rect, warmTexts[textStep], tipStyle);
                }
            }
            GUI.color = prev;
            if (textStep >= warmTexts.Count) { textWarm = true; warmTexts.Clear(); }
        }

        /// <summary>The sprites the first open draws: the bag and equipment, the column's top and its materials.</summary>
        static void CollectIcons()
        {
            AddIcon("Inventory_Back4");
            AddIcon("Inventory_Back14");
            AddIcon("UI/Creative/Infinite_Icons");
            AddIcon("UI/Bestiary/Icon_Rank_Light");
            AddIcon("UI/Craft_Toggle_0");
            AddIcon("UI/Craft_Toggle_2");
            IconStacks(inv.Slots);
            IconStacks(inv.Armor);
            IconStacks(inv.Accessories);
            IconStacks(inv.VanityArmor);
            IconStacks(inv.VanityAccessories);
            IconStacks(inv.Misc);
            int n = 0;
            foreach (var r in Recipes.All)
            {
                if (n++ >= WarmRecipes) break;
                AddDefIcon(r.Result);
                foreach (var (t, _) in r.Ingredients) AddDefIcon(t);
            }
            // A creative save's column lists what the shop has rather than what is craftable.
            foreach (var d in Shop.Everything) AddIcon(d?.Texture);
        }

        /// <summary>A Hallownest key item's picture comes from the game, not from Terraria's files.</summary>
        static void AddDefIcon(ItemType t)
        {
            var d = Def(t);
            if (d == null || d.HkItem) return;
            AddIcon(d.Texture);
        }

        /// <summary>The text the first open lays out: the panel's own lines and the item names it shows.</summary>
        static void CollectTexts()
        {
            foreach (var s in uiTexts) AddText(Lang.T(s));
            foreach (var f in filters) AddText(FilterName(f));
            AddText("0123456789×/：");
            TextStacks(inv.Slots);
            TextStacks(inv.Armor);
            TextStacks(inv.Accessories);
            TextStacks(inv.VanityArmor);
            TextStacks(inv.VanityAccessories);
            TextStacks(inv.Misc);
            int n = 0;
            foreach (var r in Recipes.All)
            {
                if (n++ >= WarmRecipes) break;
                AddText(Def(r.Result)?.Name);
                foreach (var (t, _) in r.Ingredients) AddText(Def(t)?.Name);
            }
        }

        static ItemDef Def(ItemType t) => ItemDb.Get(t);

        static void IconStacks(ItemStack[] slots)
        {
            foreach (var s in slots) if (!s.IsEmpty) AddIcon(Def(s.Type)?.Texture);
        }

        static void TextStacks(ItemStack[] slots)
        {
            foreach (var s in slots) if (!s.IsEmpty) AddText(Def(s.Type)?.Name);
        }

        static void AddIcon(string s)
        {
            if (!string.IsNullOrEmpty(s) && seenIcons.Add(s)) warmIcons.Add(s);
        }

        static void AddText(string s)
        {
            if (!string.IsNullOrEmpty(s) && seenTexts.Add(s)) warmTexts.Add(s);
        }

        /// <summary>The lines the panel itself draws, so their glyphs are laid out as well.</summary>
        static readonly string[] uiTexts =
        {
            "制作", "背包", "图鉴", "设置", "泰拉瑞亚设置", "出售", "圣巢背包", "钱币槽", "装备", "配装",
            "防御", "生命", "魔力", "伤害", "暴击", "使用时间", "击退", "材料", "消耗品", "可放置",
            "套装奖励：", "全部配方", "搜索：", "搜索物品名（点这里输入）", "筛选制作配方", "关闭筛选",
            "按原版的物品分类挑配方，或搜索物品名", "点击只看这一类", "（再点一次回到全部）", "这一类现在没有可做的配方",
            "打开制作窗口", "关闭制作窗口", "制作（徒手、附近的工作站，或靠近长椅、在德特茅斯）",
            "（徒手和附近的工作站；其余要靠近长椅或在德特茅斯）", "点击拿取，按住连续拿取",
        };
    }
}
