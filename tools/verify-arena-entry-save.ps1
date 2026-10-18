$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$code = [IO.File]::ReadAllText((Join-Path $root 'src/TerraKnight/Items/BossArena.cs'))
function Method([string]$signature) {
    $begin = $code.IndexOf($signature)
    if ($begin -lt 0) { throw "Missing hook: $signature" }
    $brace = $code.IndexOf('{', $begin)
    $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $code.Length) { if ($code[$end] -eq '{') { $depth++ }; if ($code[$end] -eq '}') { $depth-- }; $end++ }
    $code.Substring($begin, $end - $begin)
}
$prefix = @'
using System;
using System.Collections.Generic;
public class PersistentBoolData {public string sceneName,id;public bool activated,semiPersistent;}
public class SceneData {public static SceneData instance=new SceneData();public Dictionary<string,bool> data=new Dictionary<string,bool>();public void SaveMyState(PersistentBoolData p){data[p.sceneName+"/"+p.id]=p.activated;}}
public static class ArenaEntrySaveScenarios {
 class Arena {public string[] States={"Mines_18/Battle Scene","Mines_18/Mega Zombie Beam Miner (1)"};}
 class Visit {public bool Arrived;public Arena Arena=new Arena();}
 static Visit visit=new Visit();static Visit saving;
'@
$suffix = @'
 static void Seed(){foreach(var k in visit.Arena.States)SceneData.instance.data[k]=true;}
 static bool All(bool value){foreach(var k in visit.Arena.States)if(SceneData.instance.data[k]!=value)return false;return true;}
 public static string Run(){
  Seed();saving=visit;RearmAfterDepartureSave();if(!All(true))throw new Exception("SaveGame scope modified arena states");saving=null;
  RearmAfterDepartureSave();if(!All(false))throw new Exception("departure save overrode rematch reset");
  Seed();visit.Arrived=true;RearmAfterDepartureSave();if(!All(true))throw new Exception("ongoing fight or victory was reset");
  visit.Arrived=false;RearmAfterDepartureSave();if(!All(false))throw new Exception("delayed departure save overrode reset");
  visit=null;RearmAfterDepartureSave();
  return "PASS 5 departure-save/rematch scope checks; no live game test.";
 }
}
'@
Add-Type -TypeDefinition ($prefix + "`n" + (Method 'static void RearmAfterDepartureSave(') + "`n" + (Method 'static PersistentBoolData StateOf(') + "`n" + $suffix)
[ArenaEntrySaveScenarios]::Run()
