using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_v5F_cave_zh"): recorder F's takes in Hallownest's rooms. The smart cursor's pickaxe digging a tunnel
    /// into a Forgotten Crossroads wall (its yellow box going ahead along the line), plain torches set in it that come out
    /// as the area's own torch; the Queen's Gardens' thorns: walked into (a mask's worth of life and poison), then blown
    /// away with dynamite and walked through.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_v5F_cave(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            mod.DevSetSmartCursor(false);
            yield return F5SmartDig(new[] { "Crossroads_04", "Crossroads_01", "Crossroads_13", "Fungus2_02", "Ruins1_01" });
            yield return F5Thorns("Fungus3_04");
            yield return F5ThornsHurt("Fungus3_04");
            F5CamOff();
        }

        /// <summary>A room's floor with a wall of diggable ground ahead (four rows high, `need` cells deep): the stand cell and the way.</summary>
        static bool F5DigSite(int need, out Vector2Int stand, out int dir, out int face)
        {
            stand = default; dir = 1; face = 0;
            var region = TerraWorld.Live;
            if (region == null) return false;
            var cr = region.CellRect;
            int best = 0;
            for (int x = cr.x + 4; x < cr.xMax - 4; x++)
                for (int y = cr.y + 4; y < cr.yMax - 8; y++)
                {
                    if (!C4Inside(region, x, y, 30, 12) || !C4Stand(x, y)) continue;
                    foreach (int d in new[] { 1, -1 })
                    {
                        // Room to walk up to it: three standing cells, then the wall.
                        int f = x + d, k = 0;
                        while (k < 6 && C4Stand(f, y)) { f += d; k++; }
                        if (k < 3 || k >= 6) continue;
                        int n = 0;
                        for (; n < need + 6; n++)
                        {
                            int cx = f + d * n;
                            bool ok = TerraWorld.IsSolid(cx, y - 1) && C4Inside(region, cx, y, 30, 12);
                            for (int r = 0; r < 4 && ok; r++) ok = C4Dig(cx, y + r);
                            if (!ok) break;
                        }
                        if (n > best) { best = n; stand = new Vector2Int(x, y); dir = d; face = f; }
                    }
                }
            return best >= need;
        }

        IEnumerator F5SmartDig(string[] scenes)
        {
            string scene = null;
            Vector2Int stand = default;
            int dir = 1, face = 0;
            foreach (var sc in scenes)
            {
                if (!entryGates.ContainsKey(sc)) continue;
                yield return EnterRoom(sc);
                if (GameManager.instance.sceneName != sc) continue;
                if (F5DigSite(10, out stand, out dir, out face)) { scene = sc; break; }
                F5Log($"智能挖掘：{sc} 没有够深的墙");
            }
            if (scene == null) yield break;
            float entered = Time.time;
            var hero = HeroController.instance;
            F5Safe();
            QuietAround(hero.transform.position, 400f);
            var inv = mod.Inventory;
            int style = RegionalTorchAreas.StyleOf(scene);
            var saved = F5Hotbar(ItemType.SolarFlarePickaxe, ItemType.Torch);
            // The area's own torches in the bag (what the smart cursor sets down for a plain one held here).
            int bagSlot = Inventory.HotbarSize + 4;
            var bagSaved = inv.Slots[bagSlot];
            inv.Slots[bagSlot] = ItemStack.Of(RegionalTorches.ItemOf(style), 30);
            inv.Selected = 0;
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, stand);
            C4Face(hero, dir);
            mod.DevSetSmartCursor(true);
            F5CamOn(new Vector2(dir * 2.5f, 1.4f), 5f);
            while (Time.time - entered < 9.5f) { QuietAround(hero.transform.position, 400f); yield return null; }
            F5ClearChat();
            F5CamSnap();
            Vector2 Cursor() => (Vector2)col.bounds.center + new Vector2(dir * 3.2f, 0.15f);
            aimInit = false;
            SmoothAim(Cursor(), 99f);
            int mined0 = 0;
            for (int n = 0; n < 16; n++) for (int r = 0; r < 4; r++) if (!TerraWorld.IsSolid(face + dir * n, stand.y + r)) mined0++;
            StartRec("v5F_smart_dig");
            yield return new WaitForSeconds(0.3f);
            // In to the wall, then the pickaxe held with the cursor out ahead: the yellow box steps along the line.
            float wallX = CellGrid.CellCenter(face, 0).x - dir * 1.0f;
            float endX = CellGrid.CellCenter(face + dir * 9, 0).x;
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                SmoothAim(Cursor(), 8f);
                F5Mouse(aimNow);
                mod.DevHold = true;
                if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow, false, true);
                // Forward while the way ahead is open at the feet and the chest.
                var ahead = CellGrid.WorldToCell(new Vector2(col.bounds.center.x + dir * (col.bounds.extents.x + 0.3f), col.bounds.min.y + 0.2f));
                bool open = !TerraWorld.IsSolid(ahead.x, ahead.y) && !TerraWorld.IsSolid(ahead.x, ahead.y + 1) && !TerraWorld.IsSolid(ahead.x, ahead.y + 2);
                Player.TerraMovement.DevMove = open ? dir : 0;
                if ((col.bounds.center.x - endX) * dir > 0f) break;
                yield return null;
            }
            mod.DevHold = null;
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.3f);
            // Torches back along the tunnel: a plain one held, the area's own set down.
            inv.Selected = 1;
            C4Face(hero, -dir);
            int torches = 0;
            for (int k = 0; k < 3; k++)
            {
                var spot = CellGrid.WorldToCell((Vector2)col.bounds.center + new Vector2(-dir * (1.0f + 1.1f * k), -0.4f));
                var at = CellGrid.CellCenter(spot.x, stand.y);
                for (float t = 0f; t < 0.4f; t += Time.deltaTime) { SmoothAim(at, 10f); F5Mouse(aimNow); yield return null; }
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(at, false, true);
                yield return new WaitForSeconds(0.35f);
            }
            for (int x = face - dir * 2; x != face + dir * 14; x += dir) if (TerraWorld.Get(x, stand.y) == CellKind.Torch) torches++;
            for (float t = 0f; t < 1.4f; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(-dir * 3f, 1.2f), 2.5f); F5Mouse(aimNow); yield return null; }
            StopRec();
            int mined = 0;
            for (int n = 0; n < 16; n++) for (int r = 0; r < 4; r++) if (!TerraWorld.IsSolid(face + dir * n, stand.y + r)) mined++;
            F5Log($"智能挖掘：{scene} 站 {stand} 往{(dir > 0 ? "右" : "左")}，挖开 {mined - mined0} 格，火把 {torches}（地区火把 {ItemDb.Get(RegionalTorches.ItemOf(style)).Name}，背包剩 {inv.Slots[bagSlot].Count}，手里普通火把 {inv.Slots[1].Count}）");
            mod.DevSetSmartCursor(false);
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = saved[i];
            inv.Slots[bagSlot] = bagSaved;
            mod.DevAim = null;
        }

        /// <summary>The room's widest thorn patch (its collider), the room entered and quiet.</summary>
        IEnumerator F5ThornRoom(string scene, System.Action<Collider2D> done)
        {
            if (GameManager.instance.sceneName != scene) yield return EnterRoom(scene);
            F5Safe();
            Collider2D thorn = null;
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                if (dh.gameObject.scene.name == scene && dh.hazardType == 2 && Player.Poison.IsThorn(dh.gameObject))
                    foreach (var c in dh.GetComponents<Collider2D>()) if (thorn == null || c.bounds.size.x > thorn.bounds.size.x) thorn = c;
            done(thorn);
        }

        /// <summary>The thorns dug out with the pickaxe, their art cut away cell by cell; then walked over unhurt.</summary>
        IEnumerator F5Thorns(string scene)
        {
            float entered = Time.time;
            Collider2D thorn = null;
            yield return F5ThornRoom(scene, c => thorn = c);
            var hero = HeroController.instance;
            if (thorn == null) { F5Log($"荆棘：{scene} 没有荆棘"); yield break; }
            QuietAround(hero.transform.position, 400f);
            var b = thorn.bounds;
            var inv = mod.Inventory;
            var saved = F5Hotbar(ItemType.SolarFlarePickaxe);
            inv.Selected = 0;
            var col = hero.GetComponent<BoxCollider2D>();
            hero.transform.position = Clear(hero, new Vector2(b.min.x - 1.6f, b.max.y + 0.6f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, 1);
            F5CamOn(new Vector2(3.0f, 2.4f), 5f);
            while (Time.time - entered < 9.5f) { QuietAround(hero.transform.position, 400f); yield return null; }
            F5ClearChat();
            F5CamSnap();
            // The spike cells of the patch's top, nearest first: what the pickaxe takes.
            var cells = new List<Vector2Int>();
            var lo = CellGrid.WorldToCell(new Vector2(b.min.x, b.min.y));
            var hi = CellGrid.WorldToCell(new Vector2(Mathf.Min(b.max.x, b.min.x + 4.5f), b.max.y + 0.5f));
            for (int x = lo.x; x <= hi.x; x++)
            {
                int took = 0;
                for (int y = hi.y; y >= lo.y && took < 2; y--)
                    if (DugHazards.IsSpikeCell(new Vector2Int(x, y))) { cells.Add(new Vector2Int(x, y)); took++; }
            }
            F5Log($"荆棘：可挖的荆棘格 {cells.Count}（{lo}..{hi}）");
            aimInit = false;
            SmoothAim(new Vector2(b.min.x + 0.8f, b.max.y + 0.5f), 99f);
            int cut0 = DugHazards.DevSpikesCut;
            StartRec("v5F_thorns");
            yield return new WaitForSeconds(0.6f);
            int dug = 0;
            foreach (var c in cells)
            {
                var at = CellGrid.CellCenter(c.x, c.y);
                // Within reach: a step along the edge when the next ones get far.
                if (at.x - col.bounds.center.x > 2.4f)
                    yield return F5WalkTo(hero, at.x - 2.0f, 1.2f, () => at);
                for (int k = 0; k < 30 && DugHazards.IsSpikeCell(c); k++)
                {
                    SmoothAim(at, 18f);
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow, false, true);
                    yield return null;
                }
                if (!DugHazards.IsSpikeCell(c)) dug++;
            }
            for (float t = 0f; t < 1.0f; t += Time.deltaTime) { SmoothAim(new Vector2(b.min.x + 2f, b.max.y + 0.8f), 3f); yield return null; }
            int hits0 = Player.PlayerHooks.DevSpikeHits;
            yield return F5WalkTo(hero, Mathf.Min(b.max.x - 1f, b.min.x + 3.6f), 2.5f, () => (Vector2)col.bounds.center + new Vector2(2.5f, 0.4f));
            yield return new WaitForSeconds(0.8f);
            StopRec();
            F5Log($"荆棘挖掉：{scene} 挖了 {dug}/{cells.Count} 格，抠掉 {DugHazards.DevSpikesCut - cut0}，走过去扎到 {Player.PlayerHooks.DevSpikeHits - hits0}，主角 {(Vector2)hero.transform.position:F1}");
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }

        /// <summary>A step into the rest of the thorns: a mask's worth of life off and poison, then the hazard's return.</summary>
        IEnumerator F5ThornsHurt(string scene)
        {
            Collider2D thorn = null;
            yield return F5ThornRoom(scene, c => thorn = c);
            var hero = HeroController.instance;
            if (thorn == null) yield break;
            var b = thorn.bounds;
            var col = hero.GetComponent<BoxCollider2D>();
            // The far end of the patch: from its right side, walking left into it.
            hero.transform.position = Clear(hero, new Vector2(b.max.x + 2.4f, b.max.y + 0.6f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, -1);
            mod.Inventory.Selected = 0;
            mod.Stats.FullHeal();
            F5CamOn(new Vector2(-2.5f, 1.8f), 5f);
            yield return new WaitForSeconds(1.5f);
            F5CamSnap();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(-2.5f, 0.4f), 99f);
            int hits0 = Player.PlayerHooks.DevSpikeHits, poison0 = Player.Poison.DevPoisoned, life0 = mod.Stats.Life;
            StartRec("v5F_thorns_hurt");
            yield return new WaitForSeconds(0.5f);
            PlayerData.instance.isInvincible = false;
            Player.TerraMovement.DevMove = -1;
            for (float t = 0f; t < 1.6f && Player.PlayerHooks.DevSpikeHits == hits0; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(-2.5f, 0.3f), 6f); yield return null; }
            Player.TerraMovement.DevMove = 0;
            int life1 = mod.Stats.Life;
            PlayerData.instance.isInvincible = true;
            // The hurt and the poison a moment, then the hazard's return plays out (the fade).
            for (float t = 0f; t < 2.6f; t += Time.deltaTime) yield return null;
            StopRec();
            F5Log($"荆棘扎人：{scene} 扎到 {Player.PlayerHooks.DevSpikeHits - hits0}，中毒 {Player.Poison.DevPoisoned - poison0}，生命 {life0}→{life1}");
            for (float t = 0f; t < 4f && !GameHooks.PlayerHasControl(); t += Time.deltaTime) yield return null;
            mod.DevAim = null;
        }
    }
}
