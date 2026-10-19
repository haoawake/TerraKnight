using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_Cfish"): recorder C's fishing clips. The Golden Fishing Rod cast from a shore: the bobber tossed
    /// out, floating, the bite, the reel and the catch; at the Blue Lake a fish, at the Lake of Unn (Greenpath's acid,
    /// with the lavaproof tackle) the area's crate, opened with a right click in the bag (what is in it popping out over
    /// the hero); a Fishing Chest caught and opened.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_Cfish(HeroController hero)
        {
            yield return P4Kit();
            yield return C4Fishing();
        }

        /// <summary>Not a clip: the shores of a few rooms' waters, photographed with the hero on them.</summary>
        IEnumerator P4_Cscout3(HeroController hero)
        {
            yield return P4Kit();
            mod.TrySwitchLoadout(1);
            string[] scenes = { "Crossroads_50", "Fungus1_01", "Fungus1_02", "Fungus1_04", "Fungus1_07", "Fungus1_10", "Fungus1_13", "Fungus1_17", "Ruins1_03", "Ruins1_04",
                "Waterways_04", "Waterways_13", "Fungus2_07", "Crossroads_35", "Deepnest_East_03", "Fungus3_26", "Fungus3_28", "RestingGrounds_10", "Abyss_08" };
            foreach (var scene in scenes)
            {
                if (!entryGates.ContainsKey(scene)) continue;
                yield return EnterRoom(scene);
                hero = HeroController.instance;
                if (GameManager.instance.sceneName != scene) continue;
                PlayerData.instance.isInvincible = true;
                QuietAround(hero.transform.position, 400f);
                foreach (bool acid in new[] { false, true })
                    for (int i = 0; i < 3; i++)
                    {
                        if (!C4Shore(scene, acid, out var water, out var stand, out int face, i)) break;
                        C4PutOn(hero, stand);
                        C4Face(hero, face);
                        yield return new WaitForSeconds(0.7f);
                        yield return C4Snap($"pond_{scene}_{(acid ? "acid" : "water")}{i}");
                        Plugin.Log.LogInfo($"[AutoTest] C4侦察3 {scene} {(acid ? "酸" : "水")}{i}：{water.min:F1}..{water.max:F1}，站 {stand}，朝 {face}，主角 {(Vector2)hero.transform.position:F1}");
                    }
            }
        }

        /// <summary>Fishing, then the house, then the dig sites' pictures, in one run.</summary>
        IEnumerator P4_Cbatch2(HeroController hero)
        {
            yield return P4Kit();
            yield return C4Fishing();
            yield return C4BuildAll();
            yield return P4_Cscout2(HeroController.instance);
        }

        /// <summary>A shore by the room's biggest water (or acid): a standing cell close to its edge, facing it.</summary>
        bool C4Shore(string scene, bool acid, out Bounds water, out Vector2Int stand, out int face, int index = 0)
        {
            water = default; stand = default; face = 1;
            var ponds = Fishing.DevPonds().Where(p => p.Scene == scene && p.Acid == acid).OrderByDescending(p => p.Tiles).ToList();
            if (ponds.Count <= index) return false;
            water = ponds[index].Area;
            float surf = water.max.y, best = float.MaxValue;
            bool found = false;
            foreach (int side in new[] { -1, 1 })
            {
                float edge = side < 0 ? water.min.x : water.max.x;
                for (float dist = 0.25f; dist <= 6f; dist += 0.5f)
                {
                    var c = CellGrid.WorldToCell(new Vector2(edge + side * dist, surf + 0.25f));
                    for (int y = c.y - 1; y <= c.y + 8; y++)
                    {
                        if (!C4Stand(c.x, y)) continue;
                        float h = CellGrid.CellCenter(c.x, y).y - surf;
                        float score = dist + Mathf.Abs(h - 0.6f) * 1.5f;
                        if (score < best) { best = score; stand = new Vector2Int(c.x, y); face = -side; found = true; }
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// A cast toward a point on the water: the drawn cursor glides there; the bobber is thrown low over the water
        /// (the line's own toss: aimed past the water's reach, so it flies and drops in, not set straight down on it).
        /// </summary>
        IEnumerator C4Cast(HeroController hero, Vector2 onWater, int face, float vx = 4.2f)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < 0.55f; t += Time.deltaTime) { SmoothAim(onWater, 9f); yield return null; }
            Vector2 from = col.bounds.center;
            Vector2 far = default;
            foreach (float deg in new[] { 18f, 26f, 35f, 45f })
            {
                var d = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad) * face, -Mathf.Sin(deg * Mathf.Deg2Rad));
                far = from + d * 60f;
                if (!Fishing.TrySurface(far, out _, out _)) break;
            }
            var rod = mod.Inventory.Current.Def;
            bool cast = Fishing.Use(hero, far, rod);
            // A gentler toss than the rod's full throw (which would carry the bobber off the screen): up and out,
            // down onto the water some way off the shore.
            var velField = typeof(Fishing).GetField("vel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (cast && velField != null) velField.SetValue(null, new Vector2(face * vx, 5.2f));
            mod.PlaySound("Item_1");
            Plugin.Log.LogInfo($"[AutoTest] 录制 钓鱼 抛竿 {cast}：{Fishing.DevLast}");
        }

        /// <summary>The bobber settled and bobbing a while, then a catch on the line (or the real one if it came first), then reeled in.</summary>
        IEnumerator C4BiteAndReel(HeroController hero, Vector2 onWater, ItemType catchType, float floatFor)
        {
            float t0 = Time.time;
            while (Fishing.Active && Time.time - t0 < floatFor && !(catchType == ItemType.None && Fishing.Biting))
            {
                SmoothAim(onWater, 4f);
                yield return null;
            }
            if (!Fishing.Active) { Plugin.Log.LogInfo("[AutoTest] 录制 钓鱼 浮漂没了"); yield break; }
            var floatField = typeof(Fishing).GetField("floating", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            bool afloat = floatField == null || (bool)floatField.GetValue(null);
            for (float t = 0f; t < 2f && !afloat; t += Time.deltaTime) { yield return null; afloat = (bool)floatField.GetValue(null); }
            if (!afloat) Plugin.Log.LogInfo("[AutoTest] 录制 钓鱼 浮漂没落进水里");
            if (!Fishing.Biting)
            {
                Fishing.DevBite(catchType == ItemType.None ? ItemType.Bass : catchType);
                mod.PlaySound("Item_2", 0.6f);
            }
            for (float t = 0f; t < 0.75f; t += Time.deltaTime) { SmoothAim(onWater, 4f); yield return null; }
            int before = Fishing.Catches;
            Fishing.Use(hero, onWater, mod.Inventory.Current.Def);
            Plugin.Log.LogInfo($"[AutoTest] 录制 钓鱼 收线：{(Fishing.Catches > before ? Fishing.DevLast : "没钓到")}");
        }

        IEnumerator C4FishAt(string clip, string scene, bool acid, ItemType catchType, float floatFor, int pond = 0)
        {
            float entered = Time.time;
            if (GameManager.instance.sceneName != scene) { yield return EnterRoom(scene); entered = Time.time; }
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：没进 {scene}"); yield break; }
            QuietAround(hero.transform.position, 400f);
            var inv = mod.Inventory;
            int accSlot = Inventory.AccessorySlots - 1;
            inv.Accessories[accSlot] = ItemStack.Of(acid ? ItemType.LavaproofTackleBag : ItemType.AnglerTackleBag, 1);
            inv.RefreshEffective();
            Equipment.Update(inv);
            inv.Selected = 9;
            if (!C4Shore(scene, acid, out var water, out var stand, out int face, pond)) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} 找不到{(acid ? "酸池" : "水")}边"); yield break; }
            C4PutOn(hero, stand);
            if (face > 0 && !hero.cState.facingRight) hero.FaceRight();
            if (face < 0 && hero.cState.facingRight) hero.FaceLeft();
            yield return new WaitForSeconds(1.4f);
            while (Time.time - entered < 7.5f) yield return null;
            var col = hero.GetComponent<BoxCollider2D>();
            float edge = face > 0 ? water.min.x : water.max.x;
            float into = Mathf.Min(7f, (water.max.x - water.min.x) * 0.55f);
            // Where the surface is open (no shore ledge over it) going out from the hero: the bobber is tossed past that.
            float open = col.bounds.center.x;
            for (float x = col.bounds.center.x; (x - water.max.x) * face < 0f && (x - water.min.x) * face > -40f; x += face * 0.25f)
            {
                var sc = CellGrid.WorldToCell(new Vector2(x, water.max.y + 0.2f));
                if (x >= water.min.x && x <= water.max.x && !TerraWorld.IsSolid(sc.x, sc.y) && !TerraWorld.IsSolid(sc.x, sc.y + 1) && !TerraWorld.IsSolid(sc.x, sc.y + 2)) { open = x; break; }
            }
            float landX = face > 0 ? Mathf.Max(edge + face * into, open + face * 2.5f) : Mathf.Min(edge + face * into, open + face * 2.5f);
            var onWater = new Vector2(landX, water.max.y + 0.15f);
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(face * 1.5f, 0.4f), 99f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{scene} {(acid ? "酸池" : "水")} {water.min:F1}..{water.max:F1}，站 {stand}（{(Vector2)col.bounds.center:F1}），朝{(face < 0 ? "左" : "右")}，渔力 {Fishing.FishingLevel(inv.Current.Def, inv)}");
            StartRec(clip);
            yield return new WaitForSeconds(0.35f);
            // The toss carries about 7.7 units at 4.2 px a tick: scaled to land where the cursor is.
            float reach = Mathf.Abs(onWater.x - col.bounds.center.x);
            yield return C4Cast(hero, onWater, face, Mathf.Clamp(4.2f * reach / 7.7f, 1.5f, 6.5f));
            yield return C4BiteAndReel(hero, onWater, catchType, floatFor);
            for (float t = 0f; t < 1.4f; t += Time.deltaTime) { SmoothAim(onWater + new Vector2(-face * 1.2f, 1.2f), 3f); yield return null; }
            StopRec();
        }

        /// <summary>The bag open, the pointer to the item and a right click on it (a crate or the Fishing Chest opens).</summary>
        IEnumerator C4OpenInBag(string clip, ItemType type)
        {
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            int slot = -1;
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == type) { slot = i; break; }
            if (slot < 0) { Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：背包里没有 {ItemDb.Get(type).Name}"); yield break; }
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.5f)));
            // Two frames after the last take, so its recorder has stopped.
            yield return null;
            yield return null;
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.6f);
            yield return new WaitForSeconds(0.45f);
            var r = InventoryGui.DevHitRect("Slot", slot);
            if (r != null) yield return Glide(r.Value.center, 0.6f);
            yield return new WaitForSeconds(0.5f);
            int filled0 = inv.Slots.Count(s => !s.IsEmpty);
            InventoryGui.DevRightClick(slot);
            yield return new WaitForSeconds(0.25f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：打开 {ItemDb.Get(type).Name}，背包格 {filled0} → {inv.Slots.Count(s => !s.IsEmpty)}");
            // A look along what came in.
            yield return Glide(r != null ? r.Value.center + new Vector2(180f, 60f) : new Vector2(Screen.width * 0.3f, Screen.height * 0.3f), 0.8f);
            yield return new WaitForSeconds(1.6f);
            StopRec();
            InventoryGui.Open = false;
            PointerOff();
        }

        IEnumerator C4Fishing()
        {
            var inv = mod.Inventory;
            mod.TrySwitchLoadout(1);
            yield return null;
            // A plain game's bag (the creative one's title off).
            var creativeSaved = (ItemStack[])inv.Slots.Clone();
            mod.ApplyCreative(false);
            inv.Infinite = false;
            inv.Slots[9] = ItemStack.Of(ItemType.GoldenFishingRod, 1);
            inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MasterBait, 99);
            Shop.DevStage = 2;
            var crate = Fishing.AreaCrateFor(Areas.AreaName("Fungus1_02"), false);
            var fogCrate = Fishing.AreaCrateFor(Areas.AreaName("Fungus3_26"), false);
            Plugin.Log.LogInfo($"[AutoTest] 录制 钓鱼：苍绿之径的宝匣 {ItemDb.Get(crate).Name}，雾之峡谷的 {ItemDb.Get(fogCrate).Name}");
            yield return C4FishAt("fish_bluelake", "Crossroads_50", false, ItemType.None, 4.5f);
            yield return C4FishAt("fish_greenpath_crate", "Fungus1_02", true, crate, 2.4f);
            yield return C4OpenInBag("fish_crate_open", crate);
            yield return C4FishAt("fish_chest", "Fungus1_02", true, FishingChest.Type, 2.0f);
            yield return C4OpenInBag("fish_chest_open", FishingChest.Type);
            yield return C4FishAt("fish_fogcanyon_crate", "Fungus3_26", true, fogCrate, 2.4f);
            yield return C4OpenInBag("fish_fogcanyon_open", fogCrate);
            mod.ApplyCreative(true);
            for (int i = 0; i < creativeSaved.Length; i++) inv.Slots[i] = creativeSaved[i];
        }
    }
}
