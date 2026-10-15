using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "v270b"): the armor of 2.7. Every new piece has its icon and its sheet among Terraria's images and is
    /// crafted, dropped or sold as its source says; the new sets worn in turn make their set and give their bonus, and are
    /// photographed on the player (the robes drawn with their own legs); Ballista Panic after a hit and the Ballistas'
    /// reload; the gem robes' features on a gem staff's bolt; the Cactus set's thorns on an enemy; the Old One's Army
    /// sets on sale within the shops' 40 slots.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator V270bTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 2.7b {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            mod.DevNoFallDamage = true;

            // 1. Every new piece: its icon and sheet in Terraria's images; crafted ones have a recipe, the others none.
            var missing = new List<string>();
            int pieces = 0;
            for (var t = ItemType.Goggles; t <= ItemType.Pumpkin; t++)
            {
                var d = ItemDb.Get(t);
                if (d == null) { missing.Add(t + "（无定义）"); continue; }
                pieces++;
                if (!File.Exists(Path.Combine(Terraria.TerrariaAssets.ImagesDir, d.Texture + ".xnb"))) missing.Add(d.Name + " 图标");
                if (d.IsArmor)
                {
                    string sheet = d.Armor == ArmorSlot.Head ? "Armor_Head_" + d.ArmorSheet : d.Armor == ArmorSlot.Body ? "Armor/Armor_" + d.ArmorSheet : "Armor_Legs_" + d.ArmorSheet;
                    if (!File.Exists(Path.Combine(Terraria.TerrariaAssets.ImagesDir, sheet + ".xnb"))) missing.Add(d.Name + " " + sheet);
                }
                bool crafted = Recipes.For(t) != null;
                if ((d.Source == ItemSource.Craft) != crafted) missing.Add($"{d.Name} 来源 {d.Source} 配方 {crafted}");
            }
            Check("新盔甲的图标、贴图和来源", missing.Count == 0, $"{pieces} 件" + (missing.Count > 0 ? "；" + string.Join("、", missing) : ""));

            // 2. The sets.
            yield return GoTo("Town", "left1");
            pd.isInvincible = true;
            var hero = HeroController.instance;
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(1f);
            var savedArmor = (ItemStack[])inv.Armor.Clone();
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            Wear();
            var b = Equipment.Current;
            var sets = new (string name, ItemType head, ItemType body, ItemType legs, ArmorSet expect, System.Func<bool> bonus, string what)[]
            {
                ("Meteor", ItemType.MeteorHelmet, ItemType.MeteorSuit, ItemType.MeteorLeggings, ArmorSet.Meteor, () => Equipment.ManaCost(ItemDb.Get(ItemType.SpaceGun)) == 0, "太空枪 0 魔力"),
                ("Ninja", ItemType.NinjaHood, ItemType.NinjaShirt, ItemType.NinjaPants, ArmorSet.Ninja, () => Equipment.Current.MoveSpeed >= 0.2f, "移速 +20%"),
                ("Mining", ItemType.MiningHelmet, ItemType.MiningShirt, ItemType.MiningPants, ArmorSet.Mining, () => Equipment.Current.PickSpeed <= -0.29f, "挖矿 −30%"),
                ("Prospector", ItemType.UltrabrightHelmet, ItemType.UpgradedMiningBody, ItemType.UpgradedMiningLegs, ArmorSet.Mining, () => Equipment.Current.NightVision && Equipment.Current.Light, "夜视和照明"),
                ("Ebonwood", ItemType.EbonwoodHelmet, ItemType.EbonwoodBreastplate, ItemType.EbonwoodGreaves, ArmorSet.Ebonwood, () => true, "防御 +1"),
                ("WoodMix", ItemType.EbonwoodHelmet, ItemType.PalmWoodBreastplate, ItemType.EbonwoodGreaves, ArmorSet.None, () => true, "不同木头不成套"),
                ("AshWood", ItemType.AshWoodHelmet, ItemType.AshWoodBreastplate, ItemType.AshWoodGreaves, ArmorSet.AshWood, () => Equipment.Current.AshWood, "熔岩减伤"),
                ("Crimson", ItemType.CrimsonHelmet, ItemType.CrimsonScalemail, ItemType.CrimsonGreaves, ArmorSet.Crimson, () => mod.Stats.CrimsonRegen, "猩红再生"),
                ("Snow", ItemType.EskimoHood, ItemType.PinkEskimoCoat, ItemType.EskimoPants, ArmorSet.Snow, () => (Equipment.Current.DebuffImmune & DebuffImmunity.Frozen) != 0, "免疫冰冻"),
                ("Cactus", ItemType.CactusHelmet, ItemType.CactusBreastplate, ItemType.CactusLeggings, ArmorSet.Cactus, () => Equipment.Current.CactusThorns, "仙人掌反伤"),
                ("Pumpkin", ItemType.PumpkinHelmet, ItemType.PumpkinBreastplate, ItemType.PumpkinLeggings, ArmorSet.Pumpkin, () => Equipment.Current.MinionDamage >= 0.1f, "伤害 +10%"),
                ("Gladiator", ItemType.GladiatorHelmet, ItemType.GladiatorBreastplate, ItemType.GladiatorLeggings, ArmorSet.Gladiator, () => Equipment.Current.NoKnockback, "免疫击退"),
                ("Fossil", ItemType.FossilHelm, ItemType.FossilShirt, ItemType.FossilPants, ArmorSet.Fossil, () => Equipment.Current.AmmoSave >= 0.2f, "省弹 20%"),
                ("Angler", ItemType.UpgradedFishingHead, ItemType.AnglerVest, ItemType.UpgradedFishingLegs, ArmorSet.Angler, () => Equipment.Current.AnglerSet, "敌人返回变慢"),
                ("AncientShadow", ItemType.AncientShadowHelmet, ItemType.ShadowScalemail, ItemType.AncientShadowGreaves, ArmorSet.Shadow, () => true, "与暗影套混穿"),
                ("AncientCobalt", ItemType.AncientCobaltHelmet, ItemType.JungleShirt, ItemType.AncientCobaltLeggings, ArmorSet.Jungle, () => true, "与丛林套混穿"),
                ("AncientIron", ItemType.AncientIronHelmet, ItemType.IronChainmail, ItemType.IronGreaves, ArmorSet.Iron, () => true, "与铁套混穿"),
                ("WizardRobe", ItemType.WizardHat, ItemType.AmethystRobe, ItemType.None, ArmorSet.Wizard, () => Equipment.Current.MagicCrit >= 0.1f, "帽子加长袍，不穿腿"),
                ("MagicHatRobe", ItemType.MagicHat, ItemType.GypsyRobe, ItemType.MoonLordLegs, ArmorSet.MagicHat, () => Equipment.Current.ManaMax >= 60, "魔力 +60"),
                ("SquireT2", ItemType.SquireGreatHelm, ItemType.SquirePlating, ItemType.SquireGreaves, ArmorSet.SquireT2, () => (Equipment.Current.Ooa & OoaSet.SquireT2) != 0 && Equipment.MaxTurrets >= 3, "哨兵上限 3"),
                ("ApprenticeT2", ItemType.ApprenticeHat, ItemType.ApprenticeRobe, ItemType.ApprenticeTrousers, ArmorSet.ApprenticeT2, () => (Equipment.Current.Ooa & OoaSet.ApprenticeT2) != 0, "焰爆强化"),
                ("HuntressT2", ItemType.HuntressWig, ItemType.HuntressJerkin, ItemType.HuntressPants, ArmorSet.HuntressT2, () => (Equipment.Current.Ooa & OoaSet.HuntressT2) != 0, "爆炸机关强化"),
                ("MonkT2", ItemType.MonkBrows, ItemType.MonkShirt, ItemType.MonkPants, ArmorSet.MonkT2, () => (Equipment.Current.Ooa & OoaSet.MonkT2) != 0, "闪电光环强化"),
                ("SquireT3", ItemType.SquireAltHead, ItemType.SquireAltShirt, ItemType.SquireAltPants, ArmorSet.SquireT3, () => (Equipment.Current.Ooa & (OoaSet.SquireT2 | OoaSet.SquireT3)) == (OoaSet.SquireT2 | OoaSet.SquireT3), "T3 含 T2"),
                ("ApprenticeT3", ItemType.ApprenticeAltHead, ItemType.ApprenticeAltShirt, ItemType.ApprenticeAltPants, ArmorSet.ApprenticeT3, () => (Equipment.Current.Ooa & OoaSet.ApprenticeT3) != 0, "焰爆极大强化"),
                ("HuntressT3", ItemType.HuntressAltHead, ItemType.HuntressAltShirt, ItemType.HuntressAltPants, ArmorSet.HuntressT3, () => (Equipment.Current.Ooa & OoaSet.HuntressT3) != 0, "机关极大强化"),
                ("MonkT3", ItemType.MonkAltHead, ItemType.MonkAltShirt, ItemType.MonkAltPants, ArmorSet.MonkT3, () => (Equipment.Current.Ooa & OoaSet.MonkT3) != 0, "光环极大强化"),
            };
            int right = 0;
            var wrong = new List<string>();
            foreach (var (name, head, body, legs, expect, bonus, what) in sets)
            {
                inv.Armor[0] = ItemStack.Of(head);
                inv.Armor[1] = ItemStack.Of(body);
                inv.Armor[2] = legs == ItemType.None ? default : ItemStack.Of(legs);
                yield return null;
                yield return null;
                bool good = Equipment.FullSet == expect && bonus();
                if (good) right++;
                else wrong.Add($"{name}（套装 {Equipment.FullSet}，{what} {bonus()}）");
                Plugin.Log.LogInfo($"[AutoTest] 2.7b 盔甲 {name}：套装 {Equipment.FullSet}（应为 {expect}），{what} {bonus()}，防御 {mod.Stats.Defense}，「{Equipment.SetBonusText(Equipment.FullSet)?.Replace('\n', ' ')}」");
                if (name != "WoodMix") yield return CloseUp("Town", "v270b_" + name, hero, 60, 3);
            }
            Check("新套装", right == sets.Length, $"{right}/{sets.Length}" + (wrong.Count > 0 ? "；" + string.Join("、", wrong) : ""));
            Check("长袍画自己的腿", Equipment.RobeLegs(ItemDb.Get(ItemType.AmethystRobe).ArmorSheet) == 91 && Equipment.RobeLegs(ItemDb.Get(ItemType.Gi).ArmorSheet) == 118
                  && Equipment.RobeLegs(ItemDb.Get(ItemType.MeteorSuit).ArmorSheet) == 0, "紫晶长袍 91、稽古衣 118、流星护甲 0");

            // 3. Ballista Panic: a hit with a Squire set on; the Ballistas' reload 60, with tier 3 30.
            inv.Armor[0] = ItemStack.Of(ItemType.SquireGreatHelm);
            inv.Armor[1] = ItemStack.Of(ItemType.SquirePlating);
            inv.Armor[2] = ItemStack.Of(ItemType.SquireGreaves);
            yield return null;
            float calm = Minions.DevBallistaDelay;
            TerraHurt.BeforeHurt();
            yield return null;
            float panic2 = Minions.DevBallistaDelay;
            inv.Armor[0] = ItemStack.Of(ItemType.SquireAltHead);
            inv.Armor[1] = ItemStack.Of(ItemType.SquireAltShirt);
            inv.Armor[2] = ItemStack.Of(ItemType.SquireAltPants);
            yield return null;
            float panic3 = Minions.DevBallistaDelay;
            Check("弩车恐慌", PlayerBuffs.Has(PotionBuffs.BallistaPanic) && calm == 160f && panic2 == 60f && panic3 == 30f, $"恐慌 {PlayerBuffs.Has(PotionBuffs.BallistaPanic)}，装填 {calm} → {panic2}（T2 恐慌）→ {panic3}（T3 恐慌）");
            PlayerBuffs.Remove(PotionBuffs.BallistaPanic);

            // 4. The gem robes: the Topaz Robe puts the burst on an Amethyst bolt; the Amethyst Robe adds RepeatsGem.
            inv.Armor[0] = default;
            inv.Armor[2] = default;
            inv.Armor[1] = ItemStack.Of(ItemType.TopazRobe);
            yield return null;
            mod.Stats.Mana = mod.Stats.ManaMax;
            mod.DevUse(ItemType.AmethystStaff, (Vector2)hero.transform.position + new Vector2(6f, 0f));
            yield return new WaitForSeconds(0.4f);
            var topaz = TerraProjectiles.DevLastGem;
            inv.Armor[1] = ItemStack.Of(ItemType.AmethystRobe);
            yield return null;
            mod.Stats.Mana = mod.Stats.ManaMax;
            mod.DevUse(ItemType.AmethystStaff, (Vector2)hero.transform.position + new Vector2(6f, 0f));
            yield return new WaitForSeconds(0.4f);
            var amethyst = TerraProjectiles.DevLastGem;
            Check("宝石长袍为宝石魔法附魔", topaz == (TerraProjectiles.GemFx.Homing | TerraProjectiles.GemFx.Aoe) && amethyst == (TerraProjectiles.GemFx.Homing | TerraProjectiles.GemFx.Repeats),
                  $"黄玉长袍 + 紫晶法杖：{topaz}；紫晶长袍 + 紫晶法杖：{amethyst}");
            Shot("Town", "v270b_gemrobe");
            yield return null;

            // 5. The shops: the Witch Doctor's tier 2 sets and the Cyborg's tier 3, all within 40 slots; the Merchant's Pink Thread.
            int wd4 = Town.TownNpcs.DevStockAt("WitchDoctor", 4, ItemType.ApprenticeHat, out bool wdHas);
            int wd5 = Town.TownNpcs.DevStockAt("WitchDoctor", 5, ItemType.SquireAltHead, out bool wdT3);
            int cy5 = Town.TownNpcs.DevStockAt("Cyborg", 5, ItemType.MonkAltHead, out bool cyHas);
            Town.TownNpcs.DevStockAt("Merchant", 0, ItemType.PinkThread, out bool thread);
            Check("旧日军团盔甲上架", wdHas && !wdT3 && cyHas && thread && wd4 <= 40 && wd5 <= 40 && cy5 <= 40, $"巫医 T2 {wdHas}（4 阶段 {wd4} 件，5 阶段 {wd5} 件），机器侠 T3 {cyHas}（{cy5} 件），商人粉线 {thread}");

            // 6. The Cactus set's thorns on an enemy in King's Pass.
            yield return GoTo("Tutorial_01", EntryGateOf("Tutorial_01"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(3f);
            hero = HeroController.instance;
            inv.Armor[0] = ItemStack.Of(ItemType.CactusHelmet);
            inv.Armor[1] = ItemStack.Of(ItemType.CactusBreastplate);
            inv.Armor[2] = ItemStack.Of(ItemType.CactusLeggings);
            yield return null;
            HealthManager enemy = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm.gameObject.activeInHierarchy && !hm.isDead && hm.hp > 0 && !EnemyStats.Of(hm).Boss && (enemy == null || Vector2.Distance(hm.transform.position, hero.transform.position) < Vector2.Distance(enemy.transform.position, hero.transform.position))) enemy = hm;
            int cactus0 = ArmorEffects.DevCactus, hp0 = enemy != null ? enemy.hp : 0;
            if (enemy != null)
            {
                hero.transform.position = enemy.transform.position + new Vector3(-1.5f, 0.5f, 0f);
                yield return new WaitForSeconds(0.3f);
                ArmorEffects.OnContact(enemy, 20);
                yield return new WaitForSeconds(0.2f);
            }
            Check("仙人掌套反伤", enemy != null && ArmorEffects.DevCactus == cactus0 + 1 && enemy.hp < hp0, enemy == null ? "找不到敌人" : $"{enemy.name} 生命 {hp0} → {enemy.hp}，反伤 {ArmorEffects.DevCactus - cactus0} 次");

            for (int i = 0; i < savedArmor.Length; i++) inv.Armor[i] = savedArmor[i];
            for (int i = 0; i < savedAcc.Length; i++) inv.Accessories[i] = savedAcc[i];
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 2.7b 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
