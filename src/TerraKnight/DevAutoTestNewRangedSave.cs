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
    /// Dev (suite "newrangedsave", the user's 10-27 "新的射手存档…我要创造模式的"): a creative save with the new weapons for trying
    /// the remade ranged weapons (RangedArts), made as newmeleesave makes its own and copied into the player's free slot
    /// afterwards. Dirtmouth (its Target Dummy by the well), creative's showcase kit, all four loadouts in the ranged set
    /// (Vortex armour and its accessories), each with a bag of its own: F1 the bows, repeaters and the slingshot with the
    /// Endless Quiver; F2 the guns with the Endless Musket Pouch and their other ammo; F3 the launchers, cannons and
    /// sprayers with theirs; F4 the thrown weapons in full stacks. Each bag latest first (its hotbar the latest ten), its ammo
    /// in the ammo slots (the rest at the bag's end), three Target Dummies and the Lunar Hook last. Wakes on Dirtmouth's bench.
    /// Lines "[AutoTest] 射手存档 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const string RangedSaveName = "新射手武器";

        static bool RangedShown(ItemDef d) => d != null && ItemDb.Get(d.Type) == d && d.IsWeapon && d.Class == DamageClass.Ranged
            && d.Type != ItemType.RevenantPulseRifle && d.Type != ItemType.GhostPipeOrgan && d.Type != ItemType.ShadeRiftRifle && d.Type != ItemType.VoidSniper;

        /// <summary>The bag a ranged weapon goes in (0 bows, 1 guns, 2 launchers/cannons/sprayers, 3 thrown).</summary>
        static int RangedBag(ItemDef d)
        {
            switch (d.UseAmmo)
            {
                case AmmoKind.Arrow: case AmmoKind.Acorn: return 0;
                case AmmoKind.Bullet: case AmmoKind.Flare: case AmmoKind.Nail: case AmmoKind.CandyCorn: case AmmoKind.Dart: return 1;
                case AmmoKind.Ale: return 3;
                case AmmoKind.None:
                    if (d.Type == ItemType.PaintballGun || d.Type == ItemType.Harpoon || d.Type == ItemType.PiranhaGun || d.Type == ItemType.Toxikarp) return 1;
                    if (d.Type == ItemType.BarrelLauncher) return 2;
                    return 3;
                default: return 2;
            }
        }

        /// <summary>Each bag's ammo, the ammo slots first.</summary>
        static readonly ItemType[][] rangedAmmo =
        {
            new[] { ItemType.EndlessQuiver, ItemType.Acorn },
            new[] { ItemType.EndlessMusketPouch, ItemType.Flare, ItemType.Nail, ItemType.CandyCorn, ItemType.PoisonDart },
            new[] { ItemType.RocketIII, ItemType.FallenStar, ItemType.Gel, ItemType.Snowball, ItemType.SandBlock, ItemType.StyngerBolt, ItemType.ExplosiveJackOLantern, ItemType.Stake },
            new[] { ItemType.Ale },
        };

        static List<ItemType>[] RangedBags()
        {
            var bags = new[] { new List<ItemType>(), new List<ItemType>(), new List<ItemType>(), new List<ItemType>() };
            foreach (var d in ItemDb.All.Where(RangedShown).OrderByDescending(d => d.Stage).ThenByDescending(d => d.Rare).ThenByDescending(d => d.Damage))
                if (!bags[RangedBag(d)].Contains(d.Type)) bags[RangedBag(d)].Add(d.Type);
            return bags;
        }

        IEnumerator NewRangedSaveTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            ShowcaseKit.Give(mod);
            yield return new WaitForSeconds(0.5f);
            BladeArts.Set(true);
            // Every loadout in the ranged set (loadout 0's worn in place).
            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                if (l == 1) continue;
                var armor = l == 0 ? inv.Armor : inv.Loadouts[l].Armor;
                var acc = l == 0 ? inv.Accessories : inv.Loadouts[l].Accessories;
                for (int i = 0; i < Inventory.ArmorSlots; i++) armor[i] = inv.Loadouts[1].Armor[i];
                for (int i = 0; i < Inventory.AccessorySlots; i++) acc[i] = inv.Loadouts[1].Accessories[i];
            }
            var lists = RangedBags();
            int Fill(ItemStack[] bag, List<ItemType> items, ItemType[] ammo)
            {
                for (int i = 0; i < bag.Length; i++) bag[i] = default;
                int extraAmmo = Mathf.Max(0, ammo.Length - Inventory.AmmoSlots);
                int room = Inventory.Size - 2 - extraAmmo, k = 0;
                for (; k < items.Count && k < room; k++)
                {
                    var d = ItemDb.Get(items[k]);
                    bag[k] = ItemStack.Of(items[k], d.MaxStack > 1 ? d.MaxStack : 1);
                }
                for (int a = 0; a < ammo.Length; a++)
                {
                    var d = ItemDb.Get(ammo[a]);
                    var stack = ItemStack.Of(ammo[a], d.MaxStack > 1 ? d.MaxStack : 1);
                    if (a < Inventory.AmmoSlots) bag[Inventory.Size + a] = stack;
                    else bag[Inventory.Size - 3 - (a - Inventory.AmmoSlots)] = stack;
                }
                bag[Inventory.Size - 2] = ItemStack.Of(ItemType.TargetDummy, 3);
                bag[Inventory.Size - 1] = ItemStack.Of(ItemType.LunarHook);
                return k;
            }
            var put = new int[4];
            put[0] = Fill(inv.Slots, lists[0], rangedAmmo[0]);
            for (int l = 1; l < 4; l++)
            {
                put[l] = Fill(inv.Loadouts[l].Bag, lists[l], rangedAmmo[l]);
                inv.Loadouts[l].Selected = 0;
            }
            inv.Selected = 0;
            inv.RefreshEffective();
            for (int l = 0; l < 4; l++)
                Plugin.Log.LogInfo($"[AutoTest] 射手存档 F{l + 1} {put[l]}/{lists[l].Count} 件，弹药 {string.Join("、", rangedAmmo[l].Select(t => ItemDb.Get(t).Name))}；快捷栏 {string.Join("、", lists[l].Take(Inventory.HotbarSize).Select(t => ItemDb.Get(t).Name))}");
            Shot("Town", "newrangedsave_bag");
            Save.ModSave.WorldName = RangedSaveName;
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
            bool all = Enumerable.Range(0, 4).All(l => put[l] == lists[l].Count) && total >= 110 && mod.Creative && newWeapons;
            Plugin.Log.LogInfo($"[AutoTest] 射手存档 {(ok && all ? "通过" : "不通过")}：{total} 件射手武器，新版武器 {newWeapons}，创造 {mod.Creative}，存档位 {slot} 保存 {ok}，目录 {TerraMod.SaveDir}");
        }

        /// <summary>
        /// Dev (suite "newrangedcheck"): the save newrangedsave made, loaded again (run-rt6seed): its name, creative, the new
        /// weapons in its file, each loadout's bag (its weapons and its ammo) and its set, a weapon of each bag fired at the
        /// dummy its new way. Lines "[AutoTest] 射手存档检查 …".
        /// </summary>
        IEnumerator NewRangedCheckTest()
        {
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            var lists = RangedBags();
            var got = new int[4];
            var ammo = new int[4];
            bool vortex = true;
            int fired = 0;
            var at = TargetDummy.DevAt;
            for (int l = 0; l < 4; l++)
            {
                if (l > 0) inv.SwitchLoadout(l);
                yield return new WaitForSeconds(0.4f);
                got[l] = inv.Slots.Take(Inventory.Size).Count(s => !s.IsEmpty && lists[l].Contains(s.Type));
                ammo[l] = rangedAmmo[l].Count(t => inv.Slots.Any(s => !s.IsEmpty && s.Type == t));
                vortex &= inv.Armor.Any(s => !s.IsEmpty && s.Type == ItemType.VortexBreastplate);
                // Its first weapon fired a few times at the dummy (the new way's shots counted).
                if (at.HasValue)
                {
                    inv.Selected = 0;
                    int before = RangedFire.DevShots;
                    var aim = at.Value + new Vector2(0f, 0.8f);
                    for (float end = Time.time + 1.5f; Time.time < end;)
                    {
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                        yield return null;
                    }
                    if (RangedFire.DevShots > before) fired++;
                }
                Shot(GameManager.instance.sceneName, $"newrangedcheck_F{l + 1}");
            }
            inv.SwitchLoadout(0);
            string json = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{GameManager.instance.profileID}.json");
            bool newWeapons = File.Exists(json) && System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(json), "\"Weapons\"\\s*:\\s*1");
            bool ok = mod.Creative && inv.BagPerLoadout && Enumerable.Range(0, 4).All(l => got[l] == lists[l].Count && ammo[l] == rangedAmmo[l].Length)
                && vortex && newWeapons && fired == 4 && Save.ModSave.WorldName == RangedSaveName && GameManager.instance.sceneName == "Town";
            Plugin.Log.LogInfo($"[AutoTest] 射手存档检查 {(ok ? "通过" : "不通过")}：场景 {GameManager.instance.sceneName}，世界名 {Save.ModSave.WorldName}，创造 {mod.Creative}，新版武器 {newWeapons}，"
                + $"F1 {got[0]}/{lists[0].Count}（弹药 {ammo[0]}/{rangedAmmo[0].Length}），F2 {got[1]}/{lists[1].Count}（{ammo[1]}/{rangedAmmo[1].Length}），F3 {got[2]}/{lists[2].Count}（{ammo[2]}/{rangedAmmo[2].Length}），"
                + $"F4 {got[3]}/{lists[3].Count}（{ammo[3]}/{rangedAmmo[3].Length}），射手套装 {vortex}，开火 {fired}/4");
        }
    }
}
