using System;
using System.Collections;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator EnvironmentDamageTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town", "left1");
            yield return new WaitForSecondsRealtime(1f);
            int checks=0, failures=0;
            void Check(string name, bool pass) { checks++; if(!pass) failures++; Plugin.Log.LogInfo($"[AutoTest] ENVIRONMENT {name} {(pass?"通过":"不通过")}"); }
            var difficulty=GameDifficulty.Current;
            var bindings=PlayerChallenges.DevBindings;
            var equipment=Equipment.Current;
            bool immortal=TerraStats.DevImmortal;
            var pd=PlayerData.instance;
            bool invincible=pd.isInvincible;
            var st=mod.Stats;
            var fields=typeof(TerraStats).GetFields(BindingFlags.Public|BindingFlags.Instance);
            var values=Array.ConvertAll(fields,f=>f.GetValue(st));
            var record=typeof(PlayerHooks).GetMethod("RecordHazard",BindingFlags.Static|BindingFlags.NonPublic);
            var forget=typeof(PlayerHooks).GetMethod("ForgetHazard",BindingFlags.Static|BindingFlags.NonPublic);
            try
            {
                PlayerChallenges.DevBindings=BossSequenceController.ChallengeBindings.None;
                Equipment.Current=new Bonuses();
                TerraStats.DevImmortal=false;
                GameDifficulty.Current=DifficultyLevel.Hollow;
                foreach(int maximum in new[]{1,2,85,350,400,425,700})
                foreach(int defense in new[]{0,999})
                foreach(float endurance in new[]{0f,.95f})
                {
                    var sample=new TerraStats{LifeMax=maximum,Life=maximum,Defense=defense,Endurance=endurance};
                    int expected=maximum/2+maximum%2;
                    int dealt=sample.TakeEnvironment(1);
                    Check($"half-max/{maximum}/{defense}/{endurance}",dealt==expected&&sample.Life==maximum-expected&&sample.LifeMax==maximum);
                }
                var twice=new TerraStats{LifeMax=400,Life=400};
                twice.TakeEnvironment(1);twice.TakeEnvironment(99999);
                Check("two-hits-lethal-without-heal",twice.Life==0&&twice.LifeMax==400);
                var shield=new TerraStats{LifeMax=400,Life=400,Shield=100};
                shield.TakeEnvironment(1);
                Check("shield-retained",shield.Life==300&&shield.Shield==0);
                foreach(var b in new[]{BossSequenceController.ChallengeBindings.None,BossSequenceController.ChallengeBindings.Shell})
                {
                    PlayerChallenges.DevBindings=b;
                    var sample=new TerraStats{LifeCrystals=15,LifeFruits=20};sample.UpdateMaximums(null,0);sample.FullHeal();
                    int expected=b==BossSequenceController.ChallengeBindings.None?213:175;
                    Check("binding-maximum/"+b,sample.TakeEnvironment(1)==expected);
                }
                PlayerChallenges.DevBindings=BossSequenceController.ChallengeBindings.None;
                foreach(var d in new[]{DifficultyLevel.Normal,DifficultyLevel.Hard,DifficultyLevel.Expert,DifficultyLevel.Master})
                {
                    GameDifficulty.Current=d;
                    var a=new TerraStats{LifeMax=400,Life=400,Defense=20};
                    var b=new TerraStats{LifeMax=400,Life=400,Defense=20};
                    Check("other-difficulty-unchanged/"+d,a.TakeEnvironment(60)==b.TakeDirect(60)&&a.Life==b.Life);
                }
                GameDifficulty.Current=DifficultyLevel.Hollow;
                Equipment.Current=new Bonuses{DeadMansSweater=true};
                foreach(int hazard in new[]{2,3,4,5}) foreach(int masks in new[]{1,2,4})
                {
                    st.LifeMax=400;st.Life=400;st.Shield=0;st.Defense=999;st.Endurance=.95f;
                    record.Invoke(null,new object[]{null,hazard});
                    try { pd.TakeHealth(masks); } finally { forget.Invoke(null,null); }
                    Check($"native-hazard/{hazard}/{masks}",st.Life==200&&st.LifeMax==400&&TerraHurt.LastDamage==200);
                }
                // Explicit environmental tagging passes through the real HeroController damage entry.
                Equipment.Current=new Bonuses();
                st.LifeMax=400;st.Life=400;st.Shield=0;st.Defense=999;st.Endurance=.95f;
                pd.isInvincible=false;
                var hero=HeroController.instance;
                hero.cState.invulnerable=false;
                PlayerHooks.HurtEnvironment(hero,1,0,null);
                Check("tagged-environment-real-entry",st.Life==200&&st.LifeMax==400);
                st.Life=400;
                record.Invoke(null,new object[]{null,1});
                try { pd.TakeHealth(1); } finally { forget.Invoke(null,null); }
                Check("environment-tag-cleared-for-enemy",st.Life>200);
            }
            finally
            {
                GameDifficulty.Current=difficulty;PlayerChallenges.DevBindings=bindings;Equipment.Current=equipment;
                TerraStats.DevImmortal=immortal;pd.isInvincible=invincible;
                forget.Invoke(null,null);
                for(int i=0;i<fields.Length;i++)if(!fields[i].IsInitOnly)fields[i].SetValue(st,values[i]);
            }
            Plugin.Log.LogInfo($"[AutoTest] ENVIRONMENT checks={checks} failures={failures} {(failures==0?"通过":"不通过")}");
        }
    }
}
