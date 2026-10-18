$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Method([string]$file,[string]$marker){
 $text=Get-Content -LiteralPath $file -Raw;$start=$text.IndexOf($marker)
 if($start -lt 0){throw "Missing $marker"};$brace=$text.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
 $text.Substring($start,$end-$start)
}
$smart=Get-Content (Join-Path $root 'src/TerraKnight/Items/SmartCursor.cs') -Raw
$smart=$smart.Substring(0,$smart.IndexOf('        static Sprite highlight;'))+'}}'
$flames=Get-Content (Join-Path $root 'src/TerraKnight/UI/WorldMapFlames.cs') -Raw
$flames=$flames.Substring(0,$flames.IndexOf('        static Texture2D grimmFlameIcon;'))+'}}'
$solar=Method (Join-Path $root 'src/TerraKnight/Items/ArmorEffects.cs') 'internal static int SolarFacing('
$shake=Method (Join-Path $root 'src/TerraKnight/Player/TerraCamera.cs') 'static void LockedShake('
$stub=@'
using System;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {
 public struct Vector2{public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);public static float Distance(Vector2 a,Vector2 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
 public struct Vector2Int{public int x,y;public Vector2Int(int a,int b){x=a;y=b;}}
 public struct Vector3{public float x,y,z;}
 public class Transform{public Vector3 position;}
 public struct Bounds{public Vector3 center,min;}
 public static class Mathf{public static int Abs(int x)=>Math.Abs(x);public static float Abs(float x)=>Math.Abs(x);public static int FloorToInt(float x)=>(int)Math.Floor(x);}
}
public class HeroController{public State cState=new State();public class State{public bool facingRight;}}
public class PlayerData{
 public bool worn,gotBrummsFlame;public int grimmChildLevel=1,flamesCollected,flamesRequired=3;public List<string> scenesFlameCollected=new List<string>();
 public bool GetBool(string s)=>worn;
}
public class Bool{public bool Value;}
namespace HutongGames.PlayMaker.Actions{public class ShakePositionV2{public Transform target;public Vector3 start;public Bool IsCameraShake=new Bool();}}
namespace TerraKnight.World {
 public enum CellKind{Empty,Original,Platform}
 public static class CellGrid{public const float CellSize=1;}
 public static class TerraWorld{
  public static Dictionary<(int,int),CellKind> cells=new Dictionary<(int,int),CellKind>();
  public static CellKind Get(int x,int y)=>cells.TryGetValue((x,y),out var v)?v:CellKind.Empty;
  public static bool CanDig(int x,int y)=>Get(x,y)==CellKind.Original;
 }
 public static class DugHazards{public static HashSet<(int,int)> cells=new HashSet<(int,int)>();public static bool IsSpikeCell(Vector2Int c)=>cells.Contains((c.x,c.y));}
}
namespace TerraKnight{public class TerraMod{public static TerraMod Instance=new TerraMod();public bool IsUsingItem;public float UseDirection=1;}}
namespace TerraKnight.Items{internal static class ArmorEffects{__SOLAR__}}
namespace TerraKnight.Player{internal static class TerraCamera{
 public static bool Active;
 
 
 __SHAKE__
 public static float TestShake(float native){LockedShake(ref native);return native;}
}}
public static class GameplayFeatureChecks{
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static int Count(PlayerData pd){int count=0;foreach(var f in TerraKnight.UI.WorldMap.GrimmFlames)if(TerraKnight.UI.WorldMap.FlameAvailable(f,pd))count++;return count;}
 public static string Run(){
  var box=new Bounds{center=new Vector3{x=.625f,y=1.3125f},min=new Vector3{x=0,y=0}};
  for(int y=0;y<3;y++)TerraKnight.World.TerraWorld.cells[(5,y)]=TerraKnight.World.CellKind.Original;
  TerraKnight.World.DugHazards.cells.Add((3,1));
  Vector2Int target;bool hit=TerraKnight.Items.SmartCursor.PickaxeTarget(box,new Vector2(6,1.3125f),c=>true,out target);
  Check(hit&&target.x==3&&target.y==1,"spike before terrain not selectable");
  TerraKnight.World.DugHazards.cells.Clear();hit=TerraKnight.Items.SmartCursor.PickaxeTarget(box,new Vector2(6,1.3125f),c=>true,out target);Check(hit&&target.x==5,"dug hazard still blocks smart cursor");
  TerraKnight.World.TerraWorld.cells.Clear();TerraKnight.World.DugHazards.cells.Add((-2,1));
  hit=TerraKnight.Items.SmartCursor.PickaxeTarget(box,new Vector2(-6,1.3125f),c=>true,out target);Check(hit&&target.x==-2,"left-facing thorn not selectable");
  TerraKnight.World.DugHazards.cells.Clear();TerraKnight.World.DugHazards.cells.Add((0,5));TerraKnight.World.TerraWorld.cells[(0,3)]=TerraKnight.World.CellKind.Platform;
  hit=TerraKnight.Items.SmartCursor.PickaxeTarget(box,new Vector2(.625f,7),c=>true,out target);Check(hit&&target.y==5,"vertical thorn or platform ray regression");
  Check(!TerraKnight.Items.SmartCursor.PickaxeTarget(box,new Vector2(.625f,7),c=>false,out target),"hazard ignores mining reach");
  var hero=new HeroController();var mod=TerraKnight.TerraMod.Instance;hero.cState.facingRight=false;mod.UseDirection=1;
  Check(TerraKnight.Items.ArmorEffects.SolarFacing(hero)==-1,"idle solar shield uses stale right weapon direction");
  hero.cState.facingRight=true;mod.UseDirection=-1;Check(TerraKnight.Items.ArmorEffects.SolarFacing(hero)==1,"idle solar shield uses stale left weapon direction");
  mod.IsUsingItem=true;Check(TerraKnight.Items.ArmorEffects.SolarFacing(hero)==-1,"using-item shield differs from visible player direction");mod.UseDirection=1;Check(TerraKnight.Items.ArmorEffects.SolarFacing(hero)==1,"using-item right direction");
  TerraKnight.Player.TerraCamera.Active=false;Check(TerraKnight.Player.TerraCamera.TestShake(.65f)==.65f,"unlocked native camera setting changed");
  TerraKnight.Player.TerraCamera.Active=true;Check(TerraKnight.Player.TerraCamera.TestShake(1f)==0f,"locked camera still shakes");
  TerraKnight.Player.TerraCamera.Active=false;Check(TerraKnight.Player.TerraCamera.TestShake(.65f)==.65f,"toggle did not restore native shake preference");
  var pd=new PlayerData{worn=true};Check(Count(pd)==3,"level-one flame set");pd.grimmChildLevel=2;Check(Count(pd)==3,"level-two flame set");
  pd.scenesFlameCollected.Add("Tutorial_01");Check(Count(pd)==2,"collected flame still shown");pd.grimmChildLevel=3;Check(Count(pd)==4,"nightmare/Brumm flames missing");
  pd.gotBrummsFlame=true;Check(Count(pd)==3,"Brumm reward flag ignored");pd.flamesCollected=3;Check(Count(pd)==0,"completed collection still shows flames");
  pd.flamesCollected=0;pd.worn=false;Check(Count(pd)==0,"unequipped Grimmchild reveals quest flames");pd.worn=true;pd.grimmChildLevel=4;Check(Count(pd)==0,"finished Grimmchild still shows targets");
  return "PASS "+n+" smart hazard/camera lock/solar facing/quest-state checks; no live UI test";
 }
}
'@
$stub=$stub.Replace('__SOLAR__',$solar).Replace('__SHAKE__',$shake)
$taskFiles=@();try{
 foreach($source in @($smart,$flames,$stub)){
  $path=Join-Path ([IO.Path]::GetTempPath()) ('gameplay-fixes-'+[guid]::NewGuid().ToString('N')+'.cs');[IO.File]::WriteAllText($path,$source);$taskFiles+=$path
 }
 Add-Type -Path $taskFiles -WarningAction SilentlyContinue -CompilerOptions '/nowarn:0649'
 [GameplayFeatureChecks]::Run()
}finally{foreach($path in $taskFiles){Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue}}
