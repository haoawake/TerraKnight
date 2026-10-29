using System.Collections;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "newmeleesave", the user's 10-26 "把新武器做一个创造模式存档给我"): a creative save for trying the remade
    /// spears, held blades and whips, made in the test copy and copied into the player's free slot afterwards. In Dirtmouth
    /// (its Target Dummy by the well), with creative's showcase kit (four loadouts in lunar armour); the melee loadout's bag
    /// holds every spear and the held blades (the hotbar those with right-click arts), the summon loadout's every whip, each
    /// ending with three Target Dummies and the Lunar Hook. Then the game saved. Lines "[AutoTest] 新近战存档 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NewMeleeSaveTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            ShowcaseKit.Give(mod);
            yield return new WaitForSeconds(0.5f);

            var showcase = new[]
            {
                ItemType.Gungnir, ItemType.NorthPole, ItemType.ChlorophytePartisan, ItemType.MushroomSpear, ItemType.ObsidianSwordfish,
                ItemType.ThunderSpear, ItemType.MythrilHalberd, ItemType.Terragrim, ItemType.Arkhalis, ItemType.PiercingStarlight,
            };
            var spears = TerraProjectiles.DevSpearItems().Select(ItemDb.Get).Where(d => d != null)
                .OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage).Select(d => d.Type).ToList();
            var melee = showcase.Concat(spears.Where(t => !showcase.Contains(t))).ToList();
            var whips = ItemDb.All.Where(d => d != null && d.IsWhip && d.Type == ItemDb.Get(d.Type).Type)
                .OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage).Select(d => d.Type).Distinct().ToList();

            int Fill(ItemStack[] bag, System.Collections.Generic.List<ItemType> items)
            {
                for (int i = 0; i < bag.Length; i++) bag[i] = default;
                int room = Inventory.Size - 2, k = 0;
                for (; k < items.Count && k < room; k++) bag[k] = ItemStack.Of(items[k]);
                bag[Inventory.Size - 2] = ItemStack.Of(ItemType.TargetDummy, 3);
                bag[Inventory.Size - 1] = ItemStack.Of(ItemType.LunarHook);
                return k;
            }
            int meleeIn = Fill(inv.Slots, melee);
            int whipsIn = Fill(inv.Loadouts[3].Bag, whips);
            inv.Loadouts[3].Selected = 0;
            inv.Selected = 0;
            inv.RefreshEffective();
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档 创造 {mod.Creative}，各配装背包 {inv.BagPerLoadout}；近战背包 {meleeIn}/{melee.Count} 把（长矛 {spears.Count}），召唤背包鞭子 {whipsIn}/{whips.Count} 把；新挥砍 {BladeArts.Enabled}");
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档 近战快捷栏：{string.Join("、", inv.Slots.Take(Inventory.HotbarSize).Select(s => s.IsEmpty ? "空" : s.Def.Name))}");
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档 鞭子：{string.Join("、", whips.Select(t => ItemDb.Get(t).Name))}");
            Shot("Town", "newmeleesave_bag");
            // Its name on the save list; loaded, the Knight wakes on Dirtmouth's bench.
            Save.ModSave.WorldName = "新近战武器";
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
            bool game = File.Exists(Path.Combine(TerraMod.SaveDir, $"user{slot}.dat"));
            bool modSave = File.Exists(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json"));
            bool all = meleeIn == melee.Count && whipsIn == whips.Count && whips.Count > 10 && spears.Count > 10 && mod.Creative;
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档 {(ok && game && modSave && all ? "通过" : "不通过")}：存档位 {slot} 保存 {ok}，user{slot}.dat {game}，TerraKnight_slot{slot}.json {modSave}，目录 {TerraMod.SaveDir}");
        }

        /// <summary>Dev (suite "newmeleecheck"): the save newmeleesave made, loaded again (run-rt2seed): creative, its name, the
        /// melee bag's spears and held blades, the summon loadout's whips once switched to. Lines "[AutoTest] 新近战存档检查 …".</summary>
        IEnumerator NewMeleeCheckTest()
        {
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            int Held(System.Func<ItemDef, bool> what) => inv.Slots.Take(Inventory.Size).Count(s => !s.IsEmpty && what(s.Def));
            var spears = TerraProjectiles.DevSpearItems();
            int melee = Held(d => spears.Contains(d.Type) || d.Type == ItemType.Arkhalis || d.Type == ItemType.Terragrim || d.Type == ItemType.PiercingStarlight);
            bool switched = inv.SwitchLoadout(3);
            yield return new WaitForSeconds(0.3f);
            int whips = Held(d => d.IsWhip);
            Shot(GameManager.instance.sceneName, "newmeleecheck_summon");
            inv.SwitchLoadout(0);
            bool ok = mod.Creative && inv.BagPerLoadout && melee == 21 && switched && whips == 18 && Save.ModSave.WorldName == "新近战武器" && GameManager.instance.sceneName == "Town";
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档检查 {(ok ? "通过" : "不通过")}：场景 {GameManager.instance.sceneName}，世界名 {Save.ModSave.WorldName}，创造 {mod.Creative}，各配装背包 {inv.BagPerLoadout}，近战背包 {melee} 把，召唤配装鞭子 {whips} 把");
        }

        /// <summary>The second round of remade melee (the user's 10-26 "继续升级剩下的这些，并把悠悠球的升级加进去"), by loadout.</summary>
        static readonly ItemType[][] secondRound =
        {
            // F1: the close weapons, the Zenith and the monk staffs, the flails and chained weapons.
            new[]
            {
                ItemType.Zenith, ItemType.MonkStaffT3, ItemType.SolarEruption, ItemType.FlaironFlail, ItemType.Flairon, ItemType.GolemFist, ItemType.HallowJoustingLance,
                ItemType.Gladius, ItemType.Ruler, ItemType.TragicUmbrella,
                ItemType.MonkStaffT2, ItemType.MonkStaffT1, ItemType.ChlorophyteSaber, ItemType.ShadowJoustingLance, ItemType.JoustingLance, ItemType.Umbrella,
                ItemType.CopperShortsword, ItemType.TinShortsword, ItemType.IronShortsword, ItemType.LeadShortsword, ItemType.SilverShortsword,
                ItemType.TungstenShortsword, ItemType.GoldShortsword, ItemType.PlatinumShortsword,
                ItemType.Mace, ItemType.FlamingMace, ItemType.BallOHurt, ItemType.TheMeatball, ItemType.BlueMoon, ItemType.Sunfury, ItemType.DaoofPow,
                ItemType.FlowerPow, ItemType.DripplerFlail, ItemType.ChainKnife, ItemType.ChainGuillotines, ItemType.KOCannon, ItemType.Anchor,
            },
            // F2: the thrown weapons.
            new[]
            {
                ItemType.DayBreak, ItemType.PaladinsHammer, ItemType.LightDisc, ItemType.PossessedHatchet, ItemType.VampireKnives, ItemType.ScourgeoftheCorruptor,
                ItemType.ShadowFlameKnife, ItemType.Bananarang, ItemType.FlyingKnife, ItemType.Flamarang,
                ItemType.WoodenBoomerang, ItemType.EnchantedBoomerang, ItemType.FruitcakeChakram, ItemType.BloodyMachete, ItemType.Shroomerang, ItemType.IceBoomerang,
                ItemType.ThornChakram, ItemType.CombatWrench, ItemType.Trimarang, ItemType.Axearang, ItemType.BouncingShield, ItemType.BoneJavelin,
            },
            // F3: the yoyos.
            new[]
            {
                ItemType.Terrarian, ItemType.Kraken, ItemType.TheEyeOfCthulhu, ItemType.RedsYoyo, ItemType.ValkyrieYoyo, ItemType.Yelets, ItemType.Code2,
                ItemType.FormatC, ItemType.HelFire, ItemType.Amarok,
                ItemType.WoodYoyo, ItemType.Rally, ItemType.CorruptYoyo, ItemType.CrimsonYoyo, ItemType.JungleYoyo, ItemType.Code1, ItemType.HiveFive,
                ItemType.Valor, ItemType.Cascade, ItemType.Chik, ItemType.Gradient,
            },
        };

        /// <summary>
        /// Dev (suite "newmeleesave2"): a creative save for the second round, made as newmeleesave makes the first: the showcase
        /// kit, then the melee loadout's bag (F1) the close weapons, Zenith, monk staffs, flails and chains; F2 and F3 dressed as
        /// the melee loadout (the Solar Flare set and its accessories) with the thrown weapons and the yoyos (F3 with the Yoyo
        /// Bag); each bag ending with three Target Dummies and the Lunar Hook; F4 the summoner's showcase. Dirtmouth's bench.
        /// Lines "[AutoTest] 新近战存档2 …".
        /// </summary>
        IEnumerator NewMeleeSave2Test()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            mod.ApplyCreative(true);
            mod.ApplyCreativeWorld();
            ShowcaseKit.Give(mod);
            yield return new WaitForSeconds(0.5f);
            var lists = secondRound.Select(l => l.Where(t => ItemDb.Get(t) != null && ItemDb.Get(t).Type == t).Distinct().ToList()).ToList();
            var missing = secondRound.SelectMany(l => l).Where(t => ItemDb.Get(t) == null || ItemDb.Get(t).Type != t).ToList();
            int Fill(ItemStack[] bag, System.Collections.Generic.List<ItemType> items)
            {
                for (int i = 0; i < bag.Length; i++) bag[i] = default;
                int room = Inventory.Size - 2, k = 0;
                for (; k < items.Count && k < room; k++) bag[k] = ItemStack.Of(items[k]);
                bag[Inventory.Size - 2] = ItemStack.Of(ItemType.TargetDummy, 3);
                bag[Inventory.Size - 1] = ItemStack.Of(ItemType.LunarHook);
                return k;
            }
            // F2 and F3 in the melee set.
            for (int l = 1; l <= 2; l++)
            {
                for (int i = 0; i < Inventory.ArmorSlots; i++) inv.Loadouts[l].Armor[i] = inv.Armor[i];
                for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Loadouts[l].Accessories[i] = inv.Accessories[i];
            }
            inv.Loadouts[2].Accessories[Inventory.AccessorySlots - 1] = ItemStack.Of(ItemType.YoyoBag);
            int[] put = { Fill(inv.Slots, lists[0]), Fill(inv.Loadouts[1].Bag, lists[1]), Fill(inv.Loadouts[2].Bag, lists[2]) };
            inv.Loadouts[1].Selected = inv.Loadouts[2].Selected = 0;
            inv.Selected = 0;
            inv.RefreshEffective();
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档2 F1 {put[0]}/{lists[0].Count}，F2 {put[1]}/{lists[1].Count}，F3 {put[2]}/{lists[2].Count}，没有的 {string.Join(",", missing)}");
            for (int l = 0; l < 3; l++)
                Plugin.Log.LogInfo($"[AutoTest] 新近战存档2 F{l + 1} 快捷栏：{string.Join("、", lists[l].Take(Inventory.HotbarSize).Select(t => ItemDb.Get(t).Name))}");
            Save.ModSave.WorldName = "新近战武器2";
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
            bool all = put[0] == lists[0].Count && put[1] == lists[1].Count && put[2] == lists[2].Count && missing.Count == 0 && mod.Creative;
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档2 {(ok && all ? "通过" : "不通过")}：存档位 {slot} 保存 {ok}，目录 {TerraMod.SaveDir}");
        }

        /// <summary>Dev (suite "newmeleecheck2"): the save newmeleesave2 made, loaded again (run-rt2seed): each loadout's bag.</summary>
        IEnumerator NewMeleeCheck2Test()
        {
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            var got = new int[3];
            for (int l = 0; l < 3; l++)
            {
                if (l > 0) inv.SwitchLoadout(l);
                yield return new WaitForSeconds(0.3f);
                got[l] = inv.Slots.Take(Inventory.Size).Count(s => !s.IsEmpty && secondRound[l].Contains(s.Type));
                Shot(GameManager.instance.sceneName, $"newmeleecheck2_F{l + 1}");
            }
            bool yoyoBag = inv.Accessories.Any(s => !s.IsEmpty && s.Type == ItemType.YoyoBag);
            inv.SwitchLoadout(0);
            int[] want = secondRound.Select(l => l.Count(t => ItemDb.Get(t) != null && ItemDb.Get(t).Type == t)).ToArray();
            bool ok = mod.Creative && inv.BagPerLoadout && got[0] == want[0] && got[1] == want[1] && got[2] == want[2] && yoyoBag
                && Save.ModSave.WorldName == "新近战武器2" && GameManager.instance.sceneName == "Town";
            Plugin.Log.LogInfo($"[AutoTest] 新近战存档检查2 {(ok ? "通过" : "不通过")}：场景 {GameManager.instance.sceneName}，世界名 {Save.ModSave.WorldName}，创造 {mod.Creative}，F1 {got[0]}/{want[0]}，F2 {got[1]}/{want[1]}，F3 {got[2]}/{want[2]}，悠悠球袋 {yoyoBag}");
        }
    }
}
