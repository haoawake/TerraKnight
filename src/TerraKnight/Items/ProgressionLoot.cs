using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Converts the legacy table into regional materials and bounded, class-balanced equipment rolls.
    /// The original ore conversion happens once, before these tables are read; counts here are final ore.
    /// </summary>
    internal static partial class ProgressionLoot
    {
        const int Main = 100, Function = 101, Rare = 102, Essential = 103, Ore = 200, Material = 201, Building = 202, Critter = 204;
        // A boss's second piece of equipment, and its second ore and material picks (the user's, 2026-10-20).
        const int Main2 = 104, Ore2 = 208, Material2 = 209;
        static readonly Dictionary<string, EnemyDrops.Drop[]> cache = new Dictionary<string, EnemyDrops.Drop[]>();
        static readonly Dictionary<string,List<ItemType>> catalogues = new Dictionary<string,List<ItemType>>();
        static readonly Dictionary<string,Dictionary<string,List<ItemType>>> sources = new Dictionary<string,Dictionary<string,List<ItemType>>>();
        static HashSet<ItemType> legacyItems;
        static Dictionary<string,int> ordinals;
        static readonly Dictionary<(int, bool), SortedDictionary<string,List<string>>> sourceRosters =
            new Dictionary<(int, bool), SortedDictionary<string,List<string>>>();

        static SortedDictionary<string,List<string>> SourceRoster(int stage,bool main)
        {
            if(sourceRosters.TryGetValue((stage,main),out var areas))return areas;
            areas=new SortedDictionary<string,List<string>>(StringComparer.Ordinal);
            foreach(var rec in ordinals.Keys) {
                if(NamedElites.Contains(rec))continue;
                if(NativeTier(rec)>stage||World.Areas.LevelOf(EnemyDrops.SceneOf(rec))>stage||MainRoute(rec)!=main)continue;
                string area=BestiaryRoute.AreaOf(rec);
                if(stage>Math.Min(5,AreaBand(area)+1))continue;
                if(!areas.TryGetValue(area,out var members))areas[area]=members=new List<string>();
                members.Add(rec);
            }
            foreach(var members in areas.Values)members.Sort(StringComparer.Ordinal);
            sourceRosters[(stage,main)]=areas;return areas;
        }
        internal static readonly HashSet<string> NamedElites = new HashSet<string> { "BigBuzzer","MageKnight","PaleLurker" };
        static readonly HashSet<string> Elites = new HashSet<string> {
            "ZombieGuard","BigBee","MushroomBrawler","RoyalGuard","FatFluke","MawlekTurret","FlameBearerLarge","FlameBearerMed",
            "GiantHopper","GorgeousHusk","GreatShieldZombie","HeavyMantis","SlashSpider"
        };
        internal static bool Eligible(EnemyDrops.Entry e) => e.Hp > 0 && e.Kind != "invulnerable" && e.Kind != "passive" && e.Kind != "non_enemy" && e.Kind != "player_shade";
        internal static bool Elite(string key, EnemyDrops.Entry e) => NamedElites.Contains(key) || e.Kind == "miniboss" || Elites.Contains(key);
        internal static bool Spawned(string key) => key == "Flukefly" || key == "HatcherBaby" || key == "BabyHatcher" || key == "JellyCrawler";

        internal static IReadOnlyList<EnemyDrops.Drop> Of(string key, EnemyDrops.Entry e, int stage, bool first, bool entry, bool final,string scene=null)
        {
            if (!Eligible(e) || Spawned(key)) return Array.Empty<EnemyDrops.Drop>();
            if(!string.IsNullOrEmpty(scene)){e.Scene=scene;e.AreaOverride=World.Areas.AreaName(scene);}
            bool boss = ProgressionBalance.FormalBoss(key);
            // A Godhome fight pays the player's own stage (Build); a boss elsewhere at least its own.
            bool godhomeFight = e.AreaOverride=="神居" && GodhomeScene(e.Scene);
            if (boss && !godhomeFight && ProgressionBalance.BossRewards.TryGetValue(key, out var reward))
                stage = Math.Max(stage, reward.Stage);
            string id = key + ":" + LootArea(key,e) + ":" + e.Scene + ":" + stage + ":" + first + ":" + entry + ":" + final + ":" + VanillaBossProgression.StateKey;
            if (!cache.TryGetValue(id, out var result))
            {
                result=Build(key,e,stage,first,entry,final,boss);
                if(!boss) {
                    result=RetainEarlierDrops(key,e,stage,entry,final,result);
                    result=BossDropPlan.Route(key,stage,result);
                    // These single-copy recipe inputs were diluted below 0.25%
                    // by material/retained pools. Keep their existing sources,
                    // reserving removed weight instead of inflating other drops.
                    var materialRows = new List<EnemyDrops.Drop>(result);
                    var sparse = new HashSet<ItemType>();
                    foreach (var type in new[] { ItemType.BlackLens, ItemType.BoneFeather })
                    {
                        float miss = 1f;
                        var grouped = new Dictionary<int, float>();
                        foreach (var row in result)
                        {
                            if (row.Type != type) continue;
                            float p = row.Chance;
                            if (row.Group > 0)
                            {
                                float total = 0, reserved = 0;
                                foreach (var option in result) if (option.Group == row.Group)
                                { total += Weight(option); reserved = Math.Max(reserved, option.GroupWeightTotal); }
                                p *= Weight(row) / Math.Max(.00001f, Math.Max(total, reserved));
                                grouped[row.Group] = (grouped.TryGetValue(row.Group, out float old) ? old : 0) + p;
                            }
                            else miss *= 1f - p;
                        }
                        foreach (float p in grouped.Values) miss *= 1f - p;
                        if (miss < 1f && 1f - miss < .01f) sparse.Add(type);
                    }
                    if (sparse.Count > 0)
                    {
                        ImproveSpecialties(materialRows, sparse, false);
                        result = materialRows.ToArray();
                    }
                }
                var mounted = new List<EnemyDrops.Drop>(result);
                Player.Mounts.AddDrops(mounted,key,stage,entry,final,e.Scene);
                VanityDrops.AddDrops(mounted,key,stage,entry,final,e.Scene);
                BossPets.AddDrops(mounted,key,e.Scene);
                result=mounted.ToArray();
                NormalizeGroupChances(result);
                cache[id]=result;
            }
            return result;
        }
        // Runtime rolls once using the first row. Retained rows must describe that
        // same roll; only Weight controls their share within it.
        static void NormalizeGroupChances(EnemyDrops.Drop[] rows)
        {
            var first=new Dictionary<int,EnemyDrops.Drop>();
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];
                if(row.Group<=0)continue;
                if(!first.TryGetValue(row.Group,out var roll))first[row.Group]=row;
                else {row.Num=roll.Num;row.Den=roll.Den;rows[i]=row;}
            }
        }
        // Old supplies keep a share of the same group roll, not another jackpot
        // per old stage. Current supplies keep 70%, retained candidates share 30%.
        static EnemyDrops.Drop[] RetainEarlierDrops(string key,EnemyDrops.Entry e,int stage,bool entry,bool final,EnemyDrops.Drop[] current)
        {
            var present=new HashSet<ItemType>();
            foreach(var row in current) present.Add(row.Type);
            var older=new List<EnemyDrops.Drop>();
            // Roster tiers are not the first stage an actual scene is reachable.
            // Greenpath enemies, for example, have NativeTier 1 but can supply
            // stage-0 ingredients. Starting at 1 silently erased those supplies.
            int firstStage=string.IsNullOrEmpty(e.Scene) ? NativeTier(key)
                : Math.Min(NativeTier(key),World.Areas.LevelOf(e.Scene));
            for(int s=Math.Max(0,firstStage);s<stage;s++)
                foreach(var row in Build(key,e,s,false,entry,final,false))
                    if(present.Add(row.Type)) older.Add(row);
            if(older.Count==0)return current;
            var rows=new List<EnemyDrops.Drop>(current);
            var groups=new HashSet<int>();
            foreach(var row in older) if(row.Group>0)groups.Add(row.Group);
            foreach(int group in groups)
            {
                float now=0,old=0;
                foreach(var row in current) if(row.Group==group)now+=Weight(row);
                foreach(var row in older) if(row.Group==group)old+=Weight(row);
                if(now<=0)continue;
                for(int i=0;i<rows.Count;i++)if(rows[i].Group==group){var row=rows[i];row.Weight=Weight(row)/now*.7f;rows[i]=row;}
                for(int i=0;i<older.Count;i++)if(older[i].Group==group){var row=older[i];row.Weight=Weight(row)/old*.3f;older[i]=row;}
            }
            rows.AddRange(older);
            return rows.ToArray();
        }
        static EnemyDrops.Drop Row(ItemType type, int group, float probability, int min = 1, int max = 1, float weight = 1)
            => new EnemyDrops.Drop { Type=type,Group=group,Num=Mathf.RoundToInt(probability*100000),Den=100000,Min=min,Max=max,LockStage=0,Weight=weight };

        /// <summary>
        /// An enemy's own row that stays outside the equipment pools, as its copy (ungrouped, unlocked): Master relics, hearts,
        /// healing, life and mana crystals, permanent upgrades (the Demon Heart on a first win only), tools and stations (at
        /// most 1 in 20), and single non-combat items, whose original roll is kept rather than pooled into a stack (which could
        /// otherwise give dozens of hooks). False when it isn't one, or isn't unlocked at the stage.
        /// </summary>
        internal static bool KeptOutsidePools(EnemyDrops.Drop d, int stage, bool first, bool entry, bool final, out EnemyDrops.Drop copy)
        {
            var def = ItemDb.Get(d.Type);
            copy = d;
            // A Master relic places too, but drops every time (MasterModeCommonDrop), not as a tool.
            bool utility=!d.Master && (def.PickPower>0 || def.AxePower>0 || def.HammerPower>0 || def.PlacesStation!=World.StationKind.None);
            bool single=def.MaxStack==1 && !def.IsWeapon && !def.IsArmor && !def.IsAccessory;
            if (!(d.Master || d.Type == ItemType.Heart || def.HealLife > 0 || def.HealMana > 0 || def.LifeCrystal || def.ManaCrystal || def.Boost != PermanentBoost.None || utility || single))
                return false;
            copy.LockStage=0;
            copy.Group=0;
            if(def.MaxStack==1 || def.LifeCrystal || def.ManaCrystal || def.Boost!=PermanentBoost.None)
            {copy.Min=copy.Max=1;copy.MeanCount=0;}
            if(utility && copy.Chance>.05f) {copy.Num=1;copy.Den=20;}
            if(d.Type==ItemType.DemonHeart && !first) return false;
            return d.Master || d.Type == ItemType.Heart || ProgressionBalance.Unlocked(d.Type,stage,entry,final);
        }

        static EnemyDrops.Drop[] Build(string key, EnemyDrops.Entry e, int stage, bool first, bool entry, bool final, bool boss)
        {
            var rows = new List<EnemyDrops.Drop>();
            var specialties = new HashSet<ItemType>();
            if (key == "Zote") return new[] {Row(ItemType.Wood,Building,1,2,4)};
            bool elite = Elite(key,e);
            string area=LootArea(key,e);
            bool stocked=MainArea(area) || RegionalProfiles.ContainsKey(area);
            // Godhome's fights pay by the player's stage (the user's), whatever the boss's own: endgame loot only once at
            // stage 5; GodhomeRewards adds its difficulty's bonus at the kill.
            bool godhome=e.AreaOverride=="神居" && GodhomeScene(e.Scene);
            if(boss && VanillaBossProgression.All.ContainsKey(key))return BossDropPlan.Drops(key,e,stage,godhome,first,entry,final);
            if(godhome)stage=Mathf.Clamp(stage,0,5);
            bool lunar=GodhomeRewards.LunarFight(key, godhome, stage);
            int tier = godhome ? stage : Mathf.Clamp(Math.Min(stage,AreaBand(area)+1),0,5);
            if (ProgressionBalance.BossRewards.TryGetValue(key,out var reward) && (boss || NamedElites.Contains(key)))
            {
                tier = godhome ? stage : reward.Stage;
                if(boss && !godhome) stage=reward.Stage; // An old boss cannot pay endgame resources.
            }
            // Genuine healing, master relics and first-only permanent upgrades survive outside equipment pools.
            foreach (var d in e.Drops ?? Array.Empty<EnemyDrops.Drop>())
            {
                var def = ItemDb.Get(d.Type);
                if(def.BaitPower>0)
                {
                    if(!d.Master && ProgressionBalance.Unlocked(d.Type,stage,entry,final))
                        rows.Add(Row(d.Type,Critter,elite ? .01f : .005f));
                    continue;
                }
                // Upgrading the area's equipment pool must not erase the original ingredients
                // of vanilla upgrade recipes (Mace, Compass, old accessories, armour, etc.).
                if (!d.Master && def.Source == ItemSource.Drop && Recipes.IsMaterial(d.Type)
                    && def.Stage >= NativeTier(key)
                    && ProgressionBalance.Unlocked(d.Type, stage, entry, final) && RegionAllows(d.Type, e.Scene, boss)
                    && (def.IsWeapon || def.IsArmor || def.IsAccessory))
                {
                    rows.Add(Row(d.Type, 0, Mathf.Clamp(d.Chance, .05f, .1f), d.Min, d.Max));
                }
                if (KeptOutsidePools(d,stage,first,entry,final,out var copy)) rows.Add(copy);
            }
            if (boss || (NamedElites.Contains(key) && first))
            {
                // Only natural, own-stage weapons. Crafted upgrades and armour must be made.
                var assigned = BossDropPlan.Weapons(key,tier);
                // A named elite outside the formal bosses (the Pale Lurker) has no BossDropPlan gear: its own reward's
                // weapons, and its accessory among them, so its first kill pays as a boss's does.
                var accessory = ItemType.None;
                if (assigned.Length == 0 && !boss && ProgressionBalance.BossRewards.TryGetValue(key, out var own))
                {
                    assigned = own.Weapons ?? Array.Empty<ItemType>();
                    accessory = own.Accessory;
                }
                int branches=0;
                for(int cls=0;cls<assigned.Length;cls++)
                    if(assigned[cls]!=ItemType.None)branches++;
                for (int cls=0;cls<4;cls++)
                {
                    var weapon = cls<assigned.Length ? assigned[cls] : ItemType.None;
                    if(weapon!=ItemType.None && !CraftOnlyLoot.OnlyCrafted(weapon)
                        && ProgressionBalance.Unlocked(weapon,stage,entry,final))
                        rows.Add(Row(weapon,Main,1,weight:1f/Math.Max(1,branches)));
                }
                if(accessory!=ItemType.None && !CraftOnlyLoot.OnlyCrafted(accessory) && ProgressionBalance.Unlocked(accessory,stage,entry,final))
                    rows.Add(Row(accessory,Main,1,weight:1f/Math.Max(1,branches)));
                // A boss's kill another piece of the same now and then: the main pick again, at 30%.
                if (boss)
                    for(int i=0,n=rows.Count;i<n;i++)if(rows[i].Group==Main) {
                        var extra=rows[i];
                        extra.Group=Main2;
                        extra.Num=Mathf.RoundToInt(extra.Den*.3f);
                        rows.Add(extra);
                    }
            }
            else
            {
                // One shared rare-equipment roll, not one independent roll per candidate. Each class's weapon, armour and
                // accessory slot is something an enemy can drop (no crafted-only, sold or boss item: CraftOnlyLoot,
                // BossDropPlan): the area's own core piece when it can be, else a droppable one of that stage (Fill).
                float chance=elite ? .24f : .10f;
                // What this kill's slots already hold: no two of its slots the same piece.
                var taken=new HashSet<ItemType>();
                for (int cls=0;cls<4;cls++)
                {
                    var weapons=new List<ItemType>();
                    if(stocked) AddPick(weapons,taken,key,area,cls,0,ProgressionBalance.Core[tier][cls],tier,stage,entry,final);
                    AddExtras(weapons,key,tier,cls,0,entry,final,specialties);
                    var armor=new List<ItemType>();
                    if(stocked) AddPick(armor,taken,key,area,cls,1,ProgressionBalance.ArmorFor(tier,cls),tier,stage,entry,final);
                    AddExtras(armor,key,tier,cls,1,entry,final,specialties);
                    foreach (var t in weapons) rows.Add(Row(t,Rare,chance,weight:.175f/Math.Max(1,weapons.Count)));
                    foreach (var t in armor) rows.Add(Row(t,Rare,chance,weight:.05f/Math.Max(1,armor.Count)));
                    var accessories=new List<ItemType>();
                    if(stocked) AddPick(accessories,taken,key,area,cls,2,new[]{ProgressionBalance.AccessoryPaths[tier][cls][1]},tier,stage,entry,final);
                    AddExtras(accessories,key,tier,cls,2,entry,final,specialties);
                    foreach(var t in accessories) rows.Add(Row(t,Rare,chance,weight:.025f/Math.Max(1,accessories.Count)));
                }
            }
            if (!boss && stocked)
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
                AddExtras(inputs,key,stage,-1,3,entry,final,specialties);
                foreach(var t in inputs) rows.Add(Row(t,Rare,elite ? .24f : .10f,weight:.025f));
            }
            var ores=new Dictionary<ItemType,float>();
            var mats=new Dictionary<ItemType,float>();
            var blocks=new Dictionary<ItemType,float>();
            foreach (var d in e.Drops ?? Array.Empty<EnemyDrops.Drop>())
            {
                var def=ItemDb.Get(d.Type);
                if(def.BaitPower>0)continue;
                if (d.Group>0 || d.Master || def.MaxStack==1 || def.IsAccessory || def.IsArmor || def.Boost!=PermanentBoost.None || def.HkItem ||
                    def.HealLife>0 || def.HealMana>0 || def.LifeCrystal || def.ManaCrystal || d.Type==ItemType.Heart || def.PickPower>0 || def.AxePower>0 || def.HammerPower>0 ||
                    def.GameTool || def.IsWeapon && def.MaxStack==1 || def.PlacesStation != World.StationKind.None) continue;
                if (!ProgressionBalance.Unlocked(d.Type,stage,entry,final) || !RegionAllows(d.Type,e.Scene,boss)) continue;
                float expected=d.Chance*(d.Min+d.Max)*.5f;
                var pool=ItemDb.IsOre(d.Type) ? ores : (d.Type==ItemType.Wood || d.Type==ItemType.StoneBlock) ? blocks : mats;
                pool[d.Type]=(pool.TryGetValue(d.Type,out float n) ? n : 0)+expected;
            }
            AddRegionalMaterials(e.Scene,stage,entry,final,boss,ores,mats);
            if(!boss)AddRegionalProfile(area,stage,entry,final,ores,mats);
            if(!boss && key.StartsWith("FlyingSentry",StringComparison.Ordinal))
                foreach(var t in new[]{ItemType.GiantHarpyFeather,ItemType.BrokenBatWing})
                    if(ProgressionBalance.Unlocked(t,stage,entry,final))mats[t]=Math.Max(Value(mats,t),1.5f);
            if(!boss && stocked)WeightRegionalOres(area,stage,ores);
            // Smelting inputs stay frequent in their actual biomes, in small stacks.
            // Do not turn a rarity cap into hundreds of extra kills for the first Imp Staff.
            if(!boss && ores.Remove(ItemType.Hellstone))
                rows.Add(Row(ItemType.Hellstone,205,elite ? 1f : .65f,elite ? 3 : 2,elite ? 6 : 4));
            if(!boss && mats.Remove(ItemType.Obsidian))
                rows.Add(Row(ItemType.Obsidian,206,elite ? 1f : .6f,elite ? 2 : 1,elite ? 4 : 3));
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
                // Flying city sentries remain reachable before the stage-4 border insects.
                if(!boss && key.StartsWith("FlyingSentry",StringComparison.Ordinal)) flight=true;
                void Soul(ItemType t)
                {
                    if (boss) rows.Add(Row(t,0,1,5,8));
                    else rows.Add(Row(t,0,elite ? 1 : .6f,elite ? 2 : 1,elite ? 4 : 3));
                }
                if (RegionAllows(ItemType.SoulofLight,e.Scene,boss) || !boss && ProfileHas(area,ItemType.SoulofLight)) Soul(ItemType.SoulofLight);
                if (RegionAllows(ItemType.SoulofNight,e.Scene,boss) || !boss && ProfileHas(area,ItemType.SoulofNight)) Soul(ItemType.SoulofNight);
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
                if(key=="LobsterLancer") rows.Add(Row(ItemType.BeetleHusk, 0, 1, 18, 23));
                ItemType fragment = key == "Grimm" || key == "NightmareGrimm" || key == "HollowKnightPrime" ? ItemType.FragmentSolar
                    : key == "WhiteDefender" || key == "Nailsage" ? ItemType.FragmentVortex
                    : key == "GreyPrince" || key == "Paintmaster" ? ItemType.FragmentNebula : ItemType.FragmentStardust;
                rows.Add(Row(fragment, 0, 1, 18, 26));
                // Once a save (EssentialDrops.With takes it out after the first).
                rows.Add(Row(ItemType.LunarCraftingStation,0,1f));
            }
            if (!boss && key.StartsWith("Zombie"))
            {
                mats[ItemType.Deathweed] = Math.Max(Value(mats, ItemType.Deathweed), .3f);
                mats[ItemType.Blinkroot] = Math.Max(Value(mats, ItemType.Blinkroot), .3f);
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
            if(boss && !godhome)AddVanillaBossMaterials(key,rows,ores,mats);
            Pool(rows,ores,Ore,boss || elite ? 1 : .6f,1.2f,boss ? -1 : stage,elite,boss,boss ? Ore2 : 0);
            Pool(rows,mats,Material,1f,boss ? 1.5f : 1.8f,boss ? -1 : stage,elite,boss,boss ? Material2 : 0);
            // Common construction stock belongs to early-route farming, not advanced fights.
            if (!boss && NativeTier(key)<2) Pool(rows,blocks,Building,.8f,1.2f,stage,elite,boss);
            // Resource ingredients keep their local sources; obsolete equipment never leaks
            // back in through a legacy crafting-input or first-kill reward route.
            if (!boss) rows.RemoveAll(d => {
                var def=ItemDb.Get(d.Type);
                return !d.Master && !ProgressionBalance.Unlocked(d.Type,stage,entry,final)
                    || (CombatWeapon(def) || def.IsArmor) && def.Stage < NativeTier(key)
                    || (CombatWeapon(def) || def.IsArmor) && def.Stage>Math.Min(5,AreaBand(area)+1);
            });
            // Late arena ranged upgrades need several ordinary sources after
            // limiting starter regions to their own equipment strength.
            if(!boss && stage>=5 && (key=="ColMiner" || key=="ColShield" || key=="ColWorm" || key=="ColMosquito"))
                rows.Add(Row(ItemType.SniperScope,Essential,AccessoryChance(elite)));
            if(!boss && ProgressionBalance.ExplorationSources.TryGetValue(key,out var exploration))
                foreach(var t in exploration)
                    if(ProgressionBalance.Unlocked(t,stage,entry,final))rows.Add(Row(t,Essential,AccessoryChance(elite)));
            if (!boss) ConsolidateAccessories(rows, elite);
            else ConsolidateAccessories(rows, elite, first ? 1f : .25f);
            // Each boss has its own signature, outside the shared accessory lottery.
            if(boss) {
                var signature=BossDropPlan.Signature(key);
                rows.RemoveAll(d=>d.Type==ProgressionBalance.BossSignatures[key] || d.Type==signature || ProgressionBalance.ExclusiveBossItems.Contains(d.Type));
                // In Godhome, by the player's stage like the rest: a later boss's signature waits for its stage.
                if(signature!=ItemType.None && (!godhome || ItemDb.Get(signature).Stage==stage)
                    && ProgressionBalance.Unlocked(signature,stage,entry,final)) rows.Add(Row(signature,0,1));
            } else rows.RemoveAll(d=>ProgressionBalance.ExclusiveBossItems.Contains(d.Type));
            // Godhome's lunar payout stays beside Expert bags; it cannot be lost on opening elsewhere.
            if(lunar || key=="FinalBoss" && !godhome) {
                rows.RemoveAll(d=>d.Type==ItemType.LunarOre);
                rows.Add(Row(ItemType.LunarOre,207,boss ? 1f : elite ? .8f : .4f,boss ? 70 : 1,boss ? 90 : 3));
            }
            if(boss)rows.RemoveAll(d=>ProgressionBalance.BasicBossExcluded.Contains(d.Type));
            rows.RemoveAll(d=>ProgressionBalance.ShopOnlyWeapons.Contains(d.Type));
            // Fish (and the fish weapons, the seafood) come only from fishing, never from an ordinary enemy (Items/FishingOnly.cs).
            if(!boss)rows.RemoveAll(d=>Fishing.FishOnly(d.Type));
            if(!boss)ImproveSpecialties(rows,specialties,elite);
            if(!lunar && !(key=="FinalBoss" && !godhome))rows.RemoveAll(d => d.Type==ItemType.LunarOre || GodhomeRewards.LunarEquipment(d.Type));
            if(boss)rows.RemoveAll(d => {
                var def=ItemDb.Get(d.Type);
                return (CombatWeapon(def) || def.IsArmor || def.IsAccessory)
                    && (CraftOnlyLoot.Blocked(d.Type) || def.Stage!=tier || !BossDropPlan.Owns(key,d.Type,tier));
            });
            return rows.ToArray();
        }

        internal static float AccessoryChance(bool elite) => elite ? .075f : .03f;
        internal static float AccessoryPoolChance(bool elite, int count) => count <= 0 ? 0f
            : Math.Min(elite ? .25f : .12f,Math.Max(AccessoryChance(elite),count*(elite ? .01f : .005f)));

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
                // One bounded pool roll, followed by an equal N-way choice. Larger pools
                // get a modest budget adjustment so individual exploration items aren't diluted away.
                rows.Add(Row(pair.Key,Essential,bossChance >= 0 ? bossChance : AccessoryPoolChance(elite, weights.Count),1,1,1f));
        }

        static float Value(Dictionary<ItemType,float> pool, ItemType t) => pool.TryGetValue(t,out float n) ? n : 0;

        // Catalogue specialties used to have one source and a tiny share of a large equipment
        // lottery. Give their two routes independent modest rolls, without inflating the
        // remaining candidates when their weights leave that lottery.
        static void ImproveSpecialties(List<EnemyDrops.Drop> rows,HashSet<ItemType> specialties,bool elite)
        {
            var types=new List<ItemType>();
            foreach(var row in rows)
                if(!row.Master && row.Chance>0 && specialties.Contains(row.Type) && !types.Contains(row.Type))
                    types.Add(row.Type);
            if(types.Count==0)return;
            types.Sort();
            var totals=new Dictionary<int,float>();
            var remaining=new Dictionary<int,float>();
            foreach(var row in rows)if(row.Group>0)
            {
                totals[row.Group]=(totals.TryGetValue(row.Group,out float sum) ? sum : 0)+Weight(row);
                if(!types.Contains(row.Type))remaining[row.Group]=(remaining.TryGetValue(row.Group,out float left) ? left : 0)+Weight(row);
            }
            var promoted=new List<EnemyDrops.Drop>();
            foreach(var type in types)
            {
                double miss=1;
                var grouped=new Dictionary<int,float>();
                var chances=new Dictionary<int,float>();
                foreach(var row in rows)
                {
                    if(row.Group>0)
                    {
                        if(!chances.ContainsKey(row.Group))chances[row.Group]=row.Chance;
                        if(row.Type==type)grouped[row.Group]=(grouped.TryGetValue(row.Group,out float weight) ? weight : 0)+Weight(row);
                    }
                    else if(row.Type==type)miss*=1-row.Chance;
                }
                foreach(var pair in grouped)miss*=1-chances[pair.Key]*pair.Value/totals[pair.Key];
                float chance=Mathf.Clamp01(Math.Max(elite ? .025f : .01f,(float)(1-miss)*1.25f));
                promoted.Add(Row(type,0,chance));
            }
            rows.RemoveAll(row=>types.Contains(row.Type));
            for(int i=0;i<rows.Count;i++)if(rows[i].Group>0)
            {
                var row=rows[i];
                float left=remaining.TryGetValue(row.Group,out float sum) ? sum : 0;
                row.Num=Mathf.RoundToInt(row.Chance*left/totals[row.Group]*100000);
                row.Den=100000;
                rows[i]=row;
            }
            rows.AddRange(promoted);
        }

        internal static bool CombatWeapon(ItemDef d) => d.IsWeapon && !d.IsAmmo && !d.GameTool && !d.HkItem && d.MaxStack==1
            && d.PickPower==0 && d.AxePower==0 && d.HammerPower==0 && d.FishingPower==0 && d.PlacesStation==World.StationKind.None;
        /// <summary>
        /// Items no enemy of Terraria's drops that go into the enemies' pools all the same (the user's): the Aglet, otherwise
        /// only a surface chest's or the Skeleton Merchant's at one moon phase in eight.
        /// </summary>
        static readonly ItemType[] PoolExtras = { ItemType.Aglet };
        internal static bool PoolExtra(ItemType t) => Array.IndexOf(PoolExtras, t) >= 0;

        /// <summary>
        /// What an enemy may drop at all: not crafted-only nor sold by a townsperson (CraftOnlyLoot), not a boss's own
        /// (BossDropPlan.BossOnly), not a shop-only weapon, lunar gear, a spell's weapon or a catch only fishing gives.
        /// </summary>
        internal static bool Droppable(ItemType t)
        {
            var d=ItemDb.Get(t);
            // Vanity comes from its own enemies only (VanityDrops), never an equipment pool's pick.
            return d!=null && !d.Placeholder && !d.HkItem && !d.Vanity && !CraftOnlyLoot.Blocked(t) && !BossDropPlan.BossOnly(t)
                && !ProgressionBalance.ShopOnlyWeapons.Contains(t) && !GodhomeRewards.LunarEquipment(t) && !SpellRewards.Is(t)
                && !Fishing.FishOnly(t);
        }

        /// <summary>A rare slot's item: the area's pick of the plan's (the core weapons, the armour path) that can drop, else Fill's.</summary>
        static void AddPick(List<ItemType> into,HashSet<ItemType> taken,string key,string area,int cls,int slot,IEnumerable<ItemType> plan,int tier,int stage,bool entry,bool final)
        {
            var ok=new List<ItemType>();
            foreach(var t in plan) if(t!=ItemType.None && Droppable(t) && ProgressionBalance.Unlocked(t,stage,entry,final)) ok.Add(t);
            var pick=Take(ok,taken,RegionalVariant(area,cls,Math.Max(1,ok.Count)));
            if(pick==ItemType.None) pick=Fill(taken,area,cls,slot,tier,NativeTier(key),stage,entry,final);
            if(pick==ItemType.None) return;
            taken.Add(pick);
            if(!into.Contains(pick)) into.Add(pick);
        }

        /// <summary>The first not yet taken, from the area's own place in the list round.</summary>
        static ItemType Take(List<ItemType> ok,HashSet<ItemType> taken,int from)
        {
            for(int i=0;i<ok.Count;i++)
            {
                var t=ok[(from+i)%ok.Count];
                if(!taken.Contains(t)) return t;
            }
            return ItemType.None;
        }

        // Slot 0 a weapon, 1 armour, 2 an accessory; kind 0 weapons, 1 armour, 2 accessories; class 4 is everyone's
        // (Kit.General); Back, the stages below the one tried.
        static readonly (int Kind,bool General,int Back)[][] fillOrder =
        {
            new[]{(0,false,0),(0,false,1),(2,false,0),(2,true,0),(1,true,0)},
            new[]{(1,false,0),(1,true,0),(1,false,1),(1,true,1),(2,false,0),(2,true,0),(0,false,0)},
            new[]{(2,false,0),(2,true,0),(2,false,1),(2,true,1),(1,true,0),(0,false,0)},
        };

        /// <summary>
        /// A rare slot with nothing of the plan that can drop: a droppable piece of the same class and kind of that stage
        /// or the one below, then the class's or everyone's of other kinds (each area its own of them, RegionalVariant),
        /// none this kill holds already; a stage with none of them, the stages below. Weapons and armour never below the
        /// enemy's own tier (Build drops those as obsolete): an accessory instead.
        /// </summary>
        static ItemType Fill(HashSet<ItemType> taken,string area,int cls,int slot,int tier,int native,int stage,bool entry,bool final)
        {
            for(int s=tier;s>=0;s--)
                foreach(var (kind,general,back) in fillOrder[slot])
                {
                    if(s-back<0 || kind!=2 && s-back<native) continue;
                    var ok=new List<ItemType>();
                    foreach(var t in DroppableIn(kind,general ? 4 : cls,s-back)) if(ProgressionBalance.Unlocked(t,stage,entry,final)) ok.Add(t);
                    var pick=Take(ok,taken,RegionalVariant(area,cls+4*slot,Math.Max(1,ok.Count)));
                    if(pick!=ItemType.None) return pick;
                }
            return ItemType.None;
        }

        static readonly Dictionary<(int,int,int),List<ItemType>> droppable=new Dictionary<(int,int,int),List<ItemType>>();

        /// <summary>The droppable weapons (kind 0), armour with defense (1) or accessories (2) of a class (4: everyone's) and stage.</summary>
        static List<ItemType> DroppableIn(int kind,int cls,int stage)
        {
            if(droppable.TryGetValue((kind,cls,stage),out var list)) return list;
            list=new List<ItemType>();
            foreach(var d in ItemDb.All)
            {
                if(d.Stage!=stage || !Droppable(d.Type)) continue;
                int own=kind==0 ? ProgressionBalance.ClassIndex(d.Class) : d.Kit==Kit.General ? 4 : ProgressionBalance.ClassIndex(d.Kit);
                bool match=kind==0 ? CombatWeapon(d) : kind==1 ? d.IsArmor && d.Defense>0 : d.IsAccessory;
                if(match && own==cls) list.Add(d.Type);
            }
            list.Sort();
            return droppable[(kind,cls,stage)]=list;
        }

        /// <summary>Dev (suite agletpool): an extra's way into the pools, step by step.</summary>
        internal static string DevExtraWhy(ItemType t)
        {
            var d=ItemDb.Get(t);
            var sb=new System.Text.StringBuilder($"阶段 {d.Stage} 档 {d.Tier} 套装 {d.Kit} 可掉 {Droppable(t)} 只能合成 {CraftOnlyLoot.OnlyCrafted(t)} 商店装备 {ShopLoot.Equipment(t)} 首领专属 {BossDropPlan.BossOnly(t)}；");
            for(int st=0;st<=5;st++) sb.Append($" 阶段{st}解锁={ProgressionBalance.Unlocked(t,st,false,false)}");
            sb.Append($"；legacy 里有={legacyItems!=null && legacyItems.Contains(t)}；");
            foreach(var kv in catalogues) if(kv.Value.Contains(t)) sb.Append($" 目录 {kv.Key}");
            foreach(var kv in sources) foreach(var r in kv.Value) if(r.Value.Contains(t)) sb.Append($" 来源 {kv.Key}→{r.Key}");
            return sb.ToString();
        }

        static void AddExtras(List<ItemType> pool,string record,int stage,int cls,int category,bool entry,bool final,HashSet<ItemType> specialties=null)
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
                foreach(var t in PoolExtras) legacyItems.Add(t);
            }
            string id=stage+":"+cls+":"+category+":"+entry+":"+final+":"+VanillaBossProgression.StateKey;
            if(!catalogues.TryGetValue(id,out var candidates))
            {
                candidates=new List<ItemType>();
                foreach(var t in legacyItems)
                {
                    var d=ItemDb.Get(t);
                    if(ProgressionBalance.ShopOnlyWeapons.Contains(t))continue;
                    if(GodhomeRewards.LunarEquipment(t))continue;
                    // Equipment only: what can't drop would leave its two sources' rolls empty.
                    if(category!=3 && !Droppable(t))continue;
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
                if(assigned.TryGetValue(record,out var local)) foreach(var t in local) { if(!pool.Contains(t)) pool.Add(t); specialties?.Add(t); }
                return;
            }
            sources[id]=assigned=new Dictionary<string,List<ItemType>>();
            void Assign(string rec,ItemType t)
            {
                if(!assigned.TryGetValue(rec,out var list)) assigned[rec]=list=new List<ItemType>();
                if(!list.Contains(t))list.Add(t);
            }
            // Assign every catalogue item to two stable regional sources. Main-route enemies
            // carry progression staples and recipe inputs; side routes carry optional variants.
            // Build the eligible roster for this tier so future-area enemies cannot strand gear.
            foreach (var t in candidates)
            {
                string theme=null; // Regional variety takes precedence over biome-themed item names.
                // Before the first ability there is no side-route roster: keep starter variants reachable.
                bool main=stage==0 || category==3 || (theme!=null ? MainArea(theme) : Recipes.IsMaterial(t) || StableHash(t.ToString())%4 != 0);
                int sourceStage=category==3 ? ItemDb.Get(t).Stage : stage;
                var areas=SourceRoster(sourceStage,main);
                if (areas.Count==0) continue;
                if (theme!=null && areas.TryGetValue(theme,out var themed))
                {
                    themed.Sort(StringComparer.Ordinal);
                    int pick=(int)(StableHash(t.ToString())%(uint)themed.Count);
                    Assign(themed[pick],t);
                    if(themed.Count>1)Assign(themed[(pick+1)%themed.Count],t);
                    continue;
                }
                var regionNames=new List<string>(areas.Keys);
                uint hash=StableHash(t.ToString());
                var roster=areas[regionNames[(int)(hash%(uint)regionNames.Count)]];
                roster.Sort(StringComparer.Ordinal);
                string source=roster[(int)((hash/(uint)regionNames.Count)%(uint)roster.Count)];
                Assign(source,t);
                // Keep the original route, then choose another eligible region when possible.
                // A one-region tier still gets a distinct second monster in that region.
                string region=regionNames[(int)(hash%(uint)regionNames.Count)];
                var alternatives=new List<string>();
                foreach(var pair in areas)if(pair.Key!=region)alternatives.AddRange(pair.Value);
                if(alternatives.Count==0)foreach(var rec in roster)if(rec!=source)alternatives.Add(rec);
                alternatives.Sort(StringComparer.Ordinal);
                if(alternatives.Count>0)Assign(alternatives[(int)((hash/17u)%(uint)alternatives.Count)],t);
            }
            if(assigned.TryGetValue(record,out var selected)) foreach(var t in selected) { if(!pool.Contains(t)) pool.Add(t); specialties?.Add(t); }
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

        internal static void ResourceRange(ItemType type,bool elite,bool boss,out int min,out int max)
        {
            var d=ItemDb.Get(type);
            if(d.BaitPower>0){min=max=1;return;}
            bool exceptional=type==ItemType.BlackLens || type==ItemType.BrokenHeroSword || type==ItemType.TurtleShell
                || type==ItemType.FrostCore || type==ItemType.AncientBattleArmorMaterial || type==ItemType.SpookyTwig
                || type==ItemType.ButterflyDust || type==ItemType.GiantHarpyFeather || type==ItemType.BoneFeather
                || type==ItemType.FireFeather || type==ItemType.IceFeather || type==ItemType.BrokenBatWing
                || type==ItemType.TatteredBeeWing || type==ItemType.BlackFairyDust;
            if(exceptional){min=1;max=boss ? 2 : 1;return;}
            if(ItemDb.IsOre(type))
            {
                if(d.Rare>=5){min=boss ? 4 : elite ? 2 : 1;max=boss ? 8 : elite ? 5 : 3;}
                else {min=boss ? 6 : elite ? 3 : 2;max=boss ? 12 : elite ? 6 : 4;}
                return;
            }
            if(d.Rare>=5){min=boss ? 2 : 1;max=boss ? 4 : elite ? 2 : 1;}
            else if(d.Rare>=2){min=boss ? 2 : 1;max=boss ? 6 : elite ? 3 : 2;}
            else if(d.Rare>=1){min=boss ? 3 : elite ? 2 : 1;max=boss ? 8 : elite ? 4 : 3;}
            else {min=boss ? 5 : elite ? 3 : 2;max=boss ? 12 : elite ? 7 : 5;}
        }

        /// <summary>
        /// A pool's one roll (its group) for one of its kinds, by weight. With a second group (a boss's), two rolls for two
        /// kinds: the kinds dealt in turn, heaviest first, into the two groups; a boss's amounts half as much again.
        /// </summary>
        static void Pool(List<EnemyDrops.Drop> rows,Dictionary<ItemType,float> pool,int group,float probability,float boost,int stage=-1,bool elite=false,bool boss=false,int second=0)
        {
            float total=0;
            foreach(var v in pool.Values) total+=v;
            if(total<=0) return;
            bool mixed=stage>0 && System.Linq.Enumerable.Any(pool.Keys,t=>ItemDb.Get(t).Stage==stage)
                && System.Linq.Enumerable.Any(pool.Keys,t=>ItemDb.Get(t).Stage<stage);
            float Bias(ItemType t)=>!mixed ? 1f : ItemDb.Get(t).Stage==stage ? 1.15f : .85f;
            probability=Math.Min(probability,total*boost);
            var kinds=new List<ItemType>(pool.Keys);
            bool split=second>0 && kinds.Count>=2;
            if(split) kinds.Sort((a,b)=>(pool[b]*Bias(b)).CompareTo(pool[a]*Bias(a)));
            var weighted=new float[2];
            for(int i=0;i<kinds.Count;i++) weighted[split ? i%2 : 0]+=pool[kinds[i]]*Bias(kinds[i]);
            for(int i=0;i<kinds.Count;i++)
            {
                var t=kinds[i];
                int half=split ? i%2 : 0;
                // Quantity belongs to the selected material's rarity, never the summed
                // expectation of every unrelated candidate in this pool.
                ResourceRange(t,elite,boss,out int low,out int high);
                if(boss && ItemDb.Get(t).BaitPower<=0) { low=Mathf.CeilToInt(low*1.5f); high=Mathf.CeilToInt(high*1.5f); }
                float w=weighted[half]>0 ? pool[t]*Bias(t)/weighted[half] : 0f;
                rows.Add(Row(t,half==0 ? group : second,probability,low,high,w));
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
                    return area=="王国边缘" || area=="愚人斗兽场" || area=="水晶山峰" || area=="泪水之城";
                case ItemType.AdamantiteOre: case ItemType.TitaniumOre: return area=="王国边缘" || area=="愚人斗兽场";
                case ItemType.LunarOre: return boss;
                case ItemType.SoulofMight: case ItemType.SoulofSight: case ItemType.SoulofFright: case ItemType.HallowedBar:
                case ItemType.FragmentSolar: case ItemType.FragmentVortex: case ItemType.FragmentNebula: case ItemType.FragmentStardust:
                    return boss;
                case ItemType.Ectoplasm: return area=="皇家水道" || scene.StartsWith("Dream_") || scene.StartsWith("GG_") || scene.StartsWith("Grimm");
                case ItemType.BeetleHusk: case ItemType.SpookyWood:
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
            // The stage-1 herb was present only in legacy grouped boss rows, which the
            // crafting-first drop rewrite retires. Keep its potion recipes reachable locally.
            if(!boss && (area=="呼啸悬崖" || area=="安息之地")) Add(mat,ItemType.Shiverthorn,.3f);
            if(jungle)
            {
                Add(mat,ItemType.JungleSpores,.8f); Add(mat,ItemType.Vine,.6f);
                Add(mat,ItemType.MudBlock,.6f);
                Add(mat,ItemType.Stinger,.6f);
                if(area=="苍绿之径") {Add(mat,ItemType.FlinxFur,.6f);Add(mat,ItemType.SturdyFossil,.6f);}
                if(stage>=4) Add(ore,ItemType.ChlorophyteOre,.5625f);
            }
            if(ore.Count==0 && (area=="国王山道" || area=="遗忘十字路" || area=="呼啸悬崖"))
            {
                Add(ore,ItemType.CopperOre,1.8f);Add(ore,ItemType.TinOre,1.8f);
                Add(ore,ItemType.IronOre,1.3f);Add(ore,ItemType.LeadOre,1.3f);Add(ore,ItemType.SilverOre,1f);
                Add(ore,ItemType.TungstenOre,1f);Add(ore,ItemType.GoldOre,.7f);Add(ore,ItemType.PlatinumOre,.7f);
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
                // Shadow Scales and Tissue Samples are the evil bosses' (VanillaBossProgression.BossMaterial), never a common enemy's.
                Add(mat,ItemType.Bone,1.5f);
            }
            if(area=="水晶山峰") { Add(ore,ItemType.Meteorite,1.5f);Add(mat,ItemType.Amethyst,.8f);Add(mat,ItemType.Ruby,.6f);Add(mat,ItemType.Diamond,.5f);Add(mat,ItemType.CrystalShard,.8f); }
            if(area=="水晶山峰") Add(ore,ItemType.Hellstone,2f);
            if(area=="水晶山峰" || area=="泪水之城") {
                Add(ore,ItemType.CobaltOre,1.2f);Add(ore,ItemType.PalladiumOre,1.2f);Add(ore,ItemType.MythrilOre,1.2f);Add(ore,ItemType.OrichalcumOre,1.2f);
                Add(ore,ItemType.AdamantiteOre,.8f);Add(ore,ItemType.TitaniumOre,.8f);
            }
            if(area=="皇家水道" && stage>=4) Add(mat,ItemType.Ectoplasm,.8f);
            if(area=="皇家水道" || area=="水晶山峰") Add(mat,ItemType.Obsidian,.6f);
            if(area=="深渊" || area=="远古盆地") { Add(ore,ItemType.Hellstone,2);Add(mat,ItemType.Obsidian,1);Add(mat,ItemType.SoulofNight,.4f); }
            if(area=="深巢" || area=="野兽巢穴") { Add(mat,ItemType.SpiderFang,.8f);Add(mat,ItemType.Cobweb,2); }
            // Bee Wax is the Hu's (the Queen Bee's place, stage 2): by the Hive (stage 4) the Bee set is long behind the
            // player, so the Hive's own is the Tattered Bee Wing (RegionalProfiles) for the hardmode Bee Wings.
            if(area=="王国边缘" || area=="愚人斗兽场")
            {
                Add(ore,ItemType.CobaltOre,1.5f);Add(ore,ItemType.PalladiumOre,1.5f);
                Add(ore,ItemType.MythrilOre,1.2f);Add(ore,ItemType.OrichalcumOre,1.2f);Add(ore,ItemType.AdamantiteOre,.8f);Add(ore,ItemType.TitaniumOre,.8f);
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
                if(RegionAllows(ItemType.LunarOre,scene,boss)) Add(ore,ItemType.LunarOre,60);
            }
        }
        internal static float Weight(EnemyDrops.Drop d) => d.Weight>0 ? d.Weight : 1;

        static ItemType RewardWeapon(int stage, int cls, ItemType preferred, bool entry, bool final)
        {
            if (ProgressionBalance.Unlocked(preferred,stage,entry,final) && ItemDb.Get(preferred).Stage>=stage && ItemDb.Get(preferred).Rare>0) return preferred;
            for (int s=stage;s>=0;s--)
                for (int i=ProgressionBalance.Core[s][cls].Length-1;i>=0;i--)
                {
                    var t=ProgressionBalance.Core[s][cls][i];
                    if (ProgressionBalance.Unlocked(t,stage,entry,final) && ItemDb.Get(t).Stage>=s && ItemDb.Get(t).Rare>0) return t;
                }
            return ProgressionBalance.Core[0][cls][0];
        }
    }
}
