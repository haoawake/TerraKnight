using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static int creativeHoverWas = -1;

        /// <summary>
        /// A creative save's stage (CreativeStage): "阶段" and six slot buttons 0-5 right of the bag's title, the chosen one
        /// in the selected slot's colour (none chosen: all of them, 5); hovered, what it opens and what follows it.
        /// </summary>
        static void DrawCreativeStage(Rect view, float ui, Vector2 mouse, float x)
        {
            int now = Shop.UnlockedStage(), over = -1;
            float y = view.y + 4f * ui, size = 22f * ui;
            Outlined(new Rect(x, y, 48f * ui, size), Lang.T("阶段"), labelStyle, new Color(1f, 0.95f, 0.6f));
            x += 44f * ui;
            for (int s = 0; s <= World.Areas.MaxLevel; s++)
            {
                var r = new Rect(x + s * (size + 3f * ui), y, size, size);
                Add(r, Kind.CreativeStage, s);
                var back = TerrariaAssets.Texture(s == now ? "Inventory_Back14" : "Inventory_Back");
                if (back != null) GUI.DrawTexture(r, back);
                var c = labelStyle.alignment;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                Outlined(r, Num(s), labelStyle, s == now ? new Color(1f, 0.85f, 0.3f) : Color.white);
                labelStyle.alignment = c;
                if (r.Contains(mouse)) over = s;
            }
            if (over >= 0)
            {
                string gate = over == 0 ? Lang.T("开局") : Lang.F("取得 {0} 种不同移动能力，顺序不限", over);
                DrawLines(mouse, new List<string> { Lang.F("创造模式：切换到阶段 {0}（{1}）", over, gate), Lang.T("商店、配方、居民和神居按这个阶段") }, Color.white);
            }
            if (over >= 0 && over != creativeHoverWas) Sound("Menu_Tick");
            creativeHoverWas = over;
        }

        /// <summary>
        /// Main.mH: with the minimap up in its frame the equipment goes down under it and the information icons' row (from
        /// 230 to 423, a row of hearts more for each past two), as far as there is room above the settings buttons (as
        /// Terraria keeps RecommendedEquipmentAreaPushUp free).
        /// </summary>
        static float EquipShift(Rect view, float ui)
        {
            if (!UI.WorldMap.MinimapVisible) return 0f;
            float want = 193f + UI.ResourceHud.ExtraHeartRows * UI.ResourceHud.RowStep;
            float bottom = 230f + (Inventory.ArmorSlots + Inventory.OpenAccessories) * SlotStep + 4f;
            float room = view.height / ui - 140f - bottom;
            return Mathf.Clamp(want, 0f, Mathf.Max(0f, room));
        }

        /// <summary>
        /// The toggles drawn with the inventory open, as Terraria's: the information icons (UI.InfoAccessories.DrawToggles)
        /// and the builder toggles' column (UI.BuilderToggles.Draw); not under the book, the Hallownest bag or the
        /// attributes, which cover them.
        /// </summary>
        static void DrawToggles(Inventory inv, Rect view, float ui, Vector2 mouse)
        {
            if (bestiaryOpen || hkOpen || statsOpen || blacklistOpen) return;
            var infoIcons = UI.InfoAccessories.DrawToggles(inv, view, ui, mouse, out string info);
            foreach (var (icon, rect) in infoIcons) Add(rect, Kind.InfoToggle, icon);
            var builderIcons = UI.BuilderToggles.Draw(view, ui, mouse, out string builder);
            foreach (var (index, rect) in builderIcons) Add(rect, Kind.BuilderToggle, index);
            string hover = info ?? builder;
            if (hover != null) DrawLines(mouse, new List<string> { hover }, Color.white);
        }
    }
}
