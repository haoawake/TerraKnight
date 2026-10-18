$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'src/TerraKnight/Items/AccessoryEffects.cs') -Raw
function Method([string]$marker){
 $start=$source.IndexOf($marker);if($start -lt 0){throw "Missing $marker"}
 $brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$stub=@'
using System;using System.Collections.Generic;using UnityEngine;using Random=UnityEngine.Random;
namespace UnityEngine {
 public struct Vector3{public static Vector3 zero=>new Vector3();public static Vector3 up=>new Vector3();public static Vector3 operator+(Vector3 a,Vector3 b)=>a;}
 public class Transform{public Vector3 position;}public class GameObject{public Scene scene=new Scene();}public class Scene{public string name="arena";}
 public static class Random{public static Queue<int> rolls=new Queue<int>();public static int Range(int a,int b){int n=rolls.Dequeue();if(n<a||n>=b)throw new Exception("random range changed");return n;}}
}
public class HealthManager{public int small,medium,large;public string record="enemy";public Transform transform=new Transform();public GameObject gameObject=new GameObject();public GameObject smallPrefab=new GameObject(),largePrefab=new GameObject();}
static class FlingUtils {public class Config{public GameObject Prefab;public int AmountMin,AmountMax;public float SpeedMin,SpeedMax,AngleMin,AngleMax;}public static Config last;public static int calls;public static void SpawnAndFling(Config c,Transform t,Vector3 v){last=c;calls++;}}
namespace TerraKnight.UI {public static class CombatText{public static int Notice;public static int shown;public static void Show(Vector3 v,string text,int c){shown++;}}}
namespace TerraKnight.Items {
class Info{public string Record;}static class EnemyStats{public static Info Of(HealthManager h)=>new Info{Record=h.record};}
static class EnemyDrops {public static int worth;public static int GeoOf(string r)=>worth;public static string KeyFor(string r,int v)=>r;public static int VariantLevel(string s)=>0;}
static class Shop{public static long earned;public static void Earn(long n){earned+=n;}public static string Money(long n)=>n.ToString();}
public static class LuckyCoinScenarios {
 const float Px=1f;
 static int DevCoins,checks;static int smallGeo(HealthManager h)=>h.small;static int mediumGeo(HealthManager h)=>h.medium;static int largeGeo(HealthManager h)=>h.large;static GameObject smallGeoPrefab(HealthManager h)=>h.smallPrefab;static GameObject largeGeoPrefab(HealthManager h)=>h.largePrefab;
 static void Check(bool b,string msg){checks++;if(!b)throw new Exception(msg);}
 static void Rolls(params int[] n){Random.rolls=new Queue<int>(n);}
 static void Clear(){Shop.earned=0;FlingUtils.calls=0;FlingUtils.last=null;TerraKnight.UI.CombatText.shown=0;DevCoins=0;EnemyDrops.worth=0;Rolls();}
 public static string Run(){
  Clear();LuckyCoin(null,1,false);Check(DevCoins==0,"null target rewarded");
  var h=new HealthManager();LuckyCoin(h,1,false);Check(DevCoins==0,"zero-value enemy rewarded");
  h.small=1;Rolls(1);LuckyCoin(h,1,false);Check(DevCoins==0&&Shop.earned==0,"non-trigger roll rewarded");
  Rolls(0,1,1,7);LuckyCoin(h,1,true);Check(Shop.earned==7&&DevCoins==7,"melee copper amount");Check(TerraKnight.UI.CombatText.shown==1,"copper change invisible");Check(FlingUtils.calls==0,"copper paid twice");
  Clear();h.small=1;Rolls(0,1,1,7);LuckyCoin(h,-1,false);Check(Shop.earned==1&&DevCoins==1,"projectile/minion stack is not one");
  Clear();Rolls(0,0,1,9);LuckyCoin(h,1,true);Check(FlingUtils.calls==1&&FlingUtils.last.Prefab==h.smallPrefab&&FlingUtils.last.AmountMin==9&&FlingUtils.last.AmountMax==9,"silver pieces");Check(Shop.earned==0,"spawned silver credited twice");
  Clear();Rolls(0,1,0,9);LuckyCoin(h,-1,true);Check(FlingUtils.calls==1&&FlingUtils.last.Prefab==h.largePrefab&&FlingUtils.last.AmountMin==36,"gold-to-geo conversion");Check(FlingUtils.last.AngleMin==110&&FlingUtils.last.AngleMax==170,"coin fling direction");
  Clear();h.smallPrefab=null;Rolls(0,0,1,3);LuckyCoin(h,1,true);Check(Shop.earned==300&&FlingUtils.calls==0&&TerraKnight.UI.CombatText.shown==1,"missing silver prefab loses money");
  Clear();h.largePrefab=null;Rolls(0,1,0,3);LuckyCoin(h,1,true);Check(Shop.earned==30000&&FlingUtils.calls==0,"missing gold prefab loses money");
  Clear();h.small=0;EnemyDrops.worth=20;Rolls(0,1,1,4);LuckyCoin(h,1,false);Check(Shop.earned==1,"summoned enemy with empty native geo fields excluded");
  Clear();h.record=null;EnemyDrops.worth=20;LuckyCoin(h,1,false);Check(Shop.earned==0,"unknown zero-value enemy rewarded");
  Clear();h.record="enemy";h.small=1;
  for(int i=0;i<100;i++){if(i%5==0)Rolls(0,1,1,6);else Rolls(i%5);LuckyCoin(h,1,false);}
  Check(DevCoins==20&&Shop.earned==20,"one-in-five trigger changed");
  Check(GeoGrabRange(false,false)==42,"base geo reach");Check(GeoGrabRange(false,true)==192,"Treasure Magnet cannot attract Geo");Check(GeoGrabRange(true,false)==392,"Gold Ring range changed");Check(GeoGrabRange(true,true)==392,"money pickup bonuses stack instead of taking maximum");
  return "PASS "+checks+" actual lucky-coin eligibility/roll/payout/visibility checks; no gameplay test";
 }
}
}
'@
$stub=$stub.Insert($stub.LastIndexOf('}', $stub.LastIndexOf('}')-1),(Method 'public static void LuckyCoin(')+"`n"+(Method 'static bool CoinTarget(')+"`n"+[regex]::Match($source,'internal static float GeoGrabRange[^;]+;').Value+"`n")
Add-Type -TypeDefinition $stub -CompilerOptions '/warn:0'
[TerraKnight.Items.LuckyCoinScenarios]::Run()
$hits=Get-Content (Join-Path $root 'src/TerraKnight/Items/MeleeHits.cs') -Raw
$projectiles=Get-Content (Join-Path $root 'src/TerraKnight/Items/TerraProjectilesAcc.cs') -Raw
if(([regex]::Matches($hits,'AccessoryEffects\.LuckyCoin\(')).Count -ne 1 -or $projectiles.Contains('AccessoryEffects.LuckyCoin(')){throw 'Lucky Coin double-roll/caller-specific wiring'}
$hook=$hits.IndexOf('AccessoryEffects.LuckyCoin(')
if($hook -lt $hits.IndexOf('info.Carry = carryBefore') -or $hook -lt $hits.IndexOf('ArmorEffects.OnHitNPC(')){throw 'Lucky Coin triggered before hit acceptance'}
if(!$hits.Contains('if (Equipment.Current.LuckyCoin) AccessoryEffects.LuckyCoin(')){throw 'Lucky Coin equipment guard missing'}
$defs=Get-Content (Join-Path $root 'src/TerraKnight/Items/ItemsAccessories2.cs') -Raw
foreach($type in @('LuckyCoin','CoinRing','GreedyRing')){if(![regex]::IsMatch($defs,'ItemType\.'+$type+'[\s\S]*?LuckyCoin = true')){throw "Composite accessory flag missing: $type"}}
'PASS shared accepted-hit hook, no duplicate projectile roll, and composite accessory wiring'


if(!$source.Contains('if (b.GoldRing || b.TreasureMagnet) PullGeo(box);') -or !$source.Contains('GeoGrabRange(Equipment.Current.GoldRing, Equipment.Current.TreasureMagnet)')){throw 'Magnet Geo attraction wiring missing'}
'PASS Treasure Magnet/Gold Ring shared Geo attraction wiring'
