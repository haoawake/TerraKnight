using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator AbilityStageTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(1f);
            int checks=0,failures=0,devStage=Shop.DevStage;
            void Check(string name,bool pass){checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] ABILITY_STAGE {name} {(pass?"通过":"不通过")}");}
            var pd=PlayerData.instance;var flags=ProgressionBalance.StageAbilityFlags;
            var before=flags.Select(pd.GetBool).ToArray();bool creative=mod.Creative,infinite=TerraMod.DevAllInfinite,free=mod.Inventory.Infinite;
            bool wing=PlayerHooks.Abilities.DoubleJump,claw=PlayerHooks.Abilities.WallCling;
            var super=PlayerHooks.LearnedSuperDash;
            void Mask(int mask){for(int i=0;i<flags.Length;i++)pd.SetBool(flags[i],(mask&(1<<i))!=0);}
            try {
                mod.ApplyCreative(false);TerraMod.DevAllInfinite=false;mod.SetInfiniteItems(false);Shop.DevStage=-1;
                PlayerHooks.Abilities.DoubleJump=PlayerHooks.Abilities.WallCling=true;
                for(int mask=0;mask<32;mask++) {
                    Mask(mask);int expected=Enumerable.Range(0,5).Count(i=>(mask&(1<<i))!=0);
                    Check("native-combination/"+mask,Shop.UnlockedStage()==expected);
                    PlayerHooks.DevDuringAbilitySwap(HeroController.instance,()=>Check("equipment-swap/"+mask,Shop.UnlockedStage()==expected));
                    Check("restored-flags/"+mask,flags.Select(pd.GetBool).SequenceEqual(Enumerable.Range(0,5).Select(i=>(mask&(1<<i))!=0)));
                    Check("godhome-stage/"+mask,World.Areas.FightLevel("GG_False_Knight")==expected);
                    foreach(var resource in ProgressionBalance.ResourceStages)
                        Check("resource/"+mask+"/"+resource.Key,ProgressionBalance.Unlocked(resource.Key)==(ItemDb.Get(resource.Key).Stage<=expected));
                }
                Mask(0);pd.hasSuperDash=true;PlayerHooks.LearnedSuperDash=false;
                Check("temporary-superdash-is-not-progress",Shop.UnlockedStage()==0);
                pd.hasSuperDash=false;PlayerHooks.LearnedSuperDash=null;
                int routes=0;
                void Permute(List<int> order) {
                    if(order.Count==5){int mask=0;for(int step=0;step<5;step++){mask|=1<<order[step];int local=mask;
                        Check("route/"+routes+"/"+step,ProgressionBalance.AbilityStage(f=>(local&(1<<Array.IndexOf(flags,f)))!=0)==step+1);}
                        routes++;return;}
                    for(int i=0;i<5;i++)if(!order.Contains(i)){order.Add(i);Permute(order);order.RemoveAt(order.Count-1);}
                }
                Permute(new List<int>());Check("all-120-orders",routes==120);
                Mask(2);yield return new WaitForSecondsRealtime(.3f);
                Check("claw-first-is-stage-one",Shop.UnlockedStage()==1);
                Check("claw-repeated-no-extra-stage",Shop.UnlockedStage()==1&&Shop.UnlockedStage()==1);
                int ordinary=World.Areas.LevelOf("Fungus2_14");
                InventoryGui.Open=true;InventoryGui.DevBestiary(true);yield return new WaitForSecondsRealtime(.6f);
                Check("book-follows-claw-stage",InventoryGui.DevBestiaryDropStage==-1&&Shop.UnlockedStage()==1);
                Shot("Town","ability_stage_claw_first");yield return new WaitForSecondsRealtime(.3f);
                pd.hasShadowDash=true;yield return null;
                Check("claw-plus-shade-is-stage-two",Shop.UnlockedStage()==2);
                Check("regional-fight-level-preserved",World.Areas.LevelOf("Fungus2_14")==ordinary);
                mod.ApplyCreative(true);CreativeStage.Set(3);Check("creative-stage-override",Shop.UnlockedStage()==3);
                Shop.DevStage=4;Check("developer-stage-override",Shop.UnlockedStage()==4);
            }finally{
                InventoryGui.Open=false;InventoryGui.DevBestiary(false);
                for(int i=0;i<flags.Length;i++)pd.SetBool(flags[i],before[i]);
                PlayerHooks.LearnedSuperDash=super;PlayerHooks.Abilities.DoubleJump=wing;PlayerHooks.Abilities.WallCling=claw;
                Shop.DevStage=devStage;mod.ApplyCreative(creative);TerraMod.DevAllInfinite=infinite;mod.SetInfiniteItems(free);
            }
            Plugin.Log.LogInfo($"[AutoTest] ABILITY_STAGE {checks} checks {failures} failures {(failures==0?"通过":"不通过")}");
        }
    }
}
