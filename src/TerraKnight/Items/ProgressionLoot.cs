using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Converts the legacy table into regional materials and bounded, class-balanced equipment rolls.
    /// The original ore conversion happens once, before these tables are read; counts here are final ore.
    /// </summary>
    internal static class ProgressionLoot
    {
        const int Main = 100, Function = 101, Rare = 102, Essential = 103, Ore = 200, Material = 201, Building = 202;
        static readonly Dictionary<string, EnemyDrops.Drop[]> cache = new Dictionary<string, EnemyDrops.Drop[]>();
        static readonly Dictionary<string,List<ItemType>> catalogues = new Dictionary<string,List<ItemType>>();
        static readonly Dictionary<string,Dictionary<string,List<ItemType>>> sources = new Dictionary<string,Dictionary<string,List<ItemType>>>();
        static HashSet<ItemType> legacyItems;
        static Dictionary<string,int> ordinals;
        internal static readonly HashSet<string> NamedElites = new HashSet<string> { "BigBuzzer","MageKnight","PaleLurker" };
        static readonly HashSet<string> Elites = new HashSet<string> {
            "ZombieGuard","BigBee","MushroomBrawler","RoyalGuard","FatFluke","MawlekTurret","FlameBearerLarge","FlameBearerMed",
            "GiantHopper","GorgeousHusk","GreatShieldZombie","HeavyMantis","SlashSpider"
        };
        internal static bool Eligible(EnemyDrops.Entry e) => e.Hp > 0 && e.Kind != "invulnerable" && e.Kind != "passive" && e.Kind != "non_enemy" && e.Kind != "player_shade";
        internal static bool Elite(string key, EnemyDrops.Entry e) => NamedElites.Contains(key) || e.Kind == "miniboss" || Elites.Contains(key);
        internal static bool Spawned(string key) => key == "Flukefly" || key == "HatcherBaby" || key == "BabyHatcher" || key == "JellyCrawler";

        internal static IReadOnlyList<EnemyDrops.Drop> Of(string key, EnemyDrops.Entry e, int stage, bool first, bool entry, bool final)
        {
            if (!Eligible(e) || Spawned(key)) return Array.Empty<EnemyDrops.Drop>();
            bool boss = ProgressionBalance.FormalBoss(key);
            if (boss && ProgressionBalance.BossRewards.TryGetValue(key, out var reward))
                stage = Math.Max(stage, reward.Stage);
            string id = key + ":" + stage + ":" + first + ":" + entry + ":" + final;
            if (!cache.TryGetValue(id, out var result)) cache[id] = result = Build(key,e,stage,first,entry,final,boss);
            return result;
        }
        static EnemyDrops.Drop Row(ItemType type, int group, float probability, int min = 1, int max = 1, float weight = 1)
            => new EnemyDrops.Drop { Type=type,Group=group,Num=Mathf.RoundToInt(probability*100000),Den=100000,Min=min,Max=max,LockStage=0,Weight=weight };

        static EnemyDrops.Drop[] Build(string key, EnemyDrops.Entry e, int stage, bool first, bool entry, bool final, bool boss)
        {
            var rows = new List<EnemyDrops.Drop>();
            if (key == "Zote") return new[] {Row(ItemType.Wood,Building,1,2,4)};
            bool elite = Elite(key,e);
            int tier = Mathf.Clamp(Math.Max(stage, NativeTier(key)),0,5);
            if (ProgressionBalance.BossRewards.TryGetValue(key,out var reward) && (boss || NamedElites.Contains(key)))
            {
                tier = reward.Stage;
                if(boss) stage=reward.Stage; // An old boss cannot pay endgame resources.
            }
            // Genuine healing, master relics and first-only permanent upgrades survive outside equipment pools.
            foreach (var d in e.Drops ?? Array.Empty<EnemyDrops.Drop>())
            {
                var def = ItemDb.Get(d.Type);
                // Upgrading the area's equipment pool must not erase the original ingredients
                // of vanilla upgrade recipes (Mace, Compass, old accessories, armour, etc.).
                if (!d.Master && def.Source == ItemSource.Drop && Recipes.IsMaterial(d.Type)
                    && def.Stage >= NativeTier(key)
                    && ProgressionBalance.Unlocked(d.Type, stage, entry, final) && RegionAllows(d.Type, e.Scene, boss)
                    && (def.IsWeapon || def.IsArmor || def.IsAccessory))
                {
                    rows.Add(Row(d.Type, 0, Mathf.Clamp(d.Chance, .05f, .1f), d.Min, d.Max));
                }
                // A Master relic places too, but drops every time (MasterModeCommonDrop), not as a tool.
                bool utility=!d.Master && (def.PickPower>0 || def.AxePower>0 || def.HammerPower>0 || def.PlacesStation!=World.StationKind.None);
                // Hooks, fishing poles and other single items are not stackable resources.
                // Preserve their original roll instead of pooling their expectation into a
                // large material stack (which can otherwise produce dozens of hooks).
                bool single=def.MaxStack==1 && !def.IsWeapon && !def.IsArmor && !def.IsAccessory;
                if (d.Master || d.Type == ItemType.Heart || def.HealLife > 0 || def.HealMana > 0 || def.LifeCrystal || def.ManaCrystal || def.Boost != PermanentBoost.None || utility && def.Stage >= NativeTier(key) || single && !utility)
                {
                    var copy=d;
                    copy.LockStage=0;
                    copy.Group=0;
                    if(def.MaxStack==1 || def.LifeCrystal || def.ManaCrystal || def.Boost!=PermanentBoost.None)
                    {copy.Min=copy.Max=1;copy.MeanCount=0;}
                    if(utility && copy.Chance>.05f) {copy.Num=1;copy.Den=20;}
                    if(d.Type==ItemType.DemonHeart && !first) continue;
                    if (d.Master || d.Type == ItemType.Heart || ProgressionBalance.Unlocked(d.Type,stage,entry,final)) rows.Add(copy);
                }
            }
            if (boss || (NamedElites.Contains(key) && first))
            {
                // Four class branches sum to .25 each even when one has more armour pieces.
                for (int cls=0;cls<4;cls++)
                {
                    var weapon = RewardWeapon(tier, cls, ProgressionBalance.BossRewards[key].Weapons[cls], entry, final);
                    var armor = new List<ItemType>();
                    foreach (var t in ProgressionBalance.ArmorFor(tier,cls))
                        if (ProgressionBalance.Unlocked(t,stage,entry,final)) armor.Add(t);
                    rows.Add(Row(weapon,Main,1,weight: armor.Count==0 ? .25f : .1875f));
                    foreach (var t in armor) rows.Add(Row(t,Main,1,weight:.0625f/armor.Count));
                }
                if (boss)
                {
                    float chance=first ? 1 : .2f;
                    var theme=ItemDb.Get(reward.Accessory);
                    if (theme.Kit == Kit.General && ProgressionBalance.Unlocked(theme.Type,Math.Max(stage,tier),entry,final))
                        rows.Add(Row(theme.Type,Function,chance));
                    else foreach (var t in ProgressionBalance.FunctionPool(tier))
                        if (ProgressionBalance.Unlocked(t,stage,entry,final)) rows.Add(Row(t,Function,chance,weight:.25f));
                }
            }
            else
            {
                // One shared rare-equipment roll, not one independent roll per candidate.
                float chance=elite ? .12f : .04f;
                for (int cls=0;cls<4;cls++)
                {
                    var weapons=new List<ItemType>();
                    for(int n=0;n<(MainRoute(key) ? 1 : 0);n++)
                    {
                        int local=(int)(StableHash(BestiaryRoute.AreaOf(key)+cls)%2);
                        var t=ProgressionBalance.Core[tier][cls][local];
                        if (ProgressionBalance.Unlocked(t,stage,entry,final)) weapons.Add(t);
                    }
                    AddExtras(weapons,key,tier,cls,0,entry,final);
                    var armor=new List<ItemType>();
                    foreach (var t in MainRoute(key) ? ProgressionBalance.ArmorFor(tier,cls) : Array.Empty<ItemType>())
                        if (ProgressionBalance.Unlocked(t,stage,entry,final)) armor.Add(t);
                    AddExtras(armor,key,tier,cls,1,entry,final);
                    foreach (var t in weapons) rows.Add(Row(t,Rare,chance,weight:.125f/Math.Max(1,weapons.Count)));
                    foreach (var t in armor) rows.Add(Row(t,Rare,chance,weight:.0625f/Math.Max(1,armor.Count)));
                    var accessories=new List<ItemType>();
                    var acc=ProgressionBalance.AccessoryPaths[tier][cls][1];
                    if (MainRoute(key) && ProgressionBalance.Unlocked(acc,stage,entry,final)) accessories.Add(acc);
                    AddExtras(accessories,key,tier,cls,2,entry,final);
                    foreach(var t in accessories) rows.Add(Row(t,Rare,chance,weight:.0625f/Math.Max(1,accessories.Count)));
                }
            }
            if (!boss && MainRoute(key))
            {
                // Keep the local accessory candidates; all ordinary-enemy accessory routes are
                // consolidated into one bounded roll below, including legacy crafting inputs.
                var essentials=new List<ItemType>();
                for (int cls=0;cls<4;cls++) foreach (var t in ProgressionBalance.AccessoryPaths[tier][cls])
                    if (ProgressionBalance.Unlocked(t,stage,entry,final) && !essentials.Contains(t)) essentials.Add(t);
                // Stable local specialties, used as relative weights in the shared accessory pool.
                if (essentials.Count > 0)
                {
                    uint hash=2166136261;
                    foreach(char c in key) hash=unchecked((hash ^ c)*16777619);
                    int at=(int)(hash % (uint)essentials.Count);
                    rows.Add(Row(essentials[at],Essential,elite ? .4f : .16f));
                    if(essentials.Count>1) rows.Add(Row(essentials[(at+1)%essentials.Count],Essential,elite ? .4f : .16f));
                }
            }

            if (!boss)
            {
                var inputs=new List<ItemType>();
                AddExtras(inputs,key,stage,-1,3,entry,final);
                foreach(var t in inputs) rows.Add(Row(t,Rare,elite ? .12f : .04f,weight:.025f));
            }
            var ores=new Dictionary<ItemType,float>();
            var mats=new Dictionary<ItemType,float>();
            var blocks=new Dictionary<ItemType,float>();
            foreach (var d in e.Drops ?? Array.Empty<EnemyDrops.Drop>())
            {
                var def=ItemDb.Get(d.Type);
                if (d.Group>0 || d.Master || def.MaxStack==1 || def.IsAccessory || def.IsArmor || def.Boost!=PermanentBoost.None || def.HkItem ||
                    def.HealLife>0 || def.HealMana>0 || def.LifeCrystal || def.ManaCrystal || d.Type==ItemType.Heart || def.PickPower>0 || def.AxePower>0 || def.HammerPower>0 ||
                    def.GameTool || def.IsWeapon && def.MaxStack==1 || def.PlacesStation != World.StationKind.None) continue;
                if (!ProgressionBalance.Unlocked(d.Type,stage,entry,final) || !RegionAllows(d.Type,e.Scene,boss)) continue;
                float expected=d.Chance*(d.Min+d.Max)*.5f;
                var pool=ItemDb.IsOre(d.Type) ? ores : (d.Type==ItemType.Wood || d.Type==ItemType.StoneBlock) ? blocks : mats;
                pool[d.Type]=(pool.TryGetValue(d.Type,out float n) ? n : 0)+expected;
            }
            AddRegionalMaterials(e.Scene,stage,entry,final,boss,ores,mats);
            if(!boss && (key=="Mosquito" || key=="BlobFlyer" || key=="LazyFlyer" || key=="BigBuzzer")
                && ProgressionBalance.Unlocked(ItemType.BeeWax,stage,entry,final))
                mats[ItemType.BeeWax]=Math.Max(Value(mats,ItemType.BeeWax),.8f);
            // The stage-2 smelting chain has an accessible exploration source as well
            // as the merchant fallback. A whole station is always one item.
            if(ProgressionBalance.Unlocked(ItemType.Hellforge,stage,entry,final))
            {
                if(key=="ZombieMiner" || key=="BeamMiner") rows.Add(Row(ItemType.Hellforge,0,.05f));
                if(key=="MegaBeamMiner") rows.Add(Row(ItemType.Hellforge,0,first ? 1f : .2f));
            }
            // Phase-defining ingredients should arrive steadily, not as a 30-ore
            // jackpot diluted by every older ore in the region's generic pool.
            if(!boss && ores.ContainsKey(ItemType.MythrilOre) && ores.ContainsKey(ItemType.OrichalcumOre))
            {
                ores.Remove(ItemType.MythrilOre);ores.Remove(ItemType.OrichalcumOre);
                rows.Add(Row(ItemType.MythrilOre,203,elite ? 1f : .8f,elite ? 3 : 2,elite ? 5 : 4));
                rows.Add(Row(ItemType.OrichalcumOre,203,elite ? 1f : .8f,elite ? 3 : 2,elite ? 5 : 4));
            }
            if(!boss && mats.Remove(ItemType.SpiderFang))
                rows.Add(Row(ItemType.SpiderFang,0,elite ? 1f : .75f,elite ? 2 : 1,elite ? 4 : 3));
            // Souls need frequent, small drops, not a rare selection from a huge mixed stack.
            foreach (var soul in new[] {ItemType.SoulofLight,ItemType.SoulofNight,ItemType.SoulofFlight}) mats.Remove(soul);
            if (ProgressionBalance.Unlocked(ItemType.SoulofLight,stage,entry,final))
            {
                bool flight=false;
                foreach (var d in e.Drops ?? Array.Empty<EnemyDrops.Drop>()) if (d.Type==ItemType.SoulofFlight) {flight=true;break;}
                void Soul(ItemType t)
                {
                    if (boss) rows.Add(Row(t,0,1,5,8));
                    else rows.Add(Row(t,0,elite ? 1 : .6f,elite ? 2 : 1,elite ? 4 : 3));
                }
                if (RegionAllows(ItemType.SoulofLight,e.Scene,boss)) Soul(ItemType.SoulofLight);
                if (RegionAllows(ItemType.SoulofNight,e.Scene,boss)) Soul(ItemType.SoulofNight);
                if (flight) Soul(ItemType.SoulofFlight);
            }
            if (boss && stage >= 4)
            {
                // A hardmode boss must fund a vanilla weapon, not one or two bars/souls.
                // These are independent guaranteed drops; never diluted into the material pool.
                foreach (var t in new[] { ItemType.HallowedBar, ItemType.SoulofMight, ItemType.SoulofSight, ItemType.SoulofFright }) mats.Remove(t);
                rows.Add(Row(ItemType.HallowedBar, 0, 1, 15, 25));
                ItemType soul = key == "HiveKnight" || key == "LobsterLancer" ? ItemType.SoulofFright
                    : key.StartsWith("Hornet") || key == "GhostMarkoth" || key == "Oblobble" ? ItemType.SoulofSight : ItemType.SoulofMight;
                rows.Add(Row(soul, 0, 1, 20, 30));
            }
            if (boss && stage >= 5)
            {
                foreach (var t in new[] { ItemType.BeetleHusk, ItemType.FragmentSolar, ItemType.FragmentVortex, ItemType.FragmentNebula, ItemType.FragmentStardust }) mats.Remove(t);
                rows.Add(Row(ItemType.BeetleHusk, 0, 1, 4, 8));
                ItemType fragment = key == "Grimm" || key == "NightmareGrimm" || key == "HollowKnightPrime" ? ItemType.FragmentSolar
                    : key == "WhiteDefender" || key == "Nailsage" ? ItemType.FragmentVortex
                    : key == "GreyPrince" || key == "Paintmaster" ? ItemType.FragmentNebula : ItemType.FragmentStardust;
                rows.Add(Row(fragment, 0, 1, 18, 26));
            }
            if (!boss && key.StartsWith("Zombie"))
            {
                mats[ItemType.Deathweed] = Math.Max(Value(mats, ItemType.Deathweed), .3f);
                if (stage >= 1) mats[ItemType.Blinkroot] = Math.Max(Value(mats, ItemType.Blinkroot), .3f);
            }
            if(boss && World.Areas.AreaName(e.Scene)=="蜂巢")
            {
                mats.Remove(ItemType.BeeWax);
                rows.Add(Row(ItemType.BeeWax,0,1,first ? 12 : 8,first ? 18 : 11));
            }
            // Stage-0 paths must not require Crystal Peak or a later NPC for the first staff.
            if (tier==0 && !boss)
            {
                mats[ItemType.Gel]=Math.Max(Value(mats,ItemType.Gel),.6f);
                mats[ItemType.Feather]=Math.Max(Value(mats,ItemType.Feather),.4f);
                mats[ItemType.FallenStar]=Math.Max(Value(mats,ItemType.FallenStar),.25f);
                mats[ItemType.Amethyst]=Math.Max(Value(mats,ItemType.Amethyst),.25f);
                mats[ItemType.Cobweb]=Math.Max(Value(mats,ItemType.Cobweb),.5f);
            }
            Pool(rows,ores,Ore,boss || elite ? 1 : .6f,1.2f);
            Pool(rows,mats,Material,1f,boss ? 1.5f : 1.8f);
            // Common construction stock belongs to early-route farming, not advanced fights.
            if (!boss && NativeTier(key)<2) Pool(rows,blocks,Building,.8f,1.2f);
            // Resource ingredients keep their local sources; obsolete equipment never leaks
            // back in through a legacy crafting-input or first-kill reward route.
            if (!boss) rows.RemoveAll(d => {
                var def=ItemDb.Get(d.Type);
                return !d.Master && !ProgressionBalance.Unlocked(d.Type,stage,entry,final)
                    || (CombatWeapon(def) || def.IsArmor || def.IsAccessory) && def.Stage < NativeTier(key);
            });
            if (!boss) ConsolidateAccessories(rows, elite);
            else ConsolidateAccessories(rows, elite, first ? 1f : .2f);
            return rows.ToArray();
        }

        internal static float AccessoryChance(bool elite) => elite ? .05f : .02f;
        internal static float AccessoryPoolChance(bool elite, int count) => count > 0 ? AccessoryChance(elite) : 0f;

        static void ConsolidateAccessories(List<EnemyDrops.Drop> rows, bool elite, float bossChance = -1f)
        {
            var weights = new Dictionary<ItemType, float>();
            float rareTotal=0, rareRemaining=0;
            foreach (var d in rows)
                if (d.Group==Rare)
                {
                    rareTotal+=Weight(d);
                    if (!ItemDb.Get(d.Type).IsAccessory) rareRemaining+=Weight(d);
                }
            foreach (var d in rows)
            {
                if (!ItemDb.Get(d.Type).IsAccessory) continue;
                float weight=d.Chance;
                if (d.Group>0)
                {
                    float total=0;
                    foreach (var option in rows) if (option.Group==d.Group) total+=Weight(option);
                    weight*=Weight(d)/Math.Max(.00001f,total);
                }
                weights[d.Type]=(weights.TryGetValue(d.Type,out float old) ? old : 0)+weight;
            }
            rows.RemoveAll(d=>ItemDb.Get(d.Type).IsAccessory);
            // Moving accessories out of the rare equipment group must not increase weapon/armour rates.
            if (rareTotal>0)
                for (int i=0;i<rows.Count;i++)
                    if (rows[i].Group==Rare)
                    {
                        var d=rows[i];
                        d.Num=Mathf.RoundToInt(d.Num*rareRemaining/rareTotal);
                        rows[i]=d;
                    }
            foreach (var pair in weights)
                // One fixed pool roll, followed by an equal N-way choice. Adding candidates
                // must never increase the chance that a kill pays an accessory.
                rows.Add(Row(pair.Key,Essential,bossChance >= 0 ? bossChance : AccessoryPoolChance(elite, weights.Count),1,1,1f));
        }

        static float Value(Dictionary<ItemType,float> pool, ItemType t) => pool.TryGetValue(t,out float n) ? n : 0;
        internal static bool CombatWeapon(ItemDef d) => d.IsWeapon && !d.IsAmmo && !d.GameTool && !d.HkItem && d.MaxStack==1
            && d.PickPower==0 && d.AxePower==0 && d.HammerPower==0 && d.FishingPower==0 && d.PlacesStation==World.StationKind.None;
        static void AddExtras(List<ItemType> pool,string record,int stage,int cls,int category,bool entry,bool final)
        {
            if(legacyItems==null)
            {
                legacyItems=new HashSet<ItemType>();
                var records=new List<string>(EnemyDrops.DevRecords);
                records.Sort((a,b)=>NamedElites.Contains(a)!=NamedElites.Contains(b) ? (NamedElites.Contains(a) ? 1 : -1) : StringComparer.Ordinal.Compare(a,b));
                ordinals=new Dictionary<string,int>();
                int ordinal=0;
                for(int i=0;i<records.Count;i++)
                {
                    var e=EnemyDrops.EntryFor(records[i]);
                    if(Eligible(e) && !Spawned(records[i]) && records[i]!="Zote" && !ProgressionBalance.FormalBoss(records[i])) ordinals[records[i]]=ordinal++;
                    foreach(var row in EnemyDrops.LegacyOf(records[i])) legacyItems.Add(row.Type);
                }
            }
            string id=stage+":"+cls+":"+category+":"+entry+":"+final;
            if(!catalogues.TryGetValue(id,out var candidates))
            {
                candidates=new List<ItemType>();
                foreach(var t in legacyItems)
                {
                    var d=ItemDb.Get(t);
                    if((category==3 ? d.Stage>stage : d.Stage!=stage) || !ProgressionBalance.Unlocked(t,stage,entry,final)) continue;
                    bool match=category==3 ? Recipes.IsMaterial(t) && (CombatWeapon(d) || d.IsArmor || d.IsAccessory)
                        : category==0 ? CombatWeapon(d) && ProgressionBalance.ClassIndex(d.Class)==cls
                        : category==1 ? d.IsArmor && (d.Kit==Kit.General || ProgressionBalance.ClassIndex(d.Kit)==cls)
                        : d.IsAccessory && (d.Kit==Kit.General || ProgressionBalance.ClassIndex(d.Kit)==cls);
                    if(match) candidates.Add(t);
                }
                candidates.Sort();
                catalogues[id]=candidates;
            }
            if(candidates.Count==0) return;
            if(sources.TryGetValue(id,out var assigned))
            {
                if(assigned.TryGetValue(record,out var local)) foreach(var t in local) if(!pool.Contains(t)) pool.Add(t);
                return;
            }
            sources[id]=assigned=new Dictionary<string,List<ItemType>>();
            void Assign(string rec,ItemType t)
            {
                if(!assigned.TryGetValue(rec,out var list)) assigned[rec]=list=new List<ItemType>();
                list.Add(t);
            }
            // Assign every catalogue item to a stable regional source. Main-route enemies
            // carry progression staples and recipe inputs; side routes carry optional variants.
            // Build the eligible roster for this tier so future-area enemies cannot strand gear.
            foreach (var t in candidates)
            {
                string theme=category==3 ? null : SpecialtyArea(t);
                // Before the first ability there is no side-route roster: keep starter variants reachable.
                bool main=stage==0 || category==3 || (theme!=null ? MainArea(theme) : Recipes.IsMaterial(t) || StableHash(t.ToString())%4 != 0);
                var areas=new SortedDictionary<string,List<string>>(StringComparer.Ordinal);
                foreach (var rec in ordinals.Keys)
                {
                    // Named elites use fixed boss-style reward tiers, including a separate
                    // first-kill branch; they cannot own a scaling catalogue's sole source.
                    if (NamedElites.Contains(rec)) continue;
                    int sourceStage=category==3 ? ItemDb.Get(t).Stage : stage;
                    if (NativeTier(rec)>sourceStage || World.Areas.LevelOf(EnemyDrops.SceneOf(rec))>sourceStage || MainRoute(rec)!=main) continue;
                    string area=BestiaryRoute.AreaOf(rec);
                    if (!areas.TryGetValue(area,out var members)) areas[area]=members=new List<string>();
                    members.Add(rec);
                }
                if (areas.Count==0) continue;
                if (theme!=null && areas.TryGetValue(theme,out var themed))
                {
                    themed.Sort(StringComparer.Ordinal);
                    Assign(themed[(int)(StableHash(t.ToString())%(uint)themed.Count)],t);
                    continue;
                }
                var regionNames=new List<string>(areas.Keys);
                uint hash=StableHash(t.ToString());
                var roster=areas[regionNames[(int)(hash%(uint)regionNames.Count)]];
                roster.Sort(StringComparer.Ordinal);
                string source=roster[(int)((hash/(uint)regionNames.Count)%(uint)roster.Count)];
                Assign(source,t);
            }
            if(assigned.TryGetValue(record,out var selected)) foreach(var t in selected) if(!pool.Contains(t)) pool.Add(t);
        }
        static uint StableHash(string text)
        {
            uint hash=2166136261;
            foreach(char c in text) hash=unchecked((hash^c)*16777619);
            return hash;
        }
        internal static bool MainRoute(string record)
            => MainArea(BestiaryRoute.AreaOf(record));
        static string SpecialtyArea(ItemType type)
        {
            string name=type.ToString();
            if(name.Contains("Bee") || name.StartsWith("Hive")) return "蜂巢";
            if(name.Contains("Spider")) return "深巢";
            if(name.Contains("Crystal")) return "水晶山峰";
            if(name.Contains("Jungle") || name.Contains("Thorn") || name.Contains("Spore")) return "真菌荒地";
            if(name.Contains("Spooky") || name.Contains("Bat") || name.Contains("Vampire")) return "格林剧团";
            return null;
        }
        static bool MainArea(string area)
        {
            switch (area)
            {
                case "国王山道": case "遗忘十字路": case "苍绿之径": case "真菌荒地":
                case "泪水之城": case "水晶山峰": case "远古盆地": case "王国边缘":
                case "深巢": case "深渊": case "黑卵神殿": return true;
                default: return false;
            }
        }
        internal static int NativeTier(string record)
        {
            switch (BestiaryRoute.AreaOf(record))
            {
                case "国王山道": case "遗忘十字路": return 0;
                case "苍绿之径": case "真菌荒地": case "雾之峡谷": case "呼啸悬崖": return 1;
                case "泪水之城": case "水晶山峰": case "皇家水道": case "安息之地": return 2;
                case "远古盆地": case "深巢": case "野兽巢穴": case "王后花园": return 3;
                case "王国边缘": case "蜂巢": case "愚人斗兽场": case "深渊": return 4;
                default: return 5;
            }
        }

        static void Pool(List<EnemyDrops.Drop> rows,Dictionary<ItemType,float> pool,int group,float probability,float boost)
        {
            float total=0;
            foreach(var v in pool.Values) total+=v;
            if(total<=0) return;
            probability=Math.Min(probability,total*boost);
            foreach(var kv in pool)
            {
                // With weights proportional to the old per-kill expectation this preserves each
                // material's expectation times boost while combining N independent rows into one.
                float mean=total*boost/probability;
                int low=Math.Max(1,(int)Math.Floor(mean));
                float w=kv.Value/total;
                var row=Row(kv.Key,group,probability,low,(int)Math.Ceiling(mean),w);
                row.MeanCount=mean;
                rows.Add(row);
            }
        }

        internal static bool RegionAllows(ItemType t,string scene,bool boss)
        {
            string area=World.Areas.AreaName(scene);
            bool jungle=area=="苍绿之径" || area=="真菌荒地" || area=="王后花园";
            switch(t)
            {
                case ItemType.CopperOre: case ItemType.TinOre: case ItemType.IronOre: case ItemType.LeadOre: case ItemType.SilverOre:
                    return area=="国王山道" || area=="遗忘十字路" || area=="呼啸悬崖";
                case ItemType.TungstenOre: case ItemType.GoldOre: case ItemType.PlatinumOre:
                    return area=="真菌荒地" || area=="呼啸悬崖" || area=="水晶山峰";
                case ItemType.JungleSpores: case ItemType.Vine: case ItemType.Stinger: case ItemType.ChlorophyteOre:
                    return jungle || (t==ItemType.Stinger && area=="蜂巢");
                case ItemType.BeeWax: return area=="蜂巢" || area=="苍绿之径";
                case ItemType.Hellstone: return area=="水晶山峰" || area=="深渊" || area=="远古盆地" || scene.StartsWith("Grimm");
                case ItemType.Obsidian: return area=="深渊" || area=="远古盆地" || area=="皇家水道" || area=="水晶山峰";
                case ItemType.DemoniteOre: case ItemType.CrimtaneOre:
                    return area=="真菌荒地" || area=="泪水之城" || area=="皇家水道";
                case ItemType.ShadowScale: case ItemType.TissueSample:
                    return area=="泪水之城" || area=="皇家水道";
                case ItemType.Meteorite: return area=="水晶山峰" || area=="泪水之城";
                case ItemType.MythrilOre: case ItemType.OrichalcumOre:
                    return area=="水晶山峰" || area=="泪水之城" || area=="王国边缘" || area=="愚人斗兽场";
                case ItemType.CobaltOre: case ItemType.PalladiumOre:
                case ItemType.AdamantiteOre: case ItemType.TitaniumOre: return area=="王国边缘" || area=="愚人斗兽场";
                case ItemType.LunarOre: return boss && (scene.Contains("Final") || scene.StartsWith("GG_"));
                case ItemType.SoulofMight: case ItemType.SoulofSight: case ItemType.SoulofFright: case ItemType.HallowedBar:
                case ItemType.FragmentSolar: case ItemType.FragmentVortex: case ItemType.FragmentNebula: case ItemType.FragmentStardust:
                    return boss;
                case ItemType.Ectoplasm: case ItemType.BeetleHusk: case ItemType.SpookyWood:
                    return scene.StartsWith("Dream_") || scene.StartsWith("GG_") || scene.StartsWith("Grimm");
                case ItemType.CrystalShard: return area=="水晶山峰";
                case ItemType.SoulofLight: return area=="雾之峡谷" || area=="水晶山峰";
                case ItemType.SoulofNight: return area=="深渊" || area=="远古盆地" || area=="深巢" || area=="野兽巢穴";
                case ItemType.SpiderFang: return area=="深巢" || area=="野兽巢穴";
                default:return true;
            }
        }
        static void AddRegionalMaterials(string scene,int stage,bool entry,bool final,bool boss,Dictionary<ItemType,float> ore,Dictionary<ItemType,float> mat)
        {
            string area=World.Areas.AreaName(scene);
            bool jungle=area=="苍绿之径" || area=="真菌荒地" || area=="王后花园";
            void Add(Dictionary<ItemType,float> pool,ItemType t,float amount)
            { if(ProgressionBalance.Unlocked(t,stage,entry,final)) pool[t]=Math.Max(Value(pool,t),amount); }
            if(jungle)
            {
                Add(mat,ItemType.JungleSpores,.8f); Add(mat,ItemType.Vine,.6f);
                Add(mat,ItemType.MudBlock,.6f);
                if(stage>=1) Add(mat,ItemType.Stinger,.6f);
                if(stage>=4 && entry) Add(ore,ItemType.ChlorophyteOre,.5625f);
            }
            if(ore.Count==0 && (area=="国王山道" || area=="遗忘十字路" || area=="呼啸悬崖"))
            {
                Add(ore,ItemType.CopperOre,1.8f);Add(ore,ItemType.TinOre,1.8f);
                Add(ore,ItemType.IronOre,1.3f);Add(ore,ItemType.LeadOre,1.3f);Add(ore,ItemType.SilverOre,1f);
            }
            if(area=="真菌荒地" || area=="呼啸悬崖" || area=="水晶山峰")
            {
                if(ore.Count==0) {Add(ore,ItemType.GoldOre,1.8f);Add(ore,ItemType.PlatinumOre,1.8f);Add(ore,ItemType.TungstenOre,1.8f);}
                Add(mat,ItemType.SturdyFossil,.6f);Add(mat,ItemType.FlinxFur,.6f);
                Add(mat,ItemType.Amber,.3f);
            }
            if(area=="真菌荒地") {Add(ore,ItemType.DemoniteOre,.9f);Add(ore,ItemType.CrimtaneOre,.9f);}
            if(area=="泪水之城" || area=="皇家水道")
            {
                Add(ore,ItemType.DemoniteOre,1.5f);Add(ore,ItemType.CrimtaneOre,1.5f);
                Add(mat,ItemType.ShadowScale,.6f);Add(mat,ItemType.TissueSample,.6f);Add(mat,ItemType.Bone,1.5f);
            }
            if(area=="水晶山峰") { Add(ore,ItemType.Meteorite,1.5f);Add(mat,ItemType.Amethyst,.8f);Add(mat,ItemType.Ruby,.6f);Add(mat,ItemType.Diamond,.5f);Add(mat,ItemType.CrystalShard,.8f); }
            if(area=="水晶山峰") Add(ore,ItemType.Hellstone,2f);
            if(area=="水晶山峰" || area=="泪水之城") {Add(ore,ItemType.MythrilOre,1.2f);Add(ore,ItemType.OrichalcumOre,1.2f);}
            if(area=="皇家水道" || area=="水晶山峰") Add(mat,ItemType.Obsidian,.6f);
            if(area=="深渊" || area=="远古盆地") { Add(ore,ItemType.Hellstone,2);Add(mat,ItemType.Obsidian,1);Add(mat,ItemType.SoulofNight,.4f); }
            if(area=="深巢" || area=="野兽巢穴") { Add(mat,ItemType.SpiderFang,.8f);Add(mat,ItemType.Cobweb,2); }
            if(area=="蜂巢") Add(mat,ItemType.BeeWax,boss ? 12.5f : 1.2f);
            if(area=="王国边缘" || area=="愚人斗兽场")
            {
                Add(ore,ItemType.CobaltOre,1.5f);Add(ore,ItemType.PalladiumOre,1.5f);
                if(entry) { Add(ore,ItemType.MythrilOre,1.2f);Add(ore,ItemType.OrichalcumOre,1.2f);Add(ore,ItemType.AdamantiteOre,.8f);Add(ore,ItemType.TitaniumOre,.8f); }
            }
            if(area=="雾之峡谷") Add(mat,ItemType.SoulofLight,.4f);
            if (!boss && stage>=5 && (scene.StartsWith("Dream_") || scene.StartsWith("GG_") || scene.StartsWith("Grimm")))
            {
                Add(mat,ItemType.Ectoplasm,.8f);
                Add(mat,ItemType.SpookyWood,3);
            }
            if(boss && stage>=4)
            {
                Add(mat,ItemType.SoulofMight,2);Add(mat,ItemType.SoulofSight,2);Add(mat,ItemType.SoulofFright,2);Add(mat,ItemType.HallowedBar,3);
            }
            if(boss && stage>=5)
            {
                Add(mat,ItemType.Ectoplasm,4);Add(mat,ItemType.SpookyWood,6);Add(mat,ItemType.BeetleHusk,4);
                Add(mat,ItemType.FragmentSolar,3);Add(mat,ItemType.FragmentVortex,3);Add(mat,ItemType.FragmentNebula,3);Add(mat,ItemType.FragmentStardust,3);
                if(final && RegionAllows(ItemType.LunarOre,scene,boss)) Add(ore,ItemType.LunarOre,60);
            }
        }
        internal static float Weight(EnemyDrops.Drop d) => d.Weight>0 ? d.Weight : 1;

        static ItemType RewardWeapon(int stage, int cls, ItemType preferred, bool entry, bool final)
        {
            if (ProgressionBalance.Unlocked(preferred,stage,entry,final)) return preferred;
            for (int s=stage;s>=0;s--)
                for (int i=ProgressionBalance.Core[s][cls].Length-1;i>=0;i--)
                {
                    var t=ProgressionBalance.Core[s][cls][i];
                    if (ProgressionBalance.Unlocked(t,stage,entry,final)) return t;
                }
            return ProgressionBalance.Core[0][cls][0];
        }
    }
}
