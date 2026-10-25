using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_v5F_prefab_zh"): recorder F's prefab-house takes on Dirtmouth's street. A random area kit bought from
    /// the Guide; houses of four areas set down in a row from their kits (the green preview going ahead of each), a fifth
    /// stacked on a roof; a flight over the little town; the housing menu: a house asked about, a townsperson moved in.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        // The row: each house's area and left cell (set by F5PrefabPlace), the street row.
        readonly List<(int Area, Vector2Int Origin)> f5Houses = new List<(int, Vector2Int)>();
        int f5Street;

        IEnumerator P4_v5F_prefab(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            mod.DevSetSmartCursor(false);
            yield return F5TownReady();
            yield return F5PrefabBuy();
            yield return F5PrefabPlace();
            yield return F5PrefabFly();
            yield return F5PrefabMoveIn();
            F5CamOff();
        }

        /// <summary>The Guide's shop: the random area kit hovered and bought for 1000 geo, put in the hotbar.</summary>
        IEnumerator F5PrefabBuy()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var at = TownNpcs.DevPos("Guide");
            if (!at.HasValue) { F5Log("预制房购买：向导不在"); yield break; }
            F5Shoo(at.Value.x - 10f, at.Value.x + 10f, 0, 10f);
            TownNpcs.DevSend("Guide", at.Value.x, 0);
            yield return new WaitForSeconds(0.3f);
            at = TownNpcs.DevPos("Guide");
            Shop.Earn(300000);
            int free = -1;
            for (int i = 2; i < Inventory.HotbarSize && free < 0; i++) if (inv.Slots[i].IsEmpty) free = i;
            if (free < 0) { inv.Slots[9] = default; free = 9; }
            var col = hero.GetComponent<BoxCollider2D>();
            F5Street(hero, at.Value.x - 5.5f, 1);
            F5CamOn(new Vector2(1.5f, 3.8f));
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.2f)));
            long purse0 = Shop.Purse;
            StartRec("v5F_prefab_buy");
            yield return new WaitForSeconds(0.3f);
            yield return F5WalkTo(hero, at.Value.x - 1.6f, 2.5f);
            C4Face(hero, 1);
            at = TownNpcs.DevPos("Guide");
            yield return Glide(GuiOf(at.Value + new Vector2(0f, 0.5f)), 0.5f);
            yield return new WaitForSeconds(0.15f);
            bool talked = TownNpcs.TryRightClick(at.Value);
            yield return new WaitForSeconds(1.2f);
            yield return ClickHit("ChatButton", 0, 0.5f);
            yield return new WaitForSeconds(0.5f);
            int slot = System.Array.FindIndex(TownNpcs.ShopItems, s => !s.IsEmpty && s.Type == ItemType.RandomPrefabHouse);
            if (slot >= 0)
            {
                var r = InventoryGui.DevHitRect("ShopSlot", slot);
                if (r != null) yield return Glide(r.Value.center, 0.6f);
                yield return new WaitForSeconds(1.4f);
                yield return ClickHit("ShopSlot", slot, 0.1f);
                yield return new WaitForSeconds(0.5f);
                yield return ClickHit("Slot", free, 0.6f);
                yield return new WaitForSeconds(0.8f);
            }
            StopRec();
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            PointerOff();
            mod.ReturnOrDropMouseItem();
            F5Log($"预制房购买：对话 {talked}，货架第 {slot} 格，钱 {purse0}→{Shop.Purse}，快捷栏 {free}：{(inv.Slots[free].IsEmpty ? "空" : inv.Slots[free].Def.Name)}");
            inv.Slots[free] = default;
        }

        IEnumerator F5PrefabPlace()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var st = TownNpcs.DevStreet;
            f5Street = F5GroundRow(70f, st.y + 1f);
            int g = f5Street;
            int x = CellGrid.WorldToCell(new Vector2(63f, st.y)).x;
            f5Houses.Clear();
            foreach (int a in new[] { 4, 10, 8, 7 })
            {
                f5Houses.Add((a, new Vector2Int(x, g + 1)));
                x += PrefabHouses.Width(a) + 3;
            }
            // The Hive's on the Crystal Peak roof (its floor on the roof's row 9... the box from row 10).
            var cp = f5Houses[1].Origin;
            f5Houses.Add((15, new Vector2Int(cp.x + PrefabHouses.Width(10) / 2 - PrefabHouses.Width(15) / 2, cp.y + 10)));
            F5Shoo(50f, x * CellGrid.CellSize + 6f, 1, 14f);
            var saved = F5Hotbar(f5Houses.Select(h => PrefabHouses.Kit(h.Area)).ToArray());
            for (int i = 0; i < f5Houses.Count; i++) inv.Slots[i] = ItemStack.Of(PrefabHouses.Kit(f5Houses[i].Area), 1);
            var col = hero.GetComponent<BoxCollider2D>();
            F5Street(hero, 60.5f, 1);
            F5CamOn(new Vector2(3.0f, 3.9f));
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            Vector2 Bottom(int i) => CellGrid.CellCenter(f5Houses[i].Origin.x + PrefabHouses.Width(f5Houses[i].Area) / 2, f5Houses[i].Origin.y) + new Vector2(0f, 0.05f);
            aimInit = false;
            SmoothAim(Bottom(0) + new Vector2(-1.5f, 3.5f), 99f);
            PointerOn(GuiOf(aimNow));
            int built = 0;
            StartRec("v5F_prefab_place");
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < f5Houses.Count; i++)
            {
                inv.Selected = i;
                var target = Bottom(i);
                if (i > 0 && i < 4)
                {
                    float walkTo = target.x - 6.5f;
                    yield return F5WalkTo(hero, walkTo, 2.5f, () => target + new Vector2(-1.5f, 1.5f));
                    C4Face(hero, 1);
                }
                if (i == 4) { f5CamOffset = new Vector2(1.0f, 5.2f); C4Face(hero, -1); }
                // The preview glides to the spot and settles a moment there (green: it fits), then the click.
                var from = aimNow;
                var via = target + new Vector2(-2.5f, 2.5f);
                for (float t = 0f; t < 0.75f; t += Time.deltaTime)
                {
                    float u = Mathf.SmoothStep(0f, 1f, t / 0.75f);
                    aimNow = Vector2.Lerp(Vector2.Lerp(from, via, u), Vector2.Lerp(via, target, u), u);
                    AimAt(aimNow);
                    yield return null;
                }
                AimAt(target);
                aimNow = target;
                yield return new WaitForSeconds(0.45f);
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                bool ok = PrefabHouses.CanBuild(f5Houses[i].Area, PrefabHouses.Origin(f5Houses[i].Area, target), hero, out var why);
                mod.DevUseSelected(target);
                yield return null;
                if (TerraWorld.IsSolid(f5Houses[i].Origin.x, f5Houses[i].Origin.y)) built++;
                else F5Log($"预制房 {i}：没盖上（{ok} {why}）");
                yield return new WaitForSeconds(0.6f);
            }
            for (float t = 0f; t < 1.4f; t += Time.deltaTime) { SmoothAim(Bottom(4) + new Vector2(0f, 3f), 2f); yield return null; }
            StopRec();
            PointerOff();
            F5Log($"预制房：盖了 {built}/{f5Houses.Count}：{string.Join(" ", f5Houses.Select(h => AreaBlocks.All[h.Area].Name + "@" + h.Origin))}");
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }

        /// <summary>Over the little town on wings, the view drawn back.</summary>
        IEnumerator F5PrefabFly()
        {
            var hero = HeroController.instance;
            if (f5Houses.Count == 0) yield break;
            float zoom0 = Player.TerraCamera.Zoom;
            var col = hero.GetComponent<BoxCollider2D>();
            float x0 = f5Houses[0].Origin.x * CellGrid.CellSize - 5f;
            var last = f5Houses[3];
            float x1 = (last.Origin.x + PrefabHouses.Width(last.Area)) * CellGrid.CellSize + 6f;
            float street = CellGrid.CellCenter(0, f5Street).y;
            F5Street(hero, x0, 1);
            Player.TerraCamera.Zoom = 1.45f;
            F5CamOn(new Vector2(2.5f, 2.0f), 5f);
            yield return new WaitForSeconds(1.0f);
            F5CamSnap();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 1f), 99f);
            StartRec("v5F_prefab_fly");
            yield return new WaitForSeconds(0.4f);
            Player.TerraMovement.DevMove = 1;
            Player.TerraMovement.DevJump = true;
            Player.TerraMovement.DevGroundJump(hero);
            float cruise = street + 8.5f;
            for (float t = 0f; t < 9f && col.bounds.center.x < x1; t += Time.deltaTime)
            {
                Player.TerraMovement.DevJump = col.bounds.center.y < cruise;
                SmoothAim((Vector2)col.bounds.center + new Vector2(3f, -1.5f), 4f);
                yield return null;
            }
            Player.TerraMovement.DevJump = false;
            for (float t = 0f; t < 2.5f && !hero.cState.onGround; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(2f, -1f), 4f); yield return null; }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.5f);
            StopRec();
            Player.TerraCamera.Zoom = zoom0;
            F5Log($"预制房飞过：从 {x0:F1} 到 {(Vector2)hero.transform.position:F1}");
        }

        /// <summary>The housing menu: the Greenpath house asked about (Terraria's verdict), then a townsperson's head put in it.</summary>
        IEnumerator F5PrefabMoveIn()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            if (f5Houses.Count == 0) yield break;
            var h = f5Houses[0];
            float w = PrefabHouses.Width(h.Area) * CellGrid.CellSize;
            var inside = CellGrid.CellCenter(h.Origin.x + 6, h.Origin.y + 3);
            var home = CellGrid.CellCenter(h.Origin.x + 9, h.Origin.y + 2);
            var col = hero.GetComponent<BoxCollider2D>();
            F5Street(hero, h.Origin.x * CellGrid.CellSize + w + 2.5f, -1);
            F5CamOn(new Vector2(-2.5f, 3.8f));
            inv.Selected = 0;
            yield return new WaitForSeconds(0.8f);
            F5CamSnap();
            var defs = TownNpcs.AllDefs;
            int who = -1;
            foreach (var name in new[] { "Dryad", "BestiaryGirl", "PartyGirl", "Nurse" })
                if (who < 0) for (int i = 0; i < defs.Length; i++) if (defs[i].Internal == name && TownNpcs.DevPos(name).HasValue) { who = i; break; }
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(-1.5f, 1.2f)));
            StartRec("v5F_prefab_movein");
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
            yield return new WaitForSeconds(1.7f);
            string moved = "";
            if (who >= 0)
            {
                var hr = InventoryGui.DevRectOf("NpcHead", who);
                if (hr != null) yield return ClickAt(hr.Value, 0.6f);
                if (!TownNpcs.Placing) TownNpcs.ClickHead(defs[who], true, false);
                yield return new WaitForSeconds(0.3f);
                yield return Glide(GuiOf(home), 0.75f);
                yield return new WaitForSeconds(0.15f);
                UI.TerraCursor.DevClick();
                TownNpcs.PlaceClick(home, hero);
                moved = string.Join(" / ", TownNpcs.DevSaid);
                yield return new WaitForSeconds(1.6f);
            }
            yield return Glide(GuiOf((Vector2)col.bounds.center + new Vector2(-2.5f, 2.5f)), 0.5f);
            InventoryGui.Open = false;
            TownNpcs.ClosePanel();
            mod.PlaySound("Menu_Close", 0.6f);
            PointerOff();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(-2.5f, 2.5f), 99f);
            for (float t = 0f; t < 2.4f; t += Time.deltaTime)
            {
                var f = who >= 0 ? TownNpcs.DevFeet(defs[who].Internal) : null;
                SmoothAim(f.HasValue ? f.Value + new Vector2(0f, 1.6f) : home, 3f);
                yield return null;
            }
            StopRec();
            mod.DevAim = null;
            F5Log($"入住：询问 {verdict}；{(who >= 0 ? defs[who].Zh : "没人")}：{moved}，家 {(who >= 0 ? TownNpcs.DevHouseOf(defs[who].Internal, out _) : "")}");
        }
    }
}
