using System.Collections;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.4, recorder E, suite "p4_v5E_inv_zh"): the inventory's newer parts, in Dirtmouth. The information
    /// accessories' lines (a Cell Phone carried), as a row of icons with the bag open, three clicked off (greyed) and the
    /// lines gone when it shuts; the Ruler switched on at the bag's left and measured out to the pointer ("宽x高"); the
    /// vanity column: a Ninja set put on over the Solar Flare armour, the drawn character changing piece by piece; the
    /// Wiki button's tooltip and its note; 泰拉瑞亚设置: the guidance tab, then the interface tab's 界面缩放 dragged from 100% to
    /// 150% (the whole interface grows when it is let go) and 恢复 100%.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly System.Reflection.FieldInfo e5Notice = AccessTools.Field(typeof(InventoryGui), "notice");
        static readonly System.Reflection.FieldInfo e5NoticeUntil = AccessTools.Field(typeof(InventoryGui), "noticeUntil");

        /// <summary>The settings window's layout (IngameOptions.Draw) at the scale in use: its tabs, the interface page's parts.</summary>
        static Rect E5OptTab(int i)
        {
            var p = UI.IngameOptions.DevPanel;
            float ui = UI.GameView.Scale;
            float rx = p.x + 30f * ui + 380f * ui + 30f * ui, ry = p.y + 70f * ui;
            float[] x = { 0f, 200f, 320f, 500f }, w = { 190f, 110f, 170f, 120f };
            return new Rect(rx + x[i] * ui, ry, w[i] * ui, 34f * ui);
        }

        static Rect E5OptPage
        {
            get
            {
                var p = UI.IngameOptions.DevPanel;
                float ui = UI.GameView.Scale;
                float rx = p.x + 30f * ui + 380f * ui + 30f * ui, ry = p.y + 70f * ui + 44f * ui;
                return new Rect(rx, ry + 10f * ui, p.xMax - 30f * ui - rx, p.yMax - 110f * ui - ry);
            }
        }

        static Rect E5ScaleBar
        {
            get
            {
                var a = E5OptPage;
                float ui = UI.GameView.Scale;
                return new Rect(a.x + 260f * ui, a.y + 7f * ui, a.width - 260f * ui - 20f * ui, 16f * ui);
            }
        }

        static Rect E5ScaleReset
        {
            get
            {
                var a = E5OptPage;
                float ui = UI.GameView.Scale;
                return new Rect(a.x, a.y + 126f * ui, 220f * ui, 44f * ui);
            }
        }

        static Rect E5OptBack
        {
            get
            {
                var p = UI.IngameOptions.DevPanel;
                float ui = UI.GameView.Scale;
                return new Rect(p.center.x - 130f * ui, p.yMax - 76f * ui, 260f * ui, 50f * ui);
            }
        }

        static Vector2 E5ScaleAt(float scale)
        {
            var b = E5ScaleBar;
            return new Vector2(b.x + b.width * Mathf.InverseLerp(UI.GameView.MinUiScale, UI.GameView.MaxUiScale, scale), b.center.y);
        }

        IEnumerator P4_v5E_inv(HeroController hero)
        {
            yield return E5OrdinaryKit();
            var inv = mod.Inventory;
            // A Cell Phone carried (every information line), the Ninja set to wear as vanity.
            inv.Slots[40] = ItemStack.Of(ItemType.CellPhone);
            inv.Slots[30] = ItemStack.Of(ItemType.NinjaHood);
            inv.Slots[31] = ItemStack.Of(ItemType.NinjaShirt);
            inv.Slots[32] = ItemStack.Of(ItemType.NinjaPants);
            for (int i = 0; i < Inventory.ArmorSlots; i++) inv.VanityArmor[i] = default;
            float ui0 = UI.GameView.WantedScale;
            UI.GameView.SetUiScale(1f);
            mod.GrantedAbilities.Add(BossMastery.Initialized);
            foreach (var r in new[] { ItemType.RelicFalseKnight, ItemType.RelicBigFly, ItemType.RelicMawlek }) mod.GrantedAbilities.Add(BossMastery.Prefix + r);
            E5PlayerName("泰拉旅人");
            yield return E5ToTown(5f);
            hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 Near(float dx, float dy) => GuiOf((Vector2)col.bounds.center + new Vector2(dx, dy));
            yield return new WaitForSeconds(1f);

            // ---- The information accessories.
            E5PointerOn(Near(2f, 1.5f));
            yield return E5Rec("inv_info");
            yield return E5Wait(1.4f);
            yield return E5Glide(new Vector2(Screen.width * 0.8f, Screen.height * 0.36f), 0.8f);
            yield return E5Wait(0.6f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return E5Wait(0.6f);
            var icons = InventoryGui.DevHitIndexes("InfoToggle");
            E5Log($"信息图标 {icons.Count} 个：{string.Join(",", icons)}；行 {string.Join(" | ", UI.InfoAccessories.DevLines)}");
            for (int k = 0; k < icons.Count && k < 4; k++)
            {
                var r = InventoryGui.DevHitRect("InfoToggle", icons[k]);
                if (r != null) { yield return E5Glide(r.Value.center, k == 0 ? 0.6f : 0.3f); yield return E5Wait(0.45f); }
            }
            foreach (int k in new[] { 1, 3, 5 })
            {
                if (k >= icons.Count) continue;
                yield return E5Hit("InfoToggle", icons[k], 0.35f, 0.2f);
                yield return E5Wait(0.5f);
            }
            yield return E5Glide(Near(2.5f, 1.5f), 0.7f);
            InventoryGui.Open = false;
            mod.PlaySound("Menu_Close", 0.7f);
            yield return E5Wait(1.8f);
            StopRec();
            // The lines back on (off the record).
            foreach (int k in icons) if (UI.InfoAccessories.Hidden(k)) UI.InfoAccessories.ToggleHidden(k);

            // ---- The Ruler.
            yield return E5Rec("inv_ruler");
            yield return E5Wait(0.3f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return E5Wait(0.5f);
            var ruler = InventoryGui.DevHitRect("BuilderToggle", 0);
            if (ruler != null)
            {
                yield return E5Glide(ruler.Value.center, 0.8f);
                yield return E5Wait(0.8f);
                e5EachFrame = () => UI.BuilderToggles.DevMouse = E5World(UI.TerraCursor.DevAt ?? Vector2.zero);
                e5EachFrame();
                UI.TerraCursor.DevClick();
                yield return null;
                e5EachFrame();
                yield return null;
                for (int i = 0; i < E5Frames(0.9f); i++) { e5EachFrame(); yield return null; }
            }
            E5Log($"标尺 {UI.BuilderToggles.RulerOn}");
            e5EachFrame = () => UI.BuilderToggles.DevMouse = E5World(UI.TerraCursor.DevAt ?? Vector2.zero);
            yield return E5Glide(Near(2.5f, 0.8f), 0.8f);
            InventoryGui.Open = false;
            mod.PlaySound("Menu_Close", 0.7f);
            e5EachFrame();
            yield return E5Glide(Near(4.5f, 0.2f), 0.8f);
            yield return E5Wait(0.5f);
            foreach (var (dx, dy) in new[] { (6.5f, 3.5f), (2f, 5f), (-4.5f, 2.5f), (-7f, -0.8f), (3.5f, -1.6f) })
            {
                yield return E5Glide(Near(dx, dy), 0.9f);
                e5EachFrame();
                for (int i = 0; i < E5Frames(0.7f); i++) { e5EachFrame(); yield return null; }
                E5Log($"标尺 量到 {UI.BuilderToggles.DevMeasure}");
            }
            StopRec();
            e5EachFrame = null;
            UI.BuilderToggles.DevMouse = null;
            if (UI.BuilderToggles.RulerOn) UI.BuilderToggles.Toggle(0);

            // ---- The vanity column.
            yield return E5Rec("inv_vanity");
            yield return E5Wait(0.3f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return E5Wait(0.6f);
            for (int i = 0; i < 3; i++)
            {
                yield return E5Hit("Slot", 30 + i, i == 0 ? 0.7f : 0.5f, 0.25f);
                yield return E5Wait(0.25f);
                yield return E5Hit("VanityArmor", i, 0.6f, 0.2f);
                yield return E5Wait(0.7f);
            }
            E5Log($"时装栏 {inv.VanityArmor[0].Def?.Name}/{inv.VanityArmor[1].Def?.Name}/{inv.VanityArmor[2].Def?.Name}，光标上 {(inv.Mouse.IsEmpty ? "空" : inv.Mouse.Def.Name)}");
            // Over the worn armour's slot and its vanity one (their tooltips).
            var arm = InventoryGui.DevHitRect("Armor", 1);
            if (arm != null) { yield return E5Glide(arm.Value.center, 0.5f); yield return E5Wait(1.0f); }
            var van = InventoryGui.DevHitRect("VanityArmor", 1);
            if (van != null) { yield return E5Glide(van.Value.center, 0.4f); yield return E5Wait(1.0f); }
            yield return E5Glide(Near(2.5f, 1.6f), 0.7f);
            InventoryGui.Open = false;
            mod.PlaySound("Menu_Close", 0.7f);
            yield return E5Wait(0.4f);
            // A few steps in the new look.
            Player.TerraMovement.DevMove = -1;
            for (int i = 0; i < E5Frames(1.0f); i++) { UI.TerraCursor.DevAt = Near(-2.5f, 1.4f); yield return null; }
            Player.TerraMovement.DevMove = 1;
            for (int i = 0; i < E5Frames(1.0f); i++) { UI.TerraCursor.DevAt = Near(2.5f, 1.4f); yield return null; }
            Player.TerraMovement.DevMove = 0;
            yield return E5Wait(0.8f);
            StopRec();

            // ---- The Wiki button.
            yield return E5Rec("inv_wiki");
            yield return E5Wait(0.3f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return E5Wait(0.5f);
            var wiki = InventoryGui.DevHitRect("Settings", 2);
            if (wiki != null)
            {
                yield return E5Glide(wiki.Value.center, 0.9f);
                yield return E5Wait(1.6f);
                // Its press: the note (the browser itself not opened on this machine).
                e5Notice?.SetValue(null, Lang.T("已在浏览器中打开 Wiki"));
                e5NoticeUntil?.SetValue(null, Time.unscaledTime + 3f);
                mod.PlaySound("Menu_Open", 0.7f);
                yield return E5Wait(2.0f);
            }
            StopRec();

            // ---- 泰拉瑞亚设置: the guidance tab, the interface scale.
            yield return E5Rec("guide_settings");
            yield return E5Wait(0.2f);
            yield return E5Hit("Settings", 1, 0.7f, 0.25f);
            yield return E5Wait(0.8f);
            E5Log($"设置开 {UI.IngameOptions.Open}");
            if (UI.IngameOptions.Open)
            {
                yield return E5GlidePress(E5OptTab(2).center, 0.8f, 0.25f);
                yield return E5Wait(0.6f);
                var page = E5OptPage;
                yield return E5Glide(new Vector2(page.x + page.width * 0.45f, page.y + page.height * 0.22f), 0.7f);
                yield return E5Wait(1.6f);
                // Its switch, from 关 to 开.
                float uiS = UI.GameView.Scale;
                yield return E5GlidePress(new Vector2(page.x + 190f * uiS, page.y + 22f * uiS), 0.6f, 0.3f);
                yield return E5Wait(0.4f);
                E5Log($"神的指引 开 {BossGuide.On}");
                yield return E5Glide(new Vector2(page.x + page.width * 0.4f, page.y + 175f * uiS), 0.7f);
                yield return E5Wait(1.6f);
                StopRec();
                yield return E5Rec("inv_uiscale");
                yield return E5Wait(0.2f);
                yield return E5GlidePress(E5OptTab(3).center, 0.6f, 0.25f);
                yield return E5Wait(0.8f);
                // The slider from 100% to 150%, held, let go: the whole interface grows.
                yield return E5Glide(E5ScaleAt(1f), 0.7f);
                yield return E5Wait(0.3f);
                UI.TerraCursor.DevHeld = true;
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                var from = E5ScaleAt(1f);
                var to = E5ScaleAt(1.5f);
                int n = E5Frames(1.3f);
                for (int i = 1; i <= n; i++)
                {
                    UI.TerraCursor.DevAt = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, i / (float)n));
                    yield return null;
                }
                yield return E5Wait(0.4f);
                UI.TerraCursor.DevHeld = false;
                yield return null;
                yield return null;
                E5Log($"界面缩放 {UI.GameView.WantedScale}（实际 {UI.GameView.Scale}）");
                yield return E5Wait(0.6f);
                // The bigger window looked over, then back to 100%.
                yield return E5Glide(E5ScaleReset.center + new Vector2(60f, -90f), 0.8f);
                yield return E5Wait(1.2f);
                yield return E5GlidePress(E5ScaleReset.center, 0.6f, 0.3f);
                yield return E5Wait(1.2f);
                E5Log($"界面缩放 恢复后 {UI.GameView.WantedScale}");
                yield return E5GlidePress(E5OptBack.center, 0.7f, 0.25f);
                yield return E5Wait(0.6f);
                StopRec();
            }
            else StopRec();
            if (UI.IngameOptions.Open) UI.IngameOptions.Hide();
            InventoryGui.Open = false;
            UI.GameView.SetUiScale(ui0);

            // ---- Back in the game: the Voice, listened to again, points the way on.
            if (BossGuide.On)
            {
                int said0 = BossGuide.DevSaid;
                yield return E5Rec("guide_rejoin");
                float t0 = Time.time;
                while (Time.time - t0 < 30f)
                {
                    float since = Time.time - t0;
                    Player.TerraMovement.DevMove = since > 1f && since < 2.4f ? 1 : 0;
                    UI.TerraCursor.DevAt = Vector2.Lerp(UI.TerraCursor.DevAt ?? Near(2.5f, 1.5f), Near(2.8f, 1.5f + Mathf.Sin(since * 0.6f) * 0.5f), 1f - Mathf.Exp(-3f * Time.deltaTime));
                    if (since > 3f && BossGuide.DevSaid - said0 >= 2 && !BossGuide.DevTalking) break;
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(0.8f);
                StopRec();
                E5Log($"神的指引 重新开启：说了 {BossGuide.DevSaid - said0} 句，最后「{BossGuide.DevLast}」");
            }
            E5PlayerName(null);
            E5PointerOff();
        }
    }
}
