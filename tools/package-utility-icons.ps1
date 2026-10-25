param([string]$SourceDir = 'C:\Users\luo\.codex\generated_images\01a14d84-f3b8-7512-83b2-f79f555e800b')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies @('System.Drawing.Common', 'System.Drawing.Primitives', 'System.Private.Windows.GdiPlus', 'System.Private.Windows.Core') -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class UtilitySpritePack {
 public static void Pack(string source,string destination) {
  using(var src=new Bitmap(source)) {
   int l=src.Width,t=src.Height,r=0,b=0;
   for(int y=0;y<src.Height;y++)for(int x=0;x<src.Width;x++)if(src.GetPixel(x,y).A>=128){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x+1);b=Math.Max(b,y+1);}
   if(r<=l||b<=t)throw new Exception("Empty sprite");
   using(var dst=new Bitmap(32,30,PixelFormat.Format32bppArgb)) {
    float scale=Math.Min(30f/(r-l),28f/(b-t));
    int w=(int)Math.Round((r-l)*scale),h=(int)Math.Round((b-t)*scale);
    using(var g=Graphics.FromImage(dst)) {
     g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
     g.DrawImage(src,new Rectangle((32-w)/2,(30-h)/2,w,h),l,t,r-l,b-t,GraphicsUnit.Pixel);
    }
    dst.Save(destination,ImageFormat.Png);
   }
  }
 }
 public static void Preview(string[] paths,string destination) {
  using(var dst=new Bitmap(288,408))using(var g=Graphics.FromImage(dst)) {
   g.Clear(Color.FromArgb(10,10,20));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
   for(int i=0;i<paths.Length;i++)using(var src=new Bitmap(paths[i]))g.DrawImage(src,new Rectangle(16+(i%2)*144,14+(i/2)*136,src.Width*4,src.Height*4));
   dst.Save(destination,ImageFormat.Png);
  }
 }
}
'@
$icons = [ordered]@{
    UtilityBlacklist = 'exec-8b89be79-40db-4f90-856b-dfe57b0d7497.png'
    UtilityBestiary = 'exec-144e87c9-6212-4fef-bd3f-163980098b71.png'
    UtilityHallownest = 'exec-e2c2f5a4-36c9-477e-9596-e3bca4bda8b7.png'
    UtilityQuickShop = 'exec-ff740b29-3572-4bc7-b9f2-ba9753aa8d5f.png'
}
$artifactDir = Join-Path $root 'artifacts/utility-icons'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
$paths = [System.Collections.Generic.List[string]]::new()
$paths.Add((Join-Path $artifactDir 'UI/ChestStack_0.png'))
$paths.Add((Join-Path $artifactDir 'UI/Sort_0.png'))
foreach ($entry in $icons.GetEnumerator()) {
    $source = Join-Path $SourceDir $entry.Value
    Copy-Item -LiteralPath $source -Destination (Join-Path $artifactDir ($entry.Key + '-source.png')) -Force
    $target = Join-Path $root ('src/TerraKnight/Resources/hk_' + $entry.Key + '.png')
    [UtilitySpritePack]::Pack($source, $target)
    $paths.Add($target)
    Write-Output ($entry.Key + ': 32x30 transparent PNG')
}
[UtilitySpritePack]::Preview($paths.ToArray(), (Join-Path $artifactDir 'utility-buttons-preview.png'))
