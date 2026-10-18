# Exercise the actual scheduler with controlled animation, FSM and scene lifecycles.
# These are managed scenario tests, not visual validation of native boss animations.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stub = @'
using System;
using System.Collections.Generic;
using System.Reflection;
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.All,AllowMultiple=true)] public class HarmonyPatch:Attribute { public HarmonyPatch(){} public HarmonyPatch(Type t,string s){} }
 public class HarmonyPostfix:Attribute{} public class HarmonyPrefix:Attribute{}
 public static class AccessTools {
  public delegate ref F FieldRef<T,F>(T x);
  static ref UnityEngine.GameObject Corpse(EnemyDeathEffects x)=>ref x.corpse;
  public static FieldRef<T,F> FieldRefAccess<T,F>(string s)=>(FieldRef<T,F>)(object)(FieldRef<EnemyDeathEffects,UnityEngine.GameObject>)Corpse;
 }
}
namespace UnityEngine {
 public enum FindObjectsInactive{Exclude,Include} public enum FindObjectsSortMode{None}
 public class Object {public static List<object> objects=new List<object>();public static T[] FindObjectsByType<T>(FindObjectsInactive a,FindObjectsSortMode b){var r=new List<T>();foreach(var o in objects)if(o is T t)r.Add(t);return r.ToArray();}}
 public static class Time { public static float time; }
 public struct Vector2 { public float x,y; public static implicit operator Vector2(Vector3 v)=>new Vector2{x=v.x,y=v.y}; }
 public struct Vector3 { public float x,y,z; public float sqrMagnitude=>x*x+y*y+z*z; }
 public struct Bounds { public Vector3 center,size; public void Encapsulate(Bounds b){} }
 public class Transform { public Vector3 position; public bool IsChildOf(Transform t)=>this==t; }
 public class GameObject {
  public string name="boss"; public bool activeInHierarchy=true; public SceneManagement.Scene scene=new SceneManagement.Scene();
  public Transform transform=new Transform(); public List<object> components=new List<object>();
  public T[] GetComponentsInChildren<T>() { var result=new List<T>();foreach(var x in components)if(x is T t)result.Add(t);return result.ToArray(); }
 }
 public class Renderer { public bool enabled=true; public Bounds bounds=new Bounds{size=new Vector3{x=1,y=1}}; }
 public class ParticleSystemRenderer:Renderer{} public class TrailRenderer:Renderer{} public class LineRenderer:Renderer{}
 public class Collider2D:Renderer{}
 public struct AnimatorStateInfo {public bool loop;public float normalizedTime;}
 public class Animator {public bool enabled=true; public int layerCount=1; public bool transition;public AnimatorStateInfo state; public bool IsInTransition(int i)=>transition;public AnimatorStateInfo GetCurrentAnimatorStateInfo(int i)=>state;}
}
namespace UnityEngine.SceneManagement {
 public class Scene {public string name="arena";public bool isLoaded=true;}
 public static class SceneManager {public static bool loaded=true;public static Scene GetSceneByName(string s)=>new Scene{isLoaded=loaded};}
}
public class HealthManager {
 public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();public UnityEngine.Transform transform=>gameObject.transform;
 public T GetComponent<T>() where T:class {foreach(var x in gameObject.components)if(x is T t)return t;return null;}
}
public class EnemyDeathEffects {public UnityEngine.GameObject corpse;}
public class tk2dSpriteAnimationClip {public enum WrapMode{Once,Loop,Single}public WrapMode wrapMode;}
public class tk2dSpriteAnimator {public UnityEngine.GameObject gameObject;public bool enabled=true,Playing;public tk2dSpriteAnimationClip CurrentClip;}
public class Var {public string Name;public UnityEngine.GameObject Value;}public class Vars{public Var[] GameObjectVariables=new Var[0];}
public class PlayMakerFSM {public UnityEngine.GameObject gameObject;public Vars FsmVariables=new Vars();public HutongGames.PlayMaker.Fsm Fsm=new HutongGames.PlayMaker.Fsm();public bool enabled=true;public string ActiveStateName="Idle"; public int GetInstanceID()=>1;}
namespace HutongGames.PlayMaker {
 public class Fsm {public FsmState ActiveState=new FsmState();public UnityEngine.GameObject GameObject;}
 public class FsmStateAction {public bool Finished;}
 public class Wait:FsmStateAction {}
 public class FsmState {public object[] Transitions=new object[]{new object()};public FsmStateAction[] Actions=new FsmStateAction[0];public Fsm Fsm;public string Name;public void OnEnter(){}public void OnExit(){}}
}
namespace TerraKnight {
 public static class Plugin {public static Logger Log=new Logger();public class Logger{public void LogInfo(string s){}}}
}
namespace TerraKnight.Net {
 public static class BossLifeRewards {
  public static string Begin(string r,string s)=>null;
  public static void Complete(string t,string r,string s,UnityEngine.Vector2 p,bool d){}
 }
}
namespace TerraKnight.Items {
 public static class BossCorpseTracking {
  public static UnityEngine.GameObject Of(EnemyDeathEffects e)=>e?.corpse;
  public static void Related(string r,string s,List<UnityEngine.GameObject> l){}
  public static void Clear(){}
 }
 public static class Relics {public static string ContextScene,ContextName;}
 public static class EnemyDrops {
  public static int paid;public static bool direct;
  public static bool IsBoss(string r)=>r=="boss"||r=="NightmareGrimm";
  public static bool LeavesOnItsOwn(string s)=>s=="dream";
  public static void Roll(string r,UnityEngine.Vector2 p,bool d,bool? f,string scene){paid++;direct=d;}
 }
 public static class BossLootTimingScenarios {
  static int checks;
  static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
  static HealthManager Body(string scene="arena"){
   BossLootTiming.Clear();UnityEngine.Object.objects.Clear();EnemyDrops.paid=0;UnityEngine.Time.time=0;UnityEngine.SceneManagement.SceneManager.loaded=true;
   var h=new HealthManager();h.gameObject.scene.name=scene;h.gameObject.components.Add(new UnityEngine.Renderer());return h;
  }
  static void Tick(float t){UnityEngine.Time.time=t;BossLootTiming.Tick();}
  static void Burst(HealthManager h){
   var s=new HutongGames.PlayMaker.FsmState{Name="Explode",Fsm=new HutongGames.PlayMaker.Fsm{GameObject=h.gameObject}};
   typeof(BossLootTiming).GetMethod("Entered",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{s});
  }
  public static string Run(){
   var h=Body();var anim=new tk2dSpriteAnimator{Playing=true,CurrentClip=new tk2dSpriteAnimationClip{wrapMode=tk2dSpriteAnimationClip.WrapMode.Once}};
   h.gameObject.components.Add(anim);BossLootTiming.Defer(h,"boss",null,true);
   Tick(1);Check(EnemyDrops.paid==0,"loot at lethal hit");
   Tick(12);Check(EnemyDrops.paid==0,"long death cut short by six-second timeout");
   Burst(h);Tick(13);Check(EnemyDrops.paid==0,"burst state exit cut short active animation");
   anim.Playing=false;Tick(14);Check(EnemyDrops.paid==1,"completed explosion did not pay");
   Tick(15);Check(EnemyDrops.paid==1 && BossLootTiming.DevPending==0,"duplicate payment");
   h=Body();BossLootTiming.Defer(h,"boss",null,false);Tick(0);h.gameObject.activeInHierarchy=false;
   Tick(1);Check(EnemyDrops.paid==0,"body-to-corpse handoff paid too early");Tick(2);Check(EnemyDrops.paid==1,"disappeared boss did not pay");
   h=Body();var fsm=new PlayMakerFSM();h.gameObject.components.Add(fsm);BossLootTiming.Defer(h,"boss",null,false);
   Tick(0);fsm.ActiveStateName="Bow";Tick(5);Tick(7);Check(EnemyDrops.paid==0,"state change did not restart settling");
   Tick(11);Check(EnemyDrops.paid==1,"persistent settled body never paid");
   h=Body();anim=new tk2dSpriteAnimator{Playing=true,CurrentClip=new tk2dSpriteAnimationClip{wrapMode=tk2dSpriteAnimationClip.WrapMode.Single}};
   h.gameObject.components.Add(anim);BossLootTiming.Defer(h,"boss",null,false);Tick(0);Tick(7);Check(EnemyDrops.paid==1,"single-frame corpse blocks forever");
   h=Body("dream");Check(BossLootTiming.Waits("boss","dream"),"automatic-exit death bypasses scheduler");
   BossLootTiming.Defer(h,"boss",null,true);Tick(0);Check(EnemyDrops.paid==0,"dream loot paid at lethal hit");
   UnityEngine.SceneManagement.SceneManager.loaded=false;Tick(1);Check(EnemyDrops.paid==1 && EnemyDrops.direct,"scene exit lost loot");
   Tick(2);Check(EnemyDrops.paid==1,"scene exit duplicate payment");
   h=Body();BossLootTiming.Defer(h,"boss",null,false);Burst(h);Tick(0);Check(EnemyDrops.paid==1,"completed burst without animation did not pay");
   h=Body();fsm=new PlayMakerFSM{ActiveStateName="Death Shake"};h.gameObject.components.Add(fsm);
   ((UnityEngine.Renderer)h.gameObject.components[0]).enabled=false;
   BossLootTiming.Defer(h,"boss",null,false);Tick(0);Tick(12);
   Check(EnemyDrops.paid==0,"invisible looping death FSM paid before self explosion");
   fsm.ActiveStateName="Death End";Burst(h);Tick(13);Check(EnemyDrops.paid==1,"finished death FSM never paid");
   h=Body();((UnityEngine.Renderer)h.gameObject.components[0]).enabled=false;
   BossLootTiming.Defer(h,"boss",null,false);Tick(0);Tick(1);Tick(2);
   Check(EnemyDrops.paid==0,"hidden renderer mistaken for destroyed death owner");
   h=Body();var detached=new UnityEngine.GameObject{name="Corpse boss Phase2"};
   var corpseAnim=new tk2dSpriteAnimator{gameObject=detached,Playing=true,CurrentClip=new tk2dSpriteAnimationClip{wrapMode=tk2dSpriteAnimationClip.WrapMode.Once}};
   detached.components.Add(corpseAnim);detached.components.Add(new UnityEngine.Renderer());UnityEngine.Object.objects.Add(corpseAnim);
   BossLootTiming.Defer(h,"boss",null,false);h.gameObject.activeInHierarchy=false;Tick(0);Tick(12);
   Check(EnemyDrops.paid==0,"FSM-spawned detached corpse ignored");
   detached.activeInHierarchy=false;Tick(13);Tick(14);Check(EnemyDrops.paid==1,"detached corpse completion did not pay once");
   // Native Nosk corpse: terminal Blow has no outgoing transition.
   h=Body();fsm=new PlayMakerFSM{ActiveStateName="Blow"};fsm.Fsm.ActiveState.Transitions=new object[0];
   h.gameObject.components.Add(fsm);BossLootTiming.Defer(h,"boss",null,false);Tick(0);Tick(7);
   Check(EnemyDrops.paid==1,"Nosk terminal Blow blocked loot until scene unload");
   // Pooled ghost corpses never populate EnemyDeathEffects.corpse.
   h=Body();var effects=new EnemyDeathEffects();h.gameObject.components.Add(effects);
   BossLootTiming.Defer(h,"boss",null,false);
   var ghost=new UnityEngine.GameObject{name="Ghost Death Galien"};
   fsm=new PlayMakerFSM{ActiveStateName="Pause"};var wait=new HutongGames.PlayMaker.Wait();fsm.Fsm.ActiveState.Actions=new[]{wait};
   ghost.components.Add(fsm);BossLootTiming.Emitted(effects,ghost);h.gameObject.activeInHierarchy=false;
   Tick(0);Tick(15);Check(EnemyDrops.paid==0,"pooled ghost's invisible Pause paid before explosion");
   wait.Finished=true;fsm.ActiveStateName="Blow";Tick(16);Tick(25);Check(EnemyDrops.paid==0,"pooled ghost Blow paid before End");
   fsm.ActiveStateName="End";fsm.Fsm.ActiveState.Transitions=new object[0];Tick(26);Tick(32);
   Check(EnemyDrops.paid==1,"pooled ghost End never paid");Tick(40);Check(EnemyDrops.paid==1,"pooled ghost paid twice");
   h=Body();var scripted=new UnityEngine.GameObject{name="Sly Death Body"};
   anim=new tk2dSpriteAnimator{gameObject=scripted,Playing=true,CurrentClip=new tk2dSpriteAnimationClip{wrapMode=tk2dSpriteAnimationClip.WrapMode.Once}};
   scripted.components.Add(anim);fsm=new PlayMakerFSM{gameObject=h.gameObject};
   fsm.FsmVariables.GameObjectVariables=new[]{new Var{Name="Corpse Instance",Value=scripted}};UnityEngine.Object.objects.Add(fsm);
   BossLootTiming.Defer(h,"boss",null,false);h.gameObject.activeInHierarchy=false;Tick(0);Tick(12);
   Check(EnemyDrops.paid==0,"scripted corpse variable ignored when its name lacks the record");
   scripted.activeInHierarchy=false;Tick(13);Tick(14);Check(EnemyDrops.paid==1,"scripted corpse completion never paid");
   h=Body("Grimm_Nightmare");BossLootTiming.Defer(h,"NightmareGrimm",null,true);
   var control=new UnityEngine.GameObject{name="Grimm Control"};control.scene.name="Grimm_Nightmare";
   var settled=typeof(BossLootTiming).GetMethod("NightmareSettled",BindingFlags.NonPublic|BindingFlags.Static);
   var nativeState=new HutongGames.PlayMaker.FsmState{Name="Explode Start",Fsm=new HutongGames.PlayMaker.Fsm{GameObject=control}};
   settled.Invoke(null,new object[]{nativeState});Check(EnemyDrops.paid==0,"nightmare loot before death explosion");
   nativeState.Name="Ash Away";settled.Invoke(null,new object[]{nativeState});Check(EnemyDrops.paid==0,"nightmare loot before final ash clears");
   nativeState.Name="Divine Leaves?";control.name="Other";settled.Invoke(null,new object[]{nativeState});Check(EnemyDrops.paid==0,"unrelated FSM released nightmare loot");
   control.name="Grimm Control";control.scene.name="GG_Grimm_Nightmare";settled.Invoke(null,new object[]{nativeState});Check(EnemyDrops.paid==0,"other arena released nightmare loot");
   control.scene.name="Grimm_Nightmare";settled.Invoke(null,new object[]{nativeState});Check(EnemyDrops.paid==1&&EnemyDrops.direct&&UnityEngine.SceneManagement.SceneManager.loaded,"nightmare loot still waits for dream exit");
   settled.Invoke(null,new object[]{nativeState});UnityEngine.SceneManagement.SceneManager.loaded=false;Tick(30);Check(EnemyDrops.paid==1&&BossLootTiming.DevPending==0,"nightmare reward paid twice after wake-up");
   return "PASS "+checks+" scheduler lifecycle checks; native animation visuals not tested.";
  }
 }
}
'@
$testSource = Join-Path ([IO.Path]::GetTempPath()) ('boss-loot-timing-' + [guid]::NewGuid().ToString('N') + '.cs')
try {
    [IO.File]::WriteAllText($testSource, $stub)
    Add-Type -Path @((Join-Path $root 'src/TerraKnight/Items/BossLootTiming.cs'), $testSource) -CompilerOptions '/nowarn:0649' -WarningAction SilentlyContinue
    [TerraKnight.Items.BossLootTimingScenarios]::Run()
} finally { Remove-Item -LiteralPath $testSource -ErrorAction SilentlyContinue }
