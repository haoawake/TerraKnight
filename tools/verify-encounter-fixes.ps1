$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$wake = Get-Content (Join-Path $root 'src/TerraKnight/Items/BossWake.cs') -Raw
$wake = [regex]::Replace($wake, '(?m)^\s*\[HarmonyLib\.[^\r\n]+', '')
$stub = @'
using System;using System.Collections.Generic;
namespace UnityEngine {public static class Time {public static float time;} }
class GameObject {public string name="boss";public Scene scene=new Scene();public HealthManager hm;public T GetComponent<T>()where T:class=>hm as T;}
class Scene {public string name="arena";}
class HealthManager {static int next;int id=++next;public bool isDead,IsInvincible;public GameObject gameObject=new GameObject();public string name=>gameObject.name;public PlayMakerFSM[] fsms=new PlayMakerFSM[0];public HealthManager(){gameObject.hm=this;}public int GetInstanceID()=>id;public T[] GetComponents<T>()=>fsms as T[];}
class PlayMakerFSM {public string FsmName;public HutongGames.PlayMaker.Fsm Fsm;public Vars FsmVariables=new Vars();public string ActiveStateName=>Fsm.ActiveState.Name;public void SendEvent(string e){} }
class Vars {public BoolVar FindFsmBool(string n)=>null;}class BoolVar {public bool Value;}
namespace HutongGames.PlayMaker {class Fsm {public GameObject GameObject;public FsmState ActiveState;}class FsmState {public string Name;public Fsm Fsm;public Transition[] Transitions=new Transition[0];}class Transition {public string EventName;} }
namespace TerraKnight {static class Plugin {public static Logger Log=new Logger();}class Logger {public void LogInfo(string s){}}static class GameHooks {public static bool PlayerHasControl()=>true;} }
namespace TerraKnight.Items {class Info {public bool Boss=true;}static class EnemyStats {public static Info Of(HealthManager h)=>new Info();public static bool Defeated(HealthManager h)=>false;} }
'@
$tests = @'
namespace TerraKnight.Items {public static class EncounterScenarios {
 static int checks;static void Check(bool value,string msg){if(!value)throw new Exception(msg);checks++;}
 static PlayMakerFSM F(HealthManager hm,string name,string state){var f=new PlayMakerFSM{FsmName=name,Fsm=new HutongGames.PlayMaker.Fsm{GameObject=hm.gameObject}};State(f,state);return f;}
 static void State(PlayMakerFSM f,string name){f.Fsm.ActiveState=new HutongGames.PlayMaker.FsmState{Name=name,Fsm=f.Fsm};}
 static void Enter(PlayMakerFSM f,string n){State(f,n);typeof(BossWake).GetMethod("Entered",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{f.Fsm.ActiveState});}
 public static string Run(){
  var hm=new HealthManager();var main=F(hm,"Control","Roar");var aux=F(hm,"Stun Control","Idle");hm.fsms=new[]{main,aux};
  Check(BossWake.Hit(hm)==BossWake.Result.Held,"scene-triggered roar damageable");
  UnityEngine.Time.time=2;Enter(aux,"Idle");Check(BossWake.Hit(hm)==BossWake.Result.Held,"auxiliary Idle released intro");
  Enter(main,"Roar End");Check(BossWake.Hit(hm)==BossWake.Result.Held,"roar end damageable");
  Enter(main,"Idle");Check(BossWake.Hit(hm)==BossWake.Result.Normal,"combat still protected");
  Enter(main,"Roar");Check(BossWake.Hit(hm)==BossWake.Result.Normal,"ordinary attack roar protected");
  var traitor=new HealthManager();var mantis=F(traitor,"Mantis","Roar");traitor.fsms=new[]{mantis};Check(BossWake.Hit(traitor)==BossWake.Result.Held,"Traitor intro damageable");Enter(mantis,"Active");Check(BossWake.Hit(traitor)==BossWake.Result.Normal,"Traitor combat protected");
  hm.gameObject.scene.name="Room_Final_Boss_Core";State(main,"H Collapsed");Check(BossWake.DreamWindow(hm),"dream entry window missed");State(main,"Idle");Check(!BossWake.DreamWindow(hm),"normal HK combat suppressed");hm.gameObject.scene.name="other";State(main,"H Collapsed");Check(!BossWake.DreamWindow(hm),"dream protection leaked to other room");
  return "PASS "+checks+" actual intro/dream-window behavior checks";
 }
} }
'@
# Keep file-level usings at the beginning of the combined compilation unit.
$wake = $wake.Replace('using System.Collections.Generic;', '').Replace('using UnityEngine;', '')
$stub = "using UnityEngine;`n" + $stub
Add-Type -TypeDefinition ($stub + $wake + $tests) -CompilerOptions '/warn:0'
[TerraKnight.Items.EncounterScenarios]::Run()

$arena = Get-Content (Join-Path $root 'src/TerraKnight/Items/BossArena.cs') -Raw
foreach ($id in @('Fungus3_23/Battle Scene','Ruins2_11/Battle Scene')) {if (!$arena.Contains($id)) {throw "Missing native state $id"}}
$drink = Get-Content (Join-Path $root 'src/TerraKnight/TerraMod.cs') -Raw
if (!$drink.Contains('PlayerBuffs.DrankManaPotion();')) {throw 'Shared mana potion path missing sickness'}
$dream = Get-Content (Join-Path $root 'src/TerraKnight/Items/DreamNailItem.cs') -Raw
if (!$dream.Contains('BossWake.DreamWindow(hm)')) {throw 'Dream nail bonus can interrupt entry'}
'PASS native reset IDs, shared potion path and dream nail guard wiring; no gameplay test'

$buffs = Get-Content (Join-Path $root 'src/TerraKnight/Player/PlayerBuffs.cs') -Raw
$dialogue = Get-Content (Join-Path $root 'src/TerraKnight/UI/DialogueBroadcast.cs') -Raw
$manaMethods = [regex]::Match($buffs, 'public static void DrankManaPotion\(\)[\s\S]*?public static float ManaSicknessPenalty[^;]+;').Value
$boxMethod = [regex]::Match($dialogue, 'internal static bool SuppressBox[^;]+;').Value
if (!$manaMethods -or !$boxMethod) {throw 'Missing mana/dialogue policy methods'}
$policies = @'
using System;
static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));}
public static class EncounterPolicies {
 const int ManaSickness=94;static int ticks;static int TimeLeft(int id)=>ticks;static void Add(int id,int n){ticks=n;}
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static string Run(){
  Check(ManaSicknessPenalty==0,"penalty without sickness");DrankManaPotion();Check(ticks==300&&Math.Abs(ManaSicknessPenalty-.25f)<.0001,"first mana drink");
  DrankManaPotion();Check(ticks==600&&ManaSicknessPenalty==.5f,"stacked mana drink");DrankManaPotion();Check(ticks==600,"duration cap");
  ticks=60;Check(Math.Abs(ManaSicknessPenalty-.05f)<.0001,"decaying penalty");ticks=0;Check(ManaSicknessPenalty==0,"expired penalty");
  Check(SuppressBox("Box Open","BOX UP"),"plain black box shown");Check(SuppressBox("Box Open Dream","BOX UP DREAM"),"dream black box shown");
  Check(!SuppressBox("Box Open YN","BOX UP YN"),"choice box suppressed");Check(!SuppressBox("Box Open","BOX DOWN"),"close event suppressed");
  return "PASS 10 actual mana stack/decay and dialogue policy checks";
 }
}
'@
$policies = $policies.Insert($policies.LastIndexOf('}'), $manaMethods + "`n" + $boxMethod + "`n")
Add-Type -TypeDefinition $policies
[EncounterPolicies]::Run()

