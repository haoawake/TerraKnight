using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Talking to the townsfolk (NPCChatPanel, 1.4.5) and their shop panel (Main.DrawInventory's
    /// shop, context 15): the chat box 500 wide at the top of the screen with its text and
    /// buttons, the shop's 40 slots under the inventory, buying onto the cursor and selling by
    /// Ctrl+click or by dropping on an empty slot; prices in geo.
    /// </summary>
    internal static partial class InventoryGui
    {
        static GUIStyle chatStyle, chatButtonStyle;
        static int chatHover = -1;

        /// <summary>Main.mouseTextColor for the chat (190↔255).</summary>
        static float ChatPulse => 222f + 32f * Mathf.Sin(Time.unscaledTime * 1.85f);

        static void DrawChat(Rect view, float ui, Vector2 mouse)
        {
            if (!TownNpcs.ChatOpen) return;
            if (chatStyle == null)
            {
                chatStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false };
                chatButtonStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            }
            chatStyle.fontSize = Mathf.RoundToInt(17 * ui);
            var lines = Wrap(TownNpcs.ChatText, 460f * ui, chatStyle, 10);
            var buttons = new List<(string Text, int Index)>();
            if (TownNpcs.TalkingHasShop) buttons.Add((Lang.T("商店"), 0));
            // The Guide's crafting (Lang.inter[25]).
            if (TownNpcs.TalkingIsGuide) buttons.Add((TT("LegacyInterface.25", "制作"), 2));
            buttons.Add((Lang.T("关闭"), 1));
            float w = 500f * ui, h = (30f + 30f * lines.Count + 30f) * ui;
            var panel = new Rect(view.center.x - w / 2f, view.y + 100f * ui, w, h);
            var back = TerrariaAssets.Texture("Chat_Back");
            var prev = GUI.color;
            GUI.color = new Color(200 / 255f, 200 / 255f, 200 / 255f, 200 / 255f);
            // Two slices of Chat_Back: the top, and its bottom 30 px.
            float top = h - 30f * ui;
            GUI.DrawTextureWithTexCoords(new Rect(panel.x, panel.y, w, top), back, new Rect(0f, 1f - (h / ui - 30f) / 500f, 1f, (h / ui - 30f) / 500f));
            GUI.DrawTextureWithTexCoords(new Rect(panel.x, panel.y + top, w, 30f * ui), back, new Rect(0f, 0f, 1f, 30f / 500f));
            GUI.color = prev;
            panels.Add(panel);
            float n = (ChatPulse * 2f + 255f) / 3f / 255f;
            for (int i = 0; i < lines.Count; i++)
                Outlined(new Rect(panel.x + 20f * ui, panel.y + (20f + i * 30f) * ui, 470f * ui, 28f * ui), lines[i], chatStyle, new Color(n, n, n, 1f));
            // The buttons, 30 px apart from x + 30; the hovered one bigger, with a brown edge.
            float bx = panel.x + 30f * ui, by = panel.yMax - 22f * ui - 6f * ui;
            int over = -1;
            float b = ChatPulse / 255f;
            foreach (var (text, index) in buttons)
            {
                chatButtonStyle.fontSize = Mathf.RoundToInt(17 * 0.9f / 0.85f * ui);
                float bw = chatButtonStyle.CalcSize(new GUIContent(text)).x;
                var r = new Rect(bx, by, bw, 22f * ui);
                bool hover = r.Contains(mouse);
                if (hover) over = index;
                chatButtonStyle.fontSize = Mathf.RoundToInt(17 * (hover ? 1.2f : 0.9f) / 0.85f * ui);
                Outlined(new Rect(bx - (hover ? bw * 0.15f : 0f), by - (hover ? 3f * ui : 0f), bw * 2f, 26f * ui), text, chatButtonStyle, new Color(b, b / 1.1f, b / 2f, 1f));
                Add(r, Kind.ChatButton, index);
                bx += bw + 30f * ui;
            }
            if (over != chatHover && over >= 0) Sound("Menu_Tick");
            chatHover = over;
        }

        static string TT(string key, string zh) => TerrariaText.Get(Lang.Culture, key) ?? Lang.T(zh);

        /// <summary>
        /// The Guide's slot (Main.DrawInventory, InGuideCraftMenu: ItemSlot context 7 at 73, 331)
        /// above the crafting column, and Main.DrawGuideCraftText beside it: "place a material here",
        /// or which item the recipes shown use and what the focused one needs to be made at.
        /// </summary>
        static void DrawGuideSlot(Vector2 at, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind)
        {
            if (!TownNpcs.GuideCraftOpen) return;
            float s = 52f * 0.85f * ui;
            var rect = new Rect(at.x, at.y, s, s);
            GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back4"));
            panels.Add(rect);
            Add(rect, Kind.GuideSlot, 0);
            var item = TownNpcs.GuideItem;
            if (!item.IsEmpty) DrawIcon(rect, item, false);
            if (rect.Contains(mouse) && !item.IsEmpty)
            {
                hovered = item;
                hoveredKind = Kind.GuideSlot;
            }
            var text = new Rect(at.x + 50f * ui, at.y + 12f * ui, 420f * ui, 24f * ui);
            float n = ChatPulse / 255f;
            var color = new Color(n, n, n, 1f);
            if (item.IsEmpty)
            {
                Outlined(text, TT("LegacyInterface.24", "在此放置材料"), labelStyle, color);
                return;
            }
            text.y -= 14f * ui;
            Outlined(text, TT("LegacyInterface.21", "所显示的配方使用此物品:") + " " + item.Def.Name, labelStyle, color);
            if (focus < 0 || focus >= recipes.Count) return;
            // Main.GetRecipeRequirementsText: the station, or "none" for one made by hand.
            var r = recipes[focus];
            string station = string.IsNullOrEmpty(r.Station) || r.Station == "徒手" ? TT("LegacyInterface.23", "无") : Lang.T(r.Station);
            Outlined(new Rect(text.x, text.y + 26f * ui, 420f * ui, 24f * ui), TT("LegacyInterface.22", "需要物件:") + " " + station, labelStyle, color);
        }

        /// <summary>ItemSlot.Handle for context 7: a click swaps the cursor's item with the slot's; only a material (an ingredient of some recipe) goes in.</summary>
        static void ActGuideSlot(bool leftPress)
        {
            if (!leftPress || !TownNpcs.GuideCraftOpen) return;
            if (!inv.Mouse.IsEmpty && !Recipes.IsMaterial(inv.Mouse.Type)) return;
            var held = inv.Mouse;
            inv.Mouse = TownNpcs.GuideItem;
            TownNpcs.GuideItem = held;
            focus = 0;
            focusY = 0f;
            Sound("Grab");
        }

        /// <summary>Dev: a click on the Guide's slot; the crafting column's results, in order.</summary>
        public static void DevGuideSlot() => ActGuideSlot(true);
        public static string DevRecipeNames(int max)
        {
            RefreshRecipes();
            var names = new List<string>();
            for (int i = 0; i < recipes.Count && i < max; i++) names.Add(recipes[i].Def.Name);
            return string.Join("、", names) + (recipes.Count > max ? $"……（共 {recipes.Count} 个）" : $"（共 {recipes.Count} 个）");
        }

        /// <summary>Utils.WordwrapString for Chinese: characters until the width runs out (at most maxLines).</summary>
        static List<string> Wrap(string text, float width, GUIStyle style, int maxLines)
        {
            var lines = new List<string>();
            foreach (var para in (text ?? "").Split('\n'))
            {
                var sb = new System.Text.StringBuilder();
                foreach (char c in para)
                {
                    sb.Append(c);
                    if (style.CalcSize(new GUIContent(sb.ToString())).x > width && sb.Length > 1)
                    {
                        sb.Length--;
                        lines.Add(sb.ToString());
                        sb.Clear();
                        sb.Append(c);
                    }
                }
                lines.Add(sb.ToString());
                if (lines.Count >= maxLines) break;
            }
            return lines;
        }

        /// <summary>The shop: 10×4 slots at 0.755 scale under the inventory, "商店" and the NPC's name beside them.</summary>
        static void DrawShopPanel(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            if (!TownNpcs.ShopOpen) return;
            float s = 52f * 0.755f * ui, step = 56f * 0.755f * ui;
            float x0 = view.x + 73f * ui, y0 = view.y + (258f + 18f) * ui;
            Outlined(new Rect(view.x + 504f * ui, y0 - 6f * ui, 300f * ui, 30f * ui), Lang.T("商店"), nameStyleLeft(ui), Color.white);
            Outlined(new Rect(view.x + 504f * ui, y0 + 26f * ui, 300f * ui, 22f * ui), TownNpcs.TalkingName, labelStyle, new Color(1f, 0.95f, 0.6f));
            Outlined(new Rect(view.x + 504f * ui, y0 + 48f * ui, 300f * ui, 22f * ui), Lang.F("持有 {0}", Shop.Money(Shop.Purse)), labelStyle, new Color(1f, 0.9f, 0.55f));
            panels.Add(new Rect(x0, y0, 560f * 0.755f * ui, 224f * 0.755f * ui));
            var prev = GUI.color;
            for (int i = 0; i < TownNpcs.ShopItems.Length; i++)
            {
                int col = i % 10, row = i / 10;
                var rect = new Rect(x0 + col * step, y0 + row * step, s, s);
                Add(rect, Kind.ShopSlot, i);
                GUI.color = new Color(220 / 255f, 220 / 255f, 220 / 255f, 220 / 255f);
                GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back6"));
                GUI.color = prev;
                var st = TownNpcs.ShopItems[i];
                if (st.IsEmpty) continue;
                DrawIcon(rect, st, false);
                if (!rect.Contains(mouse)) continue;
                hovered = st;
                hoveredKind = Kind.ShopSlot;
                long price = TownNpcs.BuyPrice(i);
                hoveredExtra = price > 0 ? Lang.F("买入价: {0}", Shop.Money(price * (Shift ? 10 : 1))) : Lang.T("无价值");
            }
        }

        /// <summary>Main.MouseText_DrawItemTooltip_AddShopLines for the player's own items while a shop is open.</summary>
        static string SellLine(ItemStack st)
        {
            long v = Shop.SellValue(st);
            return v > 0 ? Lang.F("卖出价: {0}", Shop.Money(v)) : Lang.T("无价值");
        }

        static bool soldOnPress;

        /// <summary>A click on a shop slot: buy (held: again, faster and faster), or sell the cursor's item onto an empty one.</summary>
        static void ActShop(int index, bool leftPress, bool rightPress, bool leftHeld, bool rightHeld)
        {
            if (!TownNpcs.ShopOpen) return;
            var slot = TownNpcs.ShopItems[index];
            // A sale's click held on: the item it put in that slot isn't bought straight back.
            if (!leftHeld) soldOnPress = false;
            if (slot.IsEmpty)
            {
                if (leftPress && !inv.Mouse.IsEmpty)
                {
                    TownNpcs.Sell(ref inv.Mouse);
                    soldOnPress = true;
                }
                return;
            }
            if (soldOnPress && !rightHeld) return;
            if (!(leftHeld || rightHeld) || stackSplit > 1) return;
            int n = (superFastStack + 1) * (Shift ? 10 : 1);
            if (TownNpcs.Buy(index, n, inv)) RefreshStackSplitCooldown();
        }
    }
}
