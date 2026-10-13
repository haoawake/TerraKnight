using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's information accessories (Main.DrawInfoAccs), for now the Radar: carried anywhere
    /// in the inventory or worn, it shows under the hearts how many enemies are near. Every 15
    /// ticks it counts the hostile enemies (ones that hurt on touch, more than 5 life, not the
    /// harmless kinds) whose centre is within 2000 px of the player: "附近有N个敌人！", "附近有1个敌人！"
    /// or, in grey, "附近没有敌人". Drawn while the inventory is closed, icon (UI/InfoIcon_5) then text.
    /// </summary>
    internal static class InfoAccessories
    {
        /// <summary>accThirdEye's radius: 2000 px.</summary>
        const float RadarRange = 2000f / 32f;
        static int number;
        static float nextCount;
        static GUIStyle style;

        /// <summary>Dev: the last count.</summary>
        public static int DevEnemiesNear => number;

        public static bool HasRadar(Inventory inv)
        {
            foreach (var s in inv.Slots) if (s.Type == ItemType.Radar && !s.IsEmpty) return true;
            foreach (var s in inv.Accessories) if (s.Type == ItemType.Radar && !s.IsEmpty) return true;
            return false;
        }

        /// <summary>Every frame in play: the count, refreshed every 15 ticks (a quarter second).</summary>
        public static void Tick(Inventory inv, HeroController hero)
        {
            if (hero == null || !HasRadar(inv))
            {
                nextCount = 0f;
                return;
            }
            if (Time.time < nextCount) return;
            nextCount = Time.time + 15f / 60f;
            number = Count(hero.transform.position);
        }

        public static int Count(Vector2 from)
        {
            int n = 0;
            foreach (var info in EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || !info.Harmful || EnemyStats.LifeMax(info) <= 5) continue;
                if (Vector2.Distance(hm.transform.position, from) < RadarRange) n++;
            }
            return n;
        }

        public static void Draw(Inventory inv, TerraStats stats)
        {
            if (Event.current.type != EventType.Repaint || InventoryGui.Open || !HasRadar(inv)) return;
            var view = GameView.Rect;
            float ui = GameView.Scale;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.RoundToInt(18 * ui);
            // Under the hearts (ResourceHud: rows of ten, 26 px apart, from 40 px under the life text).
            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.CeilToInt(stats.LifeMax / 20f) / 10f));
            float y = view.y + (16f + 40f + rows * 26f + 6f) * ui;
            float x = view.xMax - 280f * ui;
            var icon = TerrariaAssets.Texture("UI/InfoIcon_5");
            if (icon != null) GUI.DrawTexture(new Rect(x, y, icon.width * ui, icon.height * ui), icon);
            string text = number == 0 ? Lang.T("附近没有敌人") : number == 1 ? Lang.T("附近有1个敌人！") : Lang.F("附近有{0}个敌人！", number);
            var color = number == 0 ? new Color(100 / 255f, 100 / 255f, 100 / 255f) : Color.white;
            var r = new Rect(x + 20f * ui + (icon != null ? icon.width * ui - 14f * ui : 0f), y - 2f * ui, 260f * ui, 26f * ui);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }
    }
}
