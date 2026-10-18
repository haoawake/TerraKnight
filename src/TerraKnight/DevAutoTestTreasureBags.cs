using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "treasurebags"): Terraria's treasure bags. Below Expert a boss's loot drops as ever; in Expert it leaves its
    /// bag and only the hearts and potions beside it; in Master the relic too. A bag opened from the inventory gives its
    /// boss's loot (a first kill's bag the boss weapon); the variants' bags, the first kill's mark through the save, the
    /// names and pictures.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TreasureBagsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 宝藏袋 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            var diff = World.GameDifficulty.Current;
            var hero = HeroController.instance;
            Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
            var got = new Dictionary<ItemType, int>();
            EnemyDrops.DevSink = (t, n) => got[t] = (got.TryGetValue(t, out int c) ? c : 0) + n;
            string List() => string.Join("，", got.Select(kv => ItemDb.Get(kv.Key).Name + "×" + kv.Value));
            bool OutsideItem(ItemType t) { var d = ItemDb.Get(t); return t == ItemType.Heart || d.HealLife > 0 || d.HealMana > 0 || d.LifeCrystal || d.ManaCrystal || Relics.Is(t); }
            const string boss = "FalseKnight";

            World.GameDifficulty.Current = World.DifficultyLevel.Normal;
            EnemyDrops.Roll(boss, at, false, true);
            Check("普通难度照常掉落", got.Count > 0 && !got.Keys.Any(TreasureBags.Is) && got.Keys.Any(t => !OutsideItem(t)), List());
            got.Clear();
            World.GameDifficulty.Current = World.DifficultyLevel.Expert;
            EnemyDrops.Roll(boss, at, false, true);
            Check("专家难度留下宝藏袋，袋外只有心和药水", got.TryGetValue(ItemType.BagFalseKnight, out int n1) && n1 == 1 && got.Keys.All(t => t == ItemType.BagFalseKnight || OutsideItem(t)) && !got.Keys.Any(Relics.Is), List());
            got.Clear();
            World.GameDifficulty.Current = World.DifficultyLevel.Master;
            EnemyDrops.Roll(boss, at, false, true);
            Check("大师模式宝藏袋加圣物", got.ContainsKey(ItemType.BagFalseKnight) && got.ContainsKey(ItemType.RelicFalseKnight), List());
            got.Clear();

            // Opened from the bag: the boss's loot; a first kill's the boss weapon.
            int slot = 30;
            var bag = ItemStack.Of(ItemType.BagFalseKnight);
            bag.Forge = 1;
            inv.Slots[slot] = bag;
            bool opened = TreasureBags.TryOpen(inv, slot);
            bool weapon = got.Keys.Any(t => ProgressionLoot.CombatWeapon(ItemDb.Get(t)));
            Check("打开首杀的宝藏袋：首领的掉落和武器", opened && inv.Slots[slot].IsEmpty && got.Count > 0 && weapon && !got.Keys.Any(OutsideItem), List());
            got.Clear();
            int weapons = 0;
            for (int k = 0; k < 20; k++)
            {
                inv.Slots[slot] = ItemStack.Of(ItemType.BagFalseKnight);
                TreasureBags.TryOpen(inv, slot);
                if (got.Keys.Any(t => ProgressionLoot.CombatWeapon(ItemDb.Get(t)))) weapons++;
                got.Clear();
            }
            Check("之后的宝藏袋武器看运气", weapons < 20, $"20 个里 {weapons} 个有武器");

            Check("变体首领的宝藏袋", TreasureBags.For("MegaBeamMiner", null, "Zombie Beam Miner Rematch") == ItemType.BagEnragedGuardian
                && TreasureBags.KeyOf(ItemType.BagEnragedGuardian) == "MegaBeamMiner" && TreasureBags.For("FinalBoss", "GG_Radiance", null) == ItemType.BagAbsoluteRadiance,
                $"{TreasureBags.For("MegaBeamMiner", null, "Zombie Beam Miner Rematch")}，{TreasureBags.For("FinalBoss", "GG_Radiance", null)}");
            var back = Save.ModSave.DevRoundTrip(bag);
            Check("首杀标记存进存档、写在提示里", back.Forge == 1 && (bag.Def.Tooltip ?? "").Contains(Lang.T("首次击败：必得首领武器")) && !(ItemStack.Of(ItemType.BagFalseKnight).Def.Tooltip ?? "").Contains(Lang.T("首次击败：必得首领武器")),
                bag.Def.Tooltip?.Replace("\n", " / "));
            var tex = Relics.Texture("bag_falseknight");
            Check("名字和贴图", ItemDb.Get(ItemType.BagFalseKnight).Name.Contains(Lang.IsSource || Lang.Culture.StartsWith("zh") ? "宝藏袋" : "Treasure Bag") && tex != null && tex.width == 36 && ItemDb.Get(ItemType.BagFalseKnight).Rare == -12,
                $"{ItemDb.Get(ItemType.BagFalseKnight).Name}，{tex?.width}x{tex?.height}");

            EnemyDrops.DevSink = null;
            for (int k = 0; k < 8; k++) inv.Slots[20 + k] = ItemStack.Of(new[] { ItemType.BagFalseKnight, ItemType.BagHornet, ItemType.BagMantisLord, ItemType.BagHollowKnight, ItemType.BagGrimm, ItemType.BagMegaBeamMiner, ItemType.BagFinalBoss, ItemType.BagZote }[k]);
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.5f);
            Shot("Town", "bags_inventory");
            yield return null;
            InventoryGui.Open = false;
            for (int k = 0; k < 8; k++) inv.Slots[20 + k] = default;
            World.GameDifficulty.Current = diff;
            mod.SetInfiniteItems(wasInfinite);
            Plugin.Log.LogInfo($"[AutoTest] 宝藏袋 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
