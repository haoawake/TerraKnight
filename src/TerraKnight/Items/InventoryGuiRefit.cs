using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static float refitUntil;
        static void DrawRefit(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind)
        {
            var panel = new Rect(view.x + 35f * ui, view.y + UnderBag(ui) + 18f * ui, 510f * ui, 146f * ui);
            panels.Add(panel); HomeFrame(panel, ui);
            Outlined(new Rect(panel.x + 14f * ui, panel.y + 8f * ui, 480f * ui, 25f * ui), Lang.T("武器改制"), HomeTextStyle(false), Color.white);
            var slot = new Rect(panel.x + 16f * ui, panel.y + 42f * ui, 44f * ui, 44f * ui);
            var item = TownNpcs.ReforgeItem;
            DrawSlot(slot, item, "Inventory_Back4", false); Add(slot, Kind.ReforgeSlot, 0);
            if (slot.Contains(mouse) && !item.IsEmpty) { hovered = item; hoveredKind = Kind.ReforgeSlot; }
            var text = new Rect(slot.xMax + 16f * ui, slot.y, 420f * ui, 25f * ui);
            if (item.IsEmpty)
            {
                Outlined(text, Lang.T("放入武器，在原版与新版之间改制"), HomeTextStyle(false), Color.white);
                Outlined(new Rect(text.x, text.y + 28f * ui, text.width, text.height), Lang.T("保留词缀和锻造；价格按武器阶段与稀有度计算"), HomeTextStyle(false), new Color(.75f,.8f,.9f));
                return;
            }
            bool allowed = WeaponRefitting.CanRefit(item.Def);
            string target = World.WeaponMode.NameOf(WeaponRefitting.Effective(item.WeaponVersion) ? World.WeaponKind.Vanilla : World.WeaponKind.New);
            Outlined(text, allowed ? WeaponRefitting.Name(item) + " → " + target : Lang.T("这件物品没有另一版本"), HomeTextStyle(false), Color.white);
            Outlined(new Rect(text.x, text.y + 25f * ui, text.width, text.height), Lang.T("花费") + "：" + Shop.Money(WeaponRefitting.Cost(item)), HomeTextStyle(false), new Color(1f,.85f,.5f));
            var button = new Rect(panel.x + 16f * ui, panel.yMax - 43f * ui, 44f * ui, 30f * ui);
            bool hover = button.Contains(mouse);
            var old = GUI.color; if (!allowed || Time.unscaledTime < refitUntil) GUI.color = Color.gray;
            GUI.DrawTexture(button, TerrariaAssets.Texture("UI/Reforge_" + (hover ? "1" : "0")), ScaleMode.ScaleToFit); GUI.color = old;
            Add(button, Kind.ReforgeButton, 0);
            Outlined(new Rect(button.xMax + 16f * ui, button.y, 420f * ui, 30f * ui), Lang.T("敲击改制 · 必定成功，武器数量不变"), HomeTextStyle(false), Color.white);
            if (hover) DrawLines(mouse, new List<string> { Lang.T("改制为") + target }, Color.white);
        }
        static bool RefitWeapon()
        {
            if (!TownNpcs.RefitMode || Time.unscaledTime < refitUntil) return false;
            if (!WeaponRefitting.Convert(ref TownNpcs.ReforgeItem)) { Notice(Lang.T("无法改制：请检查武器或吉欧是否足够")); return false; }
            refitUntil = Time.unscaledTime + .35f;
            Sound("Item_37"); HammerResult(TownNpcs.ReforgeItem, false);
            return true;
        }
        internal static void DevReleaseRefit() { refitUntil = 0; }
    }
}
