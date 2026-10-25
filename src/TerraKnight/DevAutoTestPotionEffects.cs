using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator PotionEffectsAudit()
        {
            var inv=mod.Inventory;var st=mod.Stats;
            int selected=inv.Selected,over=inv.Override;bool mouse=inv.UseMouse;
            var fields=typeof(Bonuses).GetFields(BindingFlags.Instance|BindingFlags.Public);
            Dictionary<string,object> Values(Bonuses b)=>fields.ToDictionary(f=>f.Name,f=>f.GetValue(b));
            var rows=new List<object>();int checks=0,failures=0;
            void Check(string name,bool pass){checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] POTION_EFFECT {name} {(pass?"通过":"不通过")}");}
            foreach(var def in ItemDb.All.Where(Containers.IsPotion))
            {
                PlayerBuffs.Clear();st.PotionSickness=0;st.HoneyTicks=0;
                st.Life=1;st.Mana=0;Equipment.Update(inv);
                var before=Values(PotionBuffs.Bonuses());
                if(def.BuffType==0&&def.HealLife==0&&def.HealMana==0)
                {
                    rows.Add(new {type=def.Type.ToString(),name=def.Name,kind="special_travel",tooltip=def.Tooltip});continue;
                }
                inv.Selected=0;inv.Override=-1;inv.UseMouse=false;inv.Slots[0]=ItemStack.Of(def.Type,3);
                mod.DevUseSelected(HeroController.instance.transform.position);
                int remaining=inv.Slots[0].Count,life=st.Life,mana=st.Mana;float honey=st.HoneyTicks;
                Equipment.Update(inv);
                var after=Values(PotionBuffs.Bonuses());
                var changed=after.Where(k=>!Equals(before[k.Key],k.Value)).ToDictionary(k=>k.Key,k=>k.Value);
                bool applicable=PotionBuffs.Applicable(def.BuffType);
                bool direct=def.HealLife>0&&life>1||def.HealMana>0&&mana>0||honey>0;
                bool fishing=def.BuffType==PotionBuffs.Fishing||def.BuffType==PotionBuffs.Sonar||def.BuffType==PotionBuffs.Crate;
                bool active=def.BuffType>0&&PlayerBuffs.Has(def.BuffType);
                Check(def.Type+"/consumption",remaining==(applicable?2:3));
                Check(def.Type+"/effect-signal",applicable?(direct||changed.Count>0||fishing&&active):!active&&changed.Count==0);
                rows.Add(new {type=def.Type.ToString(),name=def.Name,kind=applicable?"active":"unsupported",remaining,life,mana,honey,buff=def.BuffType,active,changed,tooltip=def.Tooltip});
                yield return null;
            }
            PlayerBuffs.Clear();st.PotionSickness=0;st.HoneyTicks=0;
            // Check consumers rather than just the presence of an icon.
            inv.Slots[1]=ItemStack.Of(ItemType.MasterBait,99);var rod=ItemDb.Get(ItemType.GoldenFishingRod);
            Equipment.Update(inv);int baseFishing=Fishing.FishingLevel(rod,inv);
            PotionBuffs.Give(PotionBuffs.Fishing,10000);int boostedFishing=Fishing.FishingLevel(rod,inv);
            Check("fishing-power-consumer",boostedFishing>baseFishing);
            foreach(var pair in new[]{(PotionBuffs.Battle,.5f),(PotionBuffs.Calm,1.3f),(PotionBuffs.Invisibility,1.2f)})
            {
                PlayerBuffs.Clear();Equipment.Update(inv);float baseline=PotionBuffs.RespawnFactor;
                PotionBuffs.Give(pair.Item1,10000);Equipment.Update(inv);
                Check("respawn-consumer/"+pair.Item1,Math.Abs(PotionBuffs.RespawnFactor/baseline-pair.Item2)<.0001f);
            }
            PlayerBuffs.Clear();Equipment.Update(inv);
            var weapon=ItemDb.Get(ItemType.CopperBroadsword);float kb=Equipment.Knockback(weapon);
            PotionBuffs.Give(PotionBuffs.Titan,10000);Equipment.Update(inv);
            Check("titan-knockback-consumer",Math.Abs(Equipment.Knockback(weapon)/kb-1.5f)<.0001f);
            PlayerBuffs.Clear();Equipment.Update(inv);
            var damageProbe=new ItemDef { Type=ItemType.CopperBroadsword,Class=DamageClass.Melee,Damage=200,Knockback=1 };
            int nanitesBefore=Equipment.Damage(damageProbe);
            PotionBuffs.Give(77,10000);Equipment.Update(inv);
            int nanitesAfter=Equipment.Damage(damageProbe);
            Check("nanites-advertised-five-percent-damage",nanitesAfter>=Math.Ceiling(nanitesBefore*1.05));
            // Random teleport's real use and movement in the loaded scene.
            PlayerBuffs.Clear();st.PotionSickness=0;inv.Slots[0]=ItemStack.Of(ItemType.TeleportationPotion,3);
            var hero=HeroController.instance;var pos=hero.transform.position;int teleports=PotionBuffs.DevTeleports;
            mod.DevUseSelected(pos);
            Check("random-teleport-moves-and-consumes",PotionBuffs.DevTeleports==teleports+1&&inv.Slots[0].Count==2&&Vector2.Distance(pos,hero.transform.position)>3);
            hero.transform.position=pos;hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
            inv.Selected=selected;inv.Override=over;inv.UseMouse=mouse;
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir,"potion-effects.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {checks,failures,baseFishing,boostedFishing,nanitesBefore,nanitesAfter,rows},Newtonsoft.Json.Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] POTION_EFFECT {checks} checks {failures} failures {(failures==0?"通过":"不通过")}");
        }
    }
}
