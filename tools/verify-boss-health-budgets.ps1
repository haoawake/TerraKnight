$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Method([string]$text,[string]$marker) {
 $start=$text.IndexOf($marker);if($start -lt 0){throw "Missing $marker"}
 $brace=$text.IndexOf('{',$start);$depth=1;$end=$brace+1
 $semi=$text.IndexOf(';',$start);if($semi -ge 0 -and $semi -lt $brace){return $text.Substring($start,$semi-$start+1)}
 while($depth -gt 0){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
 $text.Substring($start,$end-$start)
}
$phases=Get-Content (Join-Path $root 'src/TerraKnight/Items/BossPhases.cs') -Raw
$phaseMethods=@('public static bool Of(','public static float WholeFactor(','static int Sisters(','static string BaseName(','static int Number(') | ForEach-Object {Method $phases $_}
$phaseFactor=[regex]::Match($phases,'(?s)public static float Factor\(.*?;').Value
$phaseMult=[regex]::Match($phases,'public static float Mult\(.*?;').Value
$phaseConstants=[regex]::Match($phases,'(?s)public const float Step.*?const float Armour.*?;').Value
$stub=@'
using System;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {public static class Mathf{
 public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));
 public static int Clamp(int x,int a,int b)=>Math.Max(a,Math.Min(b,x));public static int RoundToInt(float x)=>(int)Math.Round(x);
}}
namespace TerraKnight.World {public enum DifficultyLevel{Normal,Hard,Expert,Master}}
public class GameObject {public string name;public GameObject(string n){name=n;}}
public class HealthManager {public GameObject gameObject;public int heads;public HealthManager(string n){gameObject=new GameObject(n);}public int GetInstanceID()=>GetHashCode();}
namespace TerraKnight.Items {
 public enum ItemType{SuperHealingPotion,BottledHoney,HealingPotion,LesserHealingPotion,GreaterHealingPotion}
 internal static class EnemyDrops {
  public static int VariantLevel(string s)=>s=="Dream_01_False_Knight"||s=="Dream_02_Mage_Lord"||s=="Dream_03_Infected_Knight"||s=="GG_Failed_Champion"||s=="GG_Soul_Tyrant"||s=="GG_Lost_Kin"?5:
   s=="GG_False_Knight"||s=="GG_Soul_Master"||s=="GG_Broken_Vessel"?-1:s=="Deepnest_East_Hornet"?4:5;
 }
 internal static class EnemyStats {
  public class Info {public HealthManager Hm;public bool Boss=true;public BossStats.Ref Ref;public string Record,Scene;public float StunCarry;}
  public static int LifePhase(Info i)=>100;
 }
 internal static class GodhomeBalance {
  public class Profile {public float Life=1.3f;}
  public static Profile Current(string s,float tier=1)=>new Profile();
 }
 internal static class BossPhases {
  __CONSTANTS__
  __MULT__
  static int HeadsBeaten(HealthManager h)=>h.heads;
  internal static HashSet<int> oroUp=new HashSet<int>();
  __METHODS__
  __FACTOR__
 }
 public static class HealthBudgetChecks {
 static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static float Unit(BossStats.Ref r,string scene,TerraKnight.World.DifficultyLevel d)=>BossStats.LifeOf(r,scene,d)*BossBalance.Of(r.Record,EnemyDrops.VariantLevel(scene)).Life;
 static float Body(BossStats.Ref r,string scene,string name,int heads=0,bool up=false){
  var h=new HealthManager(name){heads=heads};if(up)BossPhases.oroUp.Add(h.GetInstanceID());
  return Unit(r,scene,TerraKnight.World.DifficultyLevel.Normal)*BossPhases.Factor(new EnemyStats.Info{Hm=h,Ref=r,Record=r.Record,Scene=scene});
 }
 public static string Run(){
  var report=new List<string>{"Key,NormalWhole,HardWhole,ExpertWhole,MasterWhole"};
  foreach(var entry in BossStats.DevAll){
   var r=entry.Value;string scene=r.Key=="FalseKnight@5"?"Dream_01_False_Knight":r.Key=="MageLord@5"?"Dream_02_Mage_Lord":r.Key=="InfectedKnight@5"?"Dream_03_Infected_Knight":"world";
   if(!BossHealthBudgets.World.ContainsKey(r.Key))continue;
   var values=new List<string>{r.Key};float last=0;
   foreach(TerraKnight.World.DifficultyLevel d in Enum.GetValues(typeof(TerraKnight.World.DifficultyLevel))){
    float whole=Unit(r,scene,d)*BossPhases.WholeFactor(r.Record,scene);
    float expected=(float)BossHealthBudgets.World[r.Key]*r.Boss.LifeAt(BossStats.Tier(d))/r.Boss.LifeAt(0);
    Check(Math.Abs(whole-expected)<5,r.Key+" budget compounded with tune or phase shrink");
    Check(whole>last,r.Key+" difficulty did not increase");last=whole;values.Add(Math.Round(whole).ToString());
   }report.Add(string.Join(",",values));
  }
  var falseDream=BossStats.Of("FalseKnight",5);float sum=0;
  for(int phase=0;phase<3;phase++)sum+=Body(falseDream,"Dream_01_False_Knight","False Knight New",phase)+Body(falseDream,"Dream_01_False_Knight","Head",phase);
  sum+=Body(falseDream,"Dream_01_False_Knight","Head",3);Check(Math.Abs(sum-100000)<5,"Failed Champion whole-fight sum");
  Check(Math.Abs(Body(falseDream,"Dream_01_False_Knight","False Knight New")/Body(falseDream,"Dream_01_False_Knight","Head")-10f/3f)<.001,"head/armor reversed");
  var soul=BossStats.Of("MageLord",5);sum=Body(soul,"Dream_02_Mage_Lord","Dream Mage Lord")+Body(soul,"Dream_02_Mage_Lord","Dream Mage Lord Phase2");Check(Math.Abs(sum-120000)<5,"Soul Tyrant two-phase sum");
  var mantis=BossStats.Of("MantisLord",-1);foreach(int sisters in new[]{2,3}){
   string room=sisters==2?"GG_Mantis_Lords":"GG_Mantis_Lords_V";sum=Body(mantis,room,"Mantis Lord");
   for(int i=1;i<=sisters;i++)sum+=Body(mantis,room,"Mantis Lord S"+i);Check(Math.Abs(sum-(sisters==2?130000:170000)*1.3f)<5,"Mantis multi-body budget");
  }
  var watcher=BossStats.Of("BlackKnight",-1);sum=0;for(int i=1;i<=6;i++)sum+=Body(watcher,"GG_Watcher_Knights","Black Knight "+i);Check(Math.Abs(sum-145000*1.3f)<5,"Watcher shared phases sum");
  var brothers=BossStats.Of("NailBros",-1);sum=Body(brothers,"GG_Nailmasters","Oro")+Body(brothers,"GG_Nailmasters","Oro",0,true)+Body(brothers,"GG_Nailmasters","Mato");Check(Math.Abs(sum-140000*1.3f)<5,"Oro/Mato refill/body budget");
  var ob=BossStats.Of("Oblobble",-1);sum=Body(ob,"GG_Oblobbles","Mega Fat Bee")+Body(ob,"GG_Oblobbles","Mega Fat Bee (1)");Check(Math.Abs(sum-90000*1.3f)<5,"Oblobbles each got the whole budget");
  var early=BossStats.Of("FalseKnight",-1);float old=early.Boss.LifeAt(0)*BossStats.CombatLifeShare(early.Stage);Check(Math.Abs(BossStats.LifeOf(early,"Crossroads_10",TerraKnight.World.DifficultyLevel.Normal)-old)<1,"early-world health changed");
  foreach(var e in BossStats.DevAll)Check(BossHealthBudgets.TryTotal(e.Value,"GG_test",TerraKnight.World.DifficultyLevel.Normal,out _),"missing individual rematch budget "+e.Key);
  foreach(var e in BossHealthBudgets.Rooms){
   var r=BossStats.Of(e.Key=="GG_Hornet_2"?"Hornet":e.Key=="GG_Crystal_Guardian_2"?"MegaBeamMiner":e.Key=="GG_Mantis_Lords_V"?"MantisLord":e.Key=="GG_Nosk_Hornet"?"MimicSpider":"FinalBoss",-1);
   Check(BossHealthBudgets.TryTotal(r,e.Key,TerraKnight.World.DifficultyLevel.Normal,out float total)&&Math.Abs(total-e.Value)<1,"Godhome variant budget missing "+e.Key);
  }
  System.IO.File.WriteAllLines("artifacts/boss-health-budgets.csv",report);
  return "PASS "+checks+" individual health/difficulty/native phase/body checks; no live boss test";
 }
 }
}
'@
$stub=$stub.Replace('__CONSTANTS__',$phaseConstants).Replace('__MULT__',$phaseMult).Replace('__METHODS__',($phaseMethods -join [Environment]::NewLine)).Replace('__FACTOR__',$phaseFactor)
$taskStub=Join-Path ([IO.Path]::GetTempPath()) ('boss-health-'+[guid]::NewGuid().ToString('N')+'.cs')
try{
 [IO.File]::WriteAllText($taskStub,$stub)
 Add-Type -Path @((Join-Path $root 'src/TerraKnight/Items/BossStats.cs'),(Join-Path $root 'src/TerraKnight/Items/BossHealthBudgets.cs'),(Join-Path $root 'src/TerraKnight/Items/BossBalance.cs'),$taskStub) -CompilerOptions '/nowarn:0649' -WarningAction SilentlyContinue
 [TerraKnight.Items.HealthBudgetChecks]::Run()
}finally{Remove-Item -LiteralPath $taskStub -ErrorAction SilentlyContinue}
