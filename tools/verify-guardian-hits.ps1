$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Extract-Method([string]$file,[string]$marker){
 $text=Get-Content -LiteralPath $file -Raw
 $start=$text.IndexOf($marker);if($start -lt 0){throw "Missing $marker"}
 $brace=$text.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
 return $text.Substring($start,$end-$start)
}
$guardian=Extract-Method (Join-Path $root 'src/TerraKnight/Items/TerraProjectilesArmor.cs') 'void GuardianPunch('
$carry=Extract-Method (Join-Path $root 'src/TerraKnight/Items/EnemyStats.cs') 'public static int ToGameDamage('
$stub=@'
using System;using System.Collections.Generic;
struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}public static Vector2 operator*(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);public static Vector2 operator-(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);}
class HealthManager {public bool isDead;public bool chase=true;public int GetInstanceID()=>1;}
class Collider2D{public HealthManager hm;public T GetComponentInParent<T>() where T:class=>hm as T;}
static class Physics2D{public static Collider2D[] boxes;public static Collider2D[] OverlapBoxAll(Vector2 a,Vector2 b,float c,int d)=>boxes;}
enum DamageClass{Summon}
class Proj{public Dictionary<int,int> Immune=new Dictionary<int,int>();public float Dir=1;}
static class TerraParticles{public static Vector2 ToWorld(Vector2 v)=>v;}
static class Equipment{public static Bonus Current=new Bonus();public class Bonus{public float MinionDamage;}}
static class SummonTags{public static int hits;public static int Modify(HealthManager h,int d,out bool c,float f,bool s,Vector2 v){c=false;return d;}public static void OnHit(HealthManager h,int d,Vector2 p,float k){hits++;}}
static class ArmorEffects{public struct Hit{public DamageClass Class;}public static Hit Context;public static int DevGuardianHits;}
static class FxParticles{public static void StardustPunch(Vector2 p,Vector2 v){}}
class MeleeHits{public bool accepted=true;public int attempts;public static bool shield;public static bool Blocks(HealthManager h,float d,DamageClass c)=>shield;public bool HitWith(Collider2D c,int d,float k,float crit,float dir,Vector2 p,bool autonomous=false){attempts++;return accepted;}}
class TerraMod{public static TerraMod Instance=new TerraMod();public MeleeHits Melee=new MeleeHits();}
static class Mathf{public static int FloorToInt(float f)=>(int)Math.Floor(f);}
static class EnemyStats{public class Info{public float Carry;public float Scale=1000;}}
public class GuardianChecks{
 const float Px=1;bool Chaseable(HealthManager h)=>h.chase&&!h.isDead;Vector2 EnemyCenterPx(HealthManager h)=>new Vector2();
 __GUARDIAN__
 __CARRY__
 static int n;static void Check(bool ok,string m){n++;if(!ok)throw new Exception(m);}
 public static string Run(){
 var owner=new GuardianChecks();var p=new Proj();var h=new HealthManager();var col=new Collider2D{hm=h};Physics2D.boxes=new[]{col,col};var m=TerraMod.Instance.Melee;
 m.accepted=false;owner.GuardianPunch(p,new Vector2());Check(p.Immune.Count==0&&SummonTags.hits==0&&ArmorEffects.DevGuardianHits==0,"rejected punch consumed cooldown/tag proc");
 m.accepted=true;owner.GuardianPunch(p,new Vector2());Check(p.Immune.Count==1&&SummonTags.hits==1&&ArmorEffects.DevGuardianHits==1,"retry or collider dedup failed");
 int before=m.attempts;owner.GuardianPunch(p,new Vector2());Check(before==m.attempts,"five-tick immunity ignored");
 p.Immune.Clear();MeleeHits.shield=true;owner.GuardianPunch(p,new Vector2());Check(before==m.attempts&&SummonTags.hits==1,"shield spent whip tag proc");MeleeHits.shield=false;
 h.chase=false;owner.GuardianPunch(p,new Vector2());Check(before==m.attempts,"unattackable target counted as hit");
 h.chase=true;int punches=0;for(int t=0;t<60;t++){var keys=new List<int>(p.Immune.Keys);foreach(int k in keys)if(--p.Immune[k]<=0)p.Immune.Remove(k);int last=SummonTags.hits;owner.GuardianPunch(p,new Vector2());punches+=SummonTags.hits-last;}Check(punches==12,"guardian does not hit every five ticks");
 var info=new EnemyStats.Info();int whole=0;for(int i=0;i<120;i++)whole+=ToGameDamage(info,30);Check(Math.Abs((whole+info.Carry)*info.Scale-3600)<0.01,"rapid fractional hits lost damage");
 return "PASS "+n+" guardian hit/cooldown/fractional damage checks (actual methods)";
 }
}
'@
$stub=$stub.Replace('__GUARDIAN__',$guardian).Replace('__CARRY__',$carry.Replace('(Info info,','(EnemyStats.Info info,').Replace('public static int','static int'))
Add-Type -TypeDefinition $stub -WarningAction SilentlyContinue -CompilerOptions '/nowarn:0649'
[GuardianChecks]::Run()

