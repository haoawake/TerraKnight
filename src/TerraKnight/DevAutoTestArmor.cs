using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "armor", accarmor-hm sec-H2): every hardmode set worn in turn: the set it makes
    /// (the head's pick, the Beetle body's), the defense, the set bonus text, and a close-up of the
    /// pieces on the player.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ArmorTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedArmor = (ItemStack[])inv.Armor.Clone();
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            Wear();

            var sets = new (string name, ItemType head, ItemType body, ItemType legs, ArmorSet expect)[]
            {
                ("CobaltCaster", ItemType.CobaltHat, ItemType.CobaltBreastplate, ItemType.CobaltLeggings, ArmorSet.CobaltCaster),
                ("CobaltMelee", ItemType.CobaltHelmet, ItemType.CobaltBreastplate, ItemType.CobaltLeggings, ArmorSet.CobaltMelee),
                ("CobaltRanged", ItemType.CobaltMask, ItemType.CobaltBreastplate, ItemType.CobaltLeggings, ArmorSet.CobaltRanged),
                ("MythrilCaster", ItemType.MythrilHood, ItemType.MythrilChainmail, ItemType.MythrilGreaves, ArmorSet.MythrilCaster),
                ("MythrilMelee", ItemType.MythrilHelmet, ItemType.MythrilChainmail, ItemType.MythrilGreaves, ArmorSet.MythrilMelee),
                ("MythrilRanged", ItemType.MythrilHat, ItemType.MythrilChainmail, ItemType.MythrilGreaves, ArmorSet.MythrilRanged),
                ("AdamantiteCaster", ItemType.AdamantiteHeadgear, ItemType.AdamantiteBreastplate, ItemType.AdamantiteLeggings, ArmorSet.AdamantiteCaster),
                ("AdamantiteMelee", ItemType.AdamantiteHelmet, ItemType.AdamantiteBreastplate, ItemType.AdamantiteLeggings, ArmorSet.AdamantiteMelee),
                ("AdamantiteRanged", ItemType.AdamantiteMask, ItemType.AdamantiteBreastplate, ItemType.AdamantiteLeggings, ArmorSet.AdamantiteRanged),
                ("Palladium", ItemType.PalladiumHeadgear, ItemType.PalladiumBreastplate, ItemType.PalladiumLeggings, ArmorSet.Palladium),
                ("Orichalcum", ItemType.OrichalcumMask, ItemType.OrichalcumBreastplate, ItemType.OrichalcumLeggings, ArmorSet.Orichalcum),
                ("Titanium", ItemType.TitaniumHelmet, ItemType.TitaniumBreastplate, ItemType.TitaniumLeggings, ArmorSet.Titanium),
                ("Frost", ItemType.FrostHelmet, ItemType.FrostBreastplate, ItemType.FrostLeggings, ArmorSet.Frost),
                ("Forbidden", ItemType.AncientBattleArmorHat, ItemType.AncientBattleArmorShirt, ItemType.AncientBattleArmorPants, ArmorSet.Forbidden),
                ("Hallowed", ItemType.HallowedHeadgear, ItemType.HallowedPlateMail, ItemType.HallowedGreaves, ArmorSet.Hallowed),
                ("HallowedHood", ItemType.HallowedHood, ItemType.HallowedPlateMail, ItemType.HallowedGreaves, ArmorSet.HallowedSummoner),
                ("AncientHallowedMix", ItemType.AncientHallowedHelmet, ItemType.HallowedPlateMail, ItemType.AncientHallowedGreaves, ArmorSet.Hallowed),
                ("AncientHallowedHood", ItemType.AncientHallowedHood, ItemType.AncientHallowedPlateMail, ItemType.AncientHallowedGreaves, ArmorSet.HallowedSummoner),
                ("Crystal", ItemType.CrystalNinjaHelmet, ItemType.CrystalNinjaChestplate, ItemType.CrystalNinjaLeggings, ArmorSet.CrystalAssassin),
                ("ChlorophyteMask", ItemType.ChlorophyteMask, ItemType.ChlorophytePlateMail, ItemType.ChlorophyteGreaves, ArmorSet.ChlorophyteMelee),
                ("ChlorophyteHelmet", ItemType.ChlorophyteHelmet, ItemType.ChlorophytePlateMail, ItemType.ChlorophyteGreaves, ArmorSet.Chlorophyte),
                ("ChlorophyteVisor", ItemType.ChlorophyteVisor, ItemType.ChlorophytePlateMail, ItemType.ChlorophyteGreaves, ArmorSet.ChlorophyteSummon),
                ("Turtle", ItemType.TurtleHelmet, ItemType.TurtleScaleMail, ItemType.TurtleLeggings, ArmorSet.Turtle),
                ("Tiki", ItemType.TikiMask, ItemType.TikiShirt, ItemType.TikiPants, ArmorSet.Tiki),
                ("Spooky", ItemType.SpookyHelmet, ItemType.SpookyBreastplate, ItemType.SpookyLeggings, ArmorSet.Spooky),
                ("Shroomite", ItemType.ShroomiteMask, ItemType.ShroomiteBreastplate, ItemType.ShroomiteLeggings, ArmorSet.Shroomite),
                ("SpectreHood", ItemType.SpectreHood, ItemType.SpectreRobe, ItemType.SpectrePants, ArmorSet.SpectreHealing),
                ("SpectreMask", ItemType.SpectreMask, ItemType.SpectreRobe, ItemType.SpectrePants, ArmorSet.SpectreDamage),
                ("BeetleScaleMail", ItemType.BeetleHelmet, ItemType.BeetleScaleMail, ItemType.BeetleLeggings, ArmorSet.BeetleDamage),
                ("BeetleShell", ItemType.BeetleHelmet, ItemType.BeetleShell, ItemType.BeetleLeggings, ArmorSet.BeetleDefense),
                ("Solar", ItemType.SolarFlareHelmet, ItemType.SolarFlareBreastplate, ItemType.SolarFlareLeggings, ArmorSet.Solar),
                ("Vortex", ItemType.VortexHelmet, ItemType.VortexBreastplate, ItemType.VortexLeggings, ArmorSet.Vortex),
                ("Nebula", ItemType.NebulaHelmet, ItemType.NebulaBreastplate, ItemType.NebulaLeggings, ArmorSet.Nebula),
                ("Stardust", ItemType.StardustHelmet, ItemType.StardustBreastplate, ItemType.StardustLeggings, ArmorSet.Stardust),
                ("Mismatch", ItemType.CobaltHat, ItemType.MythrilChainmail, ItemType.AdamantiteLeggings, ArmorSet.None),
            };
            int right = 0;
            foreach (var (name, head, body, legs, expect) in sets)
            {
                inv.Armor[0] = ItemStack.Of(head);
                inv.Armor[1] = ItemStack.Of(body);
                inv.Armor[2] = ItemStack.Of(legs);
                yield return null;
                yield return null;
                var b = Equipment.Current;
                if (Equipment.FullSet == expect) right++;
                Plugin.Log.LogInfo($"[AutoTest] 盔甲 {name}：套装 {Equipment.FullSet}（应为 {expect}），防御 {mod.Stats.Defense}（{ItemDb.Get(head).Defense}+{ItemDb.Get(body).Defense}+{ItemDb.Get(legs).Defense}），" +
                                   $"近战/远程/魔法/召唤伤害 +{b.MeleeDamage * 100f:F0}/{b.RangedDamage * 100f:F0}/{b.MagicDamage * 100f:F0}/{b.MinionDamage * 100f:F0}%，仆从 {Equipment.MaxMinions}，减伤 {b.Endurance:F2}，" +
                                   $"「{Equipment.SetBonusText(Equipment.FullSet)?.Replace('\n', ' ')}」");
                yield return CloseUp(scene, "ar_" + name, hero, 60, 3);
            }
            Plugin.Log.LogInfo($"[AutoTest] 盔甲：套装判定 {right}/{sets.Length} 对");

            for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = savedArmor[i];
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            mod.DevNoFallDamage = false;
        }
    }
}
