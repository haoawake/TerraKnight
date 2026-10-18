$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$loot=Get-Content (Join-Path $root 'src/TerraKnight/Items/ProgressionLoot.cs') -Raw
$areas=Get-Content (Join-Path $root 'src/TerraKnight/World/Areas.cs') -Raw
$items=Get-Content (Join-Path $root 'src/TerraKnight/Items/Items.cs') -Raw
function Method([string]$text,[string]$marker){
 $start=$text.IndexOf($marker);if($start -lt 0){throw "Missing $marker"}
 $brace=$text.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
 $text.Substring($start,$end-$start)
}
$enum=Method $items 'internal enum ItemType'
$worldMethods=@('public static string AreaName(','public static int LevelOf(','static bool Starts(') | ForEach-Object {Method $areas $_}
$worldMethods+=[regex]::Match($areas,'static bool IsFogCanyon[^;]+;').Value
$methods=@('static void AddExtras(','static void ImproveSpecialties(','static void ConsolidateAccessories(','static uint StableHash(','static bool MainArea(','internal static int NativeTier(') | ForEach-Object {Method $loot $_}
foreach($signature in @('internal static bool Eligible','internal static bool Spawned','internal static bool CombatWeapon','internal static bool MainRoute','internal static float Weight','static EnemyDrops.Drop Row','internal static float AccessoryChance','internal static float AccessoryPoolChance')){
 $found=[regex]::Match($loot,([regex]::Escape($signature)+'[^;]+;'));if(!$found.Success){throw "Missing $signature"};$methods+=$found.Value
}
$methods+=Method (Get-Content (Join-Path $root 'src/TerraKnight/Items/RegionalLootProfiles.cs') -Raw) 'internal static int AreaBand('
$named=[regex]::Match($loot,'internal static readonly HashSet<string> NamedElites[^;]+;').Value
$world='namespace TerraKnight.World {internal enum StationKind {None}internal static class Areas {'+($worldMethods -join "`n")+'}}'
$test=@'
using System;using System.Collections.Generic;
namespace UnityEngine {public static class Mathf {public static int Clamp(int x,int a,int b)=>Math.Max(a,Math.Min(b,x));public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));public static int RoundToInt(float x)=>(int)Math.Round(x);} }
namespace TerraKnight.Items {
internal enum DamageClass{None,Melee,Ranged,Magic,Summon}internal enum Kit{General,Melee,Ranged,Magic,Summon}
internal class ItemDef{public int Stage=-1,MaxStack=1,PickPower,AxePower,HammerPower,FishingPower;public bool IsWeapon,IsAmmo,GameTool,HkItem,IsArmor,IsAccessory;public DamageClass Class=DamageClass.Melee;public Kit Kit=Kit.General;public World.StationKind PlacesStation;}
internal static class ItemDb {public static readonly Dictionary<ItemType,ItemDef> defs=new Dictionary<ItemType,ItemDef>();public static ItemDef Get(ItemType t)=>defs.TryGetValue(t,out var d)?d:new ItemDef();}
internal static class Recipes {public static bool IsMaterial(ItemType t)=>t==ItemType.Seedler;}
internal static class ProgressionBalance {public static HashSet<ItemType> ShopOnlyWeapons=new HashSet<ItemType>();public static bool FormalBoss(string r)=>EnemyDrops.EntryFor(r).Boss&&!ProgressionLoot.NamedElites.Contains(r);public static bool Unlocked(ItemType t,int stage,bool entry,bool final)=>ItemDb.Get(t).Stage<=stage;public static int ClassIndex(DamageClass c)=>(int)c-1;public static int ClassIndex(Kit c)=>(int)c-1;}
internal static partial class EnemyDrops {
 public struct Drop{public ItemType Type;public int Num,Den,Min,Max,Group,LockStage;public bool Master;public float Weight;public float Chance=>UnityEngine.Mathf.Clamp01(Num/(float)Den);}
 public struct Entry{public string En,Zh,Kind,Scene;public int Stage,Hp,Geo;public bool Boss;public Drop[] Drops;}
 static Drop C(ItemType t,int den,int min=1,int max=1)=>new Drop{Type=t,Num=1,Den=den,Min=min,Max=max};
 static Drop D(ItemType t,int den,int min=1,int max=1,int num=1)=>new Drop{Type=t,Num=num,Den=den,Min=min,Max=max};
 static Drop G(int group,ItemType t,int den=1)=>new Drop{Type=t,Num=1,Den=den,Min=1,Max=1,Group=group};
 public static IEnumerable<string> DevRecords=>table.Keys;public static Entry EntryFor(string r)=>table[r];public static string SceneOf(string r)=>table[r].Scene;public static Drop[] LegacyOf(string r)=>table[r].Drops;
}
internal static partial class ProgressionLoot {
 const int Rare=102,Essential=103;
 static readonly Dictionary<string,List<ItemType>> catalogues=new Dictionary<string,List<ItemType>>();static readonly Dictionary<string,Dictionary<string,List<ItemType>>> sources=new Dictionary<string,Dictionary<string,List<ItemType>>>();static HashSet<ItemType> legacyItems;static Dictionary<string,int> ordinals;
 static void Reset(){catalogues.Clear();sources.Clear();legacyItems=null;ordinals=null;}
 static int checks;static void Check(bool b,string msg){checks++;if(!b)throw new Exception(msg);}
 static float Probability(List<EnemyDrops.Drop> rows,ItemType type){double miss=1;var seen=new HashSet<int>();foreach(var r in rows){if(r.Group==0){if(r.Type==type)miss*=1-r.Chance;continue;}if(!seen.Add(r.Group))continue;float total=0,own=0;foreach(var o in rows)if(o.Group==r.Group){total+=Weight(o);if(o.Type==type)own+=Weight(o);}miss*=1-r.Chance*own/total;}return (float)(1-miss);}
 internal static string Run(){
  ItemDb.defs[ItemType.Seedler]=new ItemDef{Stage=4,IsWeapon=true};
  var actual=new List<string>();var again=new List<string>();
  foreach(var rec in EnemyDrops.DevRecords){var pool=new List<ItemType>();var special=new HashSet<ItemType>();AddExtras(pool,rec,4,0,0,true,false,special);if(pool.Contains(ItemType.Seedler)){actual.Add(rec);Check(special.Contains(ItemType.Seedler),"cache misses specialty marker");Check(NativeTier(rec)<=4&&World.Areas.LevelOf(EnemyDrops.SceneOf(rec))<=4,"future-area source");Check(!NamedElites.Contains(rec)&&!ProgressionBalance.FormalBoss(rec),"boss/first-kill owns farm route");}}
  Check(actual.Count==2,"Seedler does not have two distinct normal-monster routes");Check(BestiaryRoute.AreaOf(actual[0])!=BestiaryRoute.AreaOf(actual[1]),"second Seedler source not in another eligible region");
  foreach(var rec in EnemyDrops.DevRecords){var pool=new List<ItemType>();AddExtras(pool,rec,3,0,0,false,false);Check(!pool.Contains(ItemType.Seedler),"Seedler leaks before stage4");}
  Reset();foreach(var rec in EnemyDrops.DevRecords){var pool=new List<ItemType>();AddExtras(pool,rec,4,0,0,true,false);if(pool.Contains(ItemType.Seedler))again.Add(rec);}Check(string.Join(",",actual)==string.Join(",",again),"source assignment depends on cache state");
  // The same production allocator must supply two distinct routes across every tier.
  for(int stage=0;stage<6;stage++){Reset();ItemDb.defs[ItemType.Seedler].Stage=stage;int count=0;foreach(var rec in EnemyDrops.DevRecords){var pool=new List<ItemType>();AddExtras(pool,rec,stage,0,0,true,true);if(pool.Contains(ItemType.Seedler))count++;}Check(count==2,"tier "+stage+" lacks two routes");}
  ItemDb.defs[ItemType.Seedler].Stage=4;
  foreach(bool elite in new[]{false,true})foreach(int n in new[]{2,10,100}){
   var rows=new List<EnemyDrops.Drop>{Row(ItemType.Seedler,102,.1f,weight:1),Row(ItemType.Wood,102,.1f,weight:n),Row(ItemType.HermesBoots,103,.02f),Row(ItemType.StoneBlock,0,.5f,2,4)};
   float old=Probability(rows,ItemType.Seedler),other=Probability(rows,ItemType.Wood),material=Probability(rows,ItemType.StoneBlock);
   ImproveSpecialties(rows,new HashSet<ItemType>{ItemType.Seedler},elite);
   Check(Math.Abs(Probability(rows,ItemType.Seedler)-Math.Max(elite?.025f:.01f,old*1.25f))<.00002,"specialty chance not improved");
   Check(Math.Abs(Probability(rows,ItemType.Wood)-other)<.00002,"unrelated group member chance changed");Check(Probability(rows,ItemType.StoneBlock)==material,"material rate changed");
   var selected=rows.Find(r=>r.Type==ItemType.Seedler);Check(selected.Min==1&&selected.Max==1&&selected.Group==0,"specialty quantity or independent roll");
  }
  var duplicates=new List<EnemyDrops.Drop>{Row(ItemType.Seedler,102,.2f),Row(ItemType.Seedler,102,.2f),Row(ItemType.Wood,102,.2f),Row(ItemType.Seedler,0,.1f)};
  float prior=Probability(duplicates,ItemType.Seedler);ImproveSpecialties(duplicates,new HashSet<ItemType>{ItemType.Seedler},false);Check(duplicates.FindAll(r=>r.Type==ItemType.Seedler).Count==1,"duplicate rows retained");Check(Math.Abs(Probability(duplicates,ItemType.Seedler)-prior*1.25f)<.00002,"duplicate/group union probability wrong");
  var certain=new List<EnemyDrops.Drop>{Row(ItemType.Seedler,0,1)};ImproveSpecialties(certain,new HashSet<ItemType>{ItemType.Seedler},false);Check(certain[0].Chance==1,"guaranteed reward weakened");
  var accessoryTypes=new List<ItemType>();foreach(var type in (ItemType[])Enum.GetValues(typeof(ItemType)))if(type!=ItemType.None&&type!=ItemType.Seedler){accessoryTypes.Add(type);if(accessoryTypes.Count==64)break;}
  foreach(var type in accessoryTypes)ItemDb.defs[type]=new ItemDef{Stage=0,IsAccessory=true};
  foreach(bool elite in new[]{false,true})foreach(int count in new[]{1,2,4,8,16,32,64}){
   var acc=new List<EnemyDrops.Drop>();for(int i=0;i<count;i++)acc.Add(Row(accessoryTypes[i],103,.02f));
   ConsolidateAccessories(acc,elite);float total=0;foreach(var row in acc){total+=Probability(acc,row.Type);Check(Probability(acc,row.Type)>(elite?.05f:.02f)/count,"ordinary accessory chance not raised");Check(row.Min==1&&row.Max==1,"accessory quantity changed");}
   Check(Math.Abs(total-AccessoryPoolChance(elite,count))<.00002,"accessory pool probability mismatch");Check(total<=(elite?.25f:.12f)+.00002,"unbounded accessory budget");
  }
  foreach(float bossRoll in new[]{1f,.25f}){
   var acc=new List<EnemyDrops.Drop>{Row(accessoryTypes[0],103,.2f),Row(accessoryTypes[1],103,.2f)};
   ConsolidateAccessories(acc,false,bossRoll);Check(Math.Abs(Probability(acc,accessoryTypes[0])-bossRoll/2)<.00002,"boss accessory budget or first-kill guarantee changed");
  }
  ItemDb.defs[ItemType.ShinyRedBalloon]=new ItemDef{Stage=0,IsAccessory=true};
  var balloon=new List<EnemyDrops.Drop>{Row(ItemType.ShinyRedBalloon,103,.02f),Row(ItemType.Seedler,102,.1f),Row(ItemType.ShinyRedBalloon,102,.1f)};
  float weaponRate=Probability(balloon,ItemType.Seedler);ConsolidateAccessories(balloon,false);
  Check(balloon.FindAll(r=>r.Type==ItemType.ShinyRedBalloon).Count==1,"red balloon duplicate entries");Check(Probability(balloon,ItemType.ShinyRedBalloon)>=.03f-.00001,"red balloon missed general accessory boost");Check(Math.Abs(Probability(balloon,ItemType.Seedler)-weaponRate)<.00002,"accessory adjustment changed weapon chance");
  return "Seedler production roster routes: "+string.Join(", ",actual)+"\nPASS "+checks+" actual allocator/probability checks; item metadata fixture, production enemy and area data; no gameplay test";
 }
}
}
'@
$test="using UnityEngine;`n"+$test
$insert=$test.IndexOf('internal enum DamageClass')
$test=$test.Insert($insert,$enum+"`n")
$insert=$test.IndexOf(' static void Reset()')
$test=$test.Insert($insert,$named+"`n"+($methods -join "`n")+"`n")
$test+=$world+"`npublic static class SpecialtyHarness {public static string Run()=>TerraKnight.Items.ProgressionLoot.Run();}"
$temp=Join-Path ([IO.Path]::GetTempPath()) ('specialty-loot-'+[guid]::NewGuid().ToString('N')+'.cs')
try {
 [IO.File]::WriteAllText($temp,$test)
 Add-Type -Path @($temp,(Join-Path $root 'src/TerraKnight/Items/EnemyDropData.cs'),(Join-Path $root 'src/TerraKnight/Items/BestiaryRoute.cs'),(Join-Path $root 'src/TerraKnight/Items/BestiaryRouteData.cs')) -CompilerOptions '/warn:0'
 [SpecialtyHarness]::Run()
} finally {Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue}
