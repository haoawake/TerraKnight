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
    /// Dev (suite "p4_v5F_fish_zh"): recorder F's fishing takes. The Angler in Dirtmouth gives a new Hallownest quest fish
    /// (the Spore Puffer, Fungal Wastes); it is caught in the Fungal Wastes' acid (the item's icon and words looked at in the
    /// bag); the area's crate caught there and opened; the fish handed in for the reward; another area's crate caught in the
    /// City of Tears' water and opened.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const ItemType F5QuestFish = ItemType.HkQuestFish07;

        IEnumerator P4_v5F_fish(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            var inv = mod.Inventory;
            Angler.Saved = true;
            Angler.Pending = 1;
            Angler.QuestDay = -1;
            Angler.QuestDone = false;
            Angler.Quest = F5QuestFish;
            // A plain game's bag for these takes (no creative title, room for what comes out of the crates).
            var kept = (ItemStack[])inv.Slots.Clone();
            mod.ApplyCreative(false);
            inv.Infinite = false;
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = i < Inventory.HotbarSize ? kept[i] : default;
            inv.Slots[9] = ItemStack.Of(ItemType.GoldenFishingRod, 1);
            inv.Slots[Inventory.HotbarSize + 3] = ItemStack.Of(ItemType.EmptyBucket, 1);
            inv.Slots[Inventory.HotbarSize + 5] = ItemStack.Of(ItemType.FishingPotion, 5);
            inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MasterBait, 99);
            yield return F5AnglerTalk("v5F_angler_quest", false);
            yield return F5FishTake("v5F_fish_questfish", "Fungus2_07", true, F5QuestFish, true);
            var fungal = Fishing.AreaCrateFor(Areas.AreaName("Fungus2_07"), false);
            yield return F5FishTake("v5F_fish_crate_fungal", "Fungus2_07", true, fungal, false);
            yield return F5OpenCrate("v5F_crate_open_fungal", fungal);
            var ways = Fishing.AreaCrateFor(Areas.AreaName("Waterways_01"), false);
            yield return F5FishTake("v5F_fish_crate_waterways", "Waterways_01", false, ways, false);
            yield return F5OpenCrate("v5F_crate_open_waterways", ways);
            yield return F5AnglerTalk("v5F_angler_turnin", true);
            F5CamOff();
            mod.ApplyCreative(true);
            for (int i = 0; i < kept.Length; i++) inv.Slots[i] = kept[i];
        }

        /// <summary>Dirtmouth: up to the Angler, a right click, his Quest button: today's fish (or, carrying it, the hand-in).</summary>
        IEnumerator F5AnglerTalk(string clip, bool turnIn)
        {
            yield return F5TownReady();
            float entered = Time.time - 10f;
            var hero = HeroController.instance;
            F5Safe();
            var inv = mod.Inventory;
            Vector2? at = null;
            for (float t = 0f; t < 4f && (at = TownNpcs.DevPos("Angler")) == null; t += Time.deltaTime) yield return null;
            if (at == null) { F5Log($"渔夫：不在德特茅斯（{TownNpcs.DevDescribe()}）"); yield break; }
            inv.Selected = 0;
            var col = hero.GetComponent<BoxCollider2D>();
            F5Street(hero, at.Value.x - 6.5f, 1);
            F5CamOn(new Vector2(2.0f, 3.8f));
            while (Time.time - entered < 9.5f) yield return null;
            F5Shoo(at.Value.x - 12f, at.Value.x + 8f, 0, 8f);
            TownNpcs.DevSend("Angler", at.Value.x, 0);
            yield return new WaitForSeconds(0.3f);
            at = TownNpcs.DevPos("Angler");
            F5ClearChat();
            F5CamSnap();
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.2f)));
            int done0 = Angler.QuestsDone;
            long purse0 = Shop.Purse;
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            yield return F5WalkTo(hero, at.Value.x - 1.6f, 2.5f);
            C4Face(hero, 1);
            at = TownNpcs.DevPos("Angler");
            yield return Glide(GuiOf(at.Value + new Vector2(0f, 0.5f)), 0.5f);
            yield return new WaitForSeconds(0.15f);
            bool talked = TownNpcs.TryRightClick(at.Value);
            yield return new WaitForSeconds(1.6f);
            yield return ClickHit("ChatButton", 3, 0.55f);
            string said = TownNpcs.ChatText;
            yield return new WaitForSeconds(0.2f);
            // The pointer off the words, down beside the panel.
            yield return Glide(new Vector2(Screen.width * 0.36f, Screen.height * 0.47f), 0.6f);
            yield return new WaitForSeconds(turnIn ? 2.6f : 4.2f);
            StopRec();
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            PointerOff();
            F5Log($"渔夫 {clip}：对话 {talked}，交了 {Angler.QuestsDone - done0}，钱 {purse0}→{Shop.Purse}，说：{said}");
        }

        /// <summary>A cast from a shore of the room's water (or acid), a bite of the given catch, the reel; with `look`, the bag opened on it.</summary>
        IEnumerator F5FishTake(string clip, string scene, bool acid, ItemType catchType, bool look)
        {
            float entered = Time.time;
            if (GameManager.instance.sceneName != scene) { yield return EnterRoom(scene); entered = Time.time; }
            var hero = HeroController.instance;
            F5Safe();
            if (GameManager.instance.sceneName != scene) { F5Log($"{clip}：没进 {scene}"); yield break; }
            QuietAround(hero.transform.position, 400f);
            var inv = mod.Inventory;
            int accSlot = Inventory.AccessorySlots - 1;
            inv.Accessories[accSlot] = ItemStack.Of(acid ? ItemType.LavaproofTackleBag : ItemType.AnglerTackleBag, 1);
            inv.RefreshEffective();
            Equipment.Update(inv);
            inv.Selected = 9;
            if (!C4Shore(scene, acid, out var water, out var stand, out int face)) { F5Log($"{clip}：{scene} 找不到岸"); yield break; }
            C4PutOn(hero, stand);
            C4Face(hero, face);
            F5CamOff();
            yield return new WaitForSeconds(1.4f);
            while (Time.time - entered < 8f) yield return null;
            QuietAround(hero.transform.position, 400f);
            F5ClearChat();
            var col = hero.GetComponent<BoxCollider2D>();
            float edge = face > 0 ? water.min.x : water.max.x;
            float into = Mathf.Min(6f, (water.max.x - water.min.x) * 0.5f);
            var onWater = new Vector2(edge + face * into, water.max.y + 0.15f);
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(face * 1.5f, 0.4f), 99f);
            int catches0 = Fishing.Catches;
            StartRec(clip);
            yield return new WaitForSeconds(0.35f);
            float reach = Mathf.Abs(onWater.x - col.bounds.center.x);
            yield return C4Cast(hero, onWater, face, Mathf.Clamp(4.2f * reach / 7.7f, 1.5f, 6.5f));
            yield return F5BiteAndReel(hero, onWater, catchType, 2.2f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { SmoothAim(onWater + new Vector2(-face * 1.2f, 1.2f), 3f); yield return null; }
            int slot = -1;
            for (int i = 0; i < Inventory.Size && slot < 0; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == catchType) slot = i;
            if (look && slot >= 0)
            {
                // The bag open on the catch: its picture and its words.
                PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(face * 1.5f, 1.2f)));
                InventoryGui.Open = true;
                mod.PlaySound("Menu_Open", 0.6f);
                yield return new WaitForSeconds(0.45f);
                var r = InventoryGui.DevHitRect("Slot", slot);
                if (r != null) yield return Glide(r.Value.center, 0.6f);
                yield return new WaitForSeconds(3.0f);
                InventoryGui.Open = false;
                mod.PlaySound("Menu_Close", 0.6f);
                PointerOff();
                yield return new WaitForSeconds(0.3f);
            }
            StopRec();
            Fishing.Cancel();
            F5Log($"{clip}：{scene} 钓到 {Fishing.Catches - catches0}，{(slot >= 0 ? ItemDb.Get(catchType).Name + " 在第 " + slot + " 格" : "包里没有 " + ItemDb.Get(catchType).Name)}；{Fishing.DevLast}");
        }

        /// <summary>The bobber afloat a while, then this catch on the line (whatever bit first), then reeled in.</summary>
        IEnumerator F5BiteAndReel(HeroController hero, Vector2 onWater, ItemType catchType, float floatFor)
        {
            float t0 = Time.time;
            while (Fishing.Active && Time.time - t0 < floatFor) { SmoothAim(onWater, 4f); yield return null; }
            if (!Fishing.Active) { F5Log("钓鱼：浮漂没了"); yield break; }
            Fishing.DevBite(catchType);
            mod.PlaySound("Item_2", 0.6f);
            for (float t = 0f; t < 0.75f; t += Time.deltaTime) { SmoothAim(onWater, 4f); yield return null; }
            Fishing.DevBite(catchType);
            Fishing.Use(hero, onWater, mod.Inventory.Current.Def);
        }

        /// <summary>The bag opened on a crate, a right click on it: what came out, the best piece hovered.</summary>
        IEnumerator F5OpenCrate(string clip, ItemType crate)
        {
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            int slot = -1;
            for (int i = 0; i < Inventory.Size && slot < 0; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == crate) slot = i;
            if (slot < 0) { F5Log($"{clip}：包里没有 {ItemDb.Get(crate).Name}"); yield break; }
            var before = (ItemStack[])inv.Slots.Clone();
            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.5f)));
            yield return null;
            yield return null;
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.6f);
            yield return new WaitForSeconds(0.45f);
            var r = InventoryGui.DevHitRect("Slot", slot);
            if (r != null) yield return Glide(r.Value.center, 0.6f);
            yield return new WaitForSeconds(0.6f);
            InventoryGui.DevRightClick(slot);
            yield return new WaitForSeconds(0.9f);
            // The best new thing: the rarest that came in.
            int best = -1, bestRare = int.MinValue;
            var got = new List<string>();
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = inv.Slots[i];
                if (s.IsEmpty || (!before[i].IsEmpty && before[i].Type == s.Type && before[i].Count == s.Count)) continue;
                if (s.Type == crate) continue;
                got.Add(s.Def.Name + "×" + s.Count);
                if (s.Def.Rare > bestRare) { bestRare = s.Def.Rare; best = i; }
            }
            if (best >= 0)
            {
                var rb = InventoryGui.DevHitRect("Slot", best);
                if (rb != null) yield return Glide(rb.Value.center, 0.7f);
            }
            yield return new WaitForSeconds(2.6f);
            InventoryGui.Open = false;
            mod.PlaySound("Menu_Close", 0.6f);
            PointerOff();
            yield return new WaitForSeconds(0.3f);
            StopRec();
            F5Log($"{clip}：打开 {ItemDb.Get(crate).Name}：{string.Join("，", got)}");
        }
    }
}
