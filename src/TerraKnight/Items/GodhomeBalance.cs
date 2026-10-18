using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Godhome challenge tiers, independent of overworld progression and equipment stats.</summary>
    internal static class GodhomeBalance
    {
        internal sealed class Profile
        {
            public readonly string Key, Name;
            public readonly float Life, Defense, Damage;
            public readonly int FlatDefense;
            public Profile(string key,string name,float life,float defense,int flatDefense,float damage)
            {Key=key;Name=name;Life=life;Defense=defense;FlatDefense=flatDefense;Damage=damage;}
        }
        static readonly Profile World=new Profile("world","地图",1,1,0,1);
        internal static readonly Profile[] Hall={
            new Profile("attuned","众神堂·调谐",1.30f,1.25f,5,1.25f),
            new Profile("ascended","众神堂·飞升",1.70f,1.50f,8,1.45f),
            new Profile("radiant","众神堂·辐辉",2.10f,1.80f,10,1.70f)};
        internal static readonly Profile[] Pantheons={
            new Profile("pantheon1","一门",1.40f,1.25f,5,1.20f),
            new Profile("pantheon2","二门",1.70f,1.40f,6,1.30f),
            new Profile("pantheon3","三门",2.00f,1.55f,8,1.45f),
            new Profile("pantheon4","四门",2.40f,1.70f,9,1.60f),
            new Profile("pantheon5","五门",2.90f,1.90f,10,1.75f)};
        static readonly FieldInfo sequenceField=AccessTools.Field(typeof(BossSequenceController),"currentSequence");
        // Actual shipped Resources/GG assets; tier five's achievement is ENDINGD.
        internal static int DoorOf(BossSequence sequence)
        {
            if(sequence==null)return 0;
            for(int door=1;door<=5;door++)
                if(sequence.name=="Boss Sequence Tier "+door || sequence.achievementKey=="PANTHEON"+door)return door;
            return 0;
        }
        internal static Profile Select(string scene,int door,int hallLevel)
        {
            if(!BossStats.InGodhome(scene))return World;
            return door>=1 && door<=5 ? Pantheons[door-1] : Hall[Mathf.Clamp(hallLevel,0,2)];
        }
        internal static Profile Current(string scene,float nativeHealthTier=1f)
        {
            if(!BossStats.InGodhome(scene))return World;
            int door=BossSequenceController.IsInSequence ? DoorOf(sequenceField?.GetValue(null) as BossSequence) : 0;
            var controller=BossSceneController.Instance;
            // The controller is authoritative for Radiant, whose native HP may match Ascended.
            int level=controller!=null && controller.gameObject.scene.name==scene ? controller.BossLevel
                : Mathf.Clamp(Mathf.RoundToInt((nativeHealthTier-1)*2),0,2);
            return Select(scene,door,level);
        }
        internal static int Defense(int original,string scene) => Defense(original,Current(scene));
        internal static int Defense(int original,Profile profile) => Mathf.RoundToInt(original*profile.Defense)+profile.FlatDefense;
        internal static float Damage(string scene) => Current(scene).Damage;
    }
}
