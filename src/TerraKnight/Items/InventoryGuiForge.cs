using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Goblin Tinkerer's forging (Forging): his "Forge" button opens the inventory with his slot under it, as his
    /// reforging does, for a weapon only. Beside it: its level and the stage's cap, its damage now and after, the odds,
    /// the price; the hammer under it forges (Item_37 when it takes, a dull clank when it fails).
    /// </summary>
    internal static partial class InventoryGui
    {
        static bool forgeHover;
        static float forgeScale = 1f;

        static void DrawForge(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind)
        {
            float s = 52f * 0.85f * ui;
            var rect = new Rect(view.x + 50f * ui, view.y + 270f * ui, s, s);
            GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back4"));
            Add(rect, Kind.ReforgeSlot, 0);
            var item = TownNpcs.ReforgeItem;
            if (!item.IsEmpty) DrawIcon(rect, item, false);
            if (rect.Contains(mouse) && !item.IsEmpty)
            {
                hovered = item;
                hoveredKind = Kind.ReforgeSlot;
            }
            float n = ChatPulse / 255f;
            var color = new Color(n, n, n, 1f);
            var text = new Rect(rect.x + 50f * ui, rect.y - 22f * ui, 460f * ui, 24f * ui);
            int max = Forging.MaxLevel;
            if (item.IsEmpty)
            {
                Outlined(text, Lang.T("将武器放到此处锻造"), labelStyle, color);
                Outlined(new Rect(text.x, text.y + 24f * ui, text.width, text.height), Lang.F("锻造上限 +{0}（每解锁一个阶段多 5 级）", max), labelStyle, color);
                forgeHover = false;
                return;
            }
            var def = item.Def;
            int now = Equipment.Damage(def);
            var next = item;
            next.Forge = (byte)Mathf.Min(item.Forge + 1, 255);
            int after = Equipment.Damage(next.Def);
            bool atMax = item.Forge >= max;
            var lines = new List<string>
            {
                Lang.F("锻造 +{0} / +{1}", item.Forge, max),
                atMax ? Lang.T("已达上限：解锁下一个阶段后可以继续锻造") : Lang.F("伤害 {0} → {1}", now, after),
            };
            if (!atMax)
            {
                lines.Add(Lang.F("成功率 {0}%", Mathf.RoundToInt(Forging.Chance(item) * 100f)));
                lines.Add(TT("LegacyInterface.46", "花费") + ": " + Shop.Money(Forging.Cost(item)));
            }
            for (int i = 0; i < lines.Count; i++)
                Outlined(new Rect(text.x, text.y + i * 22f * ui, text.width, text.height), lines[i], labelStyle, i == 1 && atMax ? new Color(1f, 0.75f, 0.4f) : color);
            if (atMax) { forgeHover = false; return; }
            // The hammer, under the slot: grows while hovered.
            var tex = TerrariaAssets.Texture(forgeHover ? "UI/Reforge_1" : "UI/Reforge_0");
            forgeScale = Mathf.MoveTowards(forgeScale, forgeHover ? 1.2f : 1f, 0.02f);
            float w = tex.width * forgeScale * ui, h = tex.height * forgeScale * ui;
            var at = new Vector2(rect.center.x, rect.yMax + 26f * ui);
            var hammer = new Rect(at.x - w / 2f, at.y - h / 2f, w, h);
            var hit = new Rect(at.x - 15f * ui, at.y - 15f * ui, 30f * ui, 30f * ui);
            bool over = hit.Contains(mouse);
            if (over && !forgeHover) Sound("Menu_Tick");
            forgeHover = over;
            GUI.DrawTexture(hammer, tex);
            Add(hit, Kind.ForgeButton, 0);
            if (over) DrawLines(mouse, new List<string> { Lang.T("锻造") }, Color.white);
        }

        /// <summary>The hammer: paid, then the dice; the outcome over the player.</summary>
        static bool Forge()
        {
            if (!TownNpcs.ForgeMode) return false;
            ref var item = ref TownNpcs.ReforgeItem;
            var r = Forging.Try(ref item);
            var hero = HeroController.SilentInstance;
            Vector3 over = hero != null ? hero.transform.position + Vector3.up * 1.2f : Vector3.zero;
            switch (r)
            {
                case Forging.Result.Poor:
                    Notice(Lang.T("钱不够"));
                    return false;
                case Forging.Result.AtMax:
                case Forging.Result.NotWeapon:
                    return false;
                case Forging.Result.Failed:
                    if (hero != null) UI.CombatText.Show(over, Lang.T("锻造失败"), new Color(1f, 0.45f, 0.4f));
                    Sound("Tink_0");
                    return false;
                default:
                    if (hero != null) UI.CombatText.Show(over, Lang.F("锻造成功 +{0}", item.Forge), new Color(1f, 0.85f, 0.3f), true);
                    Sound("Item_37");
                    return true;
            }
        }

        /// <summary>Dev: a forging, as the hammer's click.</summary>
        public static bool DevForge() => Forge();
    }
}
