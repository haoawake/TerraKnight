using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static bool statsOpen;
        static int statsTab, statsScroll;
        internal static bool DevStatsOpen => statsOpen;
        internal static void DevStatsTab(int tab) { statsTab = tab; statsScroll = 0; }
        static readonly string[] statsTabs = { "综合属性", "手持武器", "生效装备", "更多属性与效果" };

        static void ToggleStats()
        {
            statsOpen = !statsOpen;
            if (statsOpen)
            {
                blacklistOpen = bestiaryOpen = hkOpen = sellMode = grid = false;
                EndBookSearch();
                if (World.Chests.IsOpen) World.Chests.Close(false);
                if (Town.TownNpcs.ShopOpen || Town.TownNpcs.ReforgeOpen) Town.TownNpcs.Close(false);
            }
            Sound(statsOpen ? "Menu_Open" : "Menu_Close");
        }

        static void DrawStatsIcon(Rect rect)
        {
            // Preserve the supplied PNG byte-for-byte. Select its visible artwork in UV
            // coordinates so its transparent outer margin does not shrink the button.
            var texture = TerrariaAssets.Texture("UtilityStats");
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(258f / 1254f, (1254f - 996f) / 1254f, 750f / 1254f, 695f / 1254f));
        }

        internal static Rect StatsPanelRect(Rect view, float ui)
        {
            float width = Mathf.Min(760f * ui, view.width - 24f);
            float height = Mathf.Min(490f * ui, view.height - 30f);
            return new Rect(view.center.x - width / 2f, view.center.y - height / 2f, width, height);
        }

        static void DrawStats(Rect view, float ui, Vector2 mouse)
        {
            var panel = StatsPanelRect(view, ui);
            panels.Add(panel); Add(panel, Kind.BestiaryPanel, 0);
            Fill(panel, new Color(.055f, .035f, .10f, .99f));
            Rim(panel, new Color(.68f, .42f, .92f), 2f * ui);
            var style = new GUIStyle(labelStyle) { fontSize = Mathf.Max(11, Mathf.RoundToInt(17f * ui)), wordWrap = false, clipping = TextClipping.Clip };
            float x = panel.x + 18f * ui, y = panel.y + 14f * ui;
            DrawStatsIcon(new Rect(x, y, 30f * ui, 28f * ui));
            Outlined(new Rect(x + 40f * ui, y, panel.width - 100f * ui, 28f * ui), "紫晶属性观测仪 · 当前实际属性", style, new Color(.87f, .74f, 1f));
            var close = new Rect(panel.xMax - 42f * ui, y, 28f * ui, 28f * ui);
            Outlined(close, "×", style, Color.white); Add(close, Kind.StatsButton, 0);
            y += 36f * ui;
            float tabWidth = (panel.width - 36f * ui) / statsTabs.Length;
            for (int i = 0; i < statsTabs.Length; i++)
            {
                var tab = new Rect(x + i * tabWidth, y, tabWidth - 4f * ui, 27f * ui);
                Fill(tab, i == statsTab ? new Color(.30f, .16f, .48f) : new Color(.12f, .09f, .19f));
                Outlined(tab, statsTabs[i], style, i == statsTab ? new Color(1f, .88f, .55f) : Color.white);
                Add(tab, Kind.StatsTab, i);
            }
            y += 36f * ui;
            var rows = PlayerStatReport.Capture(statsTab);
            float rowHeight = 24f * ui;
            int visible = Mathf.Max(1, Mathf.FloorToInt((panel.yMax - y - 46f * ui) / rowHeight));
            statsScroll = Mathf.Clamp(statsScroll, 0, Mathf.Max(0, rows.Count - visible));
            string hover = null;
            for (int i = 0; i < visible && i + statsScroll < rows.Count; i++)
            {
                var row = rows[i + statsScroll];
                var rect = new Rect(x, y + i * rowHeight, panel.width - 36f * ui, rowHeight);
                if (i % 2 == 0) Fill(rect, new Color(.14f, .10f, .21f));
                float labelWidth = rect.width * .44f;
                Outlined(new Rect(rect.x + 5f * ui, rect.y, labelWidth - 10f * ui, rect.height), row.Label, style, new Color(.76f, .70f, .84f));
                Outlined(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth - 6f * ui, rect.height), row.Value, style, Color.white);
                if (rect.Contains(mouse)) hover = row.Label + "\n" + row.Value;
            }
            float footer = panel.yMax - 34f * ui;
            var up = new Rect(x, footer, 76f * ui, 25f * ui);
            var down = new Rect(panel.xMax - 94f * ui, footer, 76f * ui, 25f * ui);
            Outlined(up, "上一页", style, statsScroll > 0 ? Color.white : Color.gray);
            Outlined(down, "下一页", style, statsScroll + visible < rows.Count ? Color.white : Color.gray);
            Add(up, Kind.StatsScroll, -visible); Add(down, Kind.StatsScroll, visible);
            Outlined(new Rect(x + 90f * ui, footer, panel.width - 220f * ui, 25f * ui),
                $"{statsScroll + 1}–{Mathf.Min(rows.Count, statsScroll + visible)} / {rows.Count} · 滚轮翻阅，装备变化实时更新", style, new Color(.66f, .60f, .73f));
            if (hover != null) DrawLines(mouse, new System.Collections.Generic.List<string>(hover.Split('\n')), Color.white);
        }
    }
}
