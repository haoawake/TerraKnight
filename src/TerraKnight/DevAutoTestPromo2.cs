using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "promoA", "promoB", "promoC"): the trailer's second set of clips, each a different
    /// thing the mod does, played the way a person would: the interface worked by a pointer that glides
    /// to a button, hovers it and clicks it (<see cref="UI.TerraCursor.DevDrives"/>), the aim eased onto
    /// its target, the fights flown on wings at a steady distance, the Path of Pain flown along a route
    /// planned round its saws and thorns. Recorded at 50 frames a second, one physics step a frame.
    /// promoA starts from the save profiles (the creative panel, the difficulty, the portal), then King's
    /// Pass, Dirtmouth's townsfolk and shop, the bestiary, crafting, the loadouts, the map and a pylon;
    /// promoB the bosses, the loot and a run of weapons; promoC the Path of Pain, the hook, the pickaxe
    /// and the Gravity Globe.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        // ------------------------------------------------------------------ the pointer and the aim

        Vector2 aimNow;
        bool aimInit;

        /// <summary>The aim (and the drawn cursor) eased toward a point of the world.</summary>
        void SmoothAim(Vector2 world, float rate = 9f)
        {
            if (!aimInit)
            {
                aimNow = world;
                aimInit = true;
            }
            aimNow = Vector2.Lerp(aimNow, world, 1f - Mathf.Exp(-rate * Time.deltaTime));
            AimAt(aimNow);
        }

        static Vector2 GuiOf(Vector2 world)
        {
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return new Vector2(Screen.width / 2f, Screen.height / 2f);
            var sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        /// <summary>The pointer glides to a GUI point along a slight curve, easing in and out.</summary>
        IEnumerator Glide(Vector2 to, float seconds)
        {
            var from = UI.TerraCursor.DevAt ?? new Vector2(Screen.width * 0.55f, Screen.height * 0.6f);
            var d = to - from;
            var bend = new Vector2(-d.y, d.x).normalized * Mathf.Min(70f, d.magnitude * 0.12f);
            var ctrl = (from + to) / 2f + bend;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / seconds);
                UI.TerraCursor.DevAt = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * ctrl + u * u * to;
                yield return null;
            }
            UI.TerraCursor.DevAt = to;
        }

        /// <summary>To a rect, a short pause on it, a click.</summary>
        IEnumerator ClickAt(Rect r, float glide = 0.45f)
        {
            yield return Glide(r.center, glide);
            yield return new WaitForSeconds(0.1f);
            UI.TerraCursor.DevClick();
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.12f);
        }

        IEnumerator ClickHit(string kind, int index, float glide = 0.45f)
        {
            yield return null;
            var r = InventoryGui.DevHitRect(kind, index);
            if (r == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制 找不到按钮 {kind} {index}");
                yield break;
            }
            yield return ClickAt(r.Value, glide);
        }

        void PointerOn(Vector2 at)
        {
            UI.TerraCursor.DevDrives = true;
            UI.TerraCursor.DevAt = at;
        }

        void PointerOff()
        {
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevHeld = false;
            UI.TerraCursor.DevAt = null;
        }

        int SlotOf(ItemType t)
        {
            var inv = mod.Inventory;
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) return i;
            return -1;
        }

        // ------------------------------------------------------------------ promoA: from the menu

        /// <summary>From Boot: the profiles, the creative panel, a slot, the four difficulties and Master; recorded on into the portal.</summary>
        IEnumerator PromoMenu()
        {
            UIManager.instance.UIGoToProfileMenu();
            yield return new WaitForSeconds(2.5f);
            PointerOn(new Vector2(Screen.width * 0.5f, Screen.height * 0.62f));
            StartRec("menu_difficulty");
            yield return new WaitForSeconds(0.8f);
            yield return Glide(UI.CreativeMenu.DevRect("creative").center, 0.9f);
            yield return new WaitForSeconds(0.25f);
            UI.CreativeMenu.DevOpen();
            mod.PlaySound("Menu_Tick", 0.7f);
            yield return new WaitForSeconds(0.9f);
            yield return Glide(UI.CreativeMenu.DevRect("slot", slot).center, 0.6f);
            yield return new WaitForSeconds(0.3f);
            UI.CreativeMenu.DevChoose(slot);
            mod.PlaySound("Menu_Tick", 0.7f);
            yield return new WaitForSeconds(0.5f);
            if (UI.CreativeMenu.DevStep == "Confirm")
            {
                yield return Glide(UI.CreativeMenu.DevRect("confirm").center, 0.5f);
                yield return new WaitForSeconds(0.25f);
                UI.CreativeMenu.DevConfirm();
                mod.PlaySound("Menu_Tick", 0.7f);
                yield return new WaitForSeconds(0.5f);
            }
            for (int i = 1; i <= 4; i++)
            {
                var r = UI.CreativeMenu.DevRect("difficulty", i);
                yield return Glide(new Vector2(r.x + r.width * 0.42f, r.center.y), i == 1 ? 0.5f : 0.32f);
                yield return new WaitForSeconds(i == 4 ? 0.7f : 0.38f);
            }
            UI.CreativeMenu.DevPick(DifficultyLevel.Master);
            mod.PlaySound("Menu_Tick", 0.7f);
            yield return new WaitForSeconds(0.3f);
            PointerOff();
            mod.StartCoroutine(StopRecAfterPortal());
        }

        IEnumerator StopRecAfterPortal()
        {
            float until = Time.realtimeSinceStartup + 90f;
            GameManager g;
            while (Time.realtimeSinceStartup < until && ((g = GameManager.UnsafeInstance) == null || g.sceneName != "Tutorial_01" || !GameHooks.PlayerHasControl()))
                yield return null;
            yield return new WaitForSeconds(2.5f);
            StopRec();
        }

        IEnumerator PromoATest(HeroController hero)
        {
            // The menu's recording runs on until the portal has let the hero go.
            while (recording) yield return null;
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(0.5f);
            yield return PromoTown(hero);
            yield return PromoBestiary(hero);
            yield return PromoCrafting(hero);
            yield return PromoLoadouts(hero);
            yield return PromoMapPylon(hero);
            Plugin.Log.LogInfo("[AutoTest] 录制 promoA 完成");
        }

        /// <summary>King's Pass: a tunnel into the rock wall on the left with the smart cursor, torches set along it.</summary>
        IEnumerator PromoTunnel(HeroController hero)
        {
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            mod.TrySwitchLoadout(0);
            yield return null;
            // The thickest rock face in the scene with a floor before it: standing room (four free cells
            // over a solid one), a wall within a few cells, and diggable rock three cells high behind it,
            // only the grid's own (a breakable wall or a door is the game's object: a swing at it knocks the hero back).
            bool PlainRock(int gx, int gy)
            {
                if (!TerraWorld.IsSolid(gx, gy) || !TerraWorld.CanDig(gx, gy)) return false;
                foreach (var hit in Physics2D.OverlapBoxAll(CellGrid.CellCenter(gx, gy), new Vector2(0.45f, 0.45f), 0f, terrain))
                    if (!hit.name.StartsWith("Terra Chunk")) return false;
                return true;
            }
            // Something that rings when struck (TinkEffect) knocks the hero back at each swing, as the nail does.
            bool NearTink(Vector2 p)
            {
                foreach (var c in Physics2D.OverlapCircleAll(p, 2.5f))
                    if (c.GetComponent<TinkEffect>() != null) return true;
                return false;
            }
            var candidates = new List<(int run, Vector2Int stand, int dir)>();
            var region = TerraWorld.Live;
            int dir = -1, bestRun = 0;
            Vector2Int stand = default;
            if (region != null)
            {
                var cr = region.CellRect;
                for (int x = cr.x + 6; x < cr.xMax - 6; x += 2)
                    for (int y = cr.y + 6; y < cr.yMax - 8; y++)
                    {
                        if (!TerraWorld.IsSolid(x, y - 1) || TerraWorld.IsSolid(x, y) || TerraWorld.IsSolid(x, y + 1) || TerraWorld.IsSolid(x, y + 2) || TerraWorld.IsSolid(x, y + 3)) continue;
                        foreach (int d in new[] { -1, 1 })
                        {
                            int fx = x + d, k = 0;
                            while (k < 4 && !TerraWorld.IsSolid(fx, y)) { fx += d; k++; }
                            if (k >= 4) continue;
                            int run = 0;
                            for (int i = 0; i < 60; i++)
                            {
                                int cx = fx + d * i;
                                bool ok = true;
                                for (int r = 0; r < 3 && ok; r++) if (!PlainRock(cx, y + r)) ok = false;
                                if (!ok) break;
                                run++;
                            }
                            if (run >= 12) candidates.Add((run, new Vector2Int(x, y), d));
                        }
                    }
            }
            candidates.Sort((a, b) => b.run.CompareTo(a.run));
            foreach (var cand in candidates)
            {
                bool tink = false;
                for (int i = 0; i <= 16 && !tink; i += 2) tink = NearTink(CellGrid.CellCenter(cand.stand.x + cand.dir * i, cand.stand.y + 1));
                if (tink) continue;
                bestRun = cand.run;
                stand = cand.stand;
                dir = cand.dir;
                break;
            }
            if (bestRun > 0)
            {
                var feet = CellGrid.CellCenter(stand.x, stand.y) - new Vector2(0f, CellGrid.CellSize / 2f);
                float bottom = col.bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(feet.x, feet.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            }
            float t0;
            yield return new WaitForSeconds(1.2f);
            // Where it landed: dig toward the side with more diggable rock at its height.
            int RunFrom(int d)
            {
                var at = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
                int fx = at.x + d, k = 0;
                while (k < 5 && !TerraWorld.IsSolid(fx, at.y)) { fx += d; k++; }
                if (k >= 5) return 0;
                int run = 0;
                for (int i = 0; i < 60; i++)
                {
                    bool ok = true;
                    for (int r = 0; r < 3 && ok; r++) if (!PlainRock(fx + d * i, at.y + r)) ok = false;
                    if (!ok) break;
                    run++;
                }
                return run;
            }
            int left = RunFrom(-1), right = RunFrom(1);
            dir = right >= left ? 1 : -1;
            Plugin.Log.LogInfo($"[AutoTest] 录制 隧道：{GameManager.instance.sceneName} 选的格 {stand}（{bestRun} 格），落地后左边 {left} 格、右边 {right} 格，往{(dir < 0 ? "左" : "右")}挖");
            int pick = 1;
            var savedTorch = inv.Slots[9];
            inv.Slots[9] = ItemStack.Of(ItemType.Torch, 99);
            inv.Selected = pick;
            mod.DevSetSmartCursor(false);
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2.5f * dir, 0.1f), 99f);
            StartRec("tunnel_kingspass");
            yield return new WaitForSeconds(0.4f);
            float startX = col.bounds.center.x, lastTorch = startX - dir;
            int torches = 0, dug = 0;
            t0 = Time.time;
            float nextLog = 0f;
            while (Time.time - t0 < 7.5f)
            {
                PlayerData.instance.isInvincible = true;
                var c = (Vector2)col.bounds.center;
                if (Time.time - t0 >= nextLog)
                {
                    nextLog += 0.5f;
                    Plugin.Log.LogInfo($"[AutoTest] 录制 隧道 {Time.time - t0:F1}s：主角 {c:F2} 速度 {hero.GetComponent<Rigidbody2D>().linearVelocity:F2} 移动 {Player.TerraMovement.DevMove} 瞄准 {aimNow:F2} 用着 {mod.IsUsingItem} 着地 {hero.cState.onGround} 状态 {hero.hero_state}");
                }
                // The next solid cell of the column in front (feet to head), then of the one after it.
                int feetY = CellGrid.WorldToCell(new Vector2(c.x, col.bounds.min.y + 0.1f)).y;
                int frontX = CellGrid.WorldToCell(new Vector2(dir > 0 ? col.bounds.max.x + 0.05f : col.bounds.min.x - 0.05f, c.y)).x;
                Vector2Int? next = null;
                for (int k = 0; k < 2 && next == null; k++)
                    for (int r = 0; r < 3 && next == null; r++)
                        if (TerraWorld.IsSolid(frontX + dir * k, feetY + r)) next = new Vector2Int(frontX + dir * k, feetY + r);
                bool blocked = next != null && next.Value.x == frontX;
                SmoothAim(next != null ? CellGrid.CellCenter(next.Value.x, next.Value.y) : c + new Vector2(2.6f * dir, 0.15f), 14f);
                Player.TerraMovement.DevMove = blocked ? 0 : dir;
                if ((c.x - lastTorch) * dir > 3f)
                {
                    // A torch on the tunnel's wall behind.
                    Player.TerraMovement.DevMove = 0;
                    inv.Selected = 9;
                    var spot = c + new Vector2(-1.0f * dir, 0.5f);
                    for (float t = 0f; t < 0.18f; t += Time.deltaTime)
                    {
                        SmoothAim(spot, 20f);
                        yield return null;
                    }
                    while (!mod.DevReady) yield return null;
                    mod.DevUseSelected(spot, false, true);
                    torches++;
                    lastTorch = c.x;
                    yield return new WaitForSeconds(0.2f);
                    inv.Selected = pick;
                }
                else if (mod.DevReady && !mod.IsUsingItem && next != null)
                {
                    var target = CellGrid.CellCenter(next.Value.x, next.Value.y);
                    var before = TerraWorld.Get(next.Value.x, next.Value.y);
                    bool can = mod.DevCanUse(ItemType.SolarFlarePickaxe, target, out var cell, out _);
                    mod.DevUseSelected(target, false, true);
                    if (TerraWorld.Get(next.Value.x, next.Value.y) != before) dug++;
                    else if (Time.time - t0 < 1.5f) Plugin.Log.LogInfo($"[AutoTest] 录制 隧道：敲 {next.Value} ({before}) 没掉，能用 {can} 格 {cell}，选中 {inv.Current.Def?.Name}");
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.6f);
            StopRec();
            mod.DevSetSmartCursor(false);
            inv.Slots[9] = savedTorch;
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 隧道：挖进 {(col.bounds.center.x - startX) * dir:F1}，挥镐 {dug} 次，火把 {torches}");
        }

        /// <summary>Dirtmouth: a walk along the street past the townsfolk, then the Merchant: his chat, his shop, a buy.</summary>
        IEnumerator PromoTown(HeroController hero)
        {
            var inv = mod.Inventory;
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            var names = new[] { "Guide", "Merchant", "Nurse", "Demolitionist", "ArmsDealer", "Dryad", "GoblinTinkerer", "Wizard", "Mechanic", "Clothier", "Painter", "DyeTrader", "PartyGirl", "Stylist", "TaxCollector", "Truffle", "Pirate", "Steampunker", "Cyborg", "WitchDoctor", "Golfer", "BestiaryGirl", "Angler", "SantaClaus", "Princess", "TravellingMerchant", "SkeletonMerchant" };
            var xs = new List<float>();
            float feetY = 0f;
            foreach (var n in names)
            {
                var p = TownNpcs.DevPos(n);
                if (p == null) continue;
                xs.Add(p.Value.x);
                feetY = p.Value.y;
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 城镇：{TownNpcs.Count} 位 NPC：{TownNpcs.DevDescribe()}");
            if (xs.Count == 0) yield break;
            xs.Sort();
            var col = hero.GetComponent<BoxCollider2D>();
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(xs[0] - 5f, feetY - 20f / 32f - bottom + 0.05f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            aimInit = false;
            StartRec("town_walk");
            float t0 = Time.time, endX = xs[xs.Count - 1] + 2f;
            Player.TerraMovement.DevMove = 1;
            while (Time.time - t0 < 6.5f && col.bounds.center.x < endX)
            {
                PlayerData.instance.isInvincible = true;
                // The cursor drifts over the next townsperson ahead (its name shows, as in Terraria).
                Vector2 c = col.bounds.center;
                float next = endX;
                foreach (var x in xs) if (x > c.x + 0.8f) { next = x; break; }
                SmoothAim(new Vector2(next, feetY + 1.1f), 4f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.5f);
            StopRec();

            // The Merchant: walk up, right click, the chat, its "Shop", a buy, into the bag.
            var mpos = TownNpcs.DevPos("Merchant");
            if (mpos == null) yield break;
            hero.transform.position = Clear(hero, new Vector2(mpos.Value.x - 2.6f, feetY - 20f / 32f - bottom + 0.05f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(0.8f, 1.2f)));
            StartRec("npc_shop");
            yield return new WaitForSeconds(0.3f);
            mpos = TownNpcs.DevPos("Merchant");
            yield return Glide(GuiOf(mpos.Value + new Vector2(0f, 0.9f)), 0.55f);
            yield return new WaitForSeconds(0.2f);
            bool talked = TownNpcs.TryRightClick(TownNpcs.DevPos("Merchant").Value);
            yield return new WaitForSeconds(1.3f);
            yield return ClickHit("ChatButton", 0, 0.5f);
            yield return new WaitForSeconds(0.5f);
            var shopSlots = InventoryGui.DevHitIndexes("ShopSlot");
            int filled = 0;
            foreach (int i in shopSlots) if (i < TownNpcs.ShopItems.Length && !TownNpcs.ShopItems[i].IsEmpty) filled = i;
            // Hover along the stock, then buy one (onto the cursor) and put it in the bag.
            foreach (int i in new[] { 1, 3, 5 })
                if (i <= filled)
                {
                    var r = InventoryGui.DevHitRect("ShopSlot", i);
                    if (r != null) yield return Glide(r.Value.center, 0.3f);
                    yield return new WaitForSeconds(0.25f);
                }
            int buy = Mathf.Min(6, filled);
            yield return ClickHit("ShopSlot", buy, 0.3f);
            yield return new WaitForSeconds(0.35f);
            int empty = -1;
            for (int i = 10; i < Inventory.Size; i++) if (inv.Slots[i].IsEmpty) { empty = i; break; }
            if (empty >= 0) yield return ClickHit("Slot", empty, 0.5f);
            yield return new WaitForSeconds(0.6f);
            StopRec();
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            PointerOff();
            Plugin.Log.LogInfo($"[AutoTest] 录制 商店：对话 {talked}，商品 {TownNpcs.DevStock()}，光标 {(inv.Mouse.IsEmpty ? "空" : inv.Mouse.Def.Name)}");
            mod.ReturnOrDropMouseItem();
        }

        /// <summary>The bestiary: opened, entries hovered, one chosen (its stats and drops), a copy spawned; then a drop table in Normal and in Master.</summary>
        IEnumerator PromoBestiary(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1f, 1f)));
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.4f);
            StartRec("bestiary");
            yield return new WaitForSeconds(0.3f);
            yield return ClickHit("BestiaryButton", 0, 0.6f);
            yield return new WaitForSeconds(0.6f);
            var visible = InventoryGui.DevBestiaryVisible();
            Plugin.Log.LogInfo($"[AutoTest] 录制 图鉴：可见 {visible.Count} 个：{string.Join(",", visible)}");
            for (int k = 0; k < visible.Count && k < 4; k++)
            {
                var r = InventoryGui.DevBestiaryEntryRect(visible[Mathf.Min(visible.Count - 1, 2 + k * 3)]);
                if (r != null) yield return Glide(r.Value.center, 0.3f);
                yield return new WaitForSeconds(0.22f);
            }
            string pickRec = visible.Contains("Mawlek") ? "Mawlek" : visible.Count > 7 ? visible[7] : visible.Count > 0 ? visible[0] : null;
            if (pickRec != null)
            {
                var r = InventoryGui.DevBestiaryEntryRect(pickRec);
                if (r != null) yield return ClickAt(r.Value, 0.35f);
            }
            yield return new WaitForSeconds(1.4f);
            // Creative: the spawn button by the name makes one in front of the player.
            if (InventoryGui.DevSpawnRect != null) yield return ClickAt(InventoryGui.DevSpawnRect.Value, 0.5f);
            yield return new WaitForSeconds(0.5f);
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            yield return new WaitForSeconds(1.6f);
            StopRec();

            // The same drop table in Normal, then in Master: the chances double.
            const string rec = "AcidWalker";
            InventoryGui.Open = true;
            GameDifficulty.Current = DifficultyLevel.Normal;
            InventoryGui.DevBestiaryShow(rec);
            yield return new WaitForSeconds(0.5f);
            StartRec("bestiary_rates");
            yield return Glide(new Vector2(Screen.width * 0.62f, Screen.height * 0.55f), 0.5f);
            yield return new WaitForSeconds(1.4f);
            GameDifficulty.Current = DifficultyLevel.Master;
            Plugin.Log.LogInfo($"[AutoTest] 录制 图鉴掉率：{recFrame} 帧时换成大师");
            yield return new WaitForSeconds(1.8f);
            StopRec();
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            PointerOff();
        }

        /// <summary>Crafting from a plain bag in Dirtmouth: the column scrolled, a pickaxe and a sword made and put away.</summary>
        IEnumerator PromoCrafting(HeroController hero)
        {
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            mod.ApplyCreative(false);
            mod.SetInfiniteItems(false);
            inv.Clear();
            inv.Slots[10] = ItemStack.Of(ItemType.Wood, 250);
            inv.Slots[11] = ItemStack.Of(ItemType.StoneBlock, 120);
            inv.Slots[12] = ItemStack.Of(ItemType.IronBar, 40);
            inv.Slots[13] = ItemStack.Of(ItemType.CopperBar, 30);
            inv.Slots[14] = ItemStack.Of(ItemType.Gel, 40);
            inv.Slots[15] = ItemStack.Of(ItemType.GoldBar, 30);
            inv.Slots[16] = ItemStack.Of(ItemType.Lens, 6);
            inv.Slots[0] = ItemStack.Of(ItemType.CopperShortsword, 1);
            inv.Slots[1] = ItemStack.Of(ItemType.CopperPickaxe, 1);
            yield return null;
            var col = hero.GetComponent<BoxCollider2D>();
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1f, 1f)));
            InventoryGui.Open = true;
            InventoryGui.DevFocus(0);
            yield return new WaitForSeconds(0.4f);
            StartRec("crafting");
            yield return new WaitForSeconds(0.3f);
            var offered = InventoryGui.DevOffered();
            Plugin.Log.LogInfo($"[AutoTest] 录制 制作：{offered.Count} 个配方：{string.Join(",", offered)}");
            foreach (var want in new[] { ItemType.GoldPickaxe, ItemType.GoldBroadsword })
            {
                int target = offered.IndexOf(want);
                if (target < 0) continue;
                var r0 = InventoryGui.DevHitRect("Recipe", InventoryGui.Focus);
                if (r0 != null) yield return Glide(r0.Value.center, 0.45f);
                // The wheel down the column to it, a recipe at a time.
                while (InventoryGui.Focus != target)
                {
                    InventoryGui.DevScrollRecipes(target > InventoryGui.Focus ? 1 : -1);
                    mod.PlaySound("Menu_Tick", 0.5f);
                    yield return new WaitForSeconds(0.11f);
                }
                yield return new WaitForSeconds(0.45f);
                yield return ClickHit("Recipe", target, 0.2f);
                yield return new WaitForSeconds(0.35f);
                int empty = -1;
                for (int i = 2; i < 10; i++) if (inv.Slots[i].IsEmpty) { empty = i; break; }
                if (empty >= 0) yield return ClickHit("Slot", empty, 0.5f);
                yield return new WaitForSeconds(0.3f);
                offered = InventoryGui.DevOffered();
            }
            yield return new WaitForSeconds(0.5f);
            StopRec();
            InventoryGui.Open = false;
            PointerOff();
            mod.ReturnOrDropMouseItem();
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            mod.ApplyCreative(true);
        }

        /// <summary>The four loadouts switched with the inventory open: the armour, the wings and the bag change together.</summary>
        IEnumerator PromoLoadouts(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            mod.TrySwitchLoadout(0);
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1f, 1f)));
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.5f);
            StartRec("loadouts");
            yield return new WaitForSeconds(0.4f);
            foreach (int l in new[] { 1, 2, 3, 0 })
            {
                yield return ClickHit("Loadout", l, 0.4f);
                yield return new WaitForSeconds(0.75f);
            }
            StopRec();
            InventoryGui.Open = false;
            PointerOff();
        }

        /// <summary>The map opened and drawn back, a pylon far away hovered and clicked, the teleport.</summary>
        IEnumerator PromoMapPylon(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            Pylons.Pylon target = null;
            foreach (var want in new[] { "Ruins1_31", "Fungus3_40", "Fungus1_15", "Fungus2_02" })
                if (target == null) foreach (var p in Pylons.Unlocked) if (p.Scene == want) target = p;
            if (target == null) foreach (var p in Pylons.Unlocked) target = p;
            if (target == null) yield break;
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1f, 1f)));
            StartRec("map_pylon2");
            yield return new WaitForSeconds(0.3f);
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevZoom(3f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                UI.WorldMap.DevZoom(Mathf.Lerp(3f, 0.9f, Mathf.SmoothStep(0f, 1f, t / 1.2f)));
                yield return null;
            }
            var at = UI.WorldMap.DevPylonScreen(target);
            if (at == null)
            {
                UI.WorldMap.DevCentrePylon(target);
                yield return null;
                at = UI.WorldMap.DevPylonScreen(target);
            }
            if (at != null)
            {
                yield return Glide(at.Value, 0.9f);
                yield return new WaitForSeconds(0.6f);
                UI.TerraCursor.DevClick();
            }
            // On until the hero has come out of the far pylon's portal (the load is slow while recording).
            yield return new WaitForSeconds(0.5f);
            PointerOff();
            float until = Time.time + 25f;
            while (Time.time < until && (GameManager.instance.sceneName != target.Scene || GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl())) yield return null;
            yield return new WaitForSeconds(2.2f);
            StopRec();
            PointerOff();
            Plugin.Log.LogInfo($"[AutoTest] 录制 地图晶塔：去 {target.Scene}，图上 {at}，现在 {GameManager.instance.sceneName}");
        }

        // ------------------------------------------------------------------ promoB: the fights

        IEnumerator PromoBTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            yield return SmoothFight(hero, "fight_melee_gruz", "BigFly", "Crossroads_04", 0, new[] { ItemType.Terrarian, ItemType.Meowmere }, null, 16f, 5.5f);
            yield return SmoothFight(hero, "fight_ranged_vengefly", "BigBuzzer", "Fungus1_20_v02", 1, new[] { ItemType.Phantasm, ItemType.ElectrosphereLauncher }, null, 16f, 6.5f);
            yield return SmoothFight(hero, "fight_magic_uumuu", "MegaJellyfish", "Fungus3_archive_02_boss", 2, new[] { ItemType.NebulaBlaze, ItemType.RazorbladeTyphoon }, null, 16f, 6.5f);
            yield return SmoothFight(hero, "fight_summon_moss", "MegaMossCharger", "Fungus1_29", 3, new[] { ItemType.MoonLordWhip }, new[] { ItemType.StardustCellStaff, ItemType.LunarPortalStaff, ItemType.RainbowCrystalStaff }, 16f, 5f);
            yield return SmoothFight(hero, "fight_hive", "HiveKnight", "Hive_05", 0, new[] { ItemType.StarWrath, ItemType.Zenith }, null, 18f, 5.5f);
            yield return SmoothFight(hero, "fight_dung", "DungDefender", "Waterways_05_boss", 1, new[] { ItemType.SDMG, ItemType.CelebrationMk2 }, null, 14f, 6.5f);
            yield return SmoothFight(hero, "fight_vessel", "InfectedKnight", "Abyss_19", 2, new[] { ItemType.LastPrism, ItemType.LunarFlareBook }, null, 14f, 6.5f);
            yield return SmoothFight(hero, "fight_mawlek", "Mawlek", "Crossroads_09", 3, new[] { ItemType.Kaleidoscope }, new[] { ItemType.StardustDragonStaff, ItemType.Terraprisma }, 12f, 5f);
            yield return SmoothFight(hero, "fight_soulwarrior", "MageKnight", "Ruins1_23", 0, new[] { ItemType.Zenith, ItemType.DayBreak }, null, 12f, 5.5f);
            yield return PromoLoot(hero);
            yield return PromoWeapons(hero);
            Plugin.Log.LogInfo("[AutoTest] 录制 promoB 完成");
        }

        /// <summary>
        /// A boss fought from the wing: the hero keeps its side and a steady distance, a little above the
        /// boss, holding the air by flapping, the aim eased onto the boss; weapons in turn, minions first.
        /// </summary>
        IEnumerator SmoothFight(HeroController hero, string clip, string record, string scene, int loadout, ItemType[] weapons, ItemType[] summons, float maxTime, float distance)
        {
            var inv = mod.Inventory;
            string room = scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;
            GameDifficulty.Current = DifficultyLevel.Master;
            PlayerData.instance.isInvincible = true;
            yield return EnterRoom(room);
            hero = HeroController.instance;
            mod.TrySwitchLoadout(loadout);
            yield return null;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var all = BossesOf(record, true);
            if (all.Count == 0)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：找不到 {record}");
                yield break;
            }
            Vector2 bp = all[0].transform.position;
            int walk = 0;
            foreach (int s in new[] { -1, 1 })
            {
                var probe = bp + new Vector2(s * 7f, 3f);
                var down = Physics2D.Raycast(probe, Vector2.down, 30f, terrain);
                if (down.collider == null) continue;
                hero.transform.position = new Vector3(probe.x, down.point.y + 0.7f, hero.transform.position.z);
                walk = -s;
                break;
            }
            Player.TerraMovement.DevMove = walk;
            yield return new WaitForSeconds(1.2f);
            Player.TerraMovement.DevMove = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 20f)
            {
                PlayerData.instance.isInvincible = true;
                bool up = false;
                foreach (var hm in BossesOf(record, false)) if (!hm.isDead && !hm.IsInvincible && OnScreen(hm.transform.position)) up = true;
                if (up && GameHooks.PlayerHasControl()) break;
                yield return new WaitForSeconds(0.1f);
            }
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            aimInit = false;
            HealthManager Nearest()
            {
                HealthManager best = null;
                float bd = float.MaxValue;
                foreach (var hm in BossesOf(record, false))
                {
                    if (hm.isDead) continue;
                    float d = Vector2.Distance(hm.transform.position, col.bounds.center);
                    if (d < bd) { bd = d; best = hm; }
                }
                return best;
            }
            Vector2 Centre(HealthManager hm)
            {
                var c = hm.GetComponent<Collider2D>();
                return c != null ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
            }
            var first = Nearest();
            if (first != null) SmoothAim(Centre(first), 99f);
            StartRec(clip);
            if (summons != null)
                foreach (var s in summons)
                {
                    int si = SlotOf(s);
                    if (si < 0) continue;
                    inv.Selected = si;
                    for (int k = 0; k < 2; k++)
                    {
                        var spot = (Vector2)col.bounds.center + new Vector2(k == 0 ? 1.5f : -1.5f, 1.8f);
                        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
                        {
                            SmoothAim(spot, 14f);
                            yield return null;
                        }
                        while (!mod.DevReady || mod.IsUsingItem) yield return null;
                        mod.DevUseSelected(aimNow);
                        yield return new WaitForSeconds(0.15f);
                    }
                }
            float start = Time.time, quiet = 0f;
            int side = first != null && col.bounds.center.x < Centre(first).x ? -1 : 1, move = 0;
            float slice = Mathf.Max(4f, maxTime / weapons.Length);
            while (Time.time - start < maxTime && quiet < 1.6f)
            {
                PlayerData.instance.isInvincible = true;
                mod.Stats.Mana = mod.Stats.ManaMax;
                var target = Nearest();
                quiet = target == null ? quiet + Time.deltaTime : 0f;
                if (target != null)
                {
                    Vector2 at = Centre(target);
                    SmoothAim(at, 10f);
                    int wi = Mathf.Min(weapons.Length - 1, (int)((Time.time - start) / slice));
                    int slotIndex = SlotOf(weapons[wi]);
                    if (slotIndex >= 0) inv.Selected = slotIndex;
                    mod.DevHold = true;
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow);
                    Vector2 c = col.bounds.center;
                    // Keep this side of the boss; with a wall close behind, go over to the other.
                    if (Physics2D.Raycast(c, Vector2.right * side, 1.6f, terrain).collider != null) side = -side;
                    float wantX = at.x + side * distance;
                    var down = Physics2D.Raycast(c, Vector2.down, 40f, terrain);
                    var upHit = Physics2D.Raycast(c, Vector2.up, 40f, terrain);
                    float ground = down.collider != null ? down.point.y : c.y - 6f;
                    float ceiling = upHit.collider != null ? upHit.point.y : c.y + 30f;
                    float wantY = Mathf.Clamp(at.y + 1.5f, ground + 1.8f, Mathf.Max(ground + 1.8f, ceiling - 1.6f));
                    float dx = wantX - c.x;
                    if (Mathf.Abs(dx) > 1.3f) move = dx > 0 ? 1 : -1;
                    else if (Mathf.Abs(dx) < 0.35f) move = 0;
                    Player.TerraMovement.DevMove = move;
                    float dy = wantY - c.y, vy = rb.linearVelocity.y;
                    bool rise = dy > 0.35f || (dy > -0.6f && vy < -1.2f);
                    if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                    Player.TerraMovement.DevJump = rise;
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            mod.DevHold = null;
            yield return new WaitForSeconds(1.2f);
            StopRec();
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{record} {(quiet >= 1.6f ? "打倒了" : "没打完")}，用时 {Time.time - start:F1}s");
        }

        List<HealthManager> EnemiesOnScreen()
        {
            var list = new List<HealthManager>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!hm.isDead && hm.gameObject.activeInHierarchy && OnScreen(hm.transform.position)) list.Add(hm);
            return list;
        }

        /// <summary>The Crossroads' crowded hall in Master: enemies brought down, their loot spilled, then gathered up.</summary>
        IEnumerator PromoLoot(HeroController hero)
        {
            var inv = mod.Inventory;
            GameDifficulty.Current = DifficultyLevel.Master;
            yield return EnterRoom("Crossroads_07");
            hero = HeroController.instance;
            mod.TrySwitchLoadout(1);
            yield return new WaitForSeconds(1f);
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            int bow = SlotOf(ItemType.DaedalusStormbow);
            if (bow >= 0) inv.Selected = bow;
            aimInit = false;
            var enemies = EnemiesOnScreen();
            Plugin.Log.LogInfo($"[AutoTest] 录制 掉落：画面里 {enemies.Count} 个敌人");
            StartRec("loot_drops");
            float t0 = Time.time;
            int kills0 = 0;
            while (Time.time - t0 < 6f)
            {
                PlayerData.instance.isInvincible = true;
                HealthManager target = null;
                float bd = float.MaxValue;
                foreach (var hm in EnemiesOnScreen())
                {
                    float d = Vector2.Distance(hm.transform.position, col.bounds.center);
                    if (d < bd) { bd = d; target = hm; }
                }
                if (target != null)
                {
                    SmoothAim(target.transform.position, 8f);
                    mod.DevHold = true;
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow);
                }
                yield return null;
            }
            mod.DevHold = null;
            kills0 = WorldItems.Count;
            // Gather what fell: walk (and hop) to each stack on the floor.
            t0 = Time.time;
            while (Time.time - t0 < 4.5f)
            {
                PlayerData.instance.isInvincible = true;
                Vector2? drop = null;
                float bd2 = 18f * 18f;
                foreach (var dp in WorldItems.Positions())
                {
                    float d2 = (dp - (Vector2)col.bounds.center).sqrMagnitude;
                    if (d2 < bd2) { bd2 = d2; drop = dp; }
                }
                if (drop == null) break;
                float dx = drop.Value.x - col.bounds.center.x;
                Player.TerraMovement.DevMove = Mathf.Abs(dx) < 0.3f ? 0 : dx > 0 ? 1 : -1;
                bool rise = drop.Value.y > col.bounds.center.y + 0.8f;
                if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                Player.TerraMovement.DevJump = rise;
                SmoothAim(drop.Value, 6f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(0.8f);
            StopRec();
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 掉落：打完时地上 {kills0} 叠，捡完剩 {WorldItems.Count} 叠");
        }

        /// <summary>The City of Tears' crowded hall: a run of weapons, each a moment on the nearest enemy (the times logged for the edit).</summary>
        IEnumerator PromoWeapons(HeroController hero)
        {
            GameDifficulty.Current = DifficultyLevel.Master;
            yield return EnterRoom("Ruins1_32");
            hero = HeroController.instance;
            yield return new WaitForSeconds(1f);
            var col = hero.GetComponent<BoxCollider2D>();
            var run = new (int loadout, ItemType item)[]
            {
                (0, ItemType.InfluxWaver), (0, ItemType.NorthPole), (0, ItemType.VampireKnives), (0, ItemType.Flairon),
                (1, ItemType.Tsunami), (1, ItemType.ChainGun), (1, ItemType.SnowmanCannon), (1, ItemType.Xenopopper),
                (2, ItemType.BetsysWrath), (2, ItemType.BlizzardStaff), (2, ItemType.MagnetSphere), (2, ItemType.Razorpine),
                (3, ItemType.TempestStaff), (3, ItemType.DarkHarvest), (3, ItemType.XenoStaff), (3, ItemType.MorningStar),
            };
            aimInit = false;
            mod.TrySwitchLoadout(0);
            yield return null;
            StartRec("weapons_run");
            float recStart = Time.time;
            var marks = new System.Text.StringBuilder();
            foreach (var (loadout, item) in run)
            {
                // A loadout can't change while an item is out (a flail, a yoyo): wait for it to come back.
                mod.DevHold = null;
                for (float t = 0f; t < 1.2f && mod.IsUsingItem; t += Time.deltaTime) yield return null;
                for (int k = 0; k < 30 && !mod.TrySwitchLoadout(loadout) && SlotOf(item) < 0; k++) yield return null;
                yield return null;
                int slotIndex = SlotOf(item);
                if (slotIndex < 0)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 录制 武器：配装 {loadout} 里找不到 {item}");
                    continue;
                }
                mod.Inventory.Selected = slotIndex;
                marks.Append($"{item}@{Time.time - recStart:F2} ");
                float t0 = Time.time;
                bool summoned = false;
                while (Time.time - t0 < 1.5f)
                {
                    PlayerData.instance.isInvincible = true;
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    HealthManager target = null;
                    float bd = float.MaxValue;
                    foreach (var hm in EnemiesOnScreen())
                    {
                        float d = Vector2.Distance(hm.transform.position, col.bounds.center);
                        if (d < bd) { bd = d; target = hm; }
                    }
                    Vector2 at = target != null ? (Vector2)target.transform.position : (Vector2)col.bounds.center + new Vector2(4f, 0.5f);
                    SmoothAim(at, 10f);
                    var def = ItemDb.Get(item);
                    mod.DevHold = true;
                    if (mod.DevReady && !mod.IsUsingItem && !(def.IsSummon && summoned))
                    {
                        mod.DevUseSelected(aimNow);
                        summoned |= def.IsSummon;
                    }
                    yield return null;
                }
                mod.DevHold = null;
            }
            yield return new WaitForSeconds(0.4f);
            StopRec();
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 武器：{marks}");
        }

        // ------------------------------------------------------------------ promoC: getting about

        /// <summary>Dev (suite "promoD"): the run of weapons alone.</summary>
        IEnumerator PromoDTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            yield return PromoWeapons(hero);
            Plugin.Log.LogInfo("[AutoTest] 录制 promoD 完成");
        }

        /// <summary>
        /// Dev (suite "pylonfade"): a pylon teleport from the City of Tears' Ruins1_23 into its neighbour
        /// Ruins1_31 (already loaded beside it), twice, not recorded: the camera fade's state, the scene,
        /// control and the screen's brightness every half second after the click (the recorded one stayed black).
        /// </summary>
        IEnumerator PylonFadeTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            Pylons.Pylon target = null;
            foreach (var p in Pylons.Unlocked) if (p.Scene == "Ruins1_31") target = p;
            if (target == null) yield break;
            for (int round = 1; round <= 2; round++)
            {
                yield return EnterRoom("Ruins1_23");
                yield return new WaitForSeconds(1.5f);
                float t0 = Time.realtimeSinceStartup;
                Pylons.Teleport(target);
                for (int i = 0; i < 16; i++)
                {
                    float until = Time.realtimeSinceStartup + 0.5f;
                    while (Time.realtimeSinceStartup < until) yield return null;
                    yield return new WaitForEndOfFrame();
                    var tex = ScreenCapture.CaptureScreenshotAsTexture();
                    float sum = 0f;
                    for (int y = 1; y < 8; y++)
                        for (int x = 1; x < 8; x++)
                        {
                            var c = tex.GetPixel(tex.width * x / 8, tex.height * y / 8);
                            sum += (c.r + c.g + c.b) / 3f;
                        }
                    Object.Destroy(tex);
                    var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                    Plugin.Log.LogInfo($"[AutoTest] 晶塔黑屏 第{round}次 {Time.realtimeSinceStartup - t0:F1}s：场景 {GameManager.instance.sceneName}，淡入 {fade?.ActiveStateName}，可操作 {GameHooks.PlayerHasControl()}，亮度 {sum / 49f:F3}，补发淡入 {Pylons.DevFadeKicks}");
                }
            }
        }

        /// <summary>Dev (suite "promoF"): the map and a pylon from the City of Tears, its icon in view, the arrival recorded.</summary>
        IEnumerator PromoFTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            yield return EnterRoom("Ruins1_23");
            yield return new WaitForSeconds(1f);
            yield return PromoMapPylon(HeroController.instance);
            Plugin.Log.LogInfo("[AutoTest] 录制 promoF 完成");
        }

        /// <summary>Dev (suite "promoE"): the tunnel alone, logged.</summary>
        IEnumerator PromoETest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            yield return PromoMapPylon(hero);
            Plugin.Log.LogInfo("[AutoTest] 录制 promoE 完成");
        }

        IEnumerator PromoCTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            yield return PromoPainRoute(hero);
            yield return PromoHook(hero);
            yield return PromoMine(HeroController.instance);
            yield return PromoGravity(HeroController.instance);
            yield return PromoMapPylon(HeroController.instance);
            yield return PromoStations(HeroController.instance);
            Plugin.Log.LogInfo("[AutoTest] 录制 promoC 完成");
        }

        /// <summary>
        /// The Path of Pain's first hall flown end to end along a route planned round its saws and thorns:
        /// a breadth-first search over the room's free space (the hero's box clear of the terrain and of
        /// every hazard's own shape), straightened where the line between two points is clear, then flown
        /// at a steady speed along a smooth curve through it.
        /// </summary>
        IEnumerator PromoPainRoute(HeroController hero)
        {
            const string room = "White_Palace_18";
            var gm = GameManager.instance;
            yield return EnterRoom(room);
            hero = HeroController.instance;
            mod.TrySwitchLoadout(0);
            PlayerData.instance.isInvincible = true;
            var region = TerraWorld.Live;
            if (gm.sceneName != room || region == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制 苦痛路线：没进去，在 {gm.sceneName}");
                yield break;
            }
            var hazards = new HashSet<Collider2D>();
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                if (dh.gameObject.scene.name == room)
                    foreach (var hc in dh.GetComponentsInChildren<Collider2D>()) hazards.Add(hc);
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            // Clear of the walls by the hero's own box; the hazards' shapes by a smaller one (it may graze them).
            Vector2 box = (Vector2)col.bounds.size + new Vector2(0.1f, 0.1f), core = (Vector2)col.bounds.size * 0.6f;
            const int terrainMask = 1 << ChunkColliders.TerrainLayer;
            bool Blocked(Vector2 p)
            {
                if (Physics2D.OverlapBox(p, box, 0f, terrainMask) != null) return true;
                foreach (var hit in Physics2D.OverlapBoxAll(p, core, 0f))
                    if (hazards.Contains(hit)) return true;
                return false;
            }
            // The room on a grid of 0.5-unit steps.
            const float step = 0.5f;
            var cr = region.CellRect;
            Vector2 origin = new Vector2(cr.x, cr.y) * CellGrid.CellSize;
            int w = Mathf.CeilToInt(cr.width * CellGrid.CellSize / step), h = Mathf.CeilToInt(cr.height * CellGrid.CellSize / step);
            var free = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++) free[y * w + x] = !Blocked(origin + new Vector2((x + 0.5f) * step, (y + 0.5f) * step));
                if (y % 20 == 19) yield return null;
            }
            Vector2 P(int i) => origin + new Vector2((i % w + 0.5f) * step, (i / w + 0.5f) * step);
            int Node(Vector2 p) => Mathf.Clamp((int)((p.y - origin.y) / step), 0, h - 1) * w + Mathf.Clamp((int)((p.x - origin.x) / step), 0, w - 1);
            int Nearest(Vector2 p)
            {
                int best = -1;
                float bd = float.MaxValue;
                int c = Node(p), cx = c % w, cy = c / w;
                for (int y = Mathf.Max(0, cy - 12); y <= Mathf.Min(h - 1, cy + 12); y++)
                    for (int x = Mathf.Max(0, cx - 12); x <= Mathf.Min(w - 1, cx + 12); x++)
                    {
                        int i = y * w + x;
                        if (!free[i]) continue;
                        float d = (P(i) - p).sqrMagnitude;
                        if (d < bd) { bd = d; best = i; }
                    }
                return best;
            }
            int startNode = Nearest(col.bounds.center);
            var parent = new int[w * h];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            var queue = new Queue<int>();
            if (startNode >= 0)
            {
                parent[startNode] = -1;
                queue.Enqueue(startNode);
            }
            int[] dxs = { 1, -1, 0, 0, 1, 1, -1, -1 }, dys = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + dxs[k], ny = y + dys[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (!free[j] || parent[j] != -2) continue;
                    if (k >= 4 && (!free[y * w + nx] || !free[ny * w + x])) continue;
                    parent[j] = i;
                    queue.Enqueue(j);
                }
            }
            // The way out: the farthest reachable gate that isn't the one we came in by.
            string entry = EntryGateOf(room);
            int goal = -1, goalLen = -1;
            string goalName = "";
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
            {
                if (tp.gameObject.scene.name != room || tp.name == entry) continue;
                var tc = tp.GetComponent<Collider2D>();
                int g = Nearest(tc != null ? (Vector2)tc.bounds.center : (Vector2)tp.transform.position);
                if (g < 0 || parent[g] == -2) continue;
                int len = 0;
                for (int i = g; i >= 0; i = parent[i]) len++;
                if (len > goalLen) { goalLen = len; goal = g; goalName = tp.name; }
            }
            if (goal < 0)
            {
                // No gate reached: the farthest point the search got to instead.
                for (int i = 0; i < parent.Length; i++)
                {
                    if (parent[i] == -2) continue;
                    int len = 0;
                    for (int j = i; j >= 0 && len < 100000; j = parent[j]) len++;
                    if (len > goalLen) { goalLen = len; goal = i; goalName = "最远处"; }
                }
            }
            if (goal < 0)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制 苦痛路线：起点 {startNode} 走不出去");
                yield break;
            }
            var raw = new List<Vector2>();
            for (int i = goal; i >= 0; i = parent[i]) raw.Add(P(i));
            raw.Reverse();
            // Straightened: from each point, on to the farthest one in a clear line.
            bool Clear2(Vector2 a, Vector2 b)
            {
                int n = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.25f);
                for (int s = 1; s < n; s++) if (Blocked(Vector2.Lerp(a, b, s / (float)n))) return false;
                return true;
            }
            var pts = new List<Vector2> { raw[0] };
            for (int i = 0; i < raw.Count - 1;)
            {
                int j = raw.Count - 1;
                while (j > i + 1 && !Clear2(raw[i], raw[j])) j--;
                pts.Add(raw[j]);
                i = j;
            }
            // Stop short of the far gate (its trigger would change the scene).
            if (pts.Count >= 2)
            {
                var last = pts[pts.Count - 1];
                var prev = pts[pts.Count - 2];
                float l = Vector2.Distance(prev, last);
                if (l > 2.5f) pts[pts.Count - 1] = Vector2.Lerp(prev, last, (l - 2.5f) / l);
                else pts.RemoveAt(pts.Count - 1);
            }
            // A smooth curve through them (Catmull-Rom), sampled finely, then walked at a steady speed.
            var curve = new List<Vector2>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.1f));
                for (int s = 0; s < n; s++)
                {
                    float t = s / (float)n, t2 = t * t, t3 = t2 * t;
                    curve.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            curve.Add(pts[pts.Count - 1]);
            var dist = new float[curve.Count];
            for (int i = 1; i < curve.Count; i++) dist[i] = dist[i - 1] + Vector2.Distance(curve[i - 1], curve[i]);
            float total = dist[curve.Count - 1];
            Vector2 At(float s)
            {
                int lo = 0, hi = curve.Count - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) / 2;
                    if (dist[mid] < s) lo = mid; else hi = mid;
                }
                float seg = dist[hi] - dist[lo];
                return seg > 0f ? Vector2.Lerp(curve[lo], curve[hi], (s - dist[lo]) / seg) : curve[lo];
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 苦痛路线：网格 {w}x{h}，危险物 {hazards.Count}，去 {goalName}，原路 {raw.Count} 点，拉直 {pts.Count} 点，全长 {total:F1}");
            Vector2 offset = (Vector2)col.bounds.center - (Vector2)hero.transform.position;
            const float speed = 8.5f;
            aimInit = false;
            yield return new WaitForSeconds(0.4f);
            StartRec("pain_route");
            float sNow = 0f, t0 = Time.time;
            Vector2 last2 = At(0f);
            Player.TerraMovement.DevJump = true;
            Player.TerraMovement.DevGroundJump(hero);
            // Ease into the speed over the first second.
            while (sNow < total && Time.time - t0 < 60f && gm.sceneName == room && !gm.IsInSceneTransition)
            {
                PlayerData.instance.isInvincible = true;
                float v = speed * Mathf.Clamp01((Time.time - t0) / 0.8f) * Mathf.Clamp01((total - sNow) / 2f + 0.25f);
                sNow = Mathf.Min(total, sNow + v * Time.deltaTime);
                var p = At(sNow);
                var vel = Time.deltaTime > 0f ? (p - last2) / Time.deltaTime : Vector2.zero;
                hero.transform.position = new Vector3(p.x - offset.x, p.y - offset.y, hero.transform.position.z);
                rb.linearVelocity = vel;
                Player.TerraMovement.DevMove = vel.x > 0.6f ? 1 : vel.x < -0.6f ? -1 : 0;
                Player.TerraMovement.DevJump = true;
                SmoothAim(At(Mathf.Min(total, sNow + 4f)) + new Vector2(0f, 0.5f), 5f);
                last2 = p;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(0.6f);
            StopRec();
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 苦痛路线：飞了 {sNow:F1}/{total:F1}，{Time.time - t0:F1}s，现在 {gm.sceneName} {(Vector2)hero.transform.position:F1}");
        }

        /// <summary>The Lunar Hook across a high hall: hooked to the ceiling ahead, pulled up, let go and thrown again.</summary>
        IEnumerator PromoHook(HeroController hero)
        {
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var gm = GameManager.instance;
            yield return EnterRoom("Ruins1_23");
            hero = HeroController.instance;
            mod.TrySwitchLoadout(0);
            yield return new WaitForSeconds(0.5f);
            var hook = Player.GrapplingHook.Instance;
            var col = hero.GetComponent<BoxCollider2D>();
            // Start at the end of the hall with more room ahead.
            float Room(float d) { var hit = Physics2D.Raycast(col.bounds.center, Vector2.right * d, 60f, terrain); return hit.collider != null ? hit.distance : 60f; }
            int dir = Room(1f) >= Room(-1f) ? 1 : -1;
            aimInit = false;
            StartRec("hook_swing");
            yield return new WaitForSeconds(0.4f);
            int latches = 0, misses = 0;
            float tStart = Time.time;
            while (latches < 7 && Time.time - tStart < 14f)
            {
                Vector2 c = col.bounds.center;
                Vector2? anchor = null;
                foreach (float deg in new[] { 50f, 40f, 60f, 30f, 70f, 20f })
                {
                    var d = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad) * dir, Mathf.Sin(deg * Mathf.Deg2Rad));
                    var hit = Physics2D.Raycast(c, d, 14f, terrain);
                    if (hit.collider != null && hit.distance > 2.5f) { anchor = hit.point - d * 0.1f; break; }
                }
                if (anchor == null)
                {
                    // Nothing ahead to catch: drift on a little (or turn round at the hall's end).
                    if (++misses % 4 == 0) dir = -dir;
                    Player.TerraMovement.DevMove = dir;
                    yield return new WaitForSeconds(0.2f);
                    Player.TerraMovement.DevMove = 0;
                    continue;
                }
                for (float t = 0f; t < 0.15f; t += Time.deltaTime)
                {
                    SmoothAim(anchor.Value, 25f);
                    yield return null;
                }
                hook.Throw(hero, ItemType.LunarHook, anchor.Value);
                float t1 = Time.time;
                while (hook.DevState != "Latched" && Time.time - t1 < 0.8f) yield return null;
                if (hook.DevState == "Latched") latches++;
                t1 = Time.time;
                while (Time.time - t1 < 1.1f && Vector2.Distance(col.bounds.center, hook.DevHookPosition) > 2.2f)
                {
                    SmoothAim(hook.DevHookPosition, 8f);
                    yield return null;
                }
                hook.DevJumpRelease(hero);
                Player.TerraMovement.DevMove = dir;
                yield return new WaitForSeconds(0.35f);
                Player.TerraMovement.DevMove = 0;
            }
            hook.DevRelease(hero);
            yield return new WaitForSeconds(1f);
            StopRec();
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 抓钩：钩住 {latches} 次，现在 {gm.sceneName} {(Vector2)hero.transform.position:F1}");
        }
    }
}
