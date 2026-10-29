using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "panedrag", the user's 10-29 "哪都是家会挡住合成表，让所有界面改为可拖动的"): the home storage opened beside the
    /// crafting column covers none of its recipes; pressed on its title strip and dragged by the dev pointer it follows, its
    /// slots with it, and a slot clicked where it now is takes its item; the offset is kept in the config; a double click on
    /// it puts it back. The same drag on the Amethyst Stat Viewer and the crafting column's title. Lines "[AutoTest] PaneDrag …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PaneDragTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int pass = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            { total++; if (ok) pass++; Plugin.Log.LogInfo($"[AutoTest] PaneDrag {name}：{detail} {(ok ? "通过" : "不通过")}"); }
            Player.TerraStats.DevImmortal = true; PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1"); yield return new WaitForSeconds(3f);
            var hero = HeroController.instance; var inv = mod.Inventory;
            var body = hero.GetComponent<Rigidbody2D>(); var constraints = body.constraints;
            var oldStations = Stations.Save(); var oldChests = Chests.Save(); var oldSlots = (ItemStack[])inv.Slots.Clone();
            int stage = Shop.DevStage; bool infinite = inv.Infinite;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            Player.TerraControls.DevNoRealKeys = true; WorldItems.DevNoGrab = true;
            InventoryGui.DevResetPanes();
            float ui = UI.GameView.Scale;
            IEnumerator Drag(Vector2 from, Vector2 by)
            {
                UI.TerraCursor.DevDrives = true;
                UI.TerraCursor.DevAt = from;
                yield return null;
                UI.TerraCursor.DevClick();
                UI.TerraCursor.DevHeld = true;
                yield return null;
                for (int k = 1; k <= 20; k++) { UI.TerraCursor.DevAt = from + by * (k / 20f); yield return null; }
                yield return null;
                UI.TerraCursor.DevHeld = false;
                yield return new WaitForSeconds(.2f);
            }
            IEnumerator Click(Vector2 at)
            {
                UI.TerraCursor.DevDrives = true;
                UI.TerraCursor.DevAt = at;
                yield return null;
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
            }
            bool Near(Vector2 a, Vector2 b) => (a - b).magnitude < 2f;
            try
            {
                Stations.Clear(); Chests.Clear(); inv.Clear(); inv.Mouse = default;
                inv.Infinite = false; TerraMod.DevAllInfinite = false; mod.ApplyCreative(false); Shop.DevStage = 5;
                const float cs = CellGrid.CellSize;
                int gx = 60, gy = 22;
                for (int x = gx - 12; x <= gx + 18; x++) for (int y = gy; y <= gy + 9; y++) TerraWorld.Set(x, y, y == gy ? CellKind.Stone : CellKind.Empty);
                string here = TerraWorld.Live.SceneName;
                var origin = (Vector2)TerraWorld.Live.Root.position;
                var a = new Vector2((gx + 1.5f) * cs, (gy + 1) * cs) - origin;
                Stations.PlaceIn(StationKind.HomeEverywhere, here, a);
                var home = Stations.Homes().Single(); Stations.ChangeHome(home.Key, 22, "绿洲仓库");
                var stored = new ItemStack[40]; stored[0] = ItemStack.Of(ItemType.IronBar, 7);
                Chests.AddWorld(here, a + Vector2.left * 2f, 0, stored);
                hero.transform.position = Clear(hero, origin + a + Vector2.left * 2f + Vector2.up * 2f);
                body.linearVelocity = Vector2.zero;
                // By-hand recipes for the crafting column.
                inv.Slots[12] = ItemStack.Of(ItemType.Wood, 50); inv.Slots[13] = ItemStack.Of(ItemType.Gel, 30);
                yield return new WaitForSeconds(1f);

                InventoryGui.Open = true; yield return new WaitForSeconds(.3f);
                InventoryGui.DevHomeClick("HomeButton", 0); yield return new WaitForSeconds(.3f);
                if (!InventoryGui.HomeMergedVisible) { InventoryGui.DevHomeClick("HomeColor", 22); yield return new WaitForSeconds(.3f); }
                var panel = InventoryGui.DevHomePanelRect;
                int recipes = 0, covered = 0;
                for (int i = 0; i < 200; i++)
                {
                    var r = InventoryGui.DevRectOf("Recipe", i);
                    if (!r.HasValue) continue;
                    recipes++;
                    if (r.Value.Overlaps(panel)) covered++;
                }
                Check("仓库打开时不挡制作栏", InventoryGui.HomeMergedVisible && recipes > 0 && covered == 0, $"仓库 {panel}，配方 {recipes} 个，被挡 {covered}");
                Shot(here, "panedrag_default"); yield return new WaitForSeconds(.2f);

                // Its title strip, between the title and the buttons, dragged down and to the right.
                var slot0 = InventoryGui.DevRectOf("HomeSlot", 0);
                var by = new Vector2(260f, 90f) * ui;
                yield return Drag(new Vector2(panel.x + panel.width * 0.45f, panel.y + 5f * ui), by);
                var moved = InventoryGui.DevHomePanelRect;
                var slot0b = InventoryGui.DevRectOf("HomeSlot", 0);
                Check("拖动仓库跟着鼠标走", Near(moved.position, panel.position + by), $"{panel.position} → {moved.position}（拖了 {by}，{InventoryGui.DevLastGrab}）");
                Check("格子一起移动", slot0.HasValue && slot0b.HasValue && Near(slot0b.Value.position, slot0.Value.position + by),
                    slot0.HasValue && slot0b.HasValue ? $"{slot0.Value.position} → {slot0b.Value.position}" : "没有格子");
                string saved = InventoryGui.DevPaneSetting;
                Check("位置记进配置", saved != null && saved.Contains("Home="), saved ?? "没有配置");
                Shot(here, "panedrag_moved"); yield return new WaitForSeconds(.2f);
                // A slot clicked where it is now takes its item.
                if (slot0b.HasValue) yield return Click(slot0b.Value.center);
                yield return new WaitForSeconds(.2f);
                Check("移动后点格子照常拿起物品", inv.Mouse.Type == ItemType.IronBar && inv.Mouse.Count == 7, $"光标上 {inv.Mouse.Type} ×{inv.Mouse.Count}");
                if (slot0b.HasValue) yield return Click(slot0b.Value.center);
                yield return new WaitForSeconds(.2f);
                inv.Mouse = default;
                // A double click on the title strip puts it back.
                var title = new Vector2(moved.x + moved.width * 0.45f, moved.y + 5f * ui);
                yield return Click(title);
                yield return Click(title);
                yield return new WaitForSeconds(.2f);
                var back = InventoryGui.DevHomePanelRect;
                Check("双击还原位置", Near(back.position, panel.position), $"{back.position}，原 {panel.position}，配置 {InventoryGui.DevPaneSetting}");

                // The Amethyst Stat Viewer by its background.
                InventoryGui.DevUtilityClick(4); yield return new WaitForSeconds(.3f);
                var tab = InventoryGui.DevRectOf("StatsTab", 0);
                var stats = InventoryGui.StatsPanelRect(UI.GameView.Rect, ui);
                if (tab.HasValue)
                {
                    // Its bottom-left corner, inside the rim.
                    var grip = new Vector2(stats.x + 6f * ui, stats.yMax - 6f * ui);
                    var by2 = new Vector2(-180f, 60f) * ui;
                    yield return Drag(grip, by2);
                    var tab2 = InventoryGui.DevRectOf("StatsTab", 0);
                    Check("属性观测仪可拖动", tab2.HasValue && Near(tab2.Value.position, tab.Value.position + by2), (tab2.HasValue ? $"{tab.Value.position} → {tab2.Value.position}" : "没有") + $"（{InventoryGui.DevLastGrab}，按在 {InventoryGui.DevHitAt(grip)}）");
                }
                else Check("属性观测仪可拖动", false, "没打开");
                InventoryGui.DevUtilityClick(4); yield return new WaitForSeconds(.3f);

                // The crafting column by its title.
                if (InventoryGui.HomeMergedVisible) InventoryGui.DevHomeClick("HomeAction", 0);
                yield return new WaitForSeconds(.3f);
                var recipe = Enumerable.Range(0, 200).Select(i => InventoryGui.DevRectOf("Recipe", i)).FirstOrDefault(r => r.HasValue);
                var toggle = InventoryGui.DevRectOf("GridToggle", 0);
                if (recipe.HasValue && toggle.HasValue)
                {
                    // "制作" sits left of the toggle, a little above it.
                    var label = new Vector2(toggle.Value.center.x - 10f * ui, toggle.Value.center.y - 30f * ui);
                    var by3 = new Vector2(120f, -40f) * ui;
                    yield return Drag(label, by3);
                    // A recipe's slot (the toggle grows under the pointer).
                    var recipe2 = Enumerable.Range(0, 200).Select(i => InventoryGui.DevRectOf("Recipe", i)).FirstOrDefault(r => r.HasValue);
                    Check("制作栏可拖动", recipe2.HasValue && Near(recipe2.Value.position, recipe.Value.position + by3), recipe2.HasValue ? $"{recipe.Value.position} → {recipe2.Value.position}" : "没有");
                    Shot(here, "panedrag_craft"); yield return new WaitForSeconds(.2f);
                }
                else Check("制作栏可拖动", false, "没有配方");
            }
            finally
            {
                UI.TerraCursor.DevDrives = false; UI.TerraCursor.DevAt = null; UI.TerraCursor.DevHeld = false;
                InventoryGui.DevResetPanes();
                InventoryGui.Open = false;
                Stations.Load(oldStations); Chests.Load(oldChests);
                for (int i = 0; i < oldSlots.Length; i++) inv.Slots[i] = oldSlots[i];
                inv.Mouse = default; inv.Infinite = infinite; Shop.DevStage = stage;
                body.constraints = constraints; Player.TerraControls.DevNoRealKeys = false; WorldItems.DevNoGrab = false;
                Player.TerraStats.DevImmortal = false; mod.DevNoFallDamage = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] PaneDrag 合计 {pass}/{total}");
        }
    }
}
