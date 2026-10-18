using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ArmorRestoreTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int checks=0, failures=0;
            var observations=new List<object>();
            void Check(string label, bool pass, string detail="")
            {
                checks++; if(!pass) failures++;
                if(!pass) Plugin.Log.LogError($"[AutoTest] 护甲原版 {label} {detail} 不通过");
            }
            // Compare independent Terraria assembly output, including zeroes, with the
            // initialized item database. AllCrit is represented separately in the port.
            string[] fields={"Defense","ManaMax","MaxMinions","MaxTurrets","LifeRegen","MeleeDamage","RangedDamage","MagicDamage","MinionDamage",
                "MeleeCrit","RangedCrit","MagicCrit","MeleeSpeed","MoveSpeed","ManaCost","PickSpeed","Endurance","WhipRange","WhipFaster",
                "AmmoSave","ArrowMult","BulletMult","SpecialistMult","Aggro","NightVision","NoKnockback","FireWalk"};
            void Compare(string label, Bonuses actual, Bonuses expected)
            {
                actual.MeleeCrit+=actual.AllCrit;actual.RangedCrit+=actual.AllCrit;actual.MagicCrit+=actual.AllCrit;
                foreach(string field in fields)
                {
                    var f=typeof(Bonuses).GetField(field);
                    var a=f.GetValue(actual); var e=f.GetValue(expected);
                    bool pass=a is bool ? a.Equals(e) : Math.Abs(Convert.ToDouble(a)-Convert.ToDouble(e))<.0001;
                    Check(label+"/"+field,pass,$"actual={a} original={e}");
                }
            }
            var armor=ItemDb.All.Where(d=>d.IsArmor).ToArray();
            foreach(var d in armor)
            {
                int id=int.Parse(d.Texture.Substring("Item_".Length));
                bool found=VanillaArmorBonuses.Values.TryGetValue(id,out var expected);
                Check("原版引用/"+d.Type,found);
                var actual=d.Bonus;actual.Defense+=d.Defense;
                if(found) Compare(d.Type.ToString(),actual,expected);
                var original=ProgressionBalance.OriginalArmor[d.Type];
                Check("阶段不覆盖属性/"+d.Type,d.Defense==original.Defense && d.Bonus.Equals(original.Bonus)
                    && (d.Tooltip ?? "")== (original.Tooltip ?? "") && d.Set==original.Set && d.SetBonus==original.SetBonus);
                observations.Add(new {Type=d.Type.ToString(),d.Name,d.Stage,d.Defense,d.Bonus,d.Tooltip});
            }
            foreach(var pair in VanillaArmorBonuses.Sets)
            {
                Compare("套装/"+pair.Key,Equipment.SetBonus(pair.Key),pair.Value);
                Check("套装说明/"+pair.Key,!string.IsNullOrWhiteSpace(Equipment.SetBonusText(pair.Key)));
            }
            var inv=mod.Inventory;
            yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
            var armorWas=(ItemStack[])inv.Armor.Clone();var accWas=(ItemStack[])inv.Accessories.Clone();
            var difficultyWas=GameDifficulty.Current;
            int combinations=0;
            try
            {
                Array.Clear(inv.Accessories,0,inv.Accessories.Length);
                // Exercise every head/body/legs family, including both Beetle bodies and
                // ancient/current variants, through the real Equipment.Update path.
                foreach(var head in armor.Where(d=>d.Armor==ArmorSlot.Head && d.Set!=ArmorSet.None))
                foreach(var body in armor.Where(d=>d.Armor==ArmorSlot.Body && d.Set==head.Set))
                foreach(var legs in armor.Where(d=>d.Armor==ArmorSlot.Legs && d.Set==head.Set))
                {
                    PlayerBuffs.Clear();ArmorEffects.Clear();
                    inv.Armor[0]=ItemStack.Of(head.Type);inv.Armor[1]=ItemStack.Of(body.Type);inv.Armor[2]=ItemStack.Of(legs.Type);
                    var set=head.Set==ArmorSet.Beetle ? body.SetBonus : head.SetBonus!=ArmorSet.None ? head.SetBonus : head.Set;
                    Equipment.Update(inv);
                    Check("整套识别/"+head.Type+"/"+body.Type+"/"+legs.Type,Equipment.FullSet==set);
                    var expected=new Bonuses();
                    foreach(var d in new[]{head,body,legs}) expected.Add(VanillaArmorBonuses.Values[int.Parse(d.Texture.Substring(5))]);
                    if(VanillaArmorBonuses.Sets.TryGetValue(set,out var bonus)) expected.Add(bonus);
                    Compare("整套累计/"+head.Type+"/"+body.Type+"/"+legs.Type,Equipment.Current,expected);
                    combinations++;
                }
                void Dress(ItemType h,ItemType b,ItemType l)
                {
                    PlayerBuffs.Clear();ArmorEffects.Clear();TerraProjectiles.Instance.DevClear();
                    inv.Armor[0]=ItemStack.Of(h);inv.Armor[1]=ItemStack.Of(b);inv.Armor[2]=ItemStack.Of(l);Equipment.Update(inv);
                }
                Dress(ItemType.WizardHat,ItemType.DiamondRobe,ItemType.None);
                Check("巫师帽长袍无需护腿",Equipment.FullSet==ArmorSet.Wizard && Equipment.Current.MagicCrit>=.1f);
                Dress(ItemType.MoltenHelmet,ItemType.MoltenBreastplate,ItemType.MoltenGreaves);
                PlayerBuffs.Add(24,300);
                Check("熔岩套着火免疫",Equipment.Current.FireWalk && !PlayerBuffs.Has(24));
                Dress(ItemType.BeeHeadgear,ItemType.BeeBreastplate,ItemType.BeeGreaves);
                Check("蜜蜂套原版",Equipment.Current.Defense==13 && Equipment.Current.MaxMinions==2 && Mathf.Abs(Equipment.Current.MinionDamage-.23f)<.0001f);
                Dress(ItemType.SpiderMask,ItemType.SpiderBreastplate,ItemType.SpiderGreaves);
                Check("蜘蛛套原版",Equipment.Current.Defense==20 && Equipment.Current.MaxMinions==3 && Mathf.Abs(Equipment.Current.MinionDamage-.28f)<.0001f);
                Dress(ItemType.StardustHelmet,ItemType.StardustBreastplate,ItemType.StardustLeggings);
                ArmorEffects.Tick();
                Check("星尘守卫实体",PlayerBuffs.Has(ArmorEffects.StardustGuardian) && TerraProjectiles.Instance.ArmorCount(ProjKind.StardustGuardian)==1);
                Dress(ItemType.ChlorophyteMask,ItemType.ChlorophytePlateMail,ItemType.ChlorophyteGreaves);
                ArmorEffects.Tick();
                Check("叶绿水晶实体",PlayerBuffs.Has(ArmorEffects.LeafCrystal) && TerraProjectiles.Instance.ArmorCount(ProjKind.CrystalLeaf)==1);
                Dress(ItemType.BeetleHelmet,ItemType.BeetleShell,ItemType.BeetleLeggings);
                for(int i=0;i<540;i++) ArmorEffects.Tick();
                int beetleBefore=ArmorEffects.BeetleOrbs;int beetleTaken=ArmorEffects.ModifyHurt(100);
                Check("甲虫防御三层",beetleBefore==3 && beetleTaken>=54 && beetleTaken<=55 && ArmorEffects.BeetleOrbs==2,$"orbs={beetleBefore} damage={beetleTaken}");
                Dress(ItemType.SolarFlareHelmet,ItemType.SolarFlareBreastplate,ItemType.SolarFlareLeggings);
                for(int i=0;i<540;i++) ArmorEffects.Tick();
                Check("日耀三盾和冲刺",ArmorEffects.SolarShields==3 && ArmorEffects.DashKind(0)==3);
                Dress(ItemType.VortexHelmet,ItemType.VortexBreastplate,ItemType.VortexLeggings);
                ArmorEffects.KeyDoubleTapDown();
                for(int i=0;i<30;i++) ArmorEffects.Tick();
                Check("星旋隐身增伤",ArmorEffects.VortexStealth && ArmorEffects.DynamicBonuses().RangedDamage>.79f && ArmorEffects.RunSpeedMult==.3f);
                Dress(ItemType.CrystalNinjaHelmet,ItemType.CrystalNinjaChestplate,ItemType.CrystalNinjaLeggings);
                Check("水晶刺客冲刺",ArmorEffects.DashKind(0)==5);
                var target=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .FirstOrDefault(h=>!h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name=="Crossroads_01" && !EnemyStats.Of(h).Boss);
                Check("套装命中实测目标",target!=null);
                if(target!=null)
                {
                    void Hit(bool magic=false)
                    {
                        target.hp=100000;
                        ArmorEffects.Context=new ArmorEffects.Hit {Class=magic ? DamageClass.Magic : DamageClass.Melee,TrueMelee=!magic,FromProjectile=magic,ProjDamage=100};
                        mod.Melee.HitWith(target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>(),100,0,0,1,HeroController.instance.transform.position);
                    }
                    Dress(ItemType.PalladiumMask,ItemType.PalladiumBreastplate,ItemType.PalladiumLeggings);
                    Hit();Check("钯金命中快速治疗",PlayerBuffs.Has(ArmorEffects.RapidHealing) && ArmorEffects.RegenBonus==4);
                    Dress(ItemType.HallowedHood,ItemType.HallowedPlateMail,ItemType.HallowedGreaves);
                    Hit();Check("神圣命中免疫",PlayerBuffs.Has(ArmorEffects.HolyProtection) && Equipment.Current.MaxMinions==3 && ArmorEffects.TryHolyDodge() && !PlayerBuffs.Has(ArmorEffects.HolyProtection));
                    Dress(ItemType.TitaniumMask,ItemType.TitaniumBreastplate,ItemType.TitaniumLeggings);
                    int shard=ArmorEffects.DevShards;Hit();Check("钛金命中碎片",ArmorEffects.DevShards==shard+1 && TerraProjectiles.Instance.ArmorCount(ProjKind.TitaniumShard)==1);
                    Dress(ItemType.OrichalcumMask,ItemType.OrichalcumBreastplate,ItemType.OrichalcumLeggings);
                    for(int i=0;i<21;i++) ArmorEffects.Tick();
                    int petals=ArmorEffects.DevPetals;Hit();Check("山铜命中花瓣",ArmorEffects.DevPetals==petals+1);
                    Dress(ItemType.FrostHelmet,ItemType.FrostBreastplate,ItemType.FrostLeggings);
                    Hit();Check("寒霜命中霜冻",EnemyDebuffs.Has(target,EnemyDebuffs.Frostbite));
                    Dress(ItemType.SpectreHood,ItemType.SpectreRobe,ItemType.SpectrePants);
                    int heal=TerraProjectiles.DevMade(ProjKind.SpiritHeal);Hit(true);Check("幽灵兜帽治疗弹",TerraProjectiles.DevMade(ProjKind.SpiritHeal)>heal);
                    Dress(ItemType.SpectreMask,ItemType.SpectreRobe,ItemType.SpectrePants);
                    int wrath=ArmorEffects.DevWraths;Hit(true);Check("幽灵面具怒气弹",ArmorEffects.DevWraths>wrath);
                    Dress(ItemType.NebulaHelmet,ItemType.NebulaBreastplate,ItemType.NebulaLeggings);
                    int booster=ArmorEffects.DevBoosters;
                    for(int i=0;i<40 && ArmorEffects.DevBoosters==booster;i++) Hit(true);
                    Check("星云命中强化物",ArmorEffects.DevBoosters>booster);
                    for(int kind=0;kind<3;kind++) for(int i=0;i<3;i++) ArmorEffects.NebulaLevelUp(kind);
                    Check("星云三级增益",ArmorEffects.NebulaDamageLevel==3 && ArmorEffects.NebulaLifeLevel==3 && ArmorEffects.NebulaManaLevel==3 && Mathf.Abs(ArmorEffects.DynamicBonuses().MagicDamage-.45f)<.0001f);
                    Dress(ItemType.AncientBattleArmorHat,ItemType.AncientBattleArmorShirt,ItemType.AncientBattleArmorPants);
                    int storm=ArmorEffects.DevStorms;
                    mod.Stats.Mana=mod.Stats.ManaMax;
                    // The original ability needs a floor, three columns of headroom,
                    // line of sight and 20 mana. Find an actually valid local position.
                    for(int y=-12;y<=12 && ArmorEffects.DevStorms==storm;y++)
                    for(int x=-12;x<=12 && ArmorEffects.DevStorms==storm;x++)
                    {
                        mod.DevAim=(Vector2)HeroController.instance.transform.position+new Vector2(x,y);
                        ArmorEffects.KeyDoubleTapDown();
                    }
                    mod.DevAim=null;
                    Check("禁戒召唤沙暴",ArmorEffects.DevStorms==storm+1);
                }
                foreach(var lv in new[]{DifficultyLevel.Normal,DifficultyLevel.Hard,DifficultyLevel.Expert,DifficultyLevel.Master})
                {
                    GameDifficulty.Current=lv;
                    Check("斗篷原版反击/"+lv,TerraHurt.CloakDamage==(lv==DifficultyLevel.Master ? 225 : lv==DifficultyLevel.Expert ? 150 : 75));
                    Check("蜂窝原版反击/"+lv,TerraHurt.HoneyDamage(false)==(lv==DifficultyLevel.Master ? 26 : lv==DifficultyLevel.Expert ? 19 : 13));
                }
                yield return null;
            }
            finally
            {
                Array.Copy(armorWas,inv.Armor,armorWas.Length);Array.Copy(accWas,inv.Accessories,accWas.Length);
                GameDifficulty.Current=difficultyWas;PlayerBuffs.Clear();ArmorEffects.Clear();TerraProjectiles.Instance.DevClear();Equipment.Update(inv);
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"armor-restore.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,ArmorCount=armor.Length,SetCount=VanillaArmorBonuses.Sets.Count,Combinations=combinations,Items=observations},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 护甲原版汇总 {armor.Length}件 {VanillaArmorBonuses.Sets.Count}套 {combinations}搭配 {checks-failures}/{checks} {(failures==0 ? "通过" : "不通过")}");
        }
    }
}
