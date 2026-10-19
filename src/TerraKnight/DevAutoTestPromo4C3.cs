using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_Cbuild"): recorder C's home-building clips on Dirtmouth's street. A house of Dirtmouth stone
    /// (12 by 8 cells, its frame laid block by block, its wall filled in behind), a Wooden Door, Table and Chair, the
    /// Dirtmouth torch and a bed set in it; the housing menu: the "?" on the room (Terraria's verdict at the bottom left),
    /// the Guide's head and a click on the room: he moves in and walks about in it; the hero out through the door (it
    /// pushes open and shuts behind) and back; the bed made the spawn point.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        // The house: its frame's lower-left cell is (c4hx, c4hy - 1)... the floor is the street (row c4hy - 1); the room
        // inside is c4hx + 1 .. c4hx + 10, c4hy .. c4hy + 5; the roof row c4hy + 6; the door in the left wall.
        int c4hx, c4hy;

        IEnumerator P4_Cbuild(HeroController hero)
        {
            yield return P4Kit();
            yield return C4BuildAll();
        }

        bool C4HouseSite(out int x0, out int g)
        {
            x0 = g = 0;
            var st = TownNpcs.DevStreet;
            float benchX = st.x;
            int gy = CellGrid.WorldToCell(new Vector2(st.x, st.y + 0.2f)).y;
            int lo = CellGrid.WorldToCell(new Vector2(st.z - 25f, st.y)).x, hi = CellGrid.WorldToCell(new Vector2(st.w + 25f, st.y)).x;
            float best = float.MaxValue;
            var found = new List<string>();
            for (int x = lo; x <= hi; x++)
                for (int y = gy - 8; y <= gy + 8; y++)
                {
                    bool ok = true;
                    for (int cx = x - 7; cx <= x + 14 && ok; cx++)
                    {
                        if (!TerraWorld.IsSolid(cx, y - 1)) { ok = false; break; }
                        for (int cy = y; cy <= y + 10 && ok; cy++)
                        {
                            var r = TerraWorld.RegionAt(cx, cy);
                            if (r == null || r.SceneName != "Town" || TerraWorld.Get(cx, cy) != CellKind.Empty || BossRooms.Locked(cx, cy)) ok = false;
                            else if (Stations.Covers(new Vector2Int(cx, cy)) || Chests.Covers(new Vector2Int(cx, cy), orUnder: true)) ok = false;
                        }
                    }
                    if (!ok) continue;
                    float mid = CellGrid.CellCenter(x + 6, y).x;
                    if (Mathf.Abs(mid - benchX) < 7f) continue;
                    found.Add($"{x},{y}@{mid:F1}");
                    float d = Mathf.Abs(mid - (benchX + 13f));
                    if (d < best) { best = d; x0 = x; g = y; }
                }
            Plugin.Log.LogInfo($"[AutoTest] 录制 建房：长椅 ({st.x:F1},{st.y:F1})，街道 {st.z:F1}..{st.w:F1}，能盖的地方 {found.Count} 个（{string.Join(" ", found.GetRange(0, Mathf.Min(found.Count, 30)))}），选 {x0},{g}");
            return best < float.MaxValue;
        }

        /// <summary>The aim eased onto a cell, then the item in hand used there (a block, a wall, a piece of furniture).</summary>
        IEnumerator C4PlaceAt(Vector2 target, float settle = 0.07f, float rate = 22f)
        {
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                SmoothAim(target, rate);
                if (t >= settle && Vector2.Distance(aimNow, target) < 0.15f) break;
                yield return null;
            }
            while (!mod.DevReady) { SmoothAim(target, rate); yield return null; }
            mod.DevUseSelected(target, false, true);
            yield return null;
        }

        void C4Face(HeroController hero, int dir)
        {
            if (dir > 0 && !hero.cState.facingRight) hero.FaceRight();
            else if (dir < 0 && hero.cState.facingRight) hero.FaceLeft();
        }

        IEnumerator C4WalkTo(HeroController hero, float x, float maxTime, System.Func<Vector2> aim = null)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < maxTime; t += Time.deltaTime)
            {
                float dx = x - col.bounds.center.x;
                if (Mathf.Abs(dx) < 0.12f) break;
                Player.TerraMovement.DevMove = dx > 0 ? 1 : -1;
                if (aim != null) SmoothAim(aim(), 8f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
        }

        IEnumerator C4BuildAll()
        {
            yield return EnterRoom("Town");
            float entered = Time.time;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.TrySwitchLoadout(1);
            yield return null;
            var inv = mod.Inventory;
            // A plain game's bag for building (the creative one's title and infinite stacks off).
            var creativeSaved = (ItemStack[])inv.Slots.Clone();
            mod.ApplyCreative(false);
            if (!C4HouseSite(out int x0, out int g)) { Plugin.Log.LogInfo("[AutoTest] 录制 建房：德特茅斯找不到空地"); yield break; }
            c4hx = x0; c4hy = g;
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 Cell(int x, int y) => CellGrid.CellCenter(x, y);
            int dm = AreaBlocks.IndexOfScene("Town");
            if (dm < 0) dm = 1;
            // The builder's hotbar (the loadout's own kept aside), the Architect Gizmo Pack worn.
            var saved = (ItemStack[])inv.Slots.Clone();
            int accSlot = Inventory.AccessorySlots - 1;
            var savedAcc = inv.Accessories[accSlot];
            inv.Infinite = false;
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(AreaBlocks.BlockItem(dm), 250);
            inv.Slots[1] = ItemStack.Of(AreaBlocks.WallItem(dm), 250);
            inv.Slots[2] = ItemStack.Of(ItemType.WoodenDoor, 3);
            inv.Slots[3] = ItemStack.Of(ItemType.WoodenTable, 2);
            inv.Slots[4] = ItemStack.Of(ItemType.Chair, 4);
            inv.Slots[5] = ItemStack.Of(RegionalTorches.ItemOf(RegionalTorchAreas.StyleOf("Town")), 30);
            inv.Slots[6] = ItemStack.Of(ItemType.Bed, 2);
            inv.Slots[7] = default;
            inv.Accessories[accSlot] = ItemStack.Of(ItemType.ArchitectGizmoPack, 1);
            inv.RefreshEffective();
            Equipment.Update(inv);
            inv.Selected = 0;
            QuietAround(hero.transform.position, 200f);
            hero.transform.position = Clear(hero, Cell(x0 + 6, g) + new Vector2(0f, 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, 1);
            yield return new WaitForSeconds(1.5f);
            // The area's title gone first.
            while (Time.time - entered < 8f) yield return null;

            // ---- build_frame: the frame block by block, then the wall behind the room.
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2f, 0.5f), 99f);
            var frame = new List<Vector2Int>();
            for (int y = g; y <= g + 6; y++) frame.Add(new Vector2Int(x0 + 11, y));
            for (int x = x0 + 10; x >= x0 + 1; x--) frame.Add(new Vector2Int(x, g + 6));
            for (int y = g + 6; y >= g + 3; y--) frame.Add(new Vector2Int(x0, y));
            StartRec("build_frame");
            yield return new WaitForSeconds(0.4f);
            int laid = 0;
            foreach (var c in frame)
            {
                yield return C4PlaceAt(Cell(c.x, c.y));
                if (TerraWorld.IsSolid(c.x, c.y)) laid++;
            }
            yield return new WaitForSeconds(0.25f);
            inv.Selected = 1;
            yield return new WaitForSeconds(0.2f);
            int walls = 0;
            for (int y = g; y <= g + 5; y++)
                for (int i = 0; i < 10; i++)
                {
                    int x = (y - g) % 2 == 0 ? x0 + 10 - i : x0 + 1 + i;
                    yield return C4PlaceAt(Cell(x, y), 0.03f, 30f);
                    if (TerraWorld.GetWall(x, y) > 0) walls++;
                }
            yield return new WaitForSeconds(0.6f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 建房 框架：方块 {laid}/{frame.Count}，墙 {walls}/60（{ItemDb.Get(AreaBlocks.BlockItem(dm)).Name}，{ItemDb.Get(AreaBlocks.WallItem(dm)).Name}）");

            // ---- build_furnish: the door, the table, a chair facing it, the torch, the bed.
            hero.transform.position = Clear(hero, Cell(x0 + 3, g) + new Vector2(0f, 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, -1);
            yield return new WaitForSeconds(0.6f);
            SmoothAim((Vector2)col.bounds.center + new Vector2(-1.5f, 0.3f), 99f);
            StartRec("build_furnish");
            yield return new WaitForSeconds(0.4f);
            inv.Selected = 2;
            yield return new WaitForSeconds(0.25f);
            yield return C4PlaceAt(Cell(x0, g + 1), 0.25f, 10f);
            bool door = Stations.DevDoorNear(Cell(x0, g + 1), out _) != StationKind.None;
            string whyDoor = Stations.DevWhy;
            yield return new WaitForSeconds(0.3f);
            // A chair by the door facing right (the hero left of it, facing that way), the table, a chair facing left.
            yield return C4WalkTo(hero, Cell(x0 + 1, g).x + 0.1f, 1.0f, () => Cell(x0 + 2, g) + new Vector2(0f, 0.3f));
            C4Face(hero, 1);
            inv.Selected = 4;
            yield return new WaitForSeconds(0.25f);
            yield return C4PlaceAt(Cell(x0 + 2, g), 0.25f, 10f);
            int chairRight = Stations.DevFacingNear(Cell(x0 + 2, g), StationKind.Chair);
            yield return new WaitForSeconds(0.25f);
            yield return C4WalkTo(hero, Cell(x0 + 8, g).x, 1.4f, () => Cell(x0 + 4, g) + new Vector2(0f, 0.3f));
            C4Face(hero, -1);
            inv.Selected = 3;
            yield return new WaitForSeconds(0.25f);
            yield return C4PlaceAt(Cell(x0 + 4, g), 0.25f, 10f);
            yield return new WaitForSeconds(0.3f);
            inv.Selected = 4;
            C4Face(hero, -1);
            yield return new WaitForSeconds(0.25f);
            yield return C4PlaceAt(Cell(x0 + 6, g), 0.25f, 10f);
            int chairFace = Stations.DevFacingNear(Cell(x0 + 6, g), StationKind.Chair);
            yield return new WaitForSeconds(0.3f);
            inv.Selected = 5;
            yield return new WaitForSeconds(0.25f);
            yield return C4PlaceAt(Cell(x0 + 7, g + 4), 0.25f, 10f);
            yield return C4PlaceAt(Cell(x0 + 2, g + 4), 0.25f, 10f);
            yield return new WaitForSeconds(0.3f);
            // The bed on the right, its head the way the hero faces.
            yield return C4WalkTo(hero, Cell(x0 + 5, g).x, 1.2f, () => Cell(x0 + 8, g) + new Vector2(0.25f, 0.2f));
            C4Face(hero, 1);
            inv.Selected = 6;
            yield return new WaitForSeconds(0.25f);
            yield return C4PlaceAt(Cell(x0 + 8, g) + new Vector2(0.25f, 0f), 0.25f, 10f);
            bool bed = Stations.DevBoxOf(StationKind.Bed).HasValue;
            yield return new WaitForSeconds(0.9f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 建房 家具：门 {door}（{whyDoor}），椅子朝 {chairRight} 和 {chairFace}，床 {bed}（{Stations.DevWhy}）");

            // ---- build_query: the housing menu; the "?" on the room; the Guide moved in.
            var inside = Cell(x0 + 3, g + 3);

            inv.Selected = 7;
            C4Face(hero, -1);
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.2f)));
            yield return new WaitForSeconds(0.3f);
            StartRec("build_query");
            yield return new WaitForSeconds(0.4f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.6f);
            yield return new WaitForSeconds(0.5f);
            if (!TownNpcs.PanelOpen)
            {
                var page = InventoryGui.DevPageIconRect(1);
                if (page != null) yield return ClickAt(page.Value, 0.55f);
                if (!TownNpcs.PanelOpen) TownNpcs.TogglePanel();
            }
            yield return new WaitForSeconds(0.4f);
            var qr = InventoryGui.DevRectOf("NpcHead", -1);
            if (qr != null) yield return ClickAt(qr.Value, 0.5f);
            if (!TownNpcs.Querying) TownNpcs.ClickQuery(true, false);
            yield return new WaitForSeconds(0.25f);
            yield return Glide(GuiOf(inside), 0.7f);
            yield return new WaitForSeconds(0.15f);
            UI.TerraCursor.DevClick();
            string verdict = TownNpcs.PlaceClick(inside, hero) ? string.Join(" / ", TownNpcs.DevSaid) : "没受理";
            yield return new WaitForSeconds(1.6f);
            int guide = -1;
            var defs = TownNpcs.AllDefs;
            for (int i = 0; i < defs.Length; i++) if (defs[i].Internal == "Guide") guide = i;
            var gr = guide >= 0 ? InventoryGui.DevRectOf("NpcHead", guide) : null;
            if (gr != null) yield return ClickAt(gr.Value, 0.6f);
            if (!TownNpcs.Placing && guide >= 0) TownNpcs.ClickHead(defs[guide], true, false);
            yield return new WaitForSeconds(0.3f);
            var home = Cell(x0 + 5, g + 2);
            yield return Glide(GuiOf(home), 0.75f);
            yield return new WaitForSeconds(0.15f);
            UI.TerraCursor.DevClick();
            bool placed = TownNpcs.PlaceClick(home, hero);
            string moved = string.Join(" / ", TownNpcs.DevSaid);
            yield return new WaitForSeconds(1.8f);
            yield return Glide(GuiOf((Vector2)col.bounds.center + new Vector2(2.5f, 2.5f)), 0.5f);
            InventoryGui.Open = false;
            TownNpcs.ClosePanel();
            mod.PlaySound("Menu_Close", 0.6f);
            PointerOff();
            AimAt((Vector2)col.bounds.center + new Vector2(2.5f, 2.5f));
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2.5f, 2.5f), 99f);
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                var f = TownNpcs.DevFeet("Guide");
                SmoothAim(f.HasValue ? f.Value + new Vector2(0f, 1.6f) : (Vector2)col.bounds.center + new Vector2(2f, 1.5f), 3f);
                yield return null;
            }
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 建房 询问：{verdict}；向导：受理 {placed}，{moved}，家 {TownNpcs.DevHouseOf("Guide", out _)}，脚下 {TownNpcs.DevFeet("Guide")}");

            // ---- build_door: out through the door and back in (two frames after the last take: its recorder has stopped).
            yield return null;
            yield return null;
            StartRec("build_door");
            yield return new WaitForSeconds(0.3f);
            int pushed0 = Stations.DevPushedOpen, shut0 = Stations.DevPushedShut;
            yield return C4WalkTo(hero, Cell(x0 - 4, g).x, 2.5f, () => (Vector2)col.bounds.center + new Vector2(-2.5f, 0.6f));
            yield return new WaitForSeconds(0.7f);
            yield return C4WalkTo(hero, Cell(x0 + 3, g).x, 2.5f, () => (Vector2)col.bounds.center + new Vector2(2.5f, 0.6f));
            yield return new WaitForSeconds(0.8f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 建房 门：推开 {Stations.DevPushedOpen - pushed0} 次，关上 {Stations.DevPushedShut - shut0} 次，门现在 {Stations.DevDoorNear(Cell(x0, g + 1), out _)}");

            // ---- build_bed: the bed right-clicked, the spawn point set.
            var bedBox = Stations.DevBoxOf(StationKind.Bed);
            if (bedBox.HasValue)
            {

                yield return null;
                yield return null;
                StartRec("build_bed");
                yield return new WaitForSeconds(0.3f);
                yield return C4WalkTo(hero, bedBox.Value.xMin - 0.6f, 2f, () => bedBox.Value.center);
                C4Face(hero, 1);
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) { SmoothAim(bedBox.Value.center + new Vector2(0f, 0.05f), 9f); yield return null; }
                yield return new WaitForSeconds(0.2f);
                bool set = Stations.TryRightClick(bedBox.Value.center);
                yield return new WaitForSeconds(2.2f);
                StopRec();
                Plugin.Log.LogInfo($"[AutoTest] 录制 建房 床：右键 {set}，{Beds.DevLast}");
            }
            mod.DevAim = null;
            inv.Accessories[accSlot] = savedAcc;
            mod.ApplyCreative(true);
            for (int i = 0; i < creativeSaved.Length; i++) inv.Slots[i] = creativeSaved[i];
            inv.RefreshEffective();
            Equipment.Update(inv);
        }
    }
}
