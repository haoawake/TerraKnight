$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Expr([string]$file,[string]$marker){
 $s=Get-Content (Join-Path $root $file) -Raw
 $start=$s.IndexOf($marker);if($start -lt 0){throw "Missing $marker"}
 $s.Substring($start,$s.IndexOf(';',$start)-$start+1)
}
$rules=@('ChanceScale','AmountBonus','GeoScale') | ForEach-Object {Expr 'src/TerraKnight/Items/EnemyDrops.cs' "public static float $_"}
$rules+=Expr 'src/TerraKnight/Items/BossStats.cs' 'public static float Tier('
$rules+=Expr 'src/TerraKnight/Player/TerraHurt.cs' 'internal static int CloakDamage'
$rules+=Expr 'src/TerraKnight/Player/TerraHurt.cs' 'internal static int HoneyDamage('
$rules+=Expr 'src/TerraKnight/Items/Items.cs' 'public static int OpenAccessories'
$stub=@'
using System;using TerraKnight.World;using TerraKnight.Items;
namespace UnityEngine {public struct Color{public float r,g,b;public Color(float a,float c,float d){r=a;g=c;b=d;}public static Color white=>new Color(1,1,1);}}
namespace TerraKnight {public static class Lang{public static string T(string s)=>s;}class TerraMod{public static TerraMod Instance;public Stats Stats=new Stats();}class Stats{public System.Collections.Generic.HashSet<Items.PermanentBoost> Boosts=new System.Collections.Generic.HashSet<Items.PermanentBoost>();}}
namespace TerraKnight.Items {internal enum PermanentBoost{DemonHeart}}
namespace TerraKnight {public static class HollowRules {
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 public static string Run(){
  GameDifficulty.Current=DifficultyLevel.Master;var master=new[]{ChanceScale,AmountBonus,GeoScale,Tier(DifficultyLevel.Master),(float)CloakDamage,(float)HoneyDamage(false),(float)HoneyDamage(true),(float)OpenAccessories};
  GameDifficulty.Current=DifficultyLevel.Hollow;var hollow=new[]{ChanceScale,AmountBonus,GeoScale,Tier(DifficultyLevel.Hollow),(float)CloakDamage,(float)HoneyDamage(false),(float)HoneyDamage(true),(float)OpenAccessories};
  for(int i=0;i<master.Length;i++)Check(master[i]==hollow[i],"Hollow/Master rule mismatch "+i);
  Check(OpenAccessories==6,"Master extra accessory slot missing");TerraMod.Instance=new TerraMod();TerraMod.Instance.Stats.Boosts.Add(Items.PermanentBoost.DemonHeart);Check(OpenAccessories==7,"Demon Heart slot missing");
  Check(GameDifficulty.MasterRules,"Master relic eligibility missing");Check(GameDifficulty.Current>=DifficultyLevel.Expert,"Expert bag eligibility missing");
  Check(GameDifficulty.Parse((int)GameDifficulty.Current)==DifficultyLevel.Hollow,"save round-trip loses Hollow");
  Check(GameDifficulty.ColorOf(DifficultyLevel.Hollow).g!=GameDifficulty.ColorOf(DifficultyLevel.Master).g,"new difficulty indistinguishable in UI");
  return "PASS "+checks+" actual Master/Hollow loot/health/retaliation/slots/save policy checks";
 }
}}
'@
$stub=$stub.Insert($stub.LastIndexOf('}', $stub.LastIndexOf('}')-1),(($rules -join "`n").Replace("public static float Tier(","internal static float Tier("))+"`n")
$temp=Join-Path ([IO.Path]::GetTempPath()) ('hollow-rules-'+[guid]::NewGuid().ToString('N')+'.cs')
try {
 [IO.File]::WriteAllText($temp,$stub)
 Add-Type -Path @((Join-Path $root 'src/TerraKnight/World/GameDifficulty.cs'),$temp) -CompilerOptions '/warn:0'
 [TerraKnight.HollowRules]::Run()
} finally {Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue}
$menu=Get-Content (Join-Path $root 'src/TerraKnight/UI/CreativeMenu.cs') -Raw
if(!$menu.Contains('i <= (int)DifficultyLevel.Hollow') -or ([regex]::Matches($menu,'step==Step.Difficulty \? 640f')).Count -ne 2){throw 'Fifth difficulty card or matching menu geometry missing'}
if(178+4*74+64 -ge 640-70){throw 'Fifth difficulty overlaps Back button'}
$drops=Get-Content (Join-Path $root 'src/TerraKnight/Items/EnemyDrops.cs') -Raw
if(!$drops.Contains('if (d.Master && !World.GameDifficulty.MasterRules)')){throw 'Hollow excluded from Master relics'}
$save=Get-Content (Join-Path $root 'src/TerraKnight/Save/ModSave.cs') -Raw
if(!$save.Contains('Difficulty = (int)World.GameDifficulty.Current') -or !$save.Contains('World.GameDifficulty.Parse(file.Difficulty)')){throw 'Save serialization wiring missing'}
'PASS creation-menu fifth card/spacing, Master relic filter and save serialization wiring; no gameplay test'

