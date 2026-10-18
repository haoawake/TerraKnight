# The real capture/transpiler under managed stubs; native IL is read as metadata only.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$stub=@'
using System;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.All,AllowMultiple=true)]public class HarmonyPatch:Attribute{public HarmonyPatch(){}public HarmonyPatch(Type t,string n){}}
 public class HarmonyTranspiler:Attribute{}public class HarmonyPostfix:Attribute{}
 public class CodeInstruction{public OpCode opcode;public object operand;public CodeInstruction(OpCode c,object o=null){opcode=c;operand=o;}}
 public static class AccessTools{
  public delegate ref F FieldRef<T,F>(T x);
  static ref UnityEngine.GameObject Ref(EnemyDeathEffects e)=>ref e.corpse;
  public static FieldRef<T,F> FieldRefAccess<T,F>(string s)=>(FieldRef<T,F>)(object)(FieldRef<EnemyDeathEffects,UnityEngine.GameObject>)Ref;
  public static MethodInfo Method(Type t,string s)=>t.GetMethod(s,BindingFlags.Static|BindingFlags.NonPublic);
 }
}
namespace UnityEngine{public class Scene{public string name="arena";}public class GameObject{public bool activeInHierarchy=true;public Scene scene=new Scene();public string name="corpse";public T GetComponent<T>() where T:class=>new HealthManager() as T;}}
public class HealthManager{public string record="GhostGalien";}
public class EnemyDeathEffects{public UnityEngine.GameObject corpse;public HealthManager hm=new HealthManager();public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();public T GetComponent<T>() where T:class=>hm as T;}
namespace HutongGames.PlayMaker.Actions{public class Ref{public UnityEngine.GameObject Value;}public class CreateObject{public UnityEngine.GameObject Owner;public Ref storeObject;}public class SpawnObjectFromGlobalPool:CreateObject{}}
namespace TerraKnight.UI{public static class EnemyHover{public static string RecordOf(HealthManager h)=>h.record;}}
namespace TerraKnight.Items{
 public static class EnemyDrops{public static bool IsBoss(string r)=>r=="GhostGalien";}
 public static class BossLootTiming{public static UnityEngine.GameObject captured;public static void Emitted(EnemyDeathEffects e,UnityEngine.GameObject b){captured=b;}}
 public static class CorpseChecks{
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 public static string Run(){
 var fx=new EnemyDeathEffects();var actual=new UnityEngine.GameObject();
 Check(BossCorpseTracking.Of(fx)==null,"new pooled corpse should start empty");
 typeof(BossCorpseTracking).GetMethod("Remember",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{actual,fx});
 Check(fx.corpse==null&&BossCorpseTracking.Of(fx)==actual,"pooled corpse not captured exactly");
 Check(BossLootTiming.captured==actual,"credit before corpse creation lost handoff");
 var list=new List<UnityEngine.GameObject>();BossCorpseTracking.Related("GhostGalien@5","arena",list);
 Check(list.Count==1&&list[0]==actual,"phase/variant owner not retained");
 BossCorpseTracking.Related("GhostGalien","arena",list);Check(list.Count==1,"duplicate owner");
 actual.activeInHierarchy=false;list.Clear();BossCorpseTracking.Related("GhostGalien","arena",list);Check(list.Count==0,"pooled inactive body from old fight retained");
 var pre=new EnemyDeathEffects{corpse=new UnityEngine.GameObject()};Check(BossCorpseTracking.Of(pre)==pre.corpse,"pre-instantiated corpse regressed");
 var anchor=new HarmonyLib.CodeInstruction(OpCodes.Ldstr,"corpse");
 var input=new[]{new HarmonyLib.CodeInstruction(OpCodes.Ldloc_0),anchor,new HarmonyLib.CodeInstruction(OpCodes.Call),new HarmonyLib.CodeInstruction(OpCodes.Ret)};
 var method=typeof(BossCorpseTracking).GetMethod("Transpiler",BindingFlags.Static|BindingFlags.NonPublic);
 var output=new List<HarmonyLib.CodeInstruction>((IEnumerable<HarmonyLib.CodeInstruction>)method.Invoke(null,new object[]{input}));
 Check(output.Count==7&&ReferenceEquals(output[1],anchor)&&output[1].opcode==OpCodes.Dup,"capture lost anchor or branch destination");
 Check(output[2].opcode==OpCodes.Ldarg_0&&output[3].opcode==OpCodes.Call&&output[4].opcode==OpCodes.Ldstr&&(string)output[4].operand=="corpse","wrong capture stack or native arguments");
 bool failed=false;try{new List<HarmonyLib.CodeInstruction>((IEnumerable<HarmonyLib.CodeInstruction>)method.Invoke(null,new object[]{new HarmonyLib.CodeInstruction[0]}));}catch(InvalidOperationException){failed=true;}
 Check(failed,"native IL drift silently ignored");return "PASS "+n+" actual corpse capture checks";
 }
 }
}
'@
$taskStub=Join-Path ([IO.Path]::GetTempPath()) ('corpse-check-'+[guid]::NewGuid().ToString('N')+'.cs')
try{
 [IO.File]::WriteAllText($taskStub,$stub)
 Add-Type -Path @((Join-Path $root 'src/TerraKnight/Items/BossCorpseTracking.cs'),$taskStub)
 [TerraKnight.Items.CorpseChecks]::Run()
 Add-Type -Path 'F:/SteamLibrary/steamapps/common/Hollow Knight/BepInEx/core/Mono.Cecil.dll'
 $taskAssembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('F:/SteamLibrary/steamapps/common/Hollow Knight/hollow_knight_Data/Managed/Assembly-CSharp.dll')
 try{
  $taskMethod=($taskAssembly.MainModule.Types | Where-Object Name -eq 'EnemyDeathEffects').Methods | Where-Object Name -eq 'EmitCorpse'
  $taskAnchors=@($taskMethod.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'corpse' })
  if($taskAnchors.Count -ne 1 -or $taskAnchors[0].Previous.OpCode.Name -ne 'ldloc.0' -or $taskAnchors[0].Next.Operand.Name -ne 'LocateFSM'){throw 'Native corpse capture anchor changed'}
  'PASS native EmitCorpse IL anchor and argument stack'
 }finally{$taskAssembly.Dispose()}
}finally{Remove-Item -LiteralPath $taskStub -ErrorAction SilentlyContinue}
