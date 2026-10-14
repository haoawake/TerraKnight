using System.Collections;
using System.IO;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "loadout"): the four class loadouts: each keeps its own armor and accessories
    /// through a round of switches, a switch is refused mid-swing, the crafting column follows the
    /// worn loadout's class, a shared (Alt-marked) accessory works in another loadout but not
    /// beside a second pair of wings, and the loadouts survive a save and load, an old one-set save
    /// becoming the melee loadout with the ability accessories shared. Pictures of the equipment
    /// column for each loadout.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LoadoutTest(string scene, HeroController hero)
        {
            var inv = mod.Inventory;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(0.5f);
            // Every loadout empty, the melee one worn.
            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                inv.SwitchLoadout(l);
                for (int i = 0; i < Inventory.ArmorSlots; i++) inv.Armor[i] = default;
                for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = default;
            }
            inv.SwitchLoadout(0);

            Equip(new[] { ItemType.MoltenHelmet, ItemType.MoltenBreastplate, ItemType.MoltenGreaves }, new[] { ItemType.HermesBoots, ItemType.WarriorEmblem, ItemType.FeralClaws });
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 配装1（近战）：{Describe(inv)}；制作栏 {InventoryGui.DevRecipeKits()}；套装 {Equipment.FullSet}");
            yield return InventoryShot(scene, "y_loadout1");

            bool to2 = mod.TrySwitchLoadout(1);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 切到配装2：{to2}，身上 {Describe(inv)}；制作栏 {InventoryGui.DevRecipeKits()}");
            Equip(new[] { ItemType.NecroHelmet, ItemType.NecroBreastplate, ItemType.NecroGreaves }, new[] { ItemType.MagicQuiver, ItemType.RangerEmblem });
            bool to3 = mod.TrySwitchLoadout(2);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 切到配装3：{to3}；制作栏 {InventoryGui.DevRecipeKits()}");
            Equip(new[] { ItemType.JungleHat, ItemType.JungleShirt, ItemType.JunglePants }, new[] { ItemType.ManaFlower, ItemType.SorcererEmblem });
            bool to4 = mod.TrySwitchLoadout(3);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 切到配装4：{to4}；制作栏 {InventoryGui.DevRecipeKits()}");
            bool same = mod.TrySwitchLoadout(3);
            bool back = mod.TrySwitchLoadout(0);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 再点配装4（已穿着）：{same}（原版 不切换）；回到配装1：{back}，{Describe(inv)}，套装 {Equipment.FullSet}，最高跑速 {Player.TerraMovement.AccRunSpeed}");

            // Not mid-swing (Player.UsingOrReusingItem).
            var aim = (Vector2)hero.transform.position + Vector2.right * 2f;
            mod.DevUse(ItemType.CopperBroadsword, aim);
            bool midSwing = mod.TrySwitchLoadout(1);
            Plugin.Log.LogInfo($"[AutoTest] 挥剑中切换：{midSwing}（原版 拒绝），仍是配装{inv.CurrentLoadout + 1}");
            yield return new WaitForSeconds(0.8f);

            // Share: Alt+click the Hermes Boots in loadout 1, then wear loadout 4.
            InventoryGui.DevAlt = true;
            InventoryGui.DevLeftClickEquip(false, 0);
            InventoryGui.DevAlt = false;
            bool shared = inv.Accessories[0].Favorite;
            mod.TrySwitchLoadout(3);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 共享赫尔墨斯靴：标记={shared}，配装4 身上空着，生效的第 1 格 {(inv.EffectiveAccessories[0].IsEmpty ? "无" : inv.EffectiveAccessories[0].Def.Name)}（来自配装{inv.AccessorySharedFrom[0] + 1}），最高跑速 {Player.TerraMovement.AccRunSpeed}（原版 6）");
            yield return InventoryShot(scene, "y_loadout4_shared");

            // Another pair of wings worn blocks wings shared in.
            inv.Loadouts[0].Accessories[3] = ItemStack.Of(ItemType.AngelWings);
            inv.Loadouts[0].Accessories[3].Favorite = true;
            inv.Accessories[4] = ItemStack.Of(ItemType.DemonWings);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 共享天使之翼、身上戴恶魔之翼：第 4 格生效 {(inv.EffectiveAccessories[3].IsEmpty ? "无（被挡住）" : inv.EffectiveAccessories[3].Def.Name)}（原版 两对翅膀不能同时生效）");
            inv.Accessories[4] = default;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 摘下恶魔之翼后：第 4 格生效 {(inv.EffectiveAccessories[3].IsEmpty ? "无" : inv.EffectiveAccessories[3].Def.Name)}，飞行时间上限 {Player.TerraMovement.WingTimeMax}");

            // Save and load.
            string before = Describe(inv);
            Save.ModSave.Write(2, mod);
            Save.ModSave.Read(2, mod);
            yield return null;
            string after = Describe(inv);
            Plugin.Log.LogInfo($"[AutoTest] 存档往返：{(before == after ? "一致" : "不一致")}\n  存前 {before}\n  读后 {after}");

            // An old one-set save (version 3) becomes the melee loadout, the ability accessories shared.
            string path = Path.Combine(TerraMod.SaveDir, "TerraKnight_slot2.json");
            File.WriteAllText(path, "{\"Version\":3,\"Inventory\":[],\"Accessories\":[{\"Type\":\"ClimbingClaws\",\"Count\":1},{\"Type\":\"WarriorEmblem\",\"Count\":1},{\"Type\":\"Flipper\",\"Count\":1}],\"Armor\":[{\"Type\":\"IronHelmet\",\"Count\":1}],\"Scenes\":{}}");
            Save.ModSave.Read(2, mod);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 旧存档迁移：{Describe(inv)}（攀爬爪、脚蹼应共享，战士徽章不共享）");
            mod.TrySwitchLoadout(2);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 迁移后切到配装3：能蹬墙={Player.PlayerHooks.Abilities.WallCling}，能在酸水游={Equipment.Current.Swim}（共享过来的）");
            mod.TrySwitchLoadout(0);
            for (int i = 0; i < Inventory.ArmorSlots; i++) inv.Armor[i] = default;
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = default;
        }

        void Equip(ItemType[] armor, ItemType[] accessories)
        {
            var inv = mod.Inventory;
            for (int i = 0; i < Inventory.ArmorSlots; i++) inv.Armor[i] = i < armor.Length ? ItemStack.Of(armor[i]) : default;
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = i < accessories.Length ? ItemStack.Of(accessories[i]) : default;
        }

        static string Describe(Inventory inv)
        {
            string Set(ItemStack[] armor, ItemStack[] acc)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var s in armor) sb.Append(s.IsEmpty ? "-" : s.Def.Name + (s.Favorite ? "*" : "")).Append(' ');
                sb.Append('|');
                foreach (var s in acc) sb.Append(' ').Append(s.IsEmpty ? "-" : s.Def.Name + (s.Favorite ? "*" : ""));
                return sb.ToString();
            }
            var all = new System.Text.StringBuilder($"穿着配装{inv.CurrentLoadout + 1} [{Set(inv.Armor, inv.Accessories)}]");
            for (int l = 0; l < Inventory.LoadoutCount; l++)
                if (l != inv.CurrentLoadout) all.Append($" 存配装{l + 1} [{Set(inv.Loadouts[l].Armor, inv.Loadouts[l].Accessories)}]");
            return all.ToString();
        }

        /// <summary>The inventory open for a picture of the equipment column, then closed again.</summary>
        IEnumerator InventoryShot(string scene, string name)
        {
            InventoryGui.Open = true;
            yield return null;
            yield return null;
            Shot(scene, name);
            yield return null;
            yield return null;
            InventoryGui.Open = false;
        }
    }
}
