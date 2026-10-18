using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        // This is a bounded acquisition experiment, NOT a claim that a bot explored or
        // defeated the bosses. Checkpoints after zero are conditional on the ability unlock.
        sealed class NoviceLedger
        {
            public Dictionary<ItemType,int> Stock = new Dictionary<ItemType,int>();
            public HashSet<ItemType> Stations = new HashSet<ItemType>();
            public List<string> Actions = new List<string>();
            public List<NoviceCraft> Crafts = new List<NoviceCraft>();
            public long Cash, Earned, Spent;
            public int Get(ItemType t) => Stock.TryGetValue(t,out int n) ? n : 0;
            public void Add(ItemType t,int n) { Stock[t]=Get(t)+n; }
            public NoviceLedger Copy() => new NoviceLedger {Stock=new Dictionary<ItemType,int>(Stock),Stations=new HashSet<ItemType>(Stations),Actions=new List<string>(Actions),Crafts=new List<NoviceCraft>(Crafts),Cash=Cash,Earned=Earned,Spent=Spent};
        }
        sealed class NoviceCraft
        {
            public Recipe Recipe;
            public ItemType[] Stations;
            public int Batches;
        }
        sealed class NoviceCheckpoint
        {
            public int Seed, Stage, Class, Kills, FirstClassDrop;
            public string Difficulty;
            [JsonIgnore] public ItemStack Weapon;
            public object WeaponInfo => new {Type=Weapon.Type.ToString(),Weapon.Count,Weapon.Prefix,Weapon.Forge};
            public string WeaponName => Weapon.IsEmpty ? "无" : Weapon.Def.Name;
            public object WeaponParameters => Weapon.IsEmpty ? null : new {Weapon.Def.Damage,Weapon.Def.UseTime,Weapon.Def.UseAnimation,Weapon.Def.ShootSpeed,
                Projectile=Weapon.Def.Shoot.ToString(),Weapon.Def.Channel,Weapon.Def.Mana,Ammo=Weapon.Def.UseAmmo.ToString(),Basis=ProgressionBalance.OutputOf(Weapon.Def).Basis};
            public string[] ArmorNames => Armor.Select(t=>t.ToString()).ToArray();
            public ItemType Whip;
            public ItemType Sentry;
            public ItemType Ammo;
            public string[] SummonAlternatives;
            public string[] WeaponAlternatives;
            public string[] BuffItems;
            public ItemType[] BuffStations;
            public ItemType[] CarriedBuffItems;
            public ItemType[] Armor, Accessories;
            public Dictionary<ItemType,int> Stock;
            public long Cash, Earned, Spent;
            public int ForgeAttempts, ForgeFailures;
            public string[] Actions, Regions, Records;
            public float RankedDps;
        }

        IEnumerator NoviceTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town","left1");
            var inv=mod.Inventory;
            TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);inv.Infinite=false;
            // Acquisition/UI experiments have no survival component. Damage is enabled
            // explicitly for each real boss attempt below.
            PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
            int checks=0,failures=0,stageWas=Shop.DevStage;
            var diffWas=GameDifficulty.Current;var rng=UnityEngine.Random.state;var sink=EnemyDrops.DevSink;
            var rows=new List<NoviceCheckpoint>();
            void Check(string label,bool pass) {checks++;if(!pass){failures++;Plugin.Log.LogError("[AutoTest] 新手采集 "+label+" 不通过");}}
            var specs=(IDictionary)typeof(Stations).GetField("specs",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            var stationItems=new Dictionary<string,List<ItemType>>();
            foreach(var value in specs.Values)
            {
                var t=value.GetType();var item=(ItemType)t.GetField("Item").GetValue(value);
                foreach(string name in (string[])t.GetField("Counts").GetValue(value))
                {if(!stationItems.TryGetValue(name,out var list))stationItems[name]=list=new List<ItemType>();if(item!=ItemType.None)list.Add(item);}
            }
            var npcs=(NpcDef[])typeof(TownNpcs).GetProperty("All",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            var setup=typeof(TownNpcs).GetMethod("SetupShop",BindingFlags.NonPublic|BindingFlags.Static);
            var recipes=Recipes.All.Where(r=>!r.IsBought && r.Environment==CraftEnvironment.None).GroupBy(r=>r.Result).ToDictionary(g=>g.Key,g=>g.ToArray());
            // Explicit conservative region itinerary. No GG room, late garden, Hive or
            // Kingdom's Edge in the pre-dash pool. No boss loot is granted in this survey.
            string[][] regions={new[]{"遗忘十字路","苍绿之径"},new[]{"真菌荒地"},new[]{"泪水之城","水晶山峰"},
                new[]{"皇家水道","深巢","远古盆地","古老盆地","安息之地"},new[]{"王国边缘","蜂巢","王后花园"},new[]{"深渊","黑卵神殿"}};
            try
            {
                foreach(var difficulty in new[]{DifficultyLevel.Expert,DifficultyLevel.Master})
                for(int cls=0;cls<4;cls++)for(int seed=0;seed<16;seed++)
                {
                    GameDifficulty.Current=difficulty;UnityEngine.Random.InitState(1017000+seed*97+cls*7919+(int)difficulty*100000);
                    var ledger=new NoviceLedger();
                    // TerraMod.ResetPlayer's actual starter inventory/accessories.
                    ledger.Add(ItemType.CopperShortsword,1);
                    ledger.Add(ItemType.CopperPickaxe,1);ledger.Add(ItemType.WoodenBow,1);
                    ledger.Add(ItemType.WoodenArrow,100);ledger.Add(ItemType.Torch,10);
                    ledger.Add(ItemType.ClimbingClaws,1);ledger.Add(ItemType.CloudinaBottle,1);ledger.Add(ItemType.GrapplingHook,1);
                    var activeRegions=new HashSet<string>();int firstDrop=-1,totalKills=0;
                    var recordKills=new Dictionary<string,int>();
                    ItemStack weapon=cls==0?ItemStack.Of(ItemType.CopperShortsword):cls==1?ItemStack.Of(ItemType.WoodenBow):default;
                    ItemType whip=ItemType.None,sentry=ItemType.None;
                    for(int stage=0;stage<6;stage++)
                    {
                        Shop.DevStage=stage;activeRegions.UnionWith(regions[stage]);
                        UnityEngine.Random.InitState(1017000+seed*97+cls*7919+(int)difficulty*100000+stage*313);
                        var sales=new Dictionary<ItemType,long>();
                        foreach(var d in Shop.Basics.Where(d=>d.Stage<=stage))sales[d.Type]=Shop.PriceOf(d);
                        foreach(var npc in npcs.Where(n=>n.Stage<=stage && n.Kind!=NpcKind.Visitor))
                        {setup.Invoke(null,new object[]{npc});foreach(var s in TownNpcs.ShopItems.Where(s=>!s.IsEmpty))sales[s.Type]=Shop.PriceOf(s.Def);}
                        var ammoBaseline=ItemDb.All.Where(d=>d.IsAmmo && (ledger.Get(d.Type)>0 || sales.ContainsKey(d.Type)))
                            .GroupBy(d=>d.Ammo).ToDictionary(g=>g.Key,g=>g.Min(d=>d.Damage));
                        float Output(ItemDef d)=>ProgressionBalance.WeaponDps(d,Areas.Defense(stage),ammoBaseline.TryGetValue(d.UseAmmo,out int ammoDamage)?ammoDamage:0);
                        bool Acquire(ref NoviceLedger book,ItemType type,int count,HashSet<ItemType> path,int depth=0)
                        {
                            if(book.Get(type)>=count)return true;
                            if(depth>14 || !path.Add(type))return false;
                            int need=count-book.Get(type);
                            // Try recipes before purchasing. Trials are transactional: a failed
                            // branch cannot eat materials/cash or leave free stations behind.
                            if(recipes.TryGetValue(type,out var alternatives))foreach(var recipe in alternatives)
                            {
                                var trial=book.Copy();bool ready=true;
                                if(!string.IsNullOrEmpty(recipe.Station) && recipe.Station!="徒手")
                                {
                                    ready=stationItems.TryGetValue(recipe.Station,out var choices) && choices.Any(trial.Stations.Contains);
                                    if(recipe.Station=="恶魔祭坛 / 猩红祭坛")ready=true; // fixed world altars in Town
                                    if(!ready && choices!=null)foreach(var station in choices)
                                    {
                                        var st=trial.Copy();
                                        if(Acquire(ref st,station,1,new HashSet<ItemType>(path),depth+1))
                                        {st.Add(station,-1);st.Stations.Add(station);st.Actions.Add("place:"+station);trial=st;ready=true;break;}
                                    }
                                }
                                if(!ready)continue;
                                int batches=(need+recipe.Count-1)/recipe.Count;
                                foreach(var ingredient in recipe.Ingredients)
                                {
                                    if(!Acquire(ref trial,ingredient.Type,ingredient.Count*batches,new HashSet<ItemType>(path),depth+1)){ready=false;break;}
                                    // Reserve immediately: a later ingredient's recursive recipe
                                    // must not spend material already promised to this recipe.
                                    trial.Add(ingredient.Type,-ingredient.Count*batches);
                                }
                                if(!ready)continue;
                                trial.Add(type,recipe.Count*batches);
                                // Reversible wood/wall recipes can consume some of the
                                // target stock during recursion; the final quantity must
                                // still meet the caller's complete request.
                                if(trial.Get(type)<count || trial.Stock.Any(p=>p.Value<0))continue;
                                trial.Actions.Add("craft:"+type+"*"+batches);
                                trial.Crafts.Add(new NoviceCraft {Recipe=recipe,Stations=trial.Stations.ToArray(),Batches=batches});
                                book=trial;return true;
                            }
                            if(sales.TryGetValue(type,out long price) && price>0 && book.Cash>=price*need)
                            {book.Cash-=price*need;book.Spent+=price*need;book.Add(type,need);book.Actions.Add("buy:"+type+"*"+need+"@"+price);return true;}
                            return false;
                        }
                        HashSet<ItemType> PossibleTypes()
                        {
                            // Topology is ONLY a cheap prefilter. Actual Acquire below
                            // still consumes quantities and pays every purchase. Never
                            // gate a craft by its result's estimated acquisition stage.
                            var possible=new HashSet<ItemType>(ledger.Stock.Where(p=>p.Value>0).Select(p=>p.Key));
                            possible.UnionWith(sales.Keys);possible.UnionWith(ledger.Stations);
                            bool changed;
                            do
                            {
                                changed=false;
                                foreach(var alternatives in recipes.Values)foreach(var r in alternatives)
                                {
                                    if(possible.Contains(r.Result) || !r.Ingredients.All(i=>possible.Contains(i.Type)))continue;
                                    bool station=string.IsNullOrEmpty(r.Station) || r.Station=="徒手" || r.Station=="恶魔祭坛 / 猩红祭坛"
                                        || stationItems.TryGetValue(r.Station,out var choices) && choices.Any(possible.Contains);
                                    if(station)changed|=possible.Add(r.Result);
                                }
                            }while(changed);
                            return possible;
                        }
                        double GearScore(IEnumerable<ItemType> types,bool armorSet=false)
                        {
                            var items=types.Where(t=>t!=ItemType.None).Select(ItemDb.Get).ToArray();var b=new Bonuses();
                            foreach(var d in items){b.Add(d.Bonus);b.Defense+=d.Defense;}
                            var head=items.FirstOrDefault(d=>d.IsArmor && (int)d.Armor==1);
                            var body=items.FirstOrDefault(d=>d.IsArmor && (int)d.Armor==2);
                            var legs=items.FirstOrDefault(d=>d.IsArmor && (int)d.Armor==3);
                            if(head!=null && body!=null)
                            {
                                bool robe=(head.Set==ArmorSet.Wizard || head.Set==ArmorSet.MagicHat) && body.Set==ArmorSet.Robe;
                                bool full=legs!=null && head.Set!=ArmorSet.None && body.Set==head.Set && legs.Set==head.Set;
                                if(robe || full)b.Add(Equipment.SetBonus(robe?head.Set:head.Set==ArmorSet.Beetle?body.SetBonus:
                                    head.SetBonus!=ArmorSet.None?head.SetBonus:head.Set));
                            }
                            double offense=cls==0?(1+b.MeleeDamage)*(1+b.MeleeSpeed)*(1+b.MeleeCrit+b.AllCrit)
                                :cls==1?(1+b.RangedDamage)*(1+b.RangedCrit+b.AllCrit)*(1+b.ArrowDamage)
                                :cls==2?(1+b.MagicDamage)*(1+b.MagicCrit+b.AllCrit)*(1+b.ManaCost*.5+b.ManaRegenBonus/200.0)
                                :(1+b.MinionDamage)*(1+b.MaxMinions)*(1+b.WhipFaster*.25+b.WhipRange*.15);
                            if(cls==0 && !weapon.IsEmpty && weapon.Def.Shoot.ToString().EndsWith("Yoyo"))
                                offense*=(b.YoyoGlove?2:1)*(b.CounterWeight>0?1.2:1)*(b.YoyoString?1.05:1);
                            if(cls==1)offense*=Math.Sqrt(1/Math.Max(.5,1-b.AmmoSave));
                            if(cls==2)offense*=1+b.ManaMax/300.0+(b.ManaFlower?.04:0)+(b.MagicCuffs?.06:0);
                            if(cls==3)offense*=1+(b.DD2Accessory?.1:0)+(b.SilverBracer?.08:0)+(b.MobiusStrip?.08:0);
                            double incoming=Math.Max(1,Areas.MasterDamagePerMask(stage)*2-b.Defense*.5)*Math.Max(.1,1-b.Endurance);
                            double mobility=1+Math.Min(.2,b.MoveSpeed)+Math.Min(.15,b.AccRunSpeed/40)+(b.JumpCloud?.08:0)+(b.WingSlot>0?.12:0)+(b.NoKnockback?.08:0)+(b.DashKind>0?.1:0)+(b.BlackBelt?.05:0);
                            // Planning heuristic only: actual AI trials verify output and
                            // survival. Boolean effects merge via Bonuses.Add, not per-item.
                            return offense*Math.Sqrt(220/incoming)*(1+b.LifeRegen*.025)*mobility;
                        }
                        var eligible=EnemyDrops.DevRecords.Where(r=>!EnemyDrops.IsBoss(r) && !ProgressionBalance.BossRewards.ContainsKey(r)
                            && activeRegions.Contains(Areas.AreaName(EnemyDrops.SceneOf(r))) && ProgressionLoot.NativeTier(r)<=stage
                            && EnemyDrops.KindOf(r)!="passive" && EnemyDrops.GeoOf(r)>=0).ToArray();
                        var common=eligible.Where(r=>!EnemyDrops.IsEliteRecord(r)).ToArray();
                        var elites=eligible.Where(EnemyDrops.IsEliteRecord).ToArray();
                        Check("路线有普通怪/"+stage,common.Length>0);
                        EnemyDrops.DevSink=(type,n)=>{
                            // Hearts/mana stars are immediate pickups, never banked heals.
                            if(type==ItemType.Heart || type==ItemType.Star)return;
                            ledger.Add(type,n);
                            if(firstDrop<0 && ProgressionLoot.CombatWeapon(ItemDb.Get(type)) && ProgressionBalance.ClassIndex(ItemDb.Get(type).Class)==cls)firstDrop=totalKills;
                        };
                        int actionsBefore=ledger.Actions.Count;
                        int craftsBefore=ledger.Crafts.Count;
                        for(int k=0;k<180 && common.Length>0;k++)
                        {
                            totalKills++;
                            var pool=k%10==9 && elites.Length>0 ? elites : common;
                            string record=pool[UnityEngine.Random.Range(0,pool.Length)];
                            EnemyDrops.Roll(record,Vector2.zero,true,false,scene:EnemyDrops.SceneOf(record));
                            recordKills.TryGetValue(record,out int killed);recordKills[record]=++killed;
                            var banner=Banners.Of(record);
                            if(banner!=ItemType.None && killed%Banners.KillsPerBanner==0)ledger.Add(banner,1);
                            long income=EnemyDrops.GeoOf(record)*100L;ledger.Cash+=income;ledger.Earned+=income;
                            // Check gear every 30 kills, rather than choosing a perfect build
                            // with knowledge of the final inventory.
                            if(k%30!=29)continue;
                            float Score(ItemDef d)=>Output(d);
                            float held=weapon.IsEmpty ? 0 : Score(weapon.Def);
                            var reachableNow=PossibleTypes();
                            var candidates=ItemDb.All.Where(d=>ProgressionBalance.BalancedWeapon(d) && ProgressionBalance.ClassIndex(d.Class)==cls
                                && reachableNow.Contains(d.Type) && !d.Consumable && d.MaxStack<=1 && (cls!=3 || d.IsSummon && !d.Sentry)).OrderByDescending(Score);
                            foreach(var candidate in candidates)
                            {
                                if(Score(candidate)<=held*1.08f)break;
                                var trial=ledger.Copy();long reserve=trial.Cash/3;
                                trial.Cash-=reserve;
                                if(!Acquire(ref trial,candidate.Type,1,new HashSet<ItemType>()))continue;
                                if(candidate.UseAmmo!=AmmoKind.None)
                                {
                                    // A Star Cannon without stars must not beat a working bow
                                    // in the acquisition ranking. Fund thirty seconds of fire.
                                    int supply=Mathf.Clamp(Mathf.CeilToInt(30/Mathf.Max(.05f,candidate.UseTime)),30,600);
                                    bool supplied=false;
                                    foreach(var ammo in ItemDb.All.Where(d=>d.Ammo==candidate.UseAmmo && reachableNow.Contains(d.Type)).OrderBy(d=>d.Damage))
                                    {var stocked=trial.Copy();if(Acquire(ref stocked,ammo.Type,supply,new HashSet<ItemType>())){trial=stocked;supplied=true;break;}}
                                    if(!supplied)continue;
                                }
                                trial.Cash+=reserve;ledger=trial;weapon=ItemStack.Of(candidate.Type);
                                ledger.Actions.Add("equip:"+candidate.Type+"/kill"+totalKills);break;
                            }
                        }
                        var reachableGear=PossibleTypes();
                        if(cls==3)
                        foreach(var d in ItemDb.All.Where(d=>d.IsWhip && reachableGear.Contains(d.Type)).OrderByDescending(d=>ProgressionBalance.WeaponDps(d,Areas.Defense(stage))))
                        {
                            var trial=ledger.Copy();long reserve=trial.Cash*2/3;trial.Cash-=reserve;
                            if(!Acquire(ref trial,d.Type,1,new HashSet<ItemType>()))continue;
                            trial.Cash+=reserve;ledger=trial;whip=d.Type;break;
                        }
                        if(cls==3)
                        {
                            // Keep affordable alternative AIs, not merely a single highest
                            // paper-DPS staff. Each acquisition pays its actual materials and
                            // price; these alternatives can be tried against the current boss.
                            var families=new HashSet<MinionKind>();
                            if(!weapon.IsEmpty)families.Add(weapon.Def.Minion);
                            foreach(var d in ItemDb.All.Where(d=>d.IsSummon && !d.Sentry && reachableGear.Contains(d.Type))
                                .OrderByDescending(d=>ProgressionBalance.WeaponDps(d,Areas.Defense(stage))))
                            {
                                if(families.Count>=4)break;
                                if(families.Contains(d.Minion))continue;
                                var trial=ledger.Copy();long reserve=trial.Cash*9/10;trial.Cash-=reserve;
                                if(!Acquire(ref trial,d.Type,1,new HashSet<ItemType>()))continue;
                                if(!weapon.IsEmpty && trial.Get(weapon.Type)<=0)continue;
                                trial.Cash+=reserve;ledger=trial;families.Add(d.Minion);
                                ledger.Actions.Add("summon-option:"+d.Type);
                            }
                            foreach(var d in ItemDb.All.Where(d=>d.Sentry && reachableGear.Contains(d.Type)).OrderByDescending(d=>ProgressionBalance.WeaponDps(d,Areas.Defense(stage))))
                            {
                                var trial=ledger.Copy();long reserve=trial.Cash*9/10;trial.Cash-=reserve;
                                if(!Acquire(ref trial,d.Type,1,new HashSet<ItemType>()))continue;
                                trial.Cash+=reserve;ledger=trial;sentry=d.Type;ledger.Actions.Add("sentry:"+d.Type);break;
                            }
                        }
                        // Buy/craft a modest reserve, using earned money/materials only.
                        if(!weapon.IsEmpty)
                        {
                            if(weapon.Def.UseAmmo!=AmmoKind.None)
                            {
                                var ammo=ItemDb.All.Where(d=>d.Ammo==weapon.Def.UseAmmo && reachableGear.Contains(d.Type)).OrderBy(d=>d.Damage);
                                foreach(var d in ammo){var trial=ledger.Copy();if(Acquire(ref trial,d.Type,180,new HashSet<ItemType>())){ledger=trial;break;}}
                            }
                            foreach(var t in new[]{ItemType.LesserHealingPotion,ItemType.LesserManaPotion})
                            {var trial=ledger.Copy();if(Acquire(ref trial,t,cls==2?12:3,new HashSet<ItemType>()))ledger=trial;}
                            if(cls==2 || cls==3)for(int n=1;n<=9;n++)
                            {var trial=ledger.Copy();if(!Acquire(ref trial,ItemType.ManaCrystal,n,new HashSet<ItemType>()))break;ledger=trial;}
                        }
                        ItemType selectedAmmo=weapon.IsEmpty || weapon.Def.UseAmmo==AmmoKind.None?ItemType.None:
                            ledger.Stock.Where(p=>p.Value>=30 && ItemDb.Get(p.Key).Ammo==weapon.Def.UseAmmo).OrderByDescending(p=>ItemDb.Get(p.Key).Damage)
                                .Select(p=>p.Key).FirstOrDefault();
                        // Equip actual affordable pieces independently; no assumed complete set.
                        var armor=new ItemType[3];
                        var availableArmor=ItemDb.All.Where(d=>d.IsArmor && reachableGear.Contains(d.Type)).ToArray();
                        for(int slot=0;slot<3;slot++)
                        {
                            var candidates=availableArmor.Where(d=>(int)d.Armor-1==slot).Select(d=>d.Type)
                                .OrderByDescending(t=>GearScore(armor.Where((_,i)=>i!=slot).Concat(new[]{t}))).ToArray();
                            foreach(var t in candidates){var trial=ledger.Copy();long reserve=trial.Cash/2;trial.Cash-=reserve;
                                if(!Acquire(ref trial,t,1,new HashSet<ItemType>()))continue;trial.Cash+=reserve;ledger=trial;armor[slot]=t;break;}
                        }
                        // A real obtainable full set can beat three high-defense pieces.
                        var armorSets=new List<ItemType[]>();
                        foreach(var family in availableArmor.Where(d=>d.Set!=ArmorSet.None).GroupBy(d=>d.Set))
                        {
                            var body=family.FirstOrDefault(d=>(int)d.Armor==2);var legs=family.FirstOrDefault(d=>(int)d.Armor==3);
                            if(body==null || legs==null)continue;
                            foreach(var head in family.Where(d=>(int)d.Armor==1))armorSets.Add(new[]{head.Type,body.Type,legs.Type});
                        }
                        armorSets.AddRange(ProgressionBalance.ArmorPaths.Where(p=>p.Items.All(reachableGear.Contains)).Select(p=>p.Items));
                        foreach(var set in armorSets.OrderByDescending(p=>GearScore(p,true)))
                        {
                            if(GearScore(set,true)<=GearScore(armor,true))break;
                            var trial=ledger.Copy();long reserve=trial.Cash/2;trial.Cash-=reserve;bool ready=true;
                            foreach(var t in set)if(!Acquire(ref trial,t,1,new HashSet<ItemType>())){ready=false;break;}
                            if(!ready)continue;trial.Cash+=reserve;ledger=trial;armor=set.ToArray();ledger.Actions.Add("armor-set:"+string.Join("+",armor));break;
                        }
                        var equipped=new List<ItemType>();
                        var accessoryCandidates=ItemDb.All.Where(d=>d.IsAccessory && reachableGear.Contains(d.Type)).OrderByDescending(d=>GearScore(new[]{d.Type})).ToArray();
                        int accessorySlots=difficulty==DifficultyLevel.Master?6:5;
                        for(int pass=0;equipped.Count<accessorySlots && pass<accessorySlots*3;pass++)
                        {
                            bool added=false;
                            foreach(var d in accessoryCandidates.OrderByDescending(d=>GearScore(armor.Concat(equipped).Concat(new[]{d.Type}))))
                            {
                                if(equipped.Contains(d.Type) || d.Wing>0 && equipped.Any(t=>ItemDb.Get(t).Wing>0))continue;
                                var trial=ledger.Copy();long reserve=trial.Cash*4/5;trial.Cash-=reserve;
                                if(!Acquire(ref trial,d.Type,1,new HashSet<ItemType>()))continue;
                                trial.Cash+=reserve;ledger=trial;equipped.RemoveAll(t=>ledger.Get(t)<=0);equipped.Add(d.Type);added=true;break;
                            }
                            if(!added)break;
                        }
                        var accessories=equipped.ToArray();
                        var buffs=new List<ItemType>();
                        var desiredBuffs=new HashSet<int>{PotionBuffs.Ironskin,PotionBuffs.Regeneration,PotionBuffs.Swiftness,PotionBuffs.Endurance,PotionBuffs.Wrath,PotionBuffs.Rage,PotionBuffs.Lifeforce,PotionBuffs.Thorns,PotionBuffs.Inferno};
                        if(cls==3)desiredBuffs.Remove(PotionBuffs.Rage); // minions/whips do not gain ordinary crit
                        if(cls==0)desiredBuffs.Add(PotionBuffs.Titan);
                        if(cls==1){if(!weapon.IsEmpty && weapon.Def.UseAmmo==AmmoKind.Arrow)desiredBuffs.Add(PotionBuffs.Archery);desiredBuffs.Add(PotionBuffs.AmmoReservation);}
                        if(cls==2){desiredBuffs.Add(PotionBuffs.MagicPower);desiredBuffs.Add(PotionBuffs.ManaRegeneration);}
                        if(cls==3)desiredBuffs.Add(PotionBuffs.Summoning);
                        foreach(int buff in desiredBuffs)
                        foreach(var d in ItemDb.All.Where(d=>reachableGear.Contains(d.Type) && d.BuffType==buff).OrderByDescending(d=>d.BuffTime))
                        {
                            var trial=ledger.Copy();long reserve=trial.Cash*19/20;trial.Cash-=reserve;
                            if(!Acquire(ref trial,d.Type,2,new HashSet<ItemType>()) && !Acquire(ref trial,d.Type,1,new HashSet<ItemType>()))continue;
                            trial.Cash+=reserve;ledger=trial;buffs.Add(d.Type);break;
                        }
                        if(cls==0 || cls==3)
                        {
                            bool imbued=false;
                            foreach(int buff in new[]{76,71,73,74,79})
                            {
                                foreach(var d in ItemDb.All.Where(d=>reachableGear.Contains(d.Type) && d.BuffType==buff))
                                {
                                    var trial=ledger.Copy();long reserve=trial.Cash*19/20;trial.Cash-=reserve;
                                    if(!Acquire(ref trial,d.Type,2,new HashSet<ItemType>()) && !Acquire(ref trial,d.Type,1,new HashSet<ItemType>()))continue;
                                    trial.Cash+=reserve;ledger=trial;buffs.Add(d.Type);imbued=true;break;
                                }
                                if(imbued)break;
                            }
                        }
                        var foods=new[]{PotionBuffs.ExquisitelyStuffed,PotionBuffs.PlentySatisfied,PotionBuffs.WellFed};
                        bool fed=false;
                        foreach(int buff in foods)
                        {
                            foreach(var d in ItemDb.All.Where(d=>reachableGear.Contains(d.Type) && d.BuffType==buff).OrderByDescending(d=>d.BuffTime))
                            {
                                var trial=ledger.Copy();long reserve=trial.Cash*19/20;trial.Cash-=reserve;
                                if(!Acquire(ref trial,d.Type,2,new HashSet<ItemType>()) && !Acquire(ref trial,d.Type,1,new HashSet<ItemType>()))continue;
                                trial.Cash+=reserve;ledger=trial;buffs.Add(d.Type);fed=true;break;
                            }
                            if(fed)break;
                        }
                        var buffStations=new List<ItemType>();
                        foreach(var t in new[]{cls==0?ItemType.SharpeningStation:cls==1?ItemType.AmmoBox:cls==2?ItemType.CrystalBall:ItemType.BewitchingTable,
                            ItemType.SliceOfCake}.Concat(sentry!=ItemType.None?new[]{ItemType.WarTable}:Array.Empty<ItemType>()))
                        {
                            if(ledger.Stations.Contains(t)){buffStations.Add(t);continue;}
                            var trial=ledger.Copy();long reserve=trial.Cash*9/10;trial.Cash-=reserve;
                            if(!Acquire(ref trial,t,1,new HashSet<ItemType>()))continue;
                            trial.Cash+=reserve;trial.Add(t,-1);trial.Stations.Add(t);trial.Actions.Add("place-buff:"+t);ledger=trial;buffStations.Add(t);
                        }
                        var carriedBuffItems=new List<ItemType>();
                        foreach(var t in new[]{ItemType.Campfire,ItemType.HeartLantern,ItemType.StarinaBottle,ItemType.CatBast,ItemType.Sunflower,ItemType.GardenGnome})
                        {
                            var trial=ledger.Copy();long reserve=trial.Cash*19/20;trial.Cash-=reserve;
                            if(!Acquire(ref trial,t,1,new HashSet<ItemType>()))continue;
                            trial.Cash+=reserve;ledger=trial;carriedBuffItems.Add(t);
                        }
                        // Real forging RNG and wallet deduction; only a quarter of remaining
                        // funds may go into upgrades. Never force a success or best prefix.
                        int attempts=0,forgeFailures=0;
                        if(!weapon.IsEmpty)
                        {
                            Array.Clear(inv.Slots,0,inv.Slots.Length);Array.Clear(inv.Bank,0,inv.Bank.Length);Shop.SpareCopper=0;
                            Shop.Earn(ledger.Cash);long budget=ledger.Cash/4,start=Shop.Purse;
                            Forging.DevOutcome=null;
                            while(attempts<6 && Forging.Cost(weapon)<=budget && weapon.Forge<Forging.MaxLevel)
                            {
                                long cost=Forging.Cost(weapon);var result=Forging.Try(ref weapon);attempts++;budget-=cost;
                                if(result==Forging.Result.Failed)forgeFailures++;
                            }
                            ledger.Cash=Shop.Purse;ledger.Spent+=start-Shop.Purse;
                            if(attempts>0)ledger.Actions.Add("forge:"+weapon.Type+"/attempts"+attempts+"/failed"+forgeFailures+"/level"+weapon.Forge);
                        }
                        // Replay seed zero's chosen recipe paths through the actual crafting
                        // panel. Only the exact paid/rolled ingredients and owned stations are
                        // transferred from abstract town storage; test one batch per operation.
                        if(seed==0)
                        {
                            foreach(var craft in ledger.Crafts.Skip(craftsBefore))
                            {
                                Stations.Clear();var region=TerrainSystem.Current.Region;
                                Vector2 local=(Vector2)HeroController.instance.transform.position-(Vector2)region.Root.position;
                                foreach(DictionaryEntry entry in specs)
                                {
                                    var value=entry.Value;var item=(ItemType)value.GetType().GetField("Item").GetValue(value);
                                    if(craft.Stations.Contains(item))Stations.PlaceFixed((StationKind)entry.Key,region.SceneName,local+Vector2.right);
                                }
                                if(craft.Recipe.Station=="恶魔祭坛 / 猩红祭坛")Stations.PlaceFixed(StationKind.DemonAltar,region.SceneName,local+Vector2.right);
                                yield return null;
                                Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;inv.Override=-1;
                                foreach(var ingredient in craft.Recipe.Ingredients)inv.Add(ingredient.Type,ingredient.Count);
                                InventoryGui.Open=true;
                                bool made=InventoryGui.DevCraft(craft.Recipe.Result);
                                Check("实际制作/"+craft.Recipe.Result,made && inv.Mouse.Type==craft.Recipe.Result && inv.Mouse.Count==craft.Recipe.Count
                                    && craft.Recipe.Ingredients.All(i=>Recipes.Count(inv,i.Type)==0));
                                inv.Mouse=default;InventoryGui.Open=false;
                            }
                        }
                        Check("现金守恒",ledger.Earned-ledger.Spent==ledger.Cash && ledger.Cash>=0);
                        Check("库存非负",ledger.Stock.All(p=>p.Value>=0));
                        Check("无首领奖励注入",!ledger.Stock.ContainsKey(ItemType.ShieldOfCthulhu));
                        rows.Add(new NoviceCheckpoint {Seed=seed,Stage=stage,Class=cls,Difficulty=difficulty.ToString(),Kills=totalKills,FirstClassDrop=firstDrop,
                            Weapon=weapon,Whip=whip,Sentry=sentry,Ammo=selectedAmmo,BuffItems=buffs.Select(t=>t.ToString()).ToArray(),BuffStations=buffStations.ToArray(),CarriedBuffItems=carriedBuffItems.ToArray(),
                            SummonAlternatives=ledger.Stock.Where(p=>p.Value>0 && ItemDb.Get(p.Key).IsSummon && !ItemDb.Get(p.Key).Sentry).Select(p=>p.Key.ToString()).ToArray(),
                            WeaponAlternatives=ledger.Stock.Where(p=>p.Value>0 && ProgressionBalance.BalancedWeapon(ItemDb.Get(p.Key)) && ProgressionBalance.ClassIndex(ItemDb.Get(p.Key).Class)==cls
                                && !ItemDb.Get(p.Key).Consumable).Select(p=>p.Key.ToString()).ToArray(),
                            Armor=armor.ToArray(),Accessories=accessories,Stock=new Dictionary<ItemType,int>(ledger.Stock),Cash=ledger.Cash,Earned=ledger.Earned,Spent=ledger.Spent,
                            ForgeAttempts=attempts,ForgeFailures=forgeFailures,Actions=ledger.Actions.Skip(actionsBefore).ToArray(),Regions=activeRegions.ToArray(),Records=eligible,
                            RankedDps=weapon.IsEmpty?0:ProgressionBalance.WeaponDps(weapon.Def,Areas.Defense(stage),selectedAmmo==ItemType.None?0:ItemDb.Get(selectedAmmo).Damage)});
                        if(seed==0)Plugin.Log.LogInfo($"[AutoTest] 新手采集 {difficulty}/{cls}/{stage} {weapon.Type} +{weapon.Forge} money={ledger.Cash/100.0:F1} drops={firstDrop}");
                        yield return null;
                    }
                }
            }
            finally {Shop.DevStage=stageWas;GameDifficulty.Current=diffWas;UnityEngine.Random.state=rng;EnemyDrops.DevSink=sink;}
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"novice-acquisition.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,
                ConditionalCheckpoints=true,PhysicalExploration=false,BossRewardsGranted=false,UnlimitedTownStorage=true,Mining=false,Traveler=false,NaturalPrefixes=false,
                KillsPerCheckpoint=180,SeedsPerClass=16,Rows=rows},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 新手采集 汇总 {checks}项 失败{failures} profiles={rows.Count} {(failures==0?"通过":"不通过")}");
            if(suite=="noviceacquire")yield break;
            yield return NoviceBossFights(rows.Where(r=>r.Seed==0 && (suite=="noviceall" || r.Stage==0)).ToArray());
        }

        static string NoviceAcquisitionSource(string artifacts)=>Directory.GetDirectories(artifacts,"resume-noviceall-*")
            .Concat(Directory.GetDirectories(artifacts,"resume-noviceacquire-*"))
            .Select(s=>Path.Combine(s,"saves","novice-acquisition.json")).Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc).First();

        IEnumerator NoviceCampaignReplay()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            var artifacts=Directory.GetParent(Directory.GetParent(DevCopy.SaveDir).FullName).FullName;
            string source=NoviceAcquisitionSource(artifacts);
            var data=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(source));
            if((int)data["Failures"]!=0 || data["Rows"].Count()!=768)throw new InvalidOperationException("Acquisition component did not pass");
            File.Copy(source,Path.Combine(DevCopy.SaveDir,"novice-acquisition.json"));
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"novice-acquisition-origin.txt"),source+"\nPassed acquisition ledger copied unchanged for independent scene fixtures and controller retakes.");
            HashSet<string> retakes=null;
            if(suite=="noviceretake" || suite=="novicephase" || suite=="noviceinteract")
            {
                string priorSource=Directory.GetDirectories(artifacts,"resume-noviceall-*").OrderByDescending(s=>s)
                    .Select(s=>Path.Combine(s,"saves","novice-bosses.json")).First(File.Exists);
                var prior=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(priorSource));
                var stationBuffs=new Dictionary<string,int>{{"SharpeningStation",PotionBuffs.Sharpened},{"AmmoBox",PotionBuffs.AmmoBox},
                    {"CrystalBall",PotionBuffs.Clairvoyance},{"BewitchingTable",PotionBuffs.Bewitched},{"SliceOfCake",PotionBuffs.SugarRush},{"WarTable",PotionBuffs.Strategist}};
                bool MissingStationBuff(Newtonsoft.Json.Linq.JToken r)=>r["BuffStations"].Values<string>().Any(s=>stationBuffs.TryGetValue(s,out int buff)
                    && !r["StartBuffIds"].Values<int>().Contains(buff));
                var selected=prior["Results"].Where(r=>(int)r["Class"]==0 || (int)r["GearStage"]==2 || !(bool)r["Awake"] || !(bool)r["Seen"]
                    || (int)r["InitialNativeHp"]==(int)r["MinimumNativeHp"] || MissingStationBuff(r)).ToArray();
                if(suite=="noviceinteract")selected=selected.Where(r=>(int)r["GearStage"]==2).ToArray();
                retakes=new HashSet<string>(selected.Select(r=>$"{r["Difficulty"]}/{r["Class"]}/{r["GearStage"]}"));
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"retake-selection.json"),JsonConvert.SerializeObject(selected,Formatting.Indented));
            }
            var profiles=new List<NoviceCheckpoint>();
            foreach(var row in data["Rows"].Where(r=>(int)r["Seed"]==0))
            {
                if(retakes!=null && !retakes.Contains($"{row["Difficulty"]}/{row["Class"]}/{row["Stage"]}"))continue;
                var clean=(Newtonsoft.Json.Linq.JObject)row.DeepClone();
                foreach(string key in new[]{"WeaponInfo","WeaponName","WeaponParameters","ArmorNames"})clean.Remove(key);
                var profile=clean.ToObject<NoviceCheckpoint>();var weapon=row["WeaponInfo"];
                profile.Weapon=new ItemStack {Type=(ItemType)Enum.Parse(typeof(ItemType),(string)weapon["Type"]),Count=(int)weapon["Count"],Prefix=(byte)weapon["Prefix"],Forge=(byte)weapon["Forge"]};
                profiles.Add(profile);
            }
            TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);mod.Inventory.Infinite=false;
            yield return NoviceBossFights(profiles.ToArray());
        }

        IEnumerator NoviceBossFights(NoviceCheckpoint[] profiles)
        {
            var results=new List<object>();var crystalEvidence=new List<object>();
            var inv=mod.Inventory;var pd=PlayerData.instance;
            TerraKnight.Player.TerraStats.DevImmortal=false;pd.isInvincible=true;mod.DevNoFallDamage=true;
            // Visit real generated early crystals and mine them with the starter pick.
            // Travel/positioning here is assisted; the crystal placement, mining and pickup
            // are real. Six collected crystals define an exploration-equipped fixture.
            int collected=0;
            foreach(var crystal in LifeCrystals.Save().Where(c=>!c.Taken && Areas.LevelOf(c.Scene)==0).Take(10).ToArray())
            {
                if(collected>=6)break;
                yield return GoTo(crystal.Scene,EntryGateOf(crystal.Scene));
                yield return new WaitForSeconds(.4f);
                var lying=LifeCrystals.Standing().Where(c=>c.Scene==crystal.Scene).ToArray();if(lying.Length==0)continue;
                var region=TerrainSystem.Find(crystal.Scene);if(region==null)continue;
                Vector2 at=(Vector2)region.Root.position+lying[0].Local+Vector2.up*.5f;
                var hero=HeroController.instance;hero.transform.position=Clear(hero,at);
                Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Slots[0]=ItemStack.Of(ItemType.CopperPickaxe);inv.Selected=0;
                int brokenBefore=LifeCrystals.DevBroken;
                for(int n=0;n<5 && LifeCrystals.DevBroken==brokenBefore;n++){while(!mod.DevReady)yield return null;mod.DevUseSelected(at,false,true);yield return null;}
                yield return new WaitForSeconds(.7f);
                int found=Recipes.Count(inv,ItemType.LifeCrystal);collected+=found;
                crystalEvidence.Add(new {crystal.Scene,PickedUp=found});
            }
            collected=Math.Min(6,collected);
            try
            {
                foreach(var profile in profiles)
                {
                    string[] bossKeys={"Hornet","MegaMossCharger","MageLord","InfectedKnight","Hornet@4","TraitorLord"};
                    string[] bossRooms={"Fungus1_04","Fungus1_29","Ruins1_24","Abyss_19","Deepnest_East_Hornet","Fungus3_23"};
                    string bossKey=bossKeys[profile.Stage],record=bossKey.Split('@')[0],room=bossRooms[profile.Stage];
                    GameDifficulty.Current=(DifficultyLevel)Enum.Parse(typeof(DifficultyLevel),profile.Difficulty);Shop.DevStage=profile.Stage;
                    // Wait for a genuine death/respawn to finish before preparing a new fixture.
                    float until=Time.realtimeSinceStartup+35;
                    while((HeroController.instance.cState.dead || !GameHooks.PlayerHasControl()) && Time.realtimeSinceStartup<until)yield return null;
                    pd.isInvincible=true;mod.DevNoFallDamage=true;
                    yield return GoTo("Town","left1");
                    Array.Clear(inv.Slots,0,inv.Slots.Length);Array.Clear(inv.Armor,0,inv.Armor.Length);Array.Clear(inv.Accessories,0,inv.Accessories.Length);
                    Array.Clear(inv.Bank,0,inv.Bank.Length);inv.Mouse=default;inv.Override=-1;inv.UseMouse=false;inv.Infinite=false;
                    for(int i=0;i<3;i++)if(profile.Armor[i]!=ItemType.None)inv.Armor[i]=ItemStack.Of(profile.Armor[i]);
                    for(int i=0;i<profile.Accessories.Length;i++)inv.Accessories[i]=ItemStack.Of(profile.Accessories[i]);
                    inv.Slots[0]=profile.Weapon;inv.Selected=0;
                    if(profile.Whip!=ItemType.None)inv.Slots[1]=ItemStack.Of(profile.Whip);
                    if(profile.Sentry!=ItemType.None)inv.Add(profile.Sentry,1);
                    foreach(var type in profile.CarriedBuffItems)inv.Add(type,1);
                    var needed=profile.Stock.Where(p=>p.Value>0 && (ItemDb.Get(p.Key).IsAmmo || ItemDb.Get(p.Key).HealLife>0 || ItemDb.Get(p.Key).HealMana>0 || p.Key==ItemType.ManaCrystal));
                    foreach(var entry in needed)inv.Add(entry.Key,entry.Value);
                    if(profile.Ammo!=ItemType.None)
                    {
                        int ammoSlot=Array.FindIndex(inv.Slots,s=>!s.IsEmpty && s.Type==profile.Ammo);
                        if(ammoSlot>=0){var prior=inv.Slots[Inventory.Size];inv.Slots[Inventory.Size]=inv.Slots[ammoSlot];inv.Slots[ammoSlot]=prior;}
                    }
                    foreach(var type in profile.SummonAlternatives.Select(s=>(ItemType)Enum.Parse(typeof(ItemType),s)))
                        if(type!=profile.Weapon.Type && profile.Stock.TryGetValue(type,out int owned) && owned>0)inv.Add(type,1);
                    string Pattern(ItemDef d)=>d.IsSummon?"minion/"+d.Minion:d.Shoot.ToString().EndsWith("Yoyo")?"yoyo":d.Shoot.ToString().EndsWith("Flail")?"flail":(d.Fx==MeleeFx.Shortsword || d.Type.ToString().EndsWith("Shortsword"))?"shortsword":d.UseAmmo+"/"+d.Shoot;
                    var availableWeapons=new[]{profile.Weapon.Type}.Concat((profile.Class==3?profile.SummonAlternatives:profile.WeaponAlternatives)
                            .Select(s=>(ItemType)Enum.Parse(typeof(ItemType),s)).Where(t=>t!=profile.Weapon.Type)
                            .OrderByDescending(t=>ProgressionBalance.WeaponDps(ItemDb.Get(t),Areas.Defense(profile.Stage))))
                        .Distinct().GroupBy(t=>Pattern(ItemDb.Get(t))).Select(g=>g.First()).Take(4).ToArray();
                    foreach(var type in availableWeapons)
                        if(type!=profile.Weapon.Type && !Array.Exists(inv.Slots,s=>!s.IsEmpty && s.Type==type) && profile.Stock.TryGetValue(type,out int owned) && owned>0)inv.Add(type,1);
                    foreach(var type in profile.BuffItems.Select(s=>(ItemType)Enum.Parse(typeof(ItemType),s)))
                        if(profile.Stock.TryGetValue(type,out int owned) && owned>0)inv.Add(type,owned);
                    var suppliesBefore=inv.Slots.Where(s=>!s.IsEmpty && (s.Def.HealLife>0 || s.Def.HealMana>0 || s.Def.BuffType>0))
                        .GroupBy(s=>s.Type).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Count));
                    // Persistent increases are consumed through the real item-use path.
                    mod.Stats.LifeCrystals=0;mod.Stats.ManaCrystals=0;mod.Stats.LifeFruits=0;
                    inv.Add(ItemType.LifeCrystal,collected);
                    foreach(var type in new[]{ItemType.LifeCrystal,ItemType.ManaCrystal})
                    {
                        for(int n=0;n<15;n++)
                        {
                            int slot=Array.FindIndex(inv.Slots,s=>!s.IsEmpty && s.Type==type);if(slot<0)break;
                            inv.Selected=slot;while(!mod.DevReady)yield return null;mod.DevUseSelected(HeroController.instance.transform.position);yield return null;
                        }
                    }
                    inv.Selected=0;Equipment.Update(inv);mod.Stats.UpdateMaximums(pd,Equipment.Current.ManaMax);mod.Stats.FullHeal();mod.Stats.PotionSickness=0;
                    // One paid reforge, no fishing for the best prefix. The fixture's cash
                    // came exclusively from this profile's recorded enemy income.
                    Shop.SpareCopper=0;Shop.Earn(profile.Cash);long purse=Shop.Purse;int reforges=0;
                    var goblin=TownNpcs.DevPos("GoblinTinkerer");
                    if(goblin.HasValue && !profile.Weapon.IsEmpty && Prefixes.ReforgeCost(profile.Weapon)<=purse/10)
                    {
                        HeroController.instance.transform.position=Clear(HeroController.instance,goblin.Value+Vector2.left*.8f);yield return null;
                        TownNpcs.TryRightClick(goblin.Value);TownNpcs.OpenReforge();yield return null;
                        if(InventoryGui.DevPutReforge(inv.Slots[0]))
                        {
                            inv.Slots[0]=default;InventoryGui.DevReleaseHammer();if(InventoryGui.DevReforge())reforges++;
                            inv.Slots[0]=TownNpcs.ReforgeItem;TownNpcs.ReforgeItem=default;
                        }
                    }
                    var accessoryPrefixes=new List<object>();
                    if(TownNpcs.ReforgeOpen)
                    for(int i=0;i<Math.Min(2,profile.Accessories.Length);i++)
                    {
                        var item=inv.Accessories[i];if(item.IsEmpty || !Prefixes.CanHave(item.Def) || Prefixes.ReforgeCost(item)>(purse-Shop.Purse<purse/10?purse/10-(purse-Shop.Purse):0))continue;
                        inv.Mouse=default;
                        if(!InventoryGui.DevPutReforge(item))continue;
                        inv.Accessories[i]=default;InventoryGui.DevReleaseHammer();if(InventoryGui.DevReforge())reforges++;
                        inv.Accessories[i]=TownNpcs.ReforgeItem;TownNpcs.ReforgeItem=default;
                        accessoryPrefixes.Add(new {Type=inv.Accessories[i].Type.ToString(),inv.Accessories[i].Prefix});
                    }
                    TownNpcs.Close(false);InventoryGui.Open=false;
                    long reforgeSpent=purse-Shop.Purse;
                    var usedWeapon=inv.Slots[0];
                    // Each fixture owns its furniture. A previous class's station at
                    // the same spot must not intercept the next class's right click.
                    Stations.Clear();
                    BossArena.DevUnbeaten(bossKey);
                    pd.hasDash=profile.Stage>=1;pd.hasWalljump=profile.Stage>=2;pd.hasSuperDash=profile.Stage>=3;pd.hasDoubleJump=profile.Stage>=4;pd.hasShadowDash=profile.Stage>=5;
                    Minions.Instance.Clear();TerraProjectiles.Instance.DevClear();Whips.Instance.Clear();PlayerBuffs.Clear();EnemyDebuffs.Clear();
                    var consumedBuffs=new List<string>();
                    foreach(var name in profile.BuffItems)
                    {
                        var type=(ItemType)Enum.Parse(typeof(ItemType),name);int slot=Array.FindIndex(inv.Slots,s=>!s.IsEmpty && s.Type==type);
                        if(slot<0)continue;inv.Selected=slot;while(!mod.DevReady)yield return null;
                        int before=inv.Slots[slot].Count;mod.DevUseSelected(HeroController.instance.transform.position);yield return null;
                        if(inv.Slots[slot].Count<before || inv.Slots[slot].IsEmpty)consumedBuffs.Add(name);inv.Selected=0;
                    }
                    int killsBefore=Bestiary.KillsOf(record);
                    BossArena.DevWorldArena(bossKey,out string gate,out var needs);
                    yield return GoTo(room,gate ?? "left1");
                    // World room gates can be on a different floor from the arena.
                    // Place the fixture on real ground near the sleeping boss, once,
                    // before combat; do not edit positions after the attempt begins.
                    var sleeping=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                        .FirstOrDefault(t=>UI.EnemyHover.RecordOf(t)==record);
                    if(sleeping!=null)
                    {
                        Vector2 bp=sleeping.transform.position;
                        foreach(int side in new[]{-1,1})
                        {
                            Vector2 probe=bp+new Vector2(side*5,3);
                            var ground=Physics2D.Raycast(probe,Vector2.down,30,1<<ChunkColliders.TerrainLayer);
                            if(ground.collider==null)continue;
                            HeroController.instance.transform.position=Clear(HeroController.instance,new Vector2(probe.x,ground.point.y+.7f));
                            HeroController.instance.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;break;
                        }
                    }
                    if(sleeping!=null)Plugin.Log.LogInfo("[AutoTest] 新手进场状态 "+bossKey+" "+string.Join(";",sleeping.GetComponents<PlayMakerFSM>().Select(f=>f.FsmName+":"+f.ActiveStateName)));
                    // Use the mod's normal summon initialization to reach the correct
                    // arena floor/range and skip dialogue. Arbitrary room triggers can
                    // incorrectly put the player in Soul Master's lower phase-2 room.
                    var initialization=new List<string>();
                    var act=typeof(BossSummonStart).GetMethod("Act",BindingFlags.NonPublic|BindingFlags.Static);
                    yield return (IEnumerator)act.Invoke(null,new object[]{room,record,HeroController.instance,initialization,false});
                    yield return new WaitForSeconds(2);
                    bool WaitingBody(HealthManager t)=>t.GetComponents<PlayMakerFSM>().Any(f=>
                        (f.FsmName=="Control" || f.FsmName=="Mage Lord" || f.FsmName=="Mage Lord 2" || f.FsmName=="Mossy Control" || f.FsmName=="IK Control")
                        && new[]{"Sleep","Inert","Dormant","Init","Refight Ready","Waiting"}.Contains(f.ActiveStateName));
                    bool MageSecond(HealthManager t)=>t.GetComponents<PlayMakerFSM>().Any(f=>f.FsmName=="Mage Lord 2")
                        || t.gameObject.name.IndexOf("Phase2",StringComparison.OrdinalIgnoreCase)>=0
                        || t.gameObject.name.IndexOf("Phase 2",StringComparison.OrdinalIgnoreCase)>=0;
                    bool FutureMageBody(HealthManager t)=>record=="MageLord" && MageSecond(t)
                        && UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                            .Any(other=>other!=t && UI.EnemyHover.RecordOf(other)==record && !MageSecond(other)
                                && !other.isDead && other.hp>0 && !EnemyStats.Defeated(other));
                    if(record=="MageLord")foreach(var body in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>UI.EnemyHover.RecordOf(t)==record))
                        Plugin.Log.LogInfo("[AutoTest] 新手阶段本体 "+body.name+" hp="+body.hp+" pos="+body.transform.position+" active="+body.gameObject.activeInHierarchy+" "+string.Join(";",body.GetComponents<PlayMakerFSM>().Select(f=>f.FsmName+":"+f.ActiveStateName)));
                    bool Targetable(HealthManager t)=>t!=null && t.gameObject.activeInHierarchy && !t.isDead && !EnemyStats.Defeated(t)
                        && !EnemyStats.Untouchable(t) && !WaitingBody(t) && !FutureMageBody(t)
                        && t.GetComponentsInChildren<Collider2D>().Any(c=>c.enabled && c.GetComponentInParent<HealthManager>()==t);
                    bool InEncounter(HealthManager t)=>UI.EnemyHover.RecordOf(t)==record
                        || record=="TraitorLord" && UI.EnemyHover.RecordOf(t)=="HeavyMantis";
                    bool Awake() => UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .Any(t=>InEncounter(t) && (t.gameObject.scene.name==room || t.gameObject.scene.name==room+"_boss")
                            && Targetable(t));
                    float wakeDeadline=Time.time+15;float nextWake=0;
                    while(!Awake() && Time.time<wakeDeadline)
                    {
                        if(!GameHooks.PlayerHasControl()){yield return Tap(UnityEngine.InputSystem.Key.Space);continue;}
                        if(Time.time>=nextWake)
                        {
                            nextWake=Time.time+4;
                            var nudge=typeof(BossSummonStart).GetMethod("Nudge",BindingFlags.NonPublic|BindingFlags.Static);
                            yield return (IEnumerator)nudge.Invoke(null,new object[]{room,record,initialization});
                        }
                        yield return null;
                    }
                    bool awake=Awake();
                    Plugin.Log.LogInfo("[AutoTest] 新手开场初始化 "+bossKey+" "+string.Join(";",initialization));
                    if(!awake){Plugin.Log.LogWarning($"[AutoTest] 新手进场未完成 {bossKey}，不作为数值失败");Logged(()=>DumpBattleFsms(room));}
                    var h=HeroController.instance;TerraKnight.Player.TerraStats.DevImmortal=false;
                    if(record=="TraitorLord")
                    {
                        // The sleeping lord is high above the arena. Prepare this
                        // independent fixture near the actual first wave, before
                        // enabling damage and starting the combat timer.
                        var wave=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                            .FirstOrDefault(t=>UI.EnemyHover.RecordOf(t)=="HeavyMantis" && Targetable(t)
                                && (t.gameObject.scene.name==room || t.gameObject.scene.name==room+"_boss"));
                        if(wave!=null)foreach(int side in new[]{-1,1})
                        {
                            Vector2 probe=(Vector2)wave.transform.position+new Vector2(side*4,3);
                            var ground=Physics2D.Raycast(probe,Vector2.down,12,1<<ChunkColliders.TerrainLayer);
                            if(ground.collider==null)continue;
                            h.transform.position=Clear(h,new Vector2(probe.x,ground.point.y+.7f));
                            h.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                            initialization.Add("fixture-ground-near-wave:"+h.transform.position);break;
                        }
                    }
                    var stationSpecs=(IDictionary)typeof(Stations).GetField("specs",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
                    var arenaRegion=TerrainSystem.Current.Region;
                    foreach(DictionaryEntry entry in stationSpecs)
                    {
                        var item=(ItemType)entry.Value.GetType().GetField("Item").GetValue(entry.Value);
                        if(!profile.BuffStations.Contains(item))continue;
                        // Use each owned station in turn so overlapping hitboxes cannot intercept another station.
                        Stations.Clear();
                        Vector2 local=(Vector2)h.transform.position-(Vector2)arenaRegion.Root.position+Vector2.right;
                        Stations.PlaceFixed((StationKind)entry.Key,arenaRegion.SceneName,local);yield return null;
                        Stations.TryRightClick((Vector2)arenaRegion.Root.position+local+Vector2.up*.5f);
                    }
                    Equipment.Update(inv);mod.Stats.UpdateMaximums(pd,Equipment.Current.ManaMax);mod.Stats.FullHeal();
                    pd.isInvincible=false;mod.DevNoFallDamage=false;
                    var startBuffIds=PlayerBuffs.Type.Where(t=>t>0).ToArray();
                    int startLife=mod.Stats.Life,casts=0,hits=0,damageTaken=0,lastLife=startLife;
                    float start=Time.time,nextJump=0,nextLog=start,jumpUntil=0;bool won=false,seen=false,waveSeen=false;int minHp=int.MaxValue,initialHp=0;
                    int waveKillsBefore=Bestiary.KillsOf("HeavyMantis");
                    var summonChoices=new[]{usedWeapon.Type}.Concat(availableWeapons)
                        .Distinct().Where(t=>Array.Exists(inv.Slots,s=>!s.IsEmpty && s.Type==t)).Take(4).ToArray();
                    int staffSlot=0,trialIndex=0;float sampleStart=Time.time,nextStaff=Time.time+18;double sampleDamage=0;
                    var summonSamples=new List<(ItemType Staff,double Damage,float Seconds)>();var previousHp=new Dictionary<int,int>();
                    var phasesSeen=new HashSet<string>();var approachedPickups=new HashSet<int>();var encounterNavigation=new List<string>();
                    float waypointJumpAt=-100;bool waypointAirJump=false;int extraJumpsBefore=TerraMovement.DevExtraJumps;
                    // Ordinary walking/jumping and real selected weapon attacks only.
                    // No boss/hero position writes or FSM/HP edits inside this loop.
                    while(awake && Time.time-start<(record=="MageLord"?180:105) && !h.cState.dead && !mod.Stats.IsDead)
                    {
                        won=Bestiary.KillsOf(record)>killsBefore;if(won)break;
                        var targets=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                            .Where(t=>InEncounter(t) && !FutureMageBody(t) && !t.isDead && (t.gameObject.scene.name==room || t.gameObject.scene.name==room+"_boss")).ToArray();
                        var target=targets.Where(Targetable).OrderBy(t=>UI.EnemyHover.RecordOf(t)==record?0:1)
                            .ThenBy(t=>Vector2.Distance(t.transform.position,h.transform.position)).FirstOrDefault()
                            ?? targets.FirstOrDefault(t=>t.gameObject.activeInHierarchy) ?? targets.FirstOrDefault();
                        if(target==null){yield return null;continue;}
                        bool bossTarget=UI.EnemyHover.RecordOf(target)==record;
                        if(target.gameObject.activeInHierarchy){if(bossTarget)seen=true;else waveSeen=true;}
                        if(bossTarget && Targetable(target))phasesSeen.Add(target.name);
                        var tc=target.GetComponent<Collider2D>();Vector2 aim=tc!=null? (Vector2)tc.bounds.center : (Vector2)target.transform.position;
                        Vector2 heroCenter=h.GetComponent<BoxCollider2D>().bounds.center;
                        var activeWeapon=inv.Slots[staffSlot];
                        if(profile.Class==0 && !activeWeapon.IsEmpty && (!activeWeapon.Def.NoMelee || activeWeapon.Type.ToString().EndsWith("Shortsword") || activeWeapon.Def.Fx==MeleeFx.Shortsword) && tc!=null)
                            aim=tc.ClosestPoint(heroCenter);
                        Vector2? encounterWaypoint=null;
                        // Soul Master's phase one leaves a fake Quake pickup. Walk into
                        // its real trigger, as a player does; never move the hero or
                        // force the phase FSM during combat.
                        if(record=="MageLord" && !targets.Any(Targetable)
                            && !targets.Any(t=>!MageSecond(t) && t.hp>0 && !EnemyStats.Defeated(t)))
                        {
                            var pickup=UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                                .Where(f=>(f.gameObject.scene.name==room || f.gameObject.scene.name==room+"_boss")
                                    && f.FsmName=="Quake Fake" && f.enabled && f.ActiveStateName=="Idle")
                                .Select(f=>f.gameObject).Distinct().OrderBy(go=>Vector2.Distance(go.transform.position,h.transform.position)).FirstOrDefault();
                            if(pickup!=null)
                            {
                                var pickupCollider=pickup.GetComponentsInChildren<Collider2D>().FirstOrDefault(c=>c.enabled);
                                encounterWaypoint=pickupCollider!=null?(Vector2)pickupCollider.bounds.center:(Vector2)pickup.transform.position;
                                aim=encounterWaypoint.Value;
                                if(approachedPickups.Add(pickup.GetInstanceID()))
                                {encounterNavigation.Add("jump-to-collect:"+pickup.name+"/"+aim);Plugin.Log.LogInfo("[AutoTest] 新手真实转阶段接近 "+pickup.name+" "+aim);Logged(()=>DumpFsms(pickup.transform));}
                            }
                        }
                        float dx=aim.x-h.transform.position.x,dy=aim.y-h.transform.position.y;
                        if(bossTarget && target.gameObject.activeInHierarchy){if(initialHp==0)initialHp=target.hp;minHp=Math.Min(minHp,target.hp);}
                        int targetId=target.GetInstanceID();
                        if(previousHp.TryGetValue(targetId,out int oldHp) && target.hp<oldHp)sampleDamage+=(oldHp-target.hp)*EnemyStats.Of(target).Scale;
                        previousHp[targetId]=target.hp;
                        if(summonChoices.Length>1 && Time.time>=nextStaff)
                        {
                            summonSamples.Add((inv.Slots[staffSlot].Type,sampleDamage,Time.time-sampleStart));
                            var chosen=++trialIndex<summonChoices.Length?summonChoices[trialIndex]:summonSamples.OrderByDescending(s=>s.Damage/Math.Max(.1,s.Seconds)).First().Staff;
                            int nextSlot=Array.FindIndex(inv.Slots,s=>!s.IsEmpty && s.Type==chosen);
                            if(nextSlot>=0 && nextSlot!=staffSlot){if(profile.Class==3)Minions.Instance.DevNoviceDismissMinions();staffSlot=nextSlot;}
                            nextStaff=trialIndex>=summonChoices.Length?float.PositiveInfinity:Time.time+18;sampleStart=Time.time;sampleDamage=0;
                            Plugin.Log.LogInfo($"[AutoTest] 新手武器换装 {bossKey}/{profile.Class} -> {chosen} 实际仆从栏={Equipment.MaxMinions}");
                        }
                        activeWeapon=inv.Slots[staffSlot];inv.Selected=staffSlot;
                        float distance=5.5f;
                        if(profile.Class==0 && !activeWeapon.IsEmpty)
                        {
                            var d=activeWeapon.Def;
                            distance=d.NoMelee?3.2f:d.Shoot!=ProjKind.None?3.5f:1.4f;
                            if(d.Fx==MeleeFx.Shortsword || d.Type.ToString().EndsWith("Shortsword"))distance=.9f;
                            var yy=(IDictionary)typeof(TerraProjectiles).GetField("yoyos",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
                            if(yy.Contains(d.Shoot))
                            {var spec=yy[d.Shoot];distance=(float)spec.GetType().GetField("Range").GetValue(spec)*Effects.TerraParticles.Px*.65f;}
                        }
                        if(profile.Class==3 && profile.Whip!=ItemType.None)distance=2.5f*(1+Equipment.Current.WhipRange);
                        if(encounterWaypoint.HasValue)distance=.1f;
                        int direction=Math.Abs(dx)>distance+.6f ? Math.Sign(dx) : Math.Abs(dx)<distance-1 && profile.Class!=0 ? -Math.Sign(dx) : 0;
                        // A projectile melee weapon also kites inside its real reach.
                        if(profile.Class==0 && !activeWeapon.IsEmpty && activeWeapon.Def.NoMelee && Math.Abs(dx)<distance-.8f)direction=-Math.Sign(dx);
                        if(encounterWaypoint.HasValue)direction=Math.Abs(dx)>.1f?Math.Sign(dx):0;
                        TerraKnight.Player.TerraMovement.DevMove=direction;
                        var enemyBody=target.GetComponent<Rigidbody2D>();float approach=enemyBody==null?0:-Math.Sign(dx)*enemyBody.linearVelocity.x;
                        if(h.cState.onGround && Time.time>=nextJump && (encounterWaypoint.HasValue || Math.Abs(dx)<3 || dy>2 || approach>2 && Math.Abs(dx)<8))
                        {
                            jumpUntil=Time.time+(encounterWaypoint.HasValue?.38f:.28f);nextJump=Time.time+1.1f;
                            if(encounterWaypoint.HasValue){waypointJumpAt=Time.time;waypointAirJump=false;}
                        }
                        if(encounterWaypoint.HasValue && !h.cState.onGround && !waypointAirJump && Time.time-waypointJumpAt>.52f
                            && Time.time-waypointJumpAt<1.1f && aim.y>heroCenter.y+.1f)
                        {
                            // A real second press activates an equipped bottle or
                            // rocket boots; no extra ability is granted by the test.
                            jumpUntil=Time.time+.55f;waypointAirJump=true;
                            encounterNavigation.Add("air-jump/has-cloud:"+Equipment.Current.JumpCloud);
                        }
                        if(Time.time<jumpUntil)KeysDown(UnityEngine.InputSystem.Key.Space);else KeysDown();
                        bool flail=!activeWeapon.IsEmpty && activeWeapon.Def.Channel && activeWeapon.Def.Shoot.ToString().EndsWith("Flail");
                        bool held=!encounterWaypoint.HasValue && !activeWeapon.IsEmpty && activeWeapon.Def.Channel && (!flail || (Time.time-start)%1.25f<.65f);
                        mod.DevAim=aim;mod.DevHold=held;
                        if(GameHooks.PlayerHasControl() && mod.DevReady)
                        {
                            int slot=-1;
                            if(mod.Stats.Life<mod.Stats.LifeMax*.5f && mod.Stats.PotionSickness<=0)
                                slot=Enumerable.Range(0,inv.Slots.Length).Where(i=>!inv.Slots[i].IsEmpty && inv.Slots[i].Def.HealLife>0).OrderByDescending(i=>Math.Min(inv.Slots[i].Def.HealLife,mod.Stats.LifeMax-mod.Stats.Life)).DefaultIfEmpty(-1).First();
                            if(slot<0 && mod.Stats.Mana<10 && profile.Class>=2)
                                slot=Enumerable.Range(0,inv.Slots.Length).Where(i=>!inv.Slots[i].IsEmpty && inv.Slots[i].Def.HealMana>0).OrderByDescending(i=>inv.Slots[i].Def.HealMana).DefaultIfEmpty(-1).First();
                            if(slot<0)slot=Array.FindIndex(inv.Slots,s=>!s.IsEmpty && profile.BuffItems.Contains(s.Type.ToString()) && PlayerBuffs.TimeLeft(s.Def.BuffType)<60);
                            if(slot>=0){inv.Selected=slot;mod.DevUseSelected(aim);inv.Selected=staffSlot;}
                            else if(Targetable(target) && !activeWeapon.IsEmpty && (!flail || held))
                            {
                                if(profile.Class!=3 || Minions.Instance.SlotsUsed<Equipment.MaxMinions){inv.Selected=staffSlot;mod.DevUseSelected(aim);casts++;}
                                else if(profile.Sentry!=ItemType.None && Minions.Instance.DevNoviceTurrets<Equipment.MaxTurrets)
                                {int sentrySlot=Array.FindIndex(inv.Slots,s=>!s.IsEmpty && s.Type==profile.Sentry);if(sentrySlot>=0){inv.Selected=sentrySlot;mod.DevUseSelected(aim);inv.Selected=staffSlot;casts++;}}
                                else if(profile.Whip!=ItemType.None){inv.Selected=1;mod.DevUseSelected(aim);inv.Selected=staffSlot;casts++;}
                            }
                        }
                        if(mod.Stats.Life<lastLife){hits++;damageTaken+=lastLife-mod.Stats.Life;}lastLife=mod.Stats.Life;
                        if(Time.time>=nextLog)
                        {nextLog=Time.time+15;if(record=="MageLord")foreach(var phaseBody in UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>UI.EnemyHover.RecordOf(t)==record))Plugin.Log.LogInfo("[AutoTest] 新手阶段跟踪 "+phaseBody.name+" hp="+phaseBody.hp+" pos="+phaseBody.transform.position+" dead="+phaseBody.isDead+" active="+phaseBody.gameObject.activeInHierarchy+" "+string.Join(";",phaseBody.GetComponents<PlayMakerFSM>().Select(f=>f.FsmName+":"+f.ActiveStateName)));Plugin.Log.LogInfo($"[AutoTest] 新手实战 {bossKey}/{profile.Difficulty}/{profile.Class} t={Time.time-start:F1} life={mod.Stats.Life}/{mod.Stats.LifeMax} boss={target.hp} pos={(Vector2)h.transform.position:F1} target={aim:F1} casts={casts}");}
                        yield return null;
                    }
                    TerraKnight.Player.TerraMovement.DevMove=0;mod.DevAim=null;mod.DevHold=null;KeysDown();
                    if(!inv.Slots[staffSlot].IsEmpty)summonSamples.Add((inv.Slots[staffSlot].Type,sampleDamage,Time.time-sampleStart));
                    won=Bestiary.KillsOf(record)>killsBefore;
                    results.Add(new {profile.Difficulty,profile.Class,profile.Seed,Boss=bossKey,GearStage=profile.Stage,Weapon=usedWeapon.Type.ToString(),usedWeapon.Prefix,usedWeapon.Forge,
                        profile.Armor,profile.Accessories,Crystals=collected,LifeMax=mod.Stats.LifeMax,ManaMax=mod.Stats.ManaMax,Awake=awake,Seen=seen,WaveSeen=waveSeen,
                        WaveKills=Bestiary.KillsOf("HeavyMantis")-waveKillsBefore,Won=won,Seconds=Time.time-start,
                        PhasesSeen=phasesSeen.ToArray(),EncounterNavigation=encounterNavigation,
                        ExtraJumps=TerraMovement.DevExtraJumps-extraJumpsBefore,
                        Life=mod.Stats.Life,HitsTaken=hits,DamageTaken=damageTaken,Casts=casts,InitialNativeHp=initialHp,MinimumNativeHp=minHp,
                        Initialization=initialization,IntroSkipped=true,
                        BuffItems=consumedBuffs,BuffStations=profile.BuffStations.Select(t=>t.ToString()).ToArray(),CarriedBuffItems=profile.CarriedBuffItems.Select(t=>t.ToString()).ToArray(),
                        ActiveBuffIds=PlayerBuffs.Type.Where(t=>t>0).ToArray(),Sentry=profile.Sentry.ToString(),MaxTurrets=Equipment.MaxTurrets,
                        StartBuffIds=startBuffIds,ConsumablesUsed=suppliesBefore.Select(p=>new {Type=p.Key.ToString(),Count=Math.Max(0,p.Value-inv.Slots.Where(s=>s.Type==p.Key).Sum(s=>s.Count))}).Where(p=>p.Count>0).ToArray(),
                        MaxMinions=Equipment.MaxMinions,Defense=mod.Stats.Defense,AccessoryPrefixes=accessoryPrefixes,
                        SummonTrials=summonSamples.Select(s=>new {Staff=s.Staff.ToString(),s.Damage,s.Seconds}).ToArray(),
                        WeaponTrials=summonSamples.Select(s=>new {Weapon=s.Staff.ToString(),s.Damage,s.Seconds}).ToArray(),FinalWeapon=inv.Slots[staffSlot].Type.ToString(),
                        Reforges=reforges,ReforgeSpent=reforgeSpent,Ammo=profile.Ammo.ToString(),AmmoLeft=inv.Slots.Where(s=>!s.IsEmpty && s.Def.IsAmmo).Sum(s=>s.Count)});
                    File.WriteAllText(Path.Combine(DevCopy.SaveDir,"novice-bosses.json"),JsonConvert.SerializeObject(new {RealAI=true,Immortal=false,DirectDamage=false,
                        TravelAssisted=true,CrystalExplorationAssisted=true,Crystals=crystalEvidence,Results=results},Formatting.Indented));
                    Shot(room,$"novice_{profile.Difficulty}_{profile.Class}_{profile.Stage}");
                    Plugin.Log.LogInfo($"[AutoTest] 新手实战结果 {bossKey}/{profile.Difficulty}/{profile.Class} win={won} life={mod.Stats.Life} casts={casts}");
                    if(!won && h.cState.dead)yield return new WaitForSeconds(12);
                }
            }
            finally {TerraKnight.Player.TerraMovement.DevMove=0;mod.DevAim=null;mod.DevHold=null;Shop.DevStage=-1;}
        }
    }
}

namespace TerraKnight.Items
{
    internal sealed partial class Minions
    {
        internal int DevNoviceTurrets => live.Count(m=>!m.Dead && m.S.Sentry);
        internal void DevNoviceDismissMinions()
        {foreach(var m in live.ToArray())if(m.S.Minion && !m.S.Sentry)Kill(m);}
    }
}
