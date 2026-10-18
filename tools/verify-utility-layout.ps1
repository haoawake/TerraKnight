$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content (Join-Path $root 'src/TerraKnight/Items/InventoryGuiUtilities.cs') -Raw
$start=$source.IndexOf('internal static Rect UtilityButtonRect(')
$brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
$method=$source.Substring($start,$end-$start)
$main=Get-Content (Join-Path $root 'src/TerraKnight/Items/InventoryGui.cs') -Raw
$constants=[regex]::Matches($main,'(?:const float (?:SlotSize|AmmoSlotSize|CoinStep|SlotStep|Margin)\s*=[^;]+;|internal static float RightOfAmmo[^;]+;)') | ForEach-Object {$_.Value}
$stub=@'
using System;
struct Rect{public float x,y,width,height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}public float xMax=>x+width;public float yMax=>y+height;public bool Overlaps(Rect b)=>x<b.xMax&&xMax>b.x&&y<b.yMax&&yMax>b.y;}
static class Inventory{public const int Columns=10,Rows=5;}
static class TerrariaAssets{public static Texture Texture(string s)=>new Texture();}class Texture{public int width=30,height=30;}
public static class UtilityLayout {
 public static string Run(){int checks=0;foreach(float scale in new[]{.65f,1f,1.5f,2f})foreach(float offset in new[]{0f,40f}){
  var view=new Rect(offset,offset,1920,1080);float rx=view.x+(Margin+Inventory.Columns*SlotStep+8)*scale;
  for(int i=0;i<4;i++){
   var r=UtilityButtonRect(view,scale,i);var ammo=new Rect(rx+CoinStep*scale,view.y+(Margin+18+(i+1)*SlotStep+(SlotSize-AmmoSlotSize)/2)*scale,AmmoSlotSize*scale,AmmoSlotSize*scale);
   if(r.x<ammo.xMax||Math.Abs(r.y+r.height/2-ammo.y-ammo.height/2)>.001f)throw new Exception("button not beside matching ammo row");checks++;
   if(r.width!=30*scale||r.height!=30*scale)throw new Exception("icon size changed");checks++;
   if(r.yMax>=view.y+(Margin+18+Inventory.Rows*SlotStep+48)*scale)throw new Exception("button reaches chest area");checks++;
   for(int j=0;j<i;j++){if(r.Overlaps(UtilityButtonRect(view,scale,j)))throw new Exception("buttons overlap");checks++;}
  }
 }return "PASS "+checks+" actual button geometry checks; no rendered gameplay test";}
}
'@
$stub=$stub.Insert($stub.LastIndexOf('}'),($constants -join "`n")+"`n"+$method+"`n")
Add-Type -TypeDefinition $stub
[UtilityLayout]::Run()
