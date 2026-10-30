using System.Collections.Generic;
using System.Linq;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static bool wardrobeOpen;
        static int wardrobeRegion = -1, wardrobePage;
        internal static bool WardrobeOpen => wardrobeOpen;
        internal static void CloseWardrobe() => wardrobeOpen = false;
        internal static Rect DevWardrobeRect;

        public static void OpenWardrobe()
        {
            World.Chests.Close(false);
            Open = true;
            bestiaryOpen = statsOpen = hkOpen = blacklistOpen = false;
            wardrobeOpen = true;
            wardrobePage = 0;
            Sound("Menu_Open");
        }

        static void TakeWardrobe(int id, bool equip)
        {
            var type = (ItemType)id;
            var copy = Wardrobe.Copy(inv, type);
            if (copy.IsEmpty) { Notice(Lang.T("获得这件时装后即可永久取用")); return; }
            if (equip)
            {
                int slot = copy.Def.Armor == ArmorSlot.Head ? 0 : copy.Def.Armor == ArmorSlot.Body ? 1 : copy.Def.Armor == ArmorSlot.Legs ? 2 : -1;
                if (slot >= 0)
                {
                    var old = inv.VanityArmor[slot];
                    if (!old.IsEmpty && !old.WardrobeCopy && inv.Pickup(old) != 0) return;
                    inv.VanityArmor[slot] = copy;
                    inv.RefreshEffective();
                    Sound("Grab");
                    return;
                }
            }
            if (!inv.Mouse.IsEmpty) { Notice(Lang.T("请先放下鼠标上的物品")); return; }
            inv.Mouse = copy;
            Sound("Grab");
        }

        internal static void DevTakeWardrobe(ItemType type, bool equip = false) => TakeWardrobe((int)type, equip);
        internal static void DevFilterWardrobe(int region) => Act(new Hit { Kind = Kind.WardrobeFilter, Index = region }, true, false, true, false);
        internal static List<Wardrobe.Outfit> DevWardrobeVisible() => Wardrobe.Outfits.Where(o => wardrobeRegion < 0 || o.Regions.Contains(wardrobeRegion)).ToList();

        static void DrawWardrobe(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string extra, ref Kind hoveredKind)
        {
            float w = Mathf.Min(920f * ui, view.width - 24f * ui);
            float h = Mathf.Min(600f * ui, view.height - 24f * ui);
            var panel = new Rect(view.center.x - w / 2f, view.center.y - h / 2f, w, h);
            DevWardrobeRect = panel;
            panels.Add(panel); Add(panel, Kind.WardrobePanel, 0);
            GUI.Box(panel, GUIContent.none);
            var tint = GUI.color;
            var small = new GUIStyle(labelStyle) { fontSize = Mathf.RoundToInt(14f * ui), clipping = TextClipping.Clip, wordWrap = false,
                padding = new RectOffset(), margin = new RectOffset(), alignment = TextAnchor.MiddleCenter };
            var centeredLabel = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(), margin = new RectOffset() };
            var centeredTitle = new GUIStyle(nameStyle) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(), margin = new RectOffset() };
            GUI.color = new Color(.10f, .14f, .26f, .97f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture); GUI.color = tint;
            if (panel.Contains(mouse)) { hovered = default; extra = null; }
            ItemStack wardrobeHover = default;
            string wardrobeExtra = null;
            Outlined(new Rect(panel.x + 44f * ui, panel.y + 12f * ui, w - 88f * ui, 26f * ui),
                Lang.F("全局衣柜 · 已收藏 {0} 件", inv.WardrobeItems.Count), centeredTitle, Color.white);
            Outlined(new Rect(panel.x + 16f * ui, panel.y + 44f * ui, w - 32f * ui, 24f * ui),
                Lang.T("左键取用 · 右键穿戴 · 永久保留 · 副本无售价，丢弃即消失"), centeredLabel, Color.white);
            void Button(Rect rect, string title, Kind kind, int index, bool selected = false)
            {
                GUI.Box(rect, GUIContent.none);
                Add(rect, kind, index);
                Outlined(rect, title, small, selected ? new Color(1f, .85f, .35f) : Color.white);
            }
            Button(new Rect(panel.xMax - 38f * ui, panel.y + 10f * ui, 28f * ui, 28f * ui), "×", Kind.WardrobeButton, 0);
            float filterW = 240f * ui;
            float rowH = Mathf.Min(28f * ui, (h - 118f * ui) / 13f);
            for (int a = -1; a < AreaBlocks.All.Length; a++)
                Button(new Rect(panel.x + 12f * ui + (a + 1) / 13 * filterW / 2f, panel.y + 78f * ui + (a + 1) % 13 * rowH, filterW / 2f - 4f * ui, rowH - 2f * ui),
                    a < 0 ? Lang.T("全部地区") : Areas.Display(AreaBlocks.All[a].Name), Kind.WardrobeFilter, a, wardrobeRegion == a);

            var shown = Wardrobe.Outfits.Where(o => wardrobeRegion < 0 || o.Regions.Contains(wardrobeRegion)).ToList();
            float cardW = (w - filterW - 48f * ui) / 4f;
            float cardH = 112f * ui;
            int rows = Mathf.Max(1, Mathf.FloorToInt((h - 125f * ui) / cardH));
            int perPage = rows * 4, pages = Mathf.Max(1, Mathf.CeilToInt(shown.Count / (float)perPage));
            wardrobePage = Mathf.Clamp(wardrobePage, 0, pages - 1);
            for (int n = 0; n < perPage && wardrobePage * perPage + n < shown.Count; n++)
            {
                var outfit = shown[wardrobePage * perPage + n];
                var card = new Rect(panel.x + filterW + 24f * ui + n % 4 * cardW, panel.y + 78f * ui + n / 4 * cardH, cardW - 6f * ui, cardH - 6f * ui);
                GUI.Box(card, GUIContent.none);
                Outlined(new Rect(card.x + 4f * ui, card.y + 3f * ui, card.width - 8f * ui, 22f * ui), Lang.T(outfit.Name), small, Color.white);
                float size = Mathf.Min(40f * ui, (card.width - 12f * ui) / 3f);
                void Piece(ItemType type, Rect rect, string empty)
                {
                    bool unlocked = type != ItemType.None && inv.WardrobeItems.Contains(type);
                    var previous = GUI.color;
                    DrawSlot(rect, unlocked ? ItemStack.Of(type) : default, "Inventory_Back", false);
                    GUI.color = previous;
                    if (type == ItemType.None)
                    {
                        GUI.color = Color.black;
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                        GUI.color = previous;
                    }
                    else
                    {
                        if (!unlocked) Outlined(rect, empty, centeredLabel, new Color(.6f, .6f, .7f));
                        Add(rect, Kind.WardrobeItem, (int)type);
                        if (rect.Contains(mouse))
                        {
                            wardrobeHover = ItemStack.Of(type);
                            wardrobeExtra = Lang.T(unlocked ? "已收藏：左键免费取用，右键穿戴\n副本无价值，不能用作合成材料或微光转化" : "尚未收藏：获得后自动收纳");
                        }
                    }
                }
                for (int s = 0; s < 3; s++) Piece(outfit.Parts[s], new Rect(card.x + 5f * ui + s * size, card.y + 28f * ui, size, size), Lang.T(s == 0 ? "头" : s == 1 ? "身" : "脚"));
                for (int s = 0; s < outfit.Extras.Count; s++) Piece(outfit.Extras[s], new Rect(card.x + 5f * ui + s * 29f * ui, card.y + 73f * ui, 26f * ui, 26f * ui), "");
            }
            float bottom = panel.yMax - 34f * ui;
            Button(new Rect(panel.x + filterW + 24f * ui, bottom, 75f * ui, 26f * ui), Lang.T("上一页"), Kind.WardrobeButton, 1);
            Outlined(new Rect(panel.x + filterW + 108f * ui, bottom, w - filterW - 212f * ui, 26f * ui), $"{wardrobePage + 1} / {pages}   ·   {shown.Count}", centeredLabel, Color.white);
            Button(new Rect(panel.xMax - 92f * ui, bottom, 75f * ui, 26f * ui), Lang.T("下一页"), Kind.WardrobeButton, 2);
            if (!wardrobeHover.IsEmpty) { hovered = wardrobeHover; extra = wardrobeExtra; hoveredKind = Kind.WardrobeItem; }
        }
    }
}
