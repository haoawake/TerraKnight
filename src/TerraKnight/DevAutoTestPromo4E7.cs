using System.Collections;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.4, recorder E, suite "p4_v5E_town_zh"): Dirtmouth's newer trade, on an ordinary save. The Travelling
    /// Merchant arriving ("Bohemas 已到达！" in the chat), walked up to and his shop opened; the Merchant's shop: a forged
    /// sword and an armour piece sold (they wait at the end of his stock) and the sword bought back for what it fetched;
    /// the Goblin Tinkerer's 圣巢补购: the Hallownest items this character has had, on sale again.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly System.Reflection.FieldInfo e5TravelerCame = AccessTools.Field(typeof(TownNpcs), "travelerCame");

        /// <summary>The hero walked along the street to an x (the aim leading), at most so long.</summary>
        IEnumerator E5WalkTo(HeroController hero, float x, float most = 6f)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < most; t += Time.deltaTime)
            {
                float dx = x - col.bounds.center.x;
                if (Mathf.Abs(dx) < 0.25f) break;
                Player.TerraMovement.DevMove = dx > 0 ? 1 : -1;
                if (UI.TerraCursor.DevAt != null)
                    UI.TerraCursor.DevAt = Vector2.Lerp(UI.TerraCursor.DevAt.Value, GuiOf((Vector2)col.bounds.center + new Vector2(Mathf.Sign(dx) * 2.5f, 1.3f)), 1f - Mathf.Exp(-4f * Time.deltaTime));
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
        }

        /// <summary>A townsperson right-clicked as a player does (the pointer glides onto them first): their chat.</summary>
        IEnumerator E5Talk(string npc)
        {
            var at = TownNpcs.DevPos(npc);
            if (at == null) { E5Log($"找不到 {npc}"); yield break; }
            yield return E5Glide(GuiOf(at.Value + new Vector2(0f, 0.6f)), 0.55f);
            yield return E5Wait(0.25f);
            at = TownNpcs.DevPos(npc);
            bool ok = TownNpcs.TryRightClick(at.Value + new Vector2(0f, 0.6f));
            E5Log($"对话 {npc}：{ok}");
            yield return E5Wait(0.9f);
        }

        int E5EmptySlot(int from)
        {
            var inv = mod.Inventory;
            for (int i = from; i < Inventory.Size; i++) if (inv.Slots[i].IsEmpty) return i;
            return -1;
        }

        IEnumerator P4_v5E_town(HeroController hero)
        {
            yield return E5OrdinaryKit();
            var inv = mod.Inventory;
            Shop.DevStage = 3;
            Shop.Earn(250000L * 100L);
            // A forged sword and a helmet to sell; a place in the bag for what is bought.
            var sword = ItemStack.Of(ItemType.Muramasa);
            sword.Forge = 4;
            inv.Slots[20] = sword;
            inv.Slots[21] = ItemStack.Of(ItemType.MythrilHelmet);
            for (int i = 22; i < 30; i++) inv.Slots[i] = default;
            // The Hallownest items this character has had (as if found along the way).
            int had = 0;
            foreach (var d in ItemDb.All)
                if (d != null && HallownestRecovery.Recoverable(d.Type) && had < 24) { inv.HallownestObtained.Add(d.Type); had++; }
            yield return E5ToTown(-21f);
            hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            yield return new WaitForSeconds(6f);
            float uiTown = UI.GameView.WantedScale;
            UI.GameView.SetUiScale(1.25f);
            E5ClearChat();
            E5Log($"城镇：阶段 {Shop.UnlockedStage()}，居民 {TownNpcs.DevDescribe()}；圣巢物品记了 {had} 件；时间 {World.TerraTime.Now:F0}s 第 {World.TerraTime.Day} 天 白天 {World.TerraTime.IsDay}");

            // ---- The Travelling Merchant arrives.
            TownNpcs.Names["TravellingMerchant"] = "Bohemas";
            World.TerraTime.DevForceDay = true;
            E5PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(2.5f, 1.3f)));
            yield return E5Rec("town_traveler");
            yield return E5Wait(1.2f);
            e5TravelerCame?.SetValue(null, World.TerraTime.Day);
            float t0 = Time.time;
            while (Time.time - t0 < 8f && TownNpcs.DevPos("TravellingMerchant") == null) yield return null;
            var him = TownNpcs.DevPos("TravellingMerchant");
            E5Log($"旅商：{(him != null ? "到了 " + him.Value.ToString("F1") : "没来")}（{Time.time - t0:F1}s）");
            yield return E5Wait(1.5f);
            if (him != null)
            {
                // Up to him (he wanders: followed where he is now), his talk, his shop.
                for (float t = 0f; t < 10f; t += Time.deltaTime)
                {
                    var now = TownNpcs.DevPos("TravellingMerchant");
                    if (now == null) break;
                    float side = now.Value.x > col.bounds.center.x ? -2f : 2f;
                    float dx = now.Value.x + side - col.bounds.center.x;
                    if (Mathf.Abs(dx) < 0.4f) break;
                    Player.TerraMovement.DevMove = dx > 0 ? 1 : -1;
                    UI.TerraCursor.DevAt = Vector2.Lerp(UI.TerraCursor.DevAt.Value, GuiOf((Vector2)col.bounds.center + new Vector2(Mathf.Sign(dx) * 2.5f, 1.3f)), 1f - Mathf.Exp(-4f * Time.deltaTime));
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return E5Wait(0.3f);
                UI.TerraCursor.DevDrives = true;
                yield return E5Talk("TravellingMerchant");
                yield return E5Wait(1.4f);
                yield return E5Hit("ChatButton", 0, 0.5f, 0.25f);
                yield return E5Wait(0.8f);
                int shown = 0;
                for (int i = 0; i < TownNpcs.ShopItems.Length && shown < 4; i++)
                {
                    if (TownNpcs.ShopItems[i].IsEmpty) continue;
                    var r = InventoryGui.DevHitRect("ShopSlot", i);
                    if (r == null) continue;
                    yield return E5Glide(r.Value.center, shown == 0 ? 0.6f : 0.4f);
                    yield return E5Wait(0.9f);
                    shown++;
                }
                E5Log($"旅商商店：{TownNpcs.DevStock()}");
                TownNpcs.Close(true);
                InventoryGui.Open = false;
                yield return E5Wait(0.5f);
            }
            StopRec();

            // ---- The Merchant: sell, then buy back.
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            var merchant = TownNpcs.DevPos("Merchant");
            if (merchant != null)
            {
                yield return D4BesideNpc(hero, "Merchant", 2.2f);
                E5ClearChat();
                E5PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(-0.6f, 1.6f)));
                yield return E5Rec("town_buyback");
                yield return E5Wait(0.3f);
                yield return E5Talk("Merchant");
                yield return E5Hit("ChatButton", 0, 0.5f, 0.25f);
                yield return E5Wait(0.8f);
                // The sword hovered (its forge level), sold with Shift; then the helmet.
                var sr = InventoryGui.DevHitRect("Slot", 20);
                if (sr != null) { yield return E5Glide(sr.Value.center, 0.7f); yield return E5Wait(0.9f); }
                long before = Shop.Purse;
                InventoryGui.DevShift = true;
                yield return E5Wait(0.25f);
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                InventoryGui.DevShift = false;
                yield return E5Wait(0.7f);
                yield return E5Glide(InventoryGui.DevHitRect("Slot", 21)?.center ?? sr?.center ?? Vector2.zero, 0.35f);
                InventoryGui.DevShift = true;
                yield return E5Wait(0.2f);
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                InventoryGui.DevShift = false;
                yield return E5Wait(0.7f);
                E5Log($"回购：卖得 {Shop.Money(Shop.Purse - before)}；商店 {TownNpcs.DevStock()}");
                // The sword waiting at the end of his stock: hovered (its price), bought back, put in the bag.
                int back = -1;
                for (int i = 0; i < TownNpcs.ShopItems.Length; i++)
                    if (TownNpcs.BuyBack[i] && TownNpcs.ShopItems[i].Type == ItemType.Muramasa) back = i;
                if (back >= 0)
                {
                    var br = InventoryGui.DevHitRect("ShopSlot", back);
                    if (br != null)
                    {
                        yield return E5Glide(br.Value.center, 0.8f);
                        yield return E5Wait(1.3f);
                        long b0 = Shop.Purse;
                        UI.TerraCursor.DevClick();
                        yield return null;
                        yield return null;
                        yield return E5Wait(0.5f);
                        yield return E5Hit("Slot", 20, 0.6f, 0.2f);
                        yield return E5Wait(0.8f);
                        E5Log($"回购：买回花了 {Shop.Money(b0 - Shop.Purse)}，格 20 {inv.Slots[20].Def?.Name} +{inv.Slots[20].Forge}");
                    }
                }
                StopRec();
                TownNpcs.Close(true);
                InventoryGui.Open = false;
                yield return new WaitForSeconds(0.5f);
            }

            // ---- The Goblin Tinkerer's 圣巢补购.
            if (TownNpcs.DevPos("GoblinTinkerer") != null)
            {
                yield return D4BesideNpc(hero, "GoblinTinkerer", 2.2f);
                E5ClearChat();
                E5PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(-0.6f, 1.6f)));
                yield return E5Rec("town_hkbuyback");
                yield return E5Wait(0.3f);
                yield return E5Talk("GoblinTinkerer");
                yield return E5Wait(0.6f);
                // His buttons: 重铸, 锻造, 圣巢补购 hovered, the last pressed.
                foreach (int b in new[] { 6, 7 })
                {
                    var r = InventoryGui.DevHitRect("ChatButton", b);
                    if (r != null) { yield return E5Glide(r.Value.center, 0.35f); yield return E5Wait(0.35f); }
                }
                yield return E5Hit("ChatButton", 8, 0.35f, 0.4f);
                yield return E5Wait(1.0f);
                E5Log($"圣巢补购：开 {TownNpcs.RecoveryShop}，{TownNpcs.DevStock()}");
                int shown = 0;
                int bought = -1;
                for (int i = 0; i < TownNpcs.ShopItems.Length && shown < 5; i++)
                {
                    if (TownNpcs.ShopItems[i].IsEmpty) continue;
                    var r = InventoryGui.DevHitRect("ShopSlot", i);
                    if (r == null) continue;
                    yield return E5Glide(r.Value.center, shown == 0 ? 0.6f : 0.4f);
                    yield return E5Wait(1.0f);
                    if (shown == 2) bought = i;
                    shown++;
                }
                if (bought >= 0)
                {
                    yield return E5Hit("ShopSlot", bought, 0.5f, 0.3f);
                    yield return E5Wait(0.5f);
                    int empty = E5EmptySlot(22);
                    if (empty >= 0) yield return E5Hit("Slot", empty, 0.6f, 0.2f);
                    yield return E5Wait(0.9f);
                }
                StopRec();
                TownNpcs.Close(true);
                InventoryGui.Open = false;
            }
            World.TerraTime.DevForceDay = null;
            Shop.DevStage = -1;
            UI.GameView.SetUiScale(uiTown);
            E5PointerOff();
        }
    }
}
