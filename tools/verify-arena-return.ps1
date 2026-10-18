$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$code = [IO.File]::ReadAllText((Join-Path $root 'src/TerraKnight/Items/BossArena.cs'))
$begin = $code.IndexOf('static void WakeByGate(')
if ($begin -lt 0) { throw 'Return hook not found' }
$brace = $code.IndexOf('{', $begin)
$depth = 1
$end = $brace + 1
while ($depth -gt 0 -and $end -lt $code.Length) { if ($code[$end] -eq '{') { $depth++ }; if ($code[$end] -eq '}') { $depth-- }; $end++ }
$method = $code.Substring($begin, $end - $begin)
$prefix = @'
using System;
public class GameManager {public enum SceneLoadVisualizations{Default,Dream}public class SceneLoadInfo{public string SceneName,EntryGateName;public SceneLoadVisualizations Visualization=SceneLoadVisualizations.Dream;}}
public class Flag {public bool Value=true;}
public class Vars {public Flag flag=new Flag();public Flag FindFsmBool(string n)=>flag;}
public class PlayMakerFSM {public string FsmName;public Vars FsmVariables=new Vars();}
public class HeroController {public static HeroController SilentInstance=new HeroController();public PlayMakerFSM[] fsms={new PlayMakerFSM{FsmName="Dream Return"},new PlayMakerFSM{FsmName="Dream Nail"}};public T[] GetComponents<T>()=>(T[])(object)fsms;}
public static class Plugin {public static Logger Log=new Logger();public class Logger{public void LogInfo(string s){}}}
public static class ArenaReturnScenarios {
 static string returnScene="origin",returnGate="left1";static bool ordinaryDreamReturn;static int DevReturnGates;
'@
$suffix = @'
 public static string Run(){
  var i=new GameManager.SceneLoadInfo{SceneName="other",EntryGateName="door_dreamReturn"};WakeByGate(i);
  if(i.EntryGateName!="door_dreamReturn"||ordinaryDreamReturn)throw new Exception("unrelated transition modified");
  i=new GameManager.SceneLoadInfo{SceneName="origin",EntryGateName="right1"};WakeByGate(i);
  if(i.EntryGateName!="right1"||ordinaryDreamReturn)throw new Exception("ordinary door mistaken for dream return");
  i=new GameManager.SceneLoadInfo{SceneName="origin",EntryGateName="door_dreamReturn"};WakeByGate(i);
  if(i.EntryGateName!="left1")throw new Exception("fallback gate not selected");
  if(i.Visualization!=GameManager.SceneLoadVisualizations.Default)throw new Exception("dream visualization retained on ordinary gate");
  foreach(var f in HeroController.SilentInstance.fsms)if(f.FsmVariables.flag.Value)throw new Exception("dream returning flag retained");
  if(!ordinaryDreamReturn||DevReturnGates!=1)throw new Exception("wake-up completion not scheduled");
  ordinaryDreamReturn=false;returnGate=null;
  i=new GameManager.SceneLoadInfo{SceneName="origin",EntryGateName="door_dreamReturn"};WakeByGate(i);
  if(i.Visualization!=GameManager.SceneLoadVisualizations.Dream||ordinaryDreamReturn)throw new Exception("native dream-return gate modified");
  return "PASS 7 ordinary/native dream-return transition checks; fade pixels and control not tested in game.";
 }
}
'@
Add-Type -TypeDefinition ($prefix + "`n" + $method + "`n" + $suffix)
[ArenaReturnScenarios]::Run()
