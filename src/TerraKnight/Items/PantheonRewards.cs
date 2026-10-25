using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    // Personal payout at native sequence completion, independently repeatable each run.
    [HarmonyPatch]
    internal static class PantheonRewards
    {
        static readonly System.Reflection.FieldInfo sequence = AccessTools.Field(typeof(BossSequenceController), "currentSequence");
        static readonly System.Reflection.FieldInfo data = AccessTools.Field(typeof(BossSequenceController), "currentData");
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> paid = new System.Runtime.CompilerServices.ConditionalWeakTable<object, object>();
        internal struct Claim { internal object Run; internal int Door, Stage; internal BossSequenceController.ChallengeBindings Bindings; }
        internal static bool TryClaim(object run)
        {
            if (run == null || paid.TryGetValue(run,out _)) return false;
            paid.Add(run,new object()); return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BossSequenceController), "FinishLastBossScene")]
        static void BeforeFinish(out Claim __state)
        {
            __state = new Claim { Run=data.GetValue(null), Door=GodhomeBalance.DoorOf(sequence.GetValue(null) as BossSequence), Stage=Shop.UnlockedStage(), Bindings=PlayerChallenges.Bindings };
        }
        [HarmonyPostfix, HarmonyPatch(typeof(BossSequenceController), "FinishLastBossScene")]
        static void AfterFinish(Claim __state)
        {
            if (!BossSequenceController.WasCompleted || __state.Door < 1 || __state.Door > 5 || !TryClaim(__state.Run)) return;
            var mod = TerraMod.Instance;
            if (mod == null || mod.Creative) return;
            mod.GrantedAbilities.Add("godhome.door.completed." + __state.Door);
            var hero=HeroController.SilentInstance;
            Vector2 at=hero!=null?(Vector2)hero.transform.position:Vector2.zero;
            foreach (var item in Contents(__state.Door,Mathf.Clamp(__state.Stage,0,5), __state.Bindings))
                EnemyDrops.Spill(item,at,true,perPlayer:true);
            var gm=GameManager.UnsafeInstance;
            if(gm!=null)Save.ModSave.Write(gm.profileID,mod);
            UI.CombatText.Show(at + Vector2.up*2, Lang.IsSource ? $"第 {__state.Door} 门通关奖励已领取" : $"Pantheon {__state.Door} completion rewards received", UI.CombatText.Notice);
        }
        internal static List<ItemStack> Contents(int door,int stage, BossSequenceController.ChallengeBindings bindings = BossSequenceController.ChallengeBindings.None)
        {
            var result=new List<ItemStack>();
            if (door<1 || door>5 || stage<0 || stage>5) return result;
            void Put(ItemType t,int n) { if (!CraftOnlyLoot.Blocked(t) && ProgressionBalance.Unlocked(t,stage,false,false)) result.Add(ItemStack.Of(t,n)); }
            // Equipment belongs to the individual boss. Completion pays materials only.
            if(stage<4) Put(new[]{ItemType.IronOre,ItemType.GoldOre,ItemType.DemoniteOre,ItemType.CobaltOre}[stage],30+9*door);
            else if(stage==4) {
                Put(ItemType.HallowedBar,15+5*door);
                foreach(var t in new[]{ItemType.SoulofMight,ItemType.SoulofSight,ItemType.SoulofFright}) Put(t,10+2*door);
            } else {
                foreach(var t in new[]{ItemType.FragmentSolar,ItemType.FragmentVortex,ItemType.FragmentNebula,ItemType.FragmentStardust})Put(t,10*door);
                if(door==5)Put(ItemType.LunarOre,80);
            }
            if(result.Count==0)
            {
                var support=new List<EnemyDrops.Drop>();BossDropPlan.AddSupport(support,VanillaBossProgression.RewardCap(stage));
                // Alternative ores share one choice, matching the support roll in a single fight.
                bool alternative=false;
                foreach(var row in support){if(row.Group>0&&alternative)continue;Put(row.Type,row.Min+door*2);if(row.Group>0)alternative=true;}
            }
            float share = PlayerChallenges.HollowReward * PlayerChallenges.CompletionShare(bindings);
            for (int i=0;i<result.Count;i++)
            {
                var item=result[i];
                item.Count=PlayerChallenges.ScaleCount(item.Count,share);
                result[i]=item;
            }
            return result;
        }
    }
}
