using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    // Authored victory packages: what the fight actually unlocks in Terraria.
    internal static class BossDropPlan
    {
        static readonly HashSet<ItemType> reserved=new HashSet<ItemType>();
        static bool built;
        /// <summary>Terraria's developer items, which its hardmode treasure bags give one of 1 time in 20: a hardmode boss's here.</summary>
        internal static readonly ItemType[] DeveloperItems={ItemType.RedsWings,ItemType.DTownsWings,ItemType.WillsWings,ItemType.CrownosWings,
            ItemType.CenxsWings,ItemType.BejeweledValkyrieWing,ItemType.Yoraiz0rWings,ItemType.JimsWings,ItemType.SkiphsWings,ItemType.LokisWings,
            ItemType.ArkhalisWings,ItemType.LeinforsWings,ItemType.GhostarsWings,ItemType.SafemanWings,ItemType.FoodBarbarianWings,
            ItemType.GroxTheGreatWings,ItemType.ChickenBonesWings,ItemType.KazzymodusWings,ItemType.LunasWings,ItemType.RedsYoyo,ItemType.ValkyrieYoyo};
        const int DeveloperGroup=105;
        internal static bool NaturalEquipment(ItemType t)
        {
            var d=ItemDb.Get(t);
            return d!=null && !d.Placeholder && d.Source==ItemSource.Drop && !CraftOnlyLoot.Blocked(t)
                && !SpellRewards.Is(t) && !Fishing.FishOnly(t);
        }
        static void Build()
        {
            if(built)return;
            foreach(var p in VanillaBossProgression.All.Values)
            {
                foreach(var t in p.Gear)reserved.Add(t);
                if(p.Signature!=ItemType.None)reserved.Add(p.Signature);
            }
            foreach(var extras in VanillaBossProgression.Extras.Values)foreach(var x in extras)reserved.Add(x.Type);
            foreach(var t in DeveloperItems)reserved.Add(t);
            built=true;
        }
        internal static ItemType[] Weapons(string key,int stage)
        {
            var result=new List<ItemType>();
            if(VanillaBossProgression.All.TryGetValue(key,out var p))foreach(var t in p.Gear)if(ProgressionLoot.CombatWeapon(ItemDb.Get(t)))result.Add(t);
            return result.ToArray();
        }
        internal static ItemType Signature(string key)=>VanillaBossProgression.All.TryGetValue(key,out var p)?p.Signature:ItemType.None;
        internal static bool Owns(string key,ItemType type,int stage)=>VanillaBossProgression.All.TryGetValue(key,out var p)&&(p.Signature==type||Array.IndexOf(p.Gear,type)>=0);
        /// <summary>
        /// One boss's own gear, token or separate roll, or a developer item. As in Terraria, it comes only from bosses: a missed
        /// piece is fought for again (its summoning item, the Colosseum's trials, the Hall of Gods), never handed to the common
        /// enemies of later areas.
        /// </summary>
        internal static bool BossGear(ItemType type){Build();return reserved.Contains(type);}
        /// <summary>An item only bosses give (BossGear, or one of the bosses' expert items): never a crate's, a chest's or a reward's.</summary>
        internal static bool BossOnly(ItemType type)=>BossGear(type)||ProgressionBalance.ExclusiveBossItems.Contains(type);
        static bool Available(ItemType t,int stage){var d=ItemDb.Get(t);return d!=null&&!d.Placeholder&&!CraftOnlyLoot.Blocked(t)&&d.Stage<=stage;}
        static EnemyDrops.Drop Row(ItemType t,int min=1,int max=1,int group=0,float chance=1f)
            =>new EnemyDrops.Drop{Type=t,Min=min,Max=max,Num=Mathf.RoundToInt(chance*100000),Den=100000,Group=group,LockStage=0,Weight=1};
        internal static EnemyDrops.Drop[] Drops(string key,EnemyDrops.Entry e,int stage,bool godhome,bool first,bool entry,bool final)
        {
            Build();
            var rows=new List<EnemyDrops.Drop>();
            if(!VanillaBossProgression.All.TryGetValue(key,out var p))return rows.ToArray();
            var cap=godhome?VanillaBossProgression.RewardCap(stage):p.Step;
            var kept=new List<EnemyDrops.Drop>();
            foreach(var d in e.Drops??Array.Empty<EnemyDrops.Drop>())
            {
                var def=ItemDb.Get(d.Type);
                if(d.Master||d.Type==ItemType.Heart||(def.HealLife>0||def.HealMana>0)&&!CraftOnlyLoot.Blocked(d.Type)&&def.Stage<=stage)
                {var copy=d;copy.Group=0;copy.LockStage=0;rows.Add(copy);}
                // The authored rows outside the equipment pools, as every other enemy keeps them (permanent upgrades, the Demon
                // Heart on a first win only; crafting stations; hooks and other single items): not a boss's own item, a mount
                // (Player.Mounts adds those), a crystal (EnemyDrops gives bosses theirs), healing above the stage, or a tool
                // (Terraria's are common enemies' drops, still had there).
                else if(!reserved.Contains(d.Type)&&!def.IsMount&&!def.LifeCrystal&&!def.ManaCrystal&&def.HealLife<=0&&def.HealMana<=0
                    &&def.PickPower<=0&&def.AxePower<=0&&def.HammerPower<=0&&!CraftOnlyLoot.Blocked(d.Type)
                    &&ProgressionLoot.KeptOutsidePools(d,stage,first,entry,final,out var extra))kept.Add(extra);
            }
            if(!godhome||cap>=p.Step && stage>=p.Band && VanillaBossProgression.Cleared(key))
            {
                foreach(var t in p.Gear)if(NaturalEquipment(t)){rows.Add(Row(t,group:100));rows.Add(Row(t,group:104,chance:.3f));}
                if(p.Signature!=ItemType.None&&NaturalEquipment(p.Signature))rows.Add(Row(p.Signature));
                foreach(var m in p.Materials)rows.Add(Row(m.Type,m.Min,m.Max,m.Group));
                if(VanillaBossProgression.Extras.TryGetValue(key,out var extras))
                    foreach(var x in extras)if(Available(x.Type,stage))rows.Add(Row(x.Type,chance:1f/x.Den));
                rows.AddRange(kept);
                // A hardmode boss: one developer item 1 time in 20, as Terraria's hardmode treasure bags.
                if(p.Band>=3)foreach(var t in DeveloperItems)if(Available(t,stage))rows.Add(Row(t,group:DeveloperGroup,chance:.05f));
            }
            else AddSupport(rows,cap);
            return rows.ToArray();
        }
        internal static void AddSupport(List<EnemyDrops.Drop> rows,VanillaBossProgression.Step step)
        {
            if(step<VanillaBossProgression.Step.Eye){rows.Add(Row(ItemType.IronOre,20,35));rows.Add(Row(ItemType.GoldOre,15,25));}
            else if(step<VanillaBossProgression.Step.Evil){rows.Add(Row(ItemType.DemoniteOre,30,60,208));rows.Add(Row(ItemType.CrimtaneOre,30,60,208));}
            else if(step<VanillaBossProgression.Step.Hardmode){rows.Add(Row(ItemType.Hellstone,30,51));rows.Add(Row(ItemType.Obsidian,10,17));}
            else if(step<VanillaBossProgression.Step.MechOne){rows.Add(Row(ItemType.AdamantiteOre,20,35,208));rows.Add(Row(ItemType.TitaniumOre,20,35,208));}
            else if(step<VanillaBossProgression.Step.MechAll)rows.Add(Row(ItemType.HallowedBar,10,20));
            else if(step<VanillaBossProgression.Step.Plantera)rows.Add(Row(ItemType.ChlorophyteOre,20,35));
            else rows.Add(Row(ItemType.Ectoplasm,8,14));
        }
        internal static EnemyDrops.Drop[] Route(string key,int stage,IReadOnlyList<EnemyDrops.Drop> drops)
        {
            Build();var rows=new List<EnemyDrops.Drop>();var totals=new Dictionary<int,float>();
            foreach(var row in drops)if(row.Group>0)totals[row.Group]=(totals.TryGetValue(row.Group,out var n)?n:0)+ProgressionLoot.Weight(row);
            foreach(var row in drops)
            {
                if(reserved.Contains(row.Type)||VanillaBossProgression.BossMaterial(row.Type)||!VanillaBossProgression.Allowed(row.Type))continue;
                var copy=row;if(row.Group>0)copy.GroupWeightTotal=Math.Max(row.GroupWeightTotal,totals[row.Group]);rows.Add(copy);
            }
            return rows.ToArray();
        }
    }
}
