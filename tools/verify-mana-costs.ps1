$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Method([string]$source,[string]$marker){
 $start=$source.IndexOf($marker);if($start -lt 0){throw "Missing $marker"}
 $brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$runtime=Get-Content (Join-Path $root 'tools/encyclopedia_runtime.json') -Raw | ConvertFrom-Json
$magic=@($runtime.data.items.PSObject.Properties | Where-Object {$_.Value.Class -eq 'Magic'})
$vanilla=Get-Content (Join-Path $root 'tools/vanilla-data/combat/vanilla-runtime.json') -Raw | ConvertFrom-Json
$rows=foreach($p in $magic){$id=$p.Value.Texture.Substring(5);$mana=$vanilla.items.$id.mana;"CheckBase(ItemType.$($p.Name), $mana);"}
$template=@'
using System;using UnityEngine;
namespace UnityEngine { public static class Mathf {
 public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
 public static int FloorToInt(float x)=>(int)Math.Floor(x);public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));
}}
namespace TerraKnight.Items {
 public enum ItemType{None,__TYPES__}
 public class ItemDef {public ItemType Type;public int Mana,HealMana;}
 public struct Stack {public ItemDef Def;public ItemType Type=>Def?.Type??ItemType.None;public bool IsEmpty=>Def==null;}
 public class Inventory {public Stack Current;}
 public struct Bonuses {public bool SpaceGunFree,ManaFlower;public float ManaCost;}
 public static class Equipment {public static Bonuses Current;__COST__}
 public class Stats {public int Mana;float manaDelay,manaAccumulator;__SPEND__}
 public class TerraMod {
  public static TerraMod Instance;public bool AltFunction,SlowMagicUse,slowSwing;public float useCooldown=1;
  public const float SlowMagic=2.5f;public static int DevManaChecks,DevFlowerTries,DevSlowCasts;
  public Inventory Inventory=new Inventory();public Stats Stats=new Stats();
  void QuickUse(Func<ItemDef,bool> f){if(f(new ItemDef{HealMana=100}))Stats.Mana+=100;}
  __CHECK__
 }
 public static class ManaChecks {
  static int checks;static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
  static void CheckBase(ItemType type,int mana){
   var d=new ItemDef{Type=type,Mana=mana};Equipment.Current=default;
   Check(Equipment.ManaCost(d)==mana,"base mana "+type);
   var mod=TerraMod.Instance=new TerraMod();mod.Stats.Mana=200;
   Check(mod.CheckMana(d)&&mod.Stats.Mana==200-mana,"actual spending "+type);
  }
  public static string Run(){
   __ROWS__
   var mod=TerraMod.Instance=new TerraMod();
   var prism=new ItemDef{Type=ItemType.LastPrism,Mana=12};
   mod.Inventory.Current=new Stack{Def=new ItemDef{Type=ItemType.LastPrism,Mana=10}};
   Equipment.Current=new Bonuses{ManaCost=.08f};mod.Stats.Mana=100;
   Check(mod.CheckMana(prism)&&mod.Stats.Mana==91,"held spell lost prefix or flower reduction");
   mod.Inventory.Current=default;mod.Stats.Mana=100;mod.CheckMana(prism);
   Check(mod.Stats.Mana==89,"base prism flower cost");
   Equipment.Current=default;mod.Stats.Mana=0;mod.CheckMana(prism,true);
   Check(mod.Stats.Mana==0&&mod.SlowMagicUse&&mod.slowSwing&&mod.useCooldown==2.5f,"out-of-mana slow casting policy");
   Equipment.Current=new Bonuses{ManaFlower=true};mod.Stats.Mana=0;mod.CheckMana(prism);
   Check(mod.Stats.Mana==88&&!mod.SlowMagicUse,"flower drinks before spending");
   Equipment.Current=new Bonuses{SpaceGunFree=true};
   foreach(var type in new[]{ItemType.SpaceGun,ItemType.ZapinatorGray,ItemType.ZapinatorOrange,ItemType.LaserRifle})
    Check(Equipment.ManaCost(new ItemDef{Type=type,Mana=16})==0,"Meteor exception "+type);
   Check(Equipment.ManaCost(prism)==12,"Meteor made unrelated spell free");
   Equipment.Current=default;mod.AltFunction=true;
   Check(Equipment.ManaCost(new ItemDef{Type=ItemType.BookStaff,Mana=20})==40,"tome alternate cost");
   Equipment.Current=new Bonuses{ManaCost=.5f};
   Check(Equipment.ManaCost(prism)==6,"50 percent reduction");
   Equipment.Current=new Bonuses{ManaCost=2};
   Check(Equipment.ManaCost(prism)==1,"minimum paid mana");
   Check(Equipment.ManaCost(new ItemDef())==0,"zero mana item charged");
   return "PASS "+checks+" actual mana-cost/spending checks across __COUNT__ magic items; no gameplay test";
  }
 }
}
'@
$src=Join-Path $root 'src/TerraKnight'
$template=$template.Replace('__TYPES__',($magic.Name -join ',')).Replace('__ROWS__',($rows -join "`n")).Replace('__COUNT__',[string]$magic.Count)
$template=$template.Replace('__COST__',(Method ([IO.File]::ReadAllText((Join-Path $src 'Items/Equipment.cs'))) 'public static int ManaCost('))
$template=$template.Replace('__CHECK__',(Method ([IO.File]::ReadAllText((Join-Path $src 'TerraMod.cs'))) 'public bool CheckMana('))
$template=$template.Replace('__SPEND__',(Method ([IO.File]::ReadAllText((Join-Path $src 'Player/TerraStats.cs'))) 'public bool SpendMana('))
Add-Type -TypeDefinition $template -CompilerOptions '/nowarn:0414' -WarningAction SilentlyContinue
[TerraKnight.Items.ManaChecks]::Run()
