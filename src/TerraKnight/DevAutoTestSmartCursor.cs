using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "smartcursor", "smartinteract"): Terraria's smart cursor in its order (SmartCursorHelper: a minable
    /// thing under the mouse, the hammer's line, torches on platforms and out of water, the smart blocks option, the Empty
    /// Bucket) and its smart interact (SmartInteractSystem: the nearest chest, station or townsperson in reach takes a
    /// right click anywhere, outlined; none with a torch under the mouse or something held on the cursor).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SmartCursorSuite(string suite)
        {
            charmPass = charmAll = 0;
            charmTag = suite;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            bool smartWas = mod.SmartCursor;
            mod.DevSetSmartCursor(true);
            bool endless = mod.Inventory.Infinite;
            mod.Inventory.Infinite = false;
            yield return CharmPlace();
            yield return ToDummy(14f);
            if (suite == "smartcursor") yield return SmartCursorTest();
            else yield return SmartInteractTest();
            SmartInteract.DevMouse = null;
            mod.Inventory.Slots[0] = default;
            mod.Inventory.Infinite = endless;
            mod.DevSetSmartCursor(smartWas);
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        // ------------------------------------------------------------------ the item's target

        IEnumerator SmartCursorTest()
        {
            var hero = HeroController.instance;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var mid = CellGrid.WorldToCell(box.center);
            var feet = CellGrid.WorldToCell(new Vector2(box.center.x, box.min.y + 0.1f));
            int gy = feet.y;
            // The ground under the feet, the work area right of the player cleared.
            Vector2 At(int dx, int dy) => CellGrid.CellCenter(feet.x + dx, gy + dy);
            Vector2Int C(int dx, int dy) => new Vector2Int(feet.x + dx, gy + dy);
            void Clear()
            {
                for (int dx = 1; dx <= 6; dx++)
                    for (int dy = 0; dy <= 5; dy++)
                    {
                        var c = C(dx, dy);
                        TerraWorld.Set(c.x, c.y, CellKind.Empty);
                        TerraWorld.SetWall(c.x, c.y, 0);
                        SetWater(c, 0);
                    }
            }
            Clear();
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 脚下格 {feet}，身体中格 {mid}，脚下一格 {TerraWorld.Get(feet.x, gy - 1)}");
            var pick = ItemDb.Get(ItemType.CopperPickaxe);
            var hammer = ItemDb.Get(ItemType.WoodenHammer);

            // A platform under the mouse, a block on the pickaxe's line before it: the platform (ForceCursorToAnyMinableThing).
            TerraWorld.Set(C(2, 0).x, C(2, 0).y, CellKind.Stone);
            TerraWorld.Set(C(4, 0).x, C(4, 0).y, CellKind.Platform);
            yield return null;
            bool okP = mod.TargetFor(pick, At(4, 0), out var pc, out bool ps);
            CharmCheck("镐子指着平台时选平台本身，不挖路上的石块", okP && pc == C(4, 0) && ps, $"选了 {pc}（平台 {C(4, 0)}，石块 {C(2, 0)}），智能 {ps}");
            // The mouse on nothing minable: the block along the line (Step_Pickaxe_MineSolids).
            bool okS = mod.TargetFor(pick, At(5, 0), out var sc, out bool ss);
            CharmCheck("指着空处时镐子照原版沿直线挖第一个石块", okS && sc == C(2, 0) && ss, $"选了 {sc}");
            Clear();

            // The hammer: two walls on its line, the mouse on the far one: the near one (Collision.HitLineWall).
            int wood = ItemDb.Get(ItemType.WoodWall).PlacesWall;
            int row = mid.y - gy;
            TerraWorld.SetWall(C(2, row).x, C(2, row).y, wood);
            TerraWorld.SetWall(C(4, row).x, C(4, row).y, wood);
            yield return null;
            bool okH = mod.TargetFor(hammer, At(4, row), out var hc, out bool hs);
            CharmCheck("锤子先找玩家到鼠标那条线上最近的墙", okH && hc == C(2, row) && hs, $"选了 {hc}（近的 {C(2, row)}，鼠标下 {C(4, row)}）");
            Clear();
            // No wall in reach: the block under the mouse (to be shaped).
            TerraWorld.Set(C(3, 0).x, C(3, 0).y, CellKind.Stone);
            yield return null;
            bool okH2 = mod.TargetFor(hammer, At(3, 0), out var hc2, out bool hs2);
            CharmCheck("附近没有墙时锤子敲鼠标下的方块", okH2 && hc2 == C(3, 0) && !hs2, $"选了 {hc2}，智能 {hs2}，能用 {okH2}");
            Clear();

            // Torches: on a flat platform, not in water.
            TerraWorld.Set(C(3, 2).x, C(3, 2).y, CellKind.Platform);
            yield return null;
            CharmCheck("火把可以放在平台上", TerraMod.CanPlace(C(3, 3), CellKind.Torch), "");
            var torch = ItemDb.Get(ItemType.Torch);
            bool okT = mod.TargetFor(torch, At(3, 3), out var tc, out bool ts);
            CharmCheck("智能光标放火把会选平台上方", okT && tc == C(3, 3) && ts, $"选了 {tc}，智能 {ts}");
            Clear();
            // A basin of water.
            TerraWorld.Set(C(2, 0).x, C(2, 0).y, CellKind.Stone);
            TerraWorld.Set(C(4, 0).x, C(4, 0).y, CellKind.Stone);
            SetWater(C(3, 0), 255);
            yield return null;
            CharmCheck("火把不能放进水里", !TerraMod.CanPlace(C(3, 0), CellKind.Torch), $"水 {TerraWorld.LiquidAt(C(3, 0).x, C(3, 0).y)}");

            // The Empty Bucket: the water nearest the mouse (Step_EmptyBuckets), and a use scoops it.
            var bucket = ItemDb.Get(ItemType.EmptyBucket);
            bool okB = mod.TargetFor(bucket, At(3, 3), out var bc, out bool bs);
            CharmCheck("空桶自动选附近的水", okB && bc == C(3, 0) && bs, $"选了 {bc}（水 {C(3, 0)}），智能 {bs}");
            mod.Inventory.Slots[0] = ItemStack.Of(ItemType.EmptyBucket);
            mod.Inventory.Selected = 0;
            mod.Inventory.Override = -1;
            int scooped = Buckets.DevScooped;
            for (float w = Time.time + 1f; !mod.DevReady && Time.time < w;) yield return null;
            mod.DevUseSelected(At(3, 3), target: true);
            yield return new WaitForSeconds(0.3f);
            CharmCheck("鼠标没指着水也能舀起来", Buckets.DevScooped == scooped + 1 && mod.Inventory.Slots[0].Type == ItemType.WaterBucket, $"舀了 {Buckets.DevScooped - scooped}，手里 {mod.Inventory.Slots[0].Type}");
            mod.Inventory.Slots[0] = default;
            Clear();

            // Smart Block Placement (LegacyMenu.215/216): on, a dirt block goes to the nearest place by the mouse; off, where the mouse is.
            var dirt = ItemDb.Get(ItemType.DirtBlock);
            bool was = Items.SmartCursor.SmartBlocks;
            Items.SmartCursor.SetBlocks(true);
            bool okD = mod.TargetFor(dirt, At(3, 3), out var dc, out bool ds);
            CharmCheck("智能放置物块开：方块放到光标旁边能放的地方", okD && ds && dc != C(3, 3), $"选了 {dc}（鼠标 {C(3, 3)}）");
            Items.SmartCursor.SetBlocks(false);
            bool okD2 = mod.TargetFor(dirt, At(3, 3), out var dc2, out bool ds2);
            CharmCheck("智能放置物块关：只放在鼠标处（悬空放不了）", !ds2 && dc2 == C(3, 3) && !okD2, $"选了 {dc2}，智能 {ds2}，能用 {okD2}");
            Items.SmartCursor.SetBlocks(was);

            // The mouse on a torch in reach: no smart step at all (IsHoveringOverAnInteractableTileThatBlocksSmartCursor).
            TerraWorld.Set(C(3, 0).x, C(3, 0).y, CellKind.Torch);
            yield return null;
            mod.TargetFor(ItemDb.Get(ItemType.WoodWall), At(3, 0), out _, out bool ws);
            CharmCheck("鼠标指着火把时智能光标不选别处", !ws, $"智能 {ws}");
            Clear();
            CharmShot("cursor");
            // The option in the settings' General column.
            UI.IngameOptions.DevPage(0);
            UI.IngameOptions.Show();
            yield return new WaitForSecondsRealtime(0.4f);
            HudShot("options");
            // The capture is taken at the frame's end: the panel stays up through it.
            yield return null;
            yield return null;
            UI.IngameOptions.Hide();
        }

        static void SetWater(Vector2Int cell, int amount)
        {
            var r = TerraWorld.RegionAt(cell.x, cell.y);
            if (r == null || r.Liquid == null) return;
            r.Liquid.Set(cell.x - r.CellOffset.x, cell.y - r.CellOffset.y, amount, LiquidKind.Water);
        }

        // ------------------------------------------------------------------ the right click's target

        IEnumerator SmartInteractTest()
        {
            var hero = HeroController.instance;
            var hb = hero.GetComponent<BoxCollider2D>().bounds;
            Vector2 home = hero.transform.position;
            // A chest to the right, a Piggy Bank to the left.
            bool chest = false, bank = false;
            foreach (float dx in new[] { 2.5f, 3.5f, 4.5f }) if (!chest) chest = Chests.TryPlace(hero, new Vector2(hb.center.x + dx, hb.min.y + 0.3f));
            mod.Inventory.Slots[0] = ItemStack.Of(ItemType.PiggyBank);
            foreach (float dx in new[] { 2.5f, 3.5f, 4.5f }) if (!bank) bank = Stations.TryPlace(StationKind.PiggyBank, hero, new Vector2(hb.center.x - dx, hb.min.y + 0.3f));
            mod.Inventory.Slots[0] = default;
            CharmCheck("摆好箱子和存钱罐", chest && bank, $"箱子 {chest}，存钱罐 {bank}");
            yield return new WaitForSeconds(0.3f);
            var bankBox = Stations.DevBoxOf(StationKind.PiggyBank);
            if (!chest || !bankBox.HasValue) yield break;
            // The mouse a little above the chest, off it: the chest.
            SmartInteract.DevMouse = new Vector2(hb.center.x + 3f, hb.max.y + 0.8f);
            yield return null;
            yield return null;
            CharmCheck("鼠标在箱子附近：选中箱子", SmartInteract.Picked == SmartInteract.Kind.Chest, $"{SmartInteract.Picked}");
            // Near the Piggy Bank: it, and a right click anywhere opens the bank.
            SmartInteract.DevMouse = new Vector2(bankBox.Value.center.x, bankBox.Value.yMax + 0.6f);
            yield return null;
            yield return null;
            CharmCheck("鼠标在存钱罐附近：选中存钱罐", SmartInteract.Picked == SmartInteract.Kind.Station && Stations.DevSmartPicked == StationKind.PiggyBank,
                $"{SmartInteract.Picked} {Stations.DevSmartPicked}");
            yield return new WaitForSeconds(0.2f);
            CharmShot("bank");
            bool used = SmartInteract.TryUse();
            yield return null;
            CharmCheck("右键打开选中的存钱罐", used && Chests.BankOpen, $"用了 {used}，银行开着 {Chests.BankOpen}");
            Chests.Close(false);
            // A gold outline on the pick.
            SpriteRenderer outline = null;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (sr.name == "Terra Station Outline" && sr.enabled && bankBox.Value.Contains((Vector2)sr.transform.position + Vector2.up * 0.1f)) outline = sr;
            CharmCheck("选中的存钱罐有金色描边", outline != null && outline.color.b < outline.color.r * 0.6f, outline != null ? $"{outline.color}" : "没有描边");
            // Something held on the cursor: nothing picked.
            mod.Inventory.Mouse = ItemStack.Of(ItemType.DirtBlock, 3);
            yield return null;
            yield return null;
            CharmCheck("光标上拿着东西时不选", SmartInteract.Picked == SmartInteract.Kind.None, $"{SmartInteract.Picked}");
            mod.Inventory.Mouse = default;
            // A torch under the mouse: nothing (DisableSmartInteract).
            var tcell = CellGrid.WorldToCell(SmartInteract.DevMouse.Value);
            var keep = TerraWorld.Get(tcell.x, tcell.y);
            TerraWorld.Set(tcell.x, tcell.y, CellKind.Torch);
            yield return null;
            yield return null;
            CharmCheck("鼠标指着火把时不选", SmartInteract.Picked == SmartInteract.Kind.None && !SmartInteract.Showing, $"{SmartInteract.Picked}");
            TerraWorld.Set(tcell.x, tcell.y, keep);
            // The smart cursor off: nothing.
            mod.DevSetSmartCursor(false);
            yield return null;
            yield return null;
            CharmCheck("智能光标关掉时不选", SmartInteract.Picked == SmartInteract.Kind.None, $"{SmartInteract.Picked}");
            mod.DevSetSmartCursor(true);
            Stations.DevRemoveNear(bankBox.Value.center, StationKind.PiggyBank);
            Chests.DevRemoveNear(new Vector2(hb.center.x + 3f, hb.min.y + 0.5f));
            // A townsperson: the Guide, the player beside him, the mouse off him.
            var guide = Town.TownNpcs.DevFeet("Guide");
            if (!guide.HasValue) { CharmCheck("找到向导", false, ""); yield break; }
            hero.transform.position = Clear(hero, guide.Value + new Vector2(-1.6f, 0.6f));
            yield return new WaitForSeconds(0.6f);
            guide = Town.TownNpcs.DevFeet("Guide");
            SmartInteract.DevMouse = guide.Value + new Vector2(1.2f, 1.2f);
            yield return null;
            yield return null;
            CharmCheck("鼠标在向导旁边：选中向导", SmartInteract.Picked == SmartInteract.Kind.Npc && Town.TownNpcs.DevSmartPicked == "Guide",
                $"{SmartInteract.Picked} {Town.TownNpcs.DevSmartPicked}");
            int talks = Town.TownNpcs.DevSmartTalks;
            bool talked = SmartInteract.TryUse();
            yield return null;
            CharmShot("guide");
            CharmCheck("右键和选中的向导说话", talked && Town.TownNpcs.TalkingIsGuide && Town.TownNpcs.DevSmartTalks == talks + 1, $"用了 {talked}");
            Town.TownNpcs.Close(false);
            hero.transform.position = Clear(hero, home);
        }
    }
}
