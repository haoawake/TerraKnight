$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Extract([string]$s,[string]$marker){
 $a=$s.IndexOf($marker);if($a -lt 0){throw "Missing $marker"};$b=$s.IndexOf('{',$a);$n=1;$e=$b+1
 while($n -gt 0){if($s[$e] -eq '{'){$n++};if($s[$e] -eq '}'){$n--};$e++};$s.Substring($a,$e-$a)
}
$loot=Get-Content "$root/src/TerraKnight/Items/ProgressionLoot.cs" -Raw
$data=Get-Content "$root/src/TerraKnight/Items/ProgressionBalanceData.cs" -Raw
$enum=Extract (Get-Content "$root/src/TerraKnight/Items/Items.cs" -Raw) 'internal enum ItemType'
$resource=(Extract $data 'internal static readonly Dictionary<ItemType, int> ResourceStages')+';'
$methods=(Extract $loot 'internal static bool RegionAllows(')+"`n"+(Extract $loot 'static void AddRegionalMaterials(')
$test=@'
using System;using System.Collections.Generic;
namespace TerraKnight.World {static class Areas {public static string AreaName(string s)=>s;}}
namespace TerraKnight.Items {
ENUM
static class ProgressionBalance {
RESOURCE
public static bool Unlocked(ItemType t,int s,bool entry,bool final)=>!ResourceStages.TryGetValue(t,out int gate)||s>=gate;
}
static class ProgressionLoot {
static float Value(Dictionary<ItemType,float> p,ItemType t)=>p.TryGetValue(t,out float n)?n:0;
METHODS
static int checks;
static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
public static string Run(){
foreach(string area in new[]{"苍绿之径","真菌荒地","王后花园"}){
var ore=new Dictionary<ItemType,float>();var mat=new Dictionary<ItemType,float>();AddRegionalMaterials(area,0,false,false,false,ore,mat);
foreach(var t in new[]{ItemType.JungleSpores,ItemType.Vine,ItemType.Stinger})Check(mat.ContainsKey(t)&&RegionAllows(t,area,false),area+" starter ingredient missing "+t);
Check(!ore.ContainsKey(ItemType.ChlorophyteOre),"early Chlorophyte");
}
foreach(string area in new[]{"王国边缘","愚人斗兽场"})foreach(int s in new[]{2,3,4}){
var ore=new Dictionary<ItemType,float>();var mat=new Dictionary<ItemType,float>();AddRegionalMaterials(area,s,false,false,false,ore,mat);
foreach(var t in new[]{ItemType.CobaltOre,ItemType.PalladiumOre,ItemType.MythrilOre,ItemType.OrichalcumOre,ItemType.AdamantiteOre,ItemType.TitaniumOre})Check(ore.ContainsKey(t)==(s>=3),area+" wrong ore gate "+t+"/"+s);
}
foreach(string area in new[]{"水晶山峰","雾之峡谷"})Check(RegionAllows(ItemType.SoulofLight,area,false),"light source lost");
foreach(string area in new[]{"远古盆地","深巢","野兽巢穴","深渊"})Check(RegionAllows(ItemType.SoulofNight,area,false),"night source lost");
foreach(var t in new[]{ItemType.SoulofLight,ItemType.SoulofNight,ItemType.SoulofFlight,ItemType.PixieDust,ItemType.UnicornHorn,ItemType.CursedFlame,ItemType.Ichor,ItemType.CrystalShard}){
Check(!ProgressionBalance.Unlocked(t,2,true,true),"pre-Hardmode leak "+t);Check(ProgressionBalance.Unlocked(t,3,false,false),"Boss flag blocks early Hardmode "+t);
}
foreach(var t in new[]{ItemType.ChlorophyteOre,ItemType.HallowedBar,ItemType.SoulofMight,ItemType.SoulofSight,ItemType.SoulofFright})Check(!ProgressionBalance.Unlocked(t,3,true,true),"mechanical material moved early "+t);
return "PASS "+checks+" production resource/region checks (area-name fixture; not gameplay)";
}
}}
public static class ResourceGateHarness {public static string Run()=>TerraKnight.Items.ProgressionLoot.Run();}
'@
$test=$test.Replace('ENUM',$enum).Replace('RESOURCE',$resource).Replace('METHODS',$methods)
Add-Type -TypeDefinition $test -CompilerOptions '/warn:0'
[ResourceGateHarness]::Run()
