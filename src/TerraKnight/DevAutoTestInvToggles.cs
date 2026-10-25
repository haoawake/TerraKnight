using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "invtoggles"), in Dirtmouth: the inventory as Terraria's (the minimap up with it open, the equipment under
    /// it and the information icons' row; a click on an icon hiding its line; the builder toggles' Ruler, off at first,
    /// drawn when on); and the altars (one put back in its place though a platform was built over it; a Pwnhammer knocks
    /// it off in two blows as its item, gone from Dirtmouth after; a weaker hammer and a pickaxe do nothing to an altar;
    /// the item set down elsewhere and knocked off again). Shots: inv_open_full, ruler_full, altar_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator InvTogglesTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 背包开关 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            const int slot = Inventory.HotbarSize - 1;
            void Take(ItemType t) { inv.Slots[slot] = ItemStack.Of(t); inv.Selected = slot; }
            // An item on the ground or already picked up by the player standing by.
            int Have(ItemType t) => WorldItems.DevCount(t) + inv.Slots.Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);
            try
            {
                yield return GoTo("Town", "left1");
                for (float t = Time.realtimeSinceStartup + 10f; Stations.DevAltarBox(StationKind.DemonAltar, true) == null && Time.realtimeSinceStartup < t;) yield return null;
                hero = HeroController.instance;
                var col = hero.GetComponent<BoxCollider2D>();
                mod.SetInfiniteItems(false);

                // ---- the inventory open
                inv.Slots[30] = ItemStack.Of(ItemType.GPS);
                inv.Slots[31] = ItemStack.Of(ItemType.Radar);
                inv.Slots[32] = ItemStack.Of(ItemType.DPSMeter);
                inv.Slots[33] = ItemStack.Of(ItemType.Stopwatch);
                InventoryGui.Open = true;
                for (int k = 0; k < 6; k++) yield return null;
                int icons = Enumerable.Range(0, 13).Count(i => InventoryGui.DevRectOf("InfoToggle", i) != null);
                var watch = InventoryGui.DevRectOf("InfoToggle", 0);
                var armor = InventoryGui.DevRectOf("Armor", 0);
                Check(UI.WorldMap.MinimapVisible, "背包打开时小地图还在");
                Check(icons >= 5 && watch != null && armor != null && armor.Value.y > watch.Value.yMax,
                    $"信息栏图标 {icons} 个，装备栏在它们下面（图标 y {watch?.y:F0}，装备 y {armor?.y:F0}）");
                Check(InventoryGui.DevRectOf("BuilderToggle", 0) != null && !UI.BuilderToggles.RulerOn, "背包左侧有标尺开关，默认关");
                Shot(scene, "inv_open_full"); for (int f = 0; f < 4; f++) yield return null;

                // A line hidden by its icon: one line fewer with the bag closed.
                InventoryGui.Open = false;
                for (int k = 0; k < 4; k++) yield return null;
                int shown = UI.InfoAccessories.DevLines.Count;
                UI.InfoAccessories.ToggleHidden(0);
                for (int k = 0; k < 4; k++) yield return null;
                int hiddenOne = UI.InfoAccessories.DevLines.Count;
                Check(UI.InfoAccessories.Hidden(0) && hiddenOne == shown - 1, $"点手表图标隐藏时间那行：{shown} → {hiddenOne} 行");
                InventoryGui.Open = true;
                for (int k = 0; k < 4; k++) yield return null;
                Check(InventoryGui.DevRectOf("InfoToggle", 0) != null, "隐藏的那行在背包里仍有图标（变灰），可以再点开");
                Shot(scene, "inv_hidden_full"); for (int f = 0; f < 4; f++) yield return null;
                InventoryGui.Open = false;
                UI.InfoAccessories.ToggleHidden(0);

                // The ruler, on.
                UI.BuilderToggles.Toggle(0);
                Vector2 c = col.bounds.center;
                UI.BuilderToggles.DevMouse = c + new Vector2(3.1f, 1.6f);
                int drawn0 = UI.BuilderToggles.DevDrawn;
                for (int k = 0; k < 10; k++) yield return null;
                var measure = UI.BuilderToggles.DevMeasure;
                Check(UI.BuilderToggles.RulerOn && UI.BuilderToggles.DevDrawn > drawn0 && measure.x >= 6 && measure.y >= 3, $"标尺开：画出标尺，量出 {measure.x}x{measure.y}");
                yield return CloseUp(scene, "ruler", hero, 120, 2);
                UI.BuilderToggles.Toggle(0);
                UI.BuilderToggles.DevMouse = null;

                // ---- the altars: a platform built over one doesn't move it at the next load.
                var demon = Stations.DevAltarBox(StationKind.DemonAltar, true);
                var crimson = Stations.DevAltarBox(StationKind.CrimsonAltar, true);
                Check(demon != null && crimson != null, $"德特茅斯两个祭坛都在（恶魔 {demon?.center:F1}，猩红 {crimson?.center:F1}）");
                if (demon == null || crimson == null) yield break;
                var d = demon.Value;
                var platform = new System.Collections.Generic.List<Vector2Int>();
                int gy = CellGrid.WorldToCell(new Vector2(d.center.x, d.yMax + 0.75f)).y;
                for (int gx = CellGrid.WorldToCell(new Vector2(d.xMin - 0.5f, 0f)).x; gx <= CellGrid.WorldToCell(new Vector2(d.xMax + 0.5f, 0f)).x; gx++)
                    if (TerraWorld.Get(gx, gy) == CellKind.Empty) { TerraWorld.Set(gx, gy, CellKind.Platform); platform.Add(new Vector2Int(gx, gy)); }
                for (int k = 0; k < 6; k++) yield return null;
                FixedStations.DevReplace();
                for (float t = Time.realtimeSinceStartup + 4f; Stations.DevAltarBox(StationKind.DemonAltar, true) == null && Time.realtimeSinceStartup < t;) yield return null;
                var again = Stations.DevAltarBox(StationKind.DemonAltar, true);
                Check(platform.Count > 0 && again != null && Vector2.Distance(again.Value.center, d.center) < 0.05f,
                    $"祭坛上方放了 {platform.Count} 格平台，重进后祭坛还在原地（{d.center:F2} → {again?.center:F2}）");
                Shot(scene, "altar_platform_full"); for (int f = 0; f < 4; f++) yield return null;
                foreach (var p in platform) TerraWorld.Set(p.x, p.y, CellKind.Empty);

                // A weaker hammer and a pickaxe do nothing to it.
                // Beside it, standing (as the player would), the altar in reach.
                IEnumerator Beside(Rect box)
                {
                    for (int tries = 0; tries < 3; tries++)
                    {
                        hero.transform.position = Clear(hero, new Vector2(box.xMin - 1.2f, box.yMin + 0.8f));
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
                        if (Stations.DevReach(hero).Overlaps(box)) break;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 背包开关 站在祭坛旁 {(Vector2)col.bounds.center:F2}，祭坛 {box.center:F2}，够得着 {Stations.DevReach(hero).Overlaps(box)}");
                }
                yield return Beside(d);
                mod.DevAim = d.center;
                // The weak hammer hurts its swinger (half the life, less defense): the game's own invincibility off for it.
                PlayerData.instance.isInvincible = false;
                mod.Stats.Life = mod.Stats.LifeMax;
                int lifeBefore = mod.Stats.Life, hurts0 = Stations.DevAltarHurts;
                Take(ItemType.IronHammer);
                mod.DevUseSelected(d.center);
                for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null;
                int lifeAfter = mod.Stats.Life;
                PlayerData.instance.isInvincible = true;
                Check(Stations.DevAltarHurts == hurts0 + 1 && lifeAfter < lifeBefore && lifeAfter >= lifeBefore / 2 - 1,
                    $"铁锤敲祭坛扣自己一半生命：{lifeBefore} → {lifeAfter}");
                mod.Stats.Life = mod.Stats.LifeMax;
                // Knocked back by the hurt: beside the altar again.
                for (float t = 0f; t < 0.8f; t += Time.deltaTime) yield return null;
                yield return Beside(d);
                Take(ItemType.GoldPickaxe);
                for (int k = 0; k < 3; k++) { mod.DevUseSelected(d.center); for (float t = 0f; t < 0.35f; t += Time.deltaTime) yield return null; }
                Check(Stations.DevAltarBox(StationKind.DemonAltar, true) != null, "铁锤和金镐敲不动祭坛");

                // The Pwnhammer: two blows, the altar's item where it stood, Dirtmouth's gone for good.
                Take(ItemType.Pwnhammer);
                int items0 = Have(ItemType.DemonAltar), smashed0 = Stations.DevSmashed;
                mod.DevUseSelected(d.center);
                for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null;
                bool afterOne = Stations.DevAltarBox(StationKind.DemonAltar, true) != null;
                mod.DevUseSelected(d.center);
                for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null;
                Check(afterOne && Stations.DevAltarBox(StationKind.DemonAltar, true) == null && Stations.DevSmashed == smashed0 + 1
                    && Have(ItemType.DemonAltar) > items0 && FixedStations.Taken(StationKind.DemonAltar),
                    $"神锤一下敲不掉、两下敲下恶魔祭坛，掉出祭坛物品 {Have(ItemType.DemonAltar) - items0} 个");
                Shot(scene, "altar_smashed_full"); for (int f = 0; f < 4; f++) yield return null;
                FixedStations.DevReplace();
                for (float t = 0f; t < 2f; t += Time.deltaTime) yield return null;
                Check(Stations.DevAltarBox(StationKind.DemonAltar, true) == null && Stations.DevAltarBox(StationKind.CrimsonAltar, true) != null,
                    "敲下的祭坛重进后不再出现，另一个还在");

                // Set down elsewhere, as any station, then knocked off again (no pickaxe takes it).
                for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.DemonAltar) inv.Slots[i] = default;
                Take(ItemType.DemonAltar);
                int placed0 = Stations.DevPlaced;
                Vector2 c2 = col.bounds.center;
                foreach (float dx in new[] { -2.5f, -3f, -3.5f, -4f, 2.5f, 3f })
                {
                    if (Stations.DevPlaced > placed0) break;
                    mod.DevUseSelected(new Vector2(c2.x + dx, col.bounds.min.y + 0.3f));
                    for (int k = 0; k < 3; k++) yield return null;
                }
                var moved = Stations.DevAltarBox(StationKind.DemonAltar, false);
                Check(Stations.DevPlaced > placed0 && moved != null, $"祭坛物品放到别处 {moved?.center:F1}");
                if (moved != null)
                {
                    Shot(scene, "altar_moved_full"); for (int f = 0; f < 4; f++) yield return null;
                    var m = moved.Value;
                    yield return Beside(m);
                    Take(ItemType.GoldPickaxe);
                    for (int k = 0; k < 3; k++) { mod.DevUseSelected(m.center); for (float t = 0f; t < 0.35f; t += Time.deltaTime) yield return null; }
                    bool pickSpared = Stations.DevAltarBox(StationKind.DemonAltar, false) != null;
                    Take(ItemType.Pwnhammer);
                    int items1 = Have(ItemType.DemonAltar);
                    for (int k = 0; k < 2; k++) { mod.DevUseSelected(m.center); for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null; }
                    Check(pickSpared && Stations.DevAltarBox(StationKind.DemonAltar, false) == null && Have(ItemType.DemonAltar) > items1,
                        $"放下的祭坛镐子挖不动（{pickSpared}），神锤能再敲下来（物品 {items1} → {Have(ItemType.DemonAltar)}）");
                }
            }
            finally
            {
                InventoryGui.Open = false;
                UI.BuilderToggles.DevMouse = null;
                mod.DevAim = null;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 背包开关 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
