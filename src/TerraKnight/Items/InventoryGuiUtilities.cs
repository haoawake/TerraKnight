using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static bool blacklistOpen;
        static int blacklistPage, utilityHover = -1;
        const int BlacklistPageSize = 40;
        internal static bool DevBlacklistOpen => blacklistOpen;
        internal static void DevBlacklistClick(int index, bool right = false) => ActBlacklist(index, right);
        internal static void DevUtilityClick(int index) => Act(new Hit
        {
            Kind = index == 4 ? Kind.StatsButton : index == 0 ? Kind.BlacklistButton : index == 1 ? Kind.BestiaryButton : index == 2 ? Kind.HkButton : Kind.SellButton,
            Index = 0,
        }, true, false, false, false);
        internal static void ClearUtilities() { blacklistOpen = statsOpen = false; blacklistPage = 0; utilityHover = -1; statsScroll = 0; }
        static readonly string[] utilityTextures = { "UtilityBlacklist", "UtilityBestiary", "UtilityHallownest", "UtilityQuickShop", "UtilityStats" };
        static readonly string[] utilityNames = { "掉落物黑名单", "怪物图鉴", "圣巢背包", "快捷商店", "紫晶属性观测仪" };

        internal static Rect UtilityButtonRect(Rect view, float ui, int index)
        {
            var tex = TerrariaAssets.Texture("UI/ChestStack_0");
            float x = view.x + RightOfAmmo(ui);
            if (index >= 4) { x += (index - 3) * (tex.width + 8f) * ui; index = 0; }
            // One button beside each ammo slot, centered on the same row.
            float y = view.y + (Margin + 18f + (index + 1) * SlotStep + (SlotSize - tex.height) / 2f) * ui;
            return new Rect(x, y, tex.width * ui, tex.height * ui);
        }

        static void DrawUtilityButtons(Rect view, float ui, Vector2 mouse)
        {
            int over = -1;
            for (int i = 0; i < utilityTextures.Length; i++)
            {
                var r = UtilityButtonRect(view, ui, i);
                bool hover = r.Contains(mouse);
                bool active = i == 4 ? statsOpen : i == 0 ? blacklistOpen : i == 1 ? bestiaryOpen : i == 2 ? hkOpen : sellMode;
                var prev = GUI.color;
                if (hover || active) GUI.color = new Color(1f, .88f, .5f);
                if (i == 4) DrawStatsIcon(r);
                else GUI.DrawTexture(r, TerrariaAssets.Texture(utilityTextures[i]), ScaleMode.StretchToFill);
                GUI.color = prev;
                Add(r, i == 4 ? Kind.StatsButton : i == 0 ? Kind.BlacklistButton : i == 1 ? Kind.BestiaryButton : i == 2 ? Kind.HkButton : Kind.SellButton, 0);
                if (!hover) continue;
                over = i;
                var lines = new List<string> { Lang.T(utilityNames[i]) };
                if (i == 0) lines.Add(Lang.T("名单中的掉落物拾取时自动出售"));
                if (i == 3) lines.Add(Lang.T("点击背包物品出售，可在出售栏按原价买回"));
                if (i == 4) lines.Add("查看含锻造、词缀与当前增益的人物属性");
                DrawLines(mouse, lines, Color.white);
            }
            if (over >= 0 && over != utilityHover) Sound("Menu_Tick");
            utilityHover = over;
        }

        static void ToggleBlacklist()
        {
            blacklistOpen = !blacklistOpen;
            if (blacklistOpen)
            {
                statsOpen = bestiaryOpen = hkOpen = sellMode = false;
                grid = false;
                if (World.Chests.IsOpen) World.Chests.Close(false);
                if (Town.TownNpcs.ShopOpen || Town.TownNpcs.ReforgeOpen) Town.TownNpcs.Close(false);
            }
            Sound(blacklistOpen ? "Menu_Open" : "Menu_Close");
        }

        static List<ItemType> BlacklistEntries()
        {
            var list = new List<ItemType>(inv.DropBlacklist);
            list.Sort();
            return list;
        }

        static void ActBlacklist(int index, bool right)
        {
            if (!blacklistOpen) return;
            var entries = BlacklistEntries();
            int last = entries.Count / BlacklistPageSize; // Keep an empty page for new entries.
            if (index == -1) { blacklistPage = Mathf.Max(0, blacklistPage - 1); Sound("Menu_Tick"); return; }
            if (index == -2) { blacklistPage = Mathf.Min(last, blacklistPage + 1); Sound("Menu_Tick"); return; }
            if (!right && !inv.Mouse.IsEmpty)
            {
                if (!LootBlacklist.Add(inv, inv.Mouse, out var why)) Notice(Lang.T(why));
                else Sound("Menu_Tick");
                return;
            }
            int at = blacklistPage * BlacklistPageSize + index;
            if (at < 0 || at >= entries.Count) return;
            inv.DropBlacklist.Remove(entries[at]);
            blacklistPage = Mathf.Min(blacklistPage, inv.DropBlacklist.Count / BlacklistPageSize);
            Sound("Menu_Tick");
        }

        /// <summary>
        /// From the panel's top to the bottom of the bag's trash slot (the last thing of the bag's): where a panel under
        /// the bag starts at the earliest, at every interface scale.
        /// </summary>
        static float UnderBag(float ui) => (Margin + 18f + Inventory.Rows * SlotStep + SlotSize) * ui;

        /// <summary>
        /// A plain or Shift click (no Ctrl or favourite key, nothing on the cursor) while the blacklist is open:
        /// its kind goes on the list, the item staying in the bag. Every other panel (the shops, the sell list, the forge
        /// and reforge slots, the Guide's) takes items the usual way: picked up, then put in. False: the click is the bag's own.
        /// </summary>
        static bool PanelTakesClick(int i, ref ItemStack slot)
        {
            if (!inv.Mouse.IsEmpty || slot.IsEmpty || Ctrl || Alt || !blacklistOpen) return false;
            BlacklistFromBag(slot);
            return true;
        }

        static void BlacklistFromBag(ItemStack stack)
        {
            if (inv.DropBlacklist.Contains(stack.Type)) Notice(Lang.T("已在黑名单中"));
            else if (!LootBlacklist.Add(inv, stack, out var why))
            {
                Notice(Lang.T(why));
                return;
            }
            else Sound("Menu_Tick");
            // Its page shown.
            int at = BlacklistEntries().IndexOf(stack.Type);
            if (at >= 0) blacklistPage = at / BlacklistPageSize;
        }

        static void DrawBlacklist(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string extra, ref Kind kind)
        {
            float step = SlotStep * ui, size = SlotSize * ui;
            float x = view.x + Margin * ui, y = view.y + UnderBag(ui) + 14f * ui;
            var panel = new Rect(x - 8f * ui, y - 8f * ui, 10f * step + 16f * ui, 4f * step + 102f * ui);
            panels.Add(panel);
            Add(panel, Kind.BestiaryPanel, 0);
            Fill(panel, new Color(.07f, .06f, .13f, .98f));
            Rim(panel, new Color(.48f, .4f, .68f), 2f * ui);
            Outlined(new Rect(x, y, panel.width, 24f * ui), Lang.T("掉落物黑名单 · 拾取时自动出售"), labelStyle, new Color(1f, .85f, .45f));
            Outlined(new Rect(x, y + 26f * ui, panel.width, 22f * ui), Lang.T("左键或Shift+左键添加；Ctrl+左键出售；点名单移除"), labelStyle, Color.white);
            var entries = BlacklistEntries();
            int last = entries.Count / BlacklistPageSize;
            blacklistPage = Mathf.Clamp(blacklistPage, 0, last);
            y += 54f * ui;
            for (int i = 0; i < BlacklistPageSize; i++)
            {
                int at = blacklistPage * BlacklistPageSize + i;
                var stack = at < entries.Count ? ItemStack.Of(entries[at]) : default;
                var rect = new Rect(x + i % 10 * step, y + i / 10 * step, size, size);
                DrawSlot(rect, stack, "Inventory_Back6", false);
                Add(rect, Kind.BlacklistEntry, i);
                if (!rect.Contains(mouse)) continue;
                hovered = stack;
                kind = Kind.BlacklistEntry;
                extra = Lang.T("名单只记录类型，不消耗添加的物品；之后捡到时自动出售")
                    + (!stack.IsEmpty ? "\n" + Lang.F("每件售价：{0}", Shop.Money(Shop.SellValue(stack))) : "");
            }
            float footer = y + 4f * step + 2f * ui;
            var prev = new Rect(x, footer, 70f * ui, 24f * ui);
            var next = new Rect(x + 170f * ui, footer, 70f * ui, 24f * ui);
            Outlined(prev, Lang.T("上一页"), labelStyle, blacklistPage > 0 ? Color.white : Color.gray);
            Outlined(next, Lang.T("下一页"), labelStyle, blacklistPage < last ? Color.white : Color.gray);
            Outlined(new Rect(x + 78f * ui, footer, 90f * ui, 24f * ui), $"{blacklistPage + 1} / {last + 1}", labelStyle, Color.white);
            Add(prev, Kind.BlacklistEntry, -1);
            Add(next, Kind.BlacklistEntry, -2);
        }
    }
}
