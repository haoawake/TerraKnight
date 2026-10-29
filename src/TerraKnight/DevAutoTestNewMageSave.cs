using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "newmagesave", the user's 10-28 "纯法师存档"): a creative save with the new weapons for trying the remade
    /// magic weapons (MageLefts, BladeSkillsMagic), made as newrangedsave makes its own and copied into the player's free
    /// slot afterwards. Dirtmouth (its Target Dummy by the well), creative's showcase kit, all four loadouts in the magic set
    /// (Nebula armour and its accessories), each with a bag of its own by stage: F1 the moon's and the last ones (stage 5),
    /// F2 stage 4, F3 stage 3, F4 everything before (stages 0-2). Each bag latest first (its hotbar the latest ten), with
    /// Super Mana Potions, Magic Power and Mana Regeneration potions, three Target Dummies and the Lunar Hook last. Wakes on
    /// Dirtmouth's bench. Lines "[AutoTest] 法师存档 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const string MageSaveName = "新法师武器";
        const int MageSet = 2;

        static bool MageShown(ItemDef d) => d != null && ItemDb.Get(d.Type) == d && d.IsWeapon && d.Class == DamageClass.Magic;

        /// <summary>The bag a magic weapon goes in (0 stage 5, 1 stage 4, 2 stage 3, 3 stages 0-2).</summary>
        static int MageBag(ItemDef d) => d.Stage >= 5 ? 0 : d.Stage == 4 ? 1 : d.Stage == 3 ? 2 : 3;

        static readonly ItemType[] mageExtras = { ItemType.SuperManaPotion, ItemType.MagicPowerPotion, ItemType.ManaRegenerationPotion };

        static List<ItemType>[] MageBags()
        {
            var bags = new[] { new List<ItemType>(), new List<ItemType>(), new List<ItemType>(), new List<ItemType>() };
            foreach (var d in ItemDb.All.Where(MageShown).OrderByDescending(d => d.Stage).ThenByDescending(d => d.Rare).ThenByDescending(d => d.Damage))
                if (!bags[MageBag(d)].Contains(d.Type)) bags[MageBag(d)].Add(d.Type);
            return bags;
        }

        IEnumerator NewMageSaveTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            ShowcaseKit.Give(mod);
            yield return new WaitForSeconds(0.5f);
            BladeArts.Set(true);
            // Every loadout in the magic set (loadout 0's worn in place).
            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                if (l == MageSet) continue;
                var armor = l == 0 ? inv.Armor : inv.Loadouts[l].Armor;
                var acc = l == 0 ? inv.Accessories : inv.Loadouts[l].Accessories;
                for (int i = 0; i < Inventory.ArmorSlots; i++) armor[i] = inv.Loadouts[MageSet].Armor[i];
                for (int i = 0; i < Inventory.AccessorySlots; i++) acc[i] = inv.Loadouts[MageSet].Accessories[i];
            }
            var lists = MageBags();
            int Fill(ItemStack[] bag, List<ItemType> items)
            {
                for (int i = 0; i < bag.Length; i++) bag[i] = default;
                int room = Inventory.Size - 2 - mageExtras.Length, k = 0;
                for (; k < items.Count && k < room; k++) bag[k] = ItemStack.Of(items[k]);
                for (int e = 0; e < mageExtras.Length; e++)
                {
                    var d = ItemDb.Get(mageExtras[e]);
                    bag[Inventory.Size - 3 - e] = ItemStack.Of(mageExtras[e], d.MaxStack > 1 ? d.MaxStack : 1);
                }
                bag[Inventory.Size - 2] = ItemStack.Of(ItemType.TargetDummy, 3);
                bag[Inventory.Size - 1] = ItemStack.Of(ItemType.LunarHook);
                return k;
            }
            var put = new int[4];
            put[0] = Fill(inv.Slots, lists[0]);
            for (int l = 1; l < 4; l++)
            {
                put[l] = Fill(inv.Loadouts[l].Bag, lists[l]);
                inv.Loadouts[l].Selected = 0;
            }
            inv.Selected = 0;
            inv.RefreshEffective();
            for (int l = 0; l < 4; l++)
                Plugin.Log.LogInfo($"[AutoTest] 法师存档 F{l + 1} {put[l]}/{lists[l].Count} 件；快捷栏 {string.Join("、", lists[l].Take(Inventory.HotbarSize).Select(t => ItemDb.Get(t).Name))}");
            Shot("Town", "newmagesave_bag");
            Save.ModSave.WorldName = MageSaveName;
            var pd = PlayerData.instance;
            pd.respawnScene = "Town";
            pd.respawnMarkerName = "RestBench";
            pd.respawnType = 1;
            pd.mapZone = GlobalEnums.MapZone.TOWN;
            yield return new WaitForSeconds(0.5f);
            var gm = GameManager.instance;
            bool done = false, ok = false;
            gm.SaveGame(r => { done = true; ok = r; });
            for (float t = 0f; t < 10f && !done; t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            int slot = gm.profileID;
            string json = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json");
            bool newWeapons = File.Exists(json) && System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(json), "\"Weapons\"\\s*:\\s*1");
            int total = lists.Sum(l => l.Count);
            bool all = Enumerable.Range(0, 4).All(l => put[l] == lists[l].Count) && total >= 80 && mod.Creative && newWeapons;
            Plugin.Log.LogInfo($"[AutoTest] 法师存档 {(ok && all ? "通过" : "不通过")}：{total} 件法师武器，新版武器 {newWeapons}，创造 {mod.Creative}，存档位 {slot} 保存 {ok}，目录 {TerraMod.SaveDir}");
        }

        /// <summary>
        /// Dev (suite "newmagecheck"): the save newmagesave made, loaded again (run-rt6seed): its name, creative, the new
        /// weapons in its file, each loadout's bag and its set, a weapon of each bag cast at the dummy its new way and its
        /// right-click art used. Lines "[AutoTest] 法师存档检查 …".
        /// </summary>
        IEnumerator NewMageCheckTest()
        {
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            var lists = MageBags();
            var got = new int[4];
            bool nebula = true;
            int cast = 0;
            var at = TargetDummy.DevAt;
            for (int l = 0; l < 4; l++)
            {
                if (l > 0) inv.SwitchLoadout(l);
                yield return new WaitForSeconds(0.4f);
                got[l] = inv.Slots.Take(Inventory.Size).Count(s => !s.IsEmpty && lists[l].Contains(s.Type));
                nebula &= inv.Armor.Any(s => !s.IsEmpty && s.Type == ItemType.NebulaBreastplate);
                // Its first weapon cast a few times at the dummy (anything it shoots counted).
                if (at.HasValue)
                {
                    inv.Selected = 0;
                    int before = TerraProjectiles.DevSpawned.Values.Sum();
                    var aim = at.Value + new Vector2(0f, 0.8f);
                    for (float end = Time.time + 1.5f; Time.time < end;)
                    {
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                        yield return null;
                    }
                    if (TerraProjectiles.DevSpawned.Values.Sum() > before) cast++;
                }
                Shot(GameManager.instance.sceneName, $"newmagecheck_F{l + 1}");
            }
            inv.SwitchLoadout(0);
            string json = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{GameManager.instance.profileID}.json");
            bool newWeapons = File.Exists(json) && System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(json), "\"Weapons\"\\s*:\\s*1");
            bool ok = mod.Creative && inv.BagPerLoadout && Enumerable.Range(0, 4).All(l => got[l] == lists[l].Count)
                && nebula && newWeapons && cast == 4 && Save.ModSave.WorldName == MageSaveName && GameManager.instance.sceneName == "Town";
            Plugin.Log.LogInfo($"[AutoTest] 法师存档检查 {(ok ? "通过" : "不通过")}：场景 {GameManager.instance.sceneName}，世界名 {Save.ModSave.WorldName}，创造 {mod.Creative}，新版武器 {newWeapons}，"
                + $"F1 {got[0]}/{lists[0].Count}，F2 {got[1]}/{lists[1].Count}，F3 {got[2]}/{lists[2].Count}，F4 {got[3]}/{lists[3].Count}，法师套装 {nebula}，施法 {cast}/4");
        }
    }
}
