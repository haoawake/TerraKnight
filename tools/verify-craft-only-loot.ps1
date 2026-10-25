$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$drops=Get-Content "$root/src/TerraKnight/Items/EnemyDrops.cs" -Raw
function Method([string]$marker){
 $a=$drops.IndexOf($marker);if($a -lt 0){throw "Missing $marker"};$b=$drops.IndexOf('{',$a);$n=1;$e=$b+1
 while($n -gt 0){if($drops[$e] -eq '{'){$n++};if($drops[$e] -eq '}'){$n--};$e++};$drops.Substring($a,$e-$a)
}
$actual=(Method 'internal static Drop Pick(')+"`n"+(Method 'internal static float GroupWeightTotal(')+"`n"+(Method 'internal static float MarginalChance(')
$test=@'
using System;using System.Collections.Generic;using Random=UnityEngine.Random;
namespace UnityEngine {static class Random {static System.Random r=new System.Random(10192026);public static float value=>(float)r.NextDouble();}}
namespace TerraKnight.Items {
enum ItemType{None,BladeofGrass,Stinger,Vine,BlizzardinaBottle,Furnace,ShopStaff}
enum ItemSource{Basic,Shop,Craft,Drop}
class ItemDef{public ItemSource Source;}
static class ItemDb {public static ItemDef Get(ItemType t)=>new ItemDef{Source=t==ItemType.BladeofGrass||t==ItemType.Furnace?ItemSource.Craft:ItemSource.Drop};}
static class ShopLoot {public static bool Equipment(ItemType t)=>t==ItemType.ShopStaff;}
static class ProgressionLoot {public static float Weight(EnemyDrops.Drop d)=>d.Weight>0?d.Weight:1;}
static class EnemyDrops {
public struct Drop{public ItemType Type;public int Group,Num,Den,Min,Max;public float Weight,GroupWeightTotal;}
public static float Scale=1;static float EffectiveChance(Drop d)=>Math.Min(1,d.Num/(float)d.Den*Scale);static bool Locked(Drop d,int stage)=>false;
METHODS
}
public static class CraftOnlyHarness {
static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
static EnemyDrops.Drop Row(ItemType t,int group,float weight)=>new EnemyDrops.Drop{Type=t,Group=group,Num=2,Den=5,Min=1,Max=1,Weight=weight};
public static string Run(){
foreach(float scale in new[]{1f,1.15f,1.3f}){
EnemyDrops.Scale=scale;
var all=new[]{Row(ItemType.BladeofGrass,9,3),Row(ItemType.Stinger,9,1),Row(ItemType.Vine,9,2),Row(ItemType.Furnace,0,1),Row(ItemType.BlizzardinaBottle,0,1),Row(ItemType.ShopStaff,0,1)};
var kept=CraftOnlyLoot.Filter(all);Check(kept.Count==3,"crafted item survives");
Check(Object.ReferenceEquals(kept,CraftOnlyLoot.Filter(all)),"unstable cached filter");
for(int i=0;i<2;i++){var d=kept[i];Check(Math.Abs(EnemyDrops.MarginalChance(kept,d,5)-EnemyDrops.MarginalChance(all,all[i+1],5))<.000001,"legal marginal rate changed");Check(d.Num==2&&d.Den==5&&d.Min==1&&d.Max==1,"roll parameters changed");}
Check(kept[2].Type==ItemType.BlizzardinaBottle&&kept[2].Group==0,"independent world-source item lost");
var grouped=new[]{kept[0],kept[1]};int none=0,a=0,b=0;
for(int i=0;i<60000;i++){var p=EnemyDrops.Pick(grouped);if(p.Type==ItemType.None)none++;else if(p.Type==ItemType.Stinger)a++;else if(p.Type==ItemType.Vine)b++;else throw new Exception("unexpected item");}
Check(Math.Abs(none/60000f-.5f)<.01&&Math.Abs(a/60000f-1f/6)<.01&&Math.Abs(b/60000f-1f/3)<.01,"reserved outcome probability drift");
Check(CraftOnlyLoot.Filter(new[]{all[0],all[3]}).Count==0,"all-craft pool survived");
Check(CraftOnlyLoot.Filter(new[]{all[5]}).Count==0,"shop equipment survived");
var legal=new[]{all[1],all[4]};Check(Object.ReferenceEquals(legal,CraftOnlyLoot.Filter(legal)),"legal-only table changed");
}
return "PASS "+checks+" production filter, sampler and preview checks; 180000 weighted picks; item metadata and Unity RNG fixtures (not gameplay)";
}
}}
'@
$test=$test.Replace('METHODS',$actual)
$temp=Join-Path ([IO.Path]::GetTempPath()) ('craft-only-'+[guid]::NewGuid().ToString('N')+'.cs')
try {
 [IO.File]::WriteAllText($temp,$test,[Text.UTF8Encoding]::new($false))
 Add-Type -Path @($temp,"$root/src/TerraKnight/Items/CraftOnlyLoot.cs") -CompilerOptions '/warn:0'
 [TerraKnight.Items.CraftOnlyHarness]::Run()
} finally {Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue}
