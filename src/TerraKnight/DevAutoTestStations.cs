using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "stations"): crafting as in Terraria, in a normal save away from any bench. Wood,
    /// stone, gel and iron in the bag: what is made by hand (the Work Bench, platforms, torches);
    /// the bench set down and what it adds (wooden armor, the Furnace, an anvil); the Furnace and
    /// the anvil set down too (the chain at the anvil); the bench knocked down with a pickaxe and its
    /// recipes gone with it; the stations through a save and back. And the loot of a kill on each
    /// difficulty (EnemyDrops' chance and count).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator StationsTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(false);
            mod.SetInfiniteItems(false);
            var inv = mod.Inventory;
            inv.Clear();
            inv.Slots[0] = ItemStack.Of(ItemType.CopperPickaxe);
            inv.Slots[20] = ItemStack.Of(ItemType.Wood, 150);
            inv.Slots[21] = ItemStack.Of(ItemType.StoneBlock, 40);
            inv.Slots[22] = ItemStack.Of(ItemType.Gel, 5);
            inv.Slots[23] = ItemStack.Of(ItemType.IronBar, 12);
            yield return null;

            string Names(IEnumerable<ItemType> types)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var t in types) sb.Append(ItemDb.Get(t).Name).Append(' ');
                return sb.ToString();
            }
            bool Offers(ItemType t) => InventoryGui.DevOffered().Contains(t);
            void Stow()
            {
                if (inv.Mouse.IsEmpty) return;
                inv.Pickup(inv.Mouse.Type, inv.Mouse.Count);
                inv.Mouse = default;
            }
            bool Craft(ItemType t)
            {
                bool ok = InventoryGui.DevCraft(t);
                Stow();
                return ok;
            }
            int Have(ItemType t) => Recipes.Count(inv, t);
            Vector2 Feet() { var b = hero.GetComponent<BoxCollider2D>().bounds; return new Vector2(b.center.x, b.min.y); }
            IEnumerator Place(ItemType t, float dx)
            {
                int slot = 1;
                for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) slot = i;
                inv.Slots[1] = inv.Slots[slot];
                if (slot != 1) inv.Slots[slot] = default;
                inv.Selected = 1;
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(Feet() + new Vector2(dx, 0.3f));
                yield return new WaitForSeconds(0.3f);
            }

            var byHand = InventoryGui.DevOffered();
            Plugin.Log.LogInfo($"[AutoTest] 工作站 空手能做：{Names(byHand)}（应有 工作台 木平台 火把，没有 木头盔 熔炉）");
            bool bench = Craft(ItemType.WorkBench);
            bool torch = Craft(ItemType.Torch);
            bool platform = Craft(ItemType.WoodPlatform);
            Plugin.Log.LogInfo($"[AutoTest] 工作站 空手做：工作台 {bench}，火把 {torch}（{Have(ItemType.Torch)} 个），木平台 {platform}（{Have(ItemType.WoodPlatform)} 个），木头盔 {Offers(ItemType.WoodHelmet)}（应为 False）");

            yield return Place(ItemType.WorkBench, 1.2f);
            Plugin.Log.LogInfo($"[AutoTest] 工作站 放下工作台：{Stations.Count} 个{Stations.DevDescribe()}；旁边能做的：{Names(InventoryGui.DevOffered())}");
            bool helmet = Craft(ItemType.WoodHelmet), chest = Craft(ItemType.WoodBreastplate), legs = Craft(ItemType.WoodGreaves);
            bool furnace = Craft(ItemType.Furnace), anvil = Craft(ItemType.IronAnvil), arrows = Craft(ItemType.WoodenArrow);
            Plugin.Log.LogInfo($"[AutoTest] 工作站 在工作台做：木甲 {helmet}/{chest}/{legs}，熔炉 {furnace}，铁砧 {anvil}，木箭 {arrows}（{Have(ItemType.WoodenArrow)} 支）");

            yield return Place(ItemType.Furnace, -1.6f);
            yield return Place(ItemType.IronAnvil, 2.8f);
            bool chain = Craft(ItemType.Chain);
            Plugin.Log.LogInfo($"[AutoTest] 工作站 熔炉和铁砧：{Stations.Count} 个{Stations.DevDescribe()}，在铁砧做链条 {chain}（{Have(ItemType.Chain)} 个），附近工作站 {string.Join("、", Stations.NearNames(hero))}");
            yield return CloseUpAt(GameManager.instance.sceneName, "stations_placed", Feet() + new Vector2(0.6f, 0.8f), 220, 2);

            // The bench knocked down: its recipes go with it, its item comes back.
            inv.Selected = 0;
            int before = Stations.Count;
            for (int k = 0; k < 20 && Stations.Count == before; k++)
            {
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(Feet() + new Vector2(1.2f, 0.2f));
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 工作站 敲掉工作台：剩 {Stations.Count} 个（应为 2），木头盔还能做 {Offers(ItemType.WoodHelmet)}（应为 False），背包里工作台 {Have(ItemType.WorkBench)} 个（捡回来应为 1）");

            // Through a save and back.
            int slot0 = GameManager.instance.profileID;
            int kept = Stations.Count;
            Save.ModSave.Write(slot0, mod);
            Save.ModSave.Read(slot0, mod);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 工作站 存档读回：{Stations.Count} 个（应为 {kept}）{Stations.DevDescribe()}");

            // Loot by difficulty: Foxsparks (1/120) and the wood (2-4) of a Crawlid.
            foreach (DifficultyLevel d in new[] { DifficultyLevel.Normal, DifficultyLevel.Hard, DifficultyLevel.Expert, DifficultyLevel.Master })
            {
                GameDifficulty.Current = d;
                var sb = new System.Text.StringBuilder();
                foreach (var dr in EnemyDrops.Of("Crawler"))
                    if (dr.Type == ItemType.FoxsparksStaff || dr.Type == ItemType.Wood || dr.Type == ItemType.CopperBar)
                        sb.Append($"{ItemDb.Get(dr.Type).Name} {EnemyDrops.EffectiveChance(dr) * 100f:0.##}% ×{dr.Min}-{EnemyDrops.EffectiveMax(dr)}；");
                Plugin.Log.LogInfo($"[AutoTest] 掉落 {GameDifficulty.NameOf(d)}：{sb}");
            }
            GameDifficulty.Current = DifficultyLevel.Master;
        }
    }
}
