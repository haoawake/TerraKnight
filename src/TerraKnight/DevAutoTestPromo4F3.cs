using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_v5F_spring_zh"): recorder F's shimmer and liquid takes. The Forgotten Crossroads' hot spring
    /// (Crossroads_30): its dormant pylon lit by the first rest on its bench; items thrown into the shimmer change and float
    /// up; the Bottomless Shimmer Bucket made by it from an Empty Bucket; then in Dirtmouth beside a prefab house a pit
    /// poured full of shimmer and swum in; another pit filled with water from one bank and acid from the other, obsidian
    /// where they meet.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_v5F_spring(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return F5SpringWake();
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            yield return F5Transmute();
            yield return F5BucketCraft();
            yield return F5ShimmerHome();
            yield return F5Liquids();
            F5CamOff();
        }

        Rect F5PylonBox(Pylons.Pylon p)
        {
            var region = TerrainSystem.Find(p.Scene);
            var corner = (Vector2)region.CellOffset * CellGrid.CellSize + p.Local;
            return new Rect(corner, new Vector2(Pylons.WidthCells, Pylons.HeightCells) * CellGrid.CellSize);
        }

        IEnumerator F5SpringWake()
        {
            yield return EnterRoom(Shimmer.Scene);
            float entered = Time.time;
            var hero = HeroController.instance;
            F5Safe();
            var p = Pylons.FindDormant(Shimmer.Scene, "RestBench");
            Transform bench = null;
            foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint")) if (go.name == "RestBench" && go.scene.name == Shimmer.Scene) bench = go.transform;
            if (p == null || bench == null) { F5Log($"温泉晶塔：未激活 {p != null}，长椅 {bench != null}"); yield break; }
            var box = F5PylonBox(p);
            var col = hero.GetComponent<BoxCollider2D>();
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(bench.position.x - 6.5f, bench.position.y + 0.5f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, 1);
            F5CamOn(new Vector2(-0.5f, 2.4f));
            while (Time.time - entered < 9.5f) yield return null;
            F5ClearChat();
            F5CamSnap();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2.5f, 0.8f), 99f);
            int lit0 = Pylons.DevDormantLit;
            StartRec("v5F_pylon_wake_spring");
            yield return new WaitForSeconds(0.4f);
            yield return F5WalkTo(hero, bench.position.x, 2.5f, () => box.center + new Vector2(-1.2f, 0.4f));
            yield return new WaitForSeconds(0.25f);
            yield return Tap(Key.W, 0.3f);
            for (float t = 0f; t < 3f && Pylons.DevDormantLit == lit0; t += Time.deltaTime) { SmoothAim(box.center + new Vector2(0f, 0.3f), 3f); yield return null; }
            bool sat = PlayerData.instance.atBench;
            for (float t = 0f; t < 2.6f; t += Time.deltaTime) { SmoothAim(box.center + new Vector2(0f, 0.5f), 3f); yield return null; }
            yield return Tap(Key.Space, 0.2f);
            yield return new WaitForSeconds(1.0f);
            StopRec();
            F5Log($"温泉晶塔：长椅 {(Vector2)bench.position:F1}，晶塔 {box}，坐下 {sat}，点亮 {Pylons.DevDormantLit - lit0}");
        }

        /// <summary>Things thrown into the spring's shimmer from its bank, one after another: each changes and floats up.</summary>
        IEnumerator F5Transmute()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var pool = Shimmer.Pool;
            if (!pool.HasValue) { F5Log("微光：没有池子"); yield break; }
            var things = new[] { ItemType.Zenith, ItemType.LifeCrystal, ItemType.RodofDiscord, ItemType.ClimbingClaws, ItemType.Flare };
            F5Log("微光转化：" + string.Join("，", things.Select(t => ItemDb.Get(t).Name + "→" + (Shimmer.TransformOf(t, out _) != ItemType.None ? ItemDb.Get(Shimmer.TransformOf(t, out _)).Name : "拆解"))));
            var saved = (ItemStack[])inv.Slots.Clone();
            for (int i = 0; i < things.Length; i++) inv.Slots[2 + i] = ItemStack.Of(things[i], things[i] == ItemType.Flare ? 30 : 1);
            inv.Selected = 1;
            var col = hero.GetComponent<BoxCollider2D>();
            float bottom = col.bounds.min.y - hero.transform.position.y;
            // The right bank, by the bench: thrown to the left they land in the spring's near end.
            float bank = pool.Value.xMax + 0.9f;
            hero.transform.position = Clear(hero, new Vector2(bank + 2.5f, pool.Value.yMax + 0.6f - bottom));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, -1);
            WorldItems.DevClear();
            WorldItems.DevNoGrab = true;
            F5CamOn(Vector2.zero);
            f5CamFix = new Vector2(pool.Value.xMax - 1.0f, pool.Value.yMax + 3.4f);
            yield return new WaitForSeconds(1.5f);
            F5ClearChat();
            F5CamSnap();
            aimInit = false;
            Vector2 over = new Vector2(pool.Value.xMax - 2f, pool.Value.yMax + 0.8f);
            SmoothAim(over + new Vector2(1f, 0.5f), 99f);
            int changed0 = WorldItems.DevShimmered;
            StartRec("v5F_shimmer_items");
            yield return new WaitForSeconds(0.3f);
            yield return F5WalkTo(hero, bank, 2f, () => over);
            C4Face(hero, -1);
            for (int i = 0; i < things.Length; i++)
            {
                inv.Selected = 2 + i;
                Vector2 aim = over + new Vector2(-0.25f * i, 0f);
                for (float t = 0f; t < 0.45f; t += Time.deltaTime) { SmoothAim(aim, 6f); yield return null; }
                while (mod.IsUsingItem || !mod.DevReady) yield return null;
                mod.ThrowHeld(false);
                for (float t = 0f; t < 0.85f; t += Time.deltaTime) { SmoothAim(aim, 6f); yield return null; }
            }
            inv.Selected = 0;
            for (float t = 0f; t < 3.4f; t += Time.deltaTime) { SmoothAim(over + new Vector2(-0.6f, 1.2f), 2f); yield return null; }
            StopRec();
            F5Log($"微光转化：转了 {WorldItems.DevShimmered - changed0}，地上 {WorldItems.DevDescribe()}，主角 {(Vector2)hero.transform.position:F1}");
            WorldItems.DevClear();
            WorldItems.DevNoGrab = false;
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }

        /// <summary>The Bottomless Shimmer Bucket made by the shimmer from an Empty Bucket (a plain bag: the recipe offered there).</summary>
        IEnumerator F5BucketCraft()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var pool = Shimmer.Pool;
            var kept = (ItemStack[])inv.Slots.Clone();
            mod.ApplyCreative(false);
            inv.Infinite = false;
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            inv.Slots[0] = kept[0];
            inv.Slots[1] = kept[1];
            inv.Slots[Inventory.HotbarSize + 2] = ItemStack.Of(ItemType.EmptyBucket, 1);
            inv.Selected = 0;
            var col = hero.GetComponent<BoxCollider2D>();
            // The bag's switch shows its pickups for a moment: gone before the take.
            yield return new WaitForSeconds(3.5f);
            F5ClearChat();
            Plugin.Log.LogInfo($"[AutoTest] 录制F 合成环境 微光 {CraftEnvironments.Available(CraftEnvironment.Shimmer)}");
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.4f)));
            yield return null;
            StartRec("v5F_shimmer_bucket_craft");
            yield return new WaitForSeconds(0.4f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.6f);
            InventoryGui.DevFocus(0);
            yield return new WaitForSeconds(0.6f);
            var offered = InventoryGui.DevOffered();
            int target = offered.IndexOf(ItemType.BottomlessShimmerBucket);
            F5Log($"微光桶：配方 {offered.Count} 个：{string.Join(",", offered)}；在第 {target}");
            if (target >= 0)
            {
                // The bucket in the bag hovered first, then the recipe column to it.
                var bag = InventoryGui.DevHitRect("Slot", Inventory.HotbarSize + 2);
                if (bag != null) { yield return Glide(bag.Value.center, 0.55f); yield return new WaitForSeconds(0.45f); }
                var r0 = InventoryGui.DevHitRect("Recipe", InventoryGui.Focus);
                if (r0 != null) yield return Glide(r0.Value.center, 0.55f);
                for (int guard = 0; guard < 40 && InventoryGui.Focus != target; guard++)
                {
                    InventoryGui.DevScrollRecipes(target > InventoryGui.Focus ? 1 : -1);
                    mod.PlaySound("Menu_Tick", 0.5f);
                    yield return new WaitForSeconds(0.13f);
                }
                yield return new WaitForSeconds(0.6f);
                yield return ClickHit("Recipe", target, 0.2f);
                yield return new WaitForSeconds(0.45f);
                yield return ClickHit("Slot", 2, 0.55f);
                yield return new WaitForSeconds(0.5f);
                var hot = InventoryGui.DevHitRect("Slot", 2);
                if (hot != null) yield return Glide(hot.Value.center + new Vector2(30f, 40f), 0.4f);
                yield return new WaitForSeconds(0.6f);
            }
            InventoryGui.Open = false;
            mod.PlaySound("Menu_Close", 0.6f);
            yield return new WaitForSeconds(0.5f);
            StopRec();
            PointerOff();
            mod.ReturnOrDropMouseItem();
            bool made = inv.Slots.Any(s => !s.IsEmpty && s.Type == ItemType.BottomlessShimmerBucket);
            F5Log($"微光桶：做出来 {made}");
            mod.ApplyCreative(true);
            for (int i = 0; i < kept.Length; i++) inv.Slots[i] = kept[i];
        }

        /// <summary>A basin of blocks set (outside the take) on the ground: walls at x0-1 and x1+1 (and at each extra column), `depth` cells high over the ground row g.</summary>
        static void F5Basin(int x0, int x1, int g, int depth, CellKind wall, params int[] pillars)
        {
            for (int x = x0 - 1; x <= x1 + 1; x++)
                for (int y = g + 1; y <= g + depth; y++)
                {
                    bool isWall = x == x0 - 1 || x == x1 + 1 || System.Array.IndexOf(pillars, x) >= 0;
                    TerraWorld.Set(x, y, isWall ? wall : CellKind.Empty);
                    TerraWorld.SetWall(x, y, 0);
                }
        }

        static int F5LiquidCells(int x0, int x1, int y0, int y1, LiquidKind kind, int least = 200)
        {
            int n = 0;
            for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) if (TerraWorld.LiquidAt(x, y) >= least && TerraWorld.LiquidKindAt(x, y) == kind) n++;
            return n;
        }

        /// <summary>Pours the held bucket into a basin's cells (the aim moving over them) for a while or until it holds `full` cells.</summary>
        IEnumerator F5Pour(int x0, int x1, int y0, float seconds, LiquidKind kind, int full)
        {
            int k = 0;
            float until = Time.time + seconds;
            while (Time.time < until && F5LiquidCells(x0, x1, y0, y0 + 3, kind) < full)
            {
                int w = x1 - x0 + 1;
                var aim = CellGrid.CellCenter(x0 + (k / 2) % w, y0 + 1 + (k % 2));
                SmoothAim(aim, 9f);
                mod.DevHold = true;
                if (mod.DevReady && !mod.IsUsingItem) { mod.DevUseSelected(aimNow); k++; }
                yield return null;
            }
            mod.DevHold = null;
        }

        /// <summary>Dirtmouth: a prefab house on the west street and beside it a basin of White Palace bricks (set up outside the take), poured full of shimmer and swum in.</summary>
        IEnumerator F5ShimmerHome()
        {
            yield return F5TownReady();
            float entered = Time.time - 10f;
            var hero = HeroController.instance;
            F5Safe();
            mod.DevSetSmartCursor(false);
            var inv = mod.Inventory;
            var st = TownNpcs.DevStreet;
            int hx = CellGrid.WorldToCell(new Vector2(64f, st.y)).x;
            int g = F5GroundRow(68f, st.y + 1f);
            bool built = PrefabHouses.Build(4, new Vector2Int(hx, g + 1));
            int bx0 = hx + PrefabHouses.Width(4) + 8, bx1 = bx0 + 8;
            int gb = F5GroundRow(CellGrid.CellCenter(bx0, 0).x, st.y + 1f);
            F5Basin(bx0, bx1, gb, 4, AreaBlocks.Kind(18));
            F5Log($"微光家：房子 {built}（{hx},{g + 1}），池 {bx0}..{bx1} 地面行 {gb}");
            var saved = (ItemStack[])inv.Slots.Clone();
            inv.Slots[2] = ItemStack.Of(ItemType.BottomlessShimmerBucket);
            inv.Selected = 2;
            var col = hero.GetComponent<BoxCollider2D>();
            float stand = CellGrid.CellCenter(bx0 - 3, gb).x;
            F5Street(hero, stand - 1.6f, 1);
            F5Shoo(60f, 100f, 1, 12f);
            F5CamOn(Vector2.zero);
            f5CamFix = new Vector2(CellGrid.CellCenter(bx0 - 4, 0).x, CellGrid.CellCenter(0, gb).y + 3.8f);
            while (Time.time - entered < 9.5f) yield return null;
            F5ClearChat();
            F5CamSnap();
            aimInit = false;
            SmoothAim(CellGrid.CellCenter(bx0 + 2, gb + 2), 99f);
            int poured0 = Buckets.DevPoured;
            StartRec("v5F_shimmer_pour");
            yield return new WaitForSeconds(0.3f);
            yield return F5WalkTo(hero, stand, 2f, () => CellGrid.CellCenter(bx0 + 2, gb + 2));
            C4Face(hero, 1);
            yield return F5Pour(bx0, bx0 + 5, gb + 1, 6f, LiquidKind.Shimmer, (bx1 - bx0 + 1) * 4 - 3);
            yield return new WaitForSeconds(0.6f);
            // In over the rim: a short hop, then a swim to the far side and back up.
            inv.Selected = 0;
            Player.TerraMovement.DevMove = 1;
            Player.TerraMovement.DevJump = true;
            Player.TerraMovement.DevGroundJump(hero);
            for (float t = 0f; t < 0.22f; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(2f, 0.5f), 6f); yield return null; }
            Player.TerraMovement.DevJump = false;
            float far = CellGrid.CellCenter(bx1 - 1, gb).x;
            for (float t = 0f; t < 2.2f && col.bounds.center.x < far; t += Time.deltaTime) { SmoothAim((Vector2)col.bounds.center + new Vector2(2f, 0.2f), 6f); yield return null; }
            Player.TerraMovement.DevMove = -1;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                Player.TerraMovement.DevJump = (t % 0.6f) < 0.15f;
                SmoothAim((Vector2)col.bounds.center + new Vector2(-2f, 0.6f), 6f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(0.9f);
            StopRec();
            F5Log($"微光家：倒了 {Buckets.DevPoured - poured0} 次，微光格 {F5LiquidCells(bx0, bx1, gb + 1, gb + 4, LiquidKind.Shimmer, 1)}，主角 {(Vector2)hero.transform.position:F1}");
            // Out again for the next take.
            hero.transform.position = Clear(hero, new Vector2(stand - 3f, CellGrid.CellCenter(0, gb).y + 1.2f));
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }

        /// <summary>
        /// Two basins of City of Tears bricks side by side (set up outside the take), the hero on the pillar between them:
        /// water poured into the left, acid into the right, then the pillar under him dug out: obsidian where they meet.
        /// </summary>
        IEnumerator F5Liquids()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var st = TownNpcs.DevStreet;
            int x0 = CellGrid.WorldToCell(new Vector2(90f, st.y)).x;
            F5Shoo(70f, 104f, 1, 12f);
            int m = x0 + 5, x1 = m + 5;
            int g = F5GroundRow(CellGrid.CellCenter(m, 0).x, st.y + 1f);
            F5Basin(x0, x1, g, 4, AreaBlocks.Kind(8), m);
            var saved = (ItemStack[])inv.Slots.Clone();
            inv.Slots[2] = ItemStack.Of(ItemType.BottomlessWaterBucket);
            inv.Slots[3] = ItemStack.Of(ItemType.BottomlessAcidBucket);
            inv.Selected = 2;
            var col = hero.GetComponent<BoxCollider2D>();
            C4PutOn(hero, new Vector2Int(m, g + 5));
            C4Face(hero, -1);
            F5CamOn(Vector2.zero);
            f5CamFix = new Vector2(CellGrid.CellCenter(m, 0).x, CellGrid.CellCenter(0, g).y + 3.6f);
            yield return new WaitForSeconds(1.5f);
            F5ClearChat();
            F5CamSnap();
            aimInit = false;
            SmoothAim(CellGrid.CellCenter(x0 + 2, g + 3), 99f);
            int poured0 = Buckets.DevPoured;
            StartRec("v5F_liquids");
            yield return new WaitForSeconds(0.4f);
            yield return F5Pour(x0, m - 1, g + 1, 3.4f, LiquidKind.Water, 15);
            C4Face(hero, 1);
            inv.Selected = 3;
            yield return new WaitForSeconds(0.3f);
            yield return F5Pour(m + 1, x1, g + 1, 3.4f, LiquidKind.Acid, 15);
            yield return new WaitForSeconds(0.4f);
            // The pillar under him dug out from the bottom up: the two meet.
            inv.Selected = 1;
            yield return new WaitForSeconds(0.25f);
            for (int y = g + 1; y <= g + 3; y++)
            {
                var c = CellGrid.CellCenter(m, y);
                for (int k = 0; k < 4 && TerraWorld.IsSolid(m, y); k++) yield return F5UseAt(c, 0.12f, 12f, true);
                yield return new WaitForSeconds(0.2f);
            }
            for (float t = 0f; t < 2.6f; t += Time.deltaTime) { SmoothAim(CellGrid.CellCenter(m, g + 2), 2.5f); yield return null; }
            StopRec();
            int obsidian = 0;
            for (int x = x0; x <= x1; x++) for (int y = g + 1; y <= g + 4; y++) if (TerraWorld.Get(x, y) == CellKind.Obsidian) obsidian++;
            F5Log($"液体：倒了 {Buckets.DevPoured - poured0} 次，水 {F5LiquidCells(x0, x1, g + 1, g + 4, LiquidKind.Water, 1)} 格，酸水 {F5LiquidCells(x0, x1, g + 1, g + 4, LiquidKind.Acid, 1)} 格，黑曜石 {obsidian} 格，柱子 {TerraWorld.Get(m, g + 1)}/{TerraWorld.Get(m, g + 2)}/{TerraWorld.Get(m, g + 3)}");
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }
    }
}
