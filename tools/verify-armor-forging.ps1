$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$stub=@'
using System;
namespace UnityEngine {public static class Mathf {public static int Clamp(int x,int a,int b)=>Math.Max(a,Math.Min(b,x));public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int RoundToInt(float x)=>(int)Math.Round(x);}public static class Random {public static float value=>.25f;} }
namespace TerraKnight {public static class Lang {public static string Culture=>"zh-CN";} }
namespace TerraKnight.Items {
 public enum ItemType{None,Armor0,Armor1,Armor2,Armor3,Armor4,Armor5,Sword,KnightNail,DreamNail}
 public enum DamageClass{None,Melee}public enum MinionKind{None}public enum WhipKind{None}
 public class ItemDef{public ItemType Type;public int Stage,Damage,Defense,MaxStack=1,PickPower,AxePower,HammerPower;public byte Forge;public DamageClass Class;public bool IsArmor,IsAmmo,Consumable,Sentry;public MinionKind Minion;public WhipKind Whip;public float UseTime=.5f;public string Name="item";public ItemDef Unprefixed,Unforged;public ItemDef Clone()=>(ItemDef)MemberwiseClone();}
 public struct ItemStack{public ItemType Type;public byte Forge;public bool IsEmpty=>Type==ItemType.None;public static ItemStack Of(ItemType t)=>new ItemStack{Type=t};public ItemDef Def=>Forging.Apply(ItemDb.Get(Type),Forge);}
 public static class ItemDb{static readonly ItemDef[] defs=Build();static ItemDef[] Build(){var a=new ItemDef[10];for(int i=0;i<a.Length;i++)a[i]=new ItemDef{Type=(ItemType)i,Stage=i>=1&&i<=6?i-1:0,IsArmor=i>=1&&i<=6,Defense=10,Damage=i>=7?20:0,Class=i>=7?DamageClass.Melee:DamageClass.None};return a;}public static ItemDef Get(ItemType t)=>defs[(int)t];}
 public static class Shop{public static int Stage;public static long Balance;public static int UnlockedStage()=>Stage;public static bool Spend(long n){if(Balance<n)return false;Balance-=n;return true;}}
 public static class TreasureBags{public static bool Is(ItemType t)=>false;public static ItemDef Marked(ItemDef d,byte l)=>d;}
 public static class ArmorForgeScenarios{
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 public static string Run(){var report=new System.Text.StringBuilder();
  for(int stage=0;stage<6;stage++){
   var t=(ItemType)(stage+1);var s=ItemStack.Of(t);int cap=3*(stage+1);long total=0,prior=0;
   Check(Forging.MaxLevelOf(t)==cap,"armor cap");
   Shop.Stage=5;Check(Forging.MaxLevelOf(t)==cap,"world progression raises low armor cap");
   Shop.Balance=100000000;Forging.DevOutcome=true;
   for(int i=0;i<cap;i++){
    long price=Forging.Cost(s),before=Shop.Balance;Check(price>prior&&price%100==0,"price progression or denomination");prior=price;total+=price;
    Check(Forging.Try(ref s)==Forging.Result.Forged&&s.Forge==i+1,"successful forge");
    Check(Shop.Balance==before-price,"wrong charge");Check(s.Def.Defense==10+i+1,"defense per success");
   }
   long funds=Shop.Balance;Check(Forging.Try(ref s)==Forging.Result.AtMax&&Shop.Balance==funds,"cap charged or allowed extra level");
   long oldBudget=500000L*(stage+1);Check(total>=oldBudget&&total-oldBudget<cap*100,"full upgrade price changed too far");
   Check(Forging.SellBonus(s)==total/5,"sale bonus mismatch");
   report.AppendLine($"stage={stage} cap=+{cap} full-upgrade={total/100} Geo (successful attempts)");
  }
  var low=ItemStack.Of(ItemType.Armor0);low.Forge=5;Check(Forging.AtMax(low)&&low.Def.Defense==15,"legacy over-cap armor downgraded");
  low.Forge=0;Shop.Balance=10000000;Forging.DevOutcome=false;long beforeFail=Shop.Balance,cost=Forging.Cost(low);Check(Forging.Try(ref low)==Forging.Result.Failed&&low.Forge==0&&Shop.Balance==beforeFail-cost,"failed forge changed level or price");
  Shop.Balance=0;Check(Forging.Try(ref low)==Forging.Result.Poor&&low.Forge==0,"insufficient funds allowed upgrade");
  Check(Forging.Chance(low)==.5f,"success chance changed");
  for(int stage=0;stage<6;stage++)Check(Forging.StageCap(stage)==5*(stage+1),"weapon cap changed");
  Check(Forging.MaxLevelOf(ItemType.Sword)==5,"weapon item cap changed");Shop.Stage=5;Check(Forging.MaxLevelOf(ItemType.KnightNail)==30&&Forging.MaxLevelOf(ItemType.DreamNail)==30,"progression nail cap changed");
  Forging.DevOutcome=null;report.AppendLine("PASS "+checks+" actual forging cap/cost/defense/payment/legacy checks; no gameplay test");return report.ToString();
 }
 }
}
'@
$temp=Join-Path ([IO.Path]::GetTempPath()) ('armor-forging-'+[guid]::NewGuid().ToString('N')+'.cs')
try {
 [IO.File]::WriteAllText($temp,$stub)
 Add-Type -Path @((Join-Path $root 'src/TerraKnight/Items/Forging.cs'),$temp) -CompilerOptions '/nowarn:0649'
 [TerraKnight.Items.ArmorForgeScenarios]::Run()
} finally {Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue}
