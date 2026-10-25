Add-Type -AssemblyName System.Drawing
$base=Join-Path (Split-Path $PSScriptRoot -Parent) '角色特殊动画素材包'
$ref=[Drawing.Bitmap]::new((Join-Path $base '原版参考/terrarian-default-0.png'))
$new=[Drawing.Bitmap]::new((Join-Path $base '动画单帧/absorb_magic/00.png'))
$out=[Drawing.Bitmap]::new(500,310)
$g=[Drawing.Graphics]::FromImage($out)
$g.Clear([Drawing.Color]::FromArgb(37,48,65))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
$l=256;$t=256;$r=0;$b=0
for($y=0;$y -lt 256;$y++){for($x=0;$x -lt 256;$x++){if($ref.GetPixel($x,$y).A -ge 245){$l=[Math]::Min($l,$x);$r=[Math]::Max($r,$x+1);$t=[Math]::Min($t,$y);$b=[Math]::Max($b,$y+1)}}}
# Reference camera covers 56 game pixels vertically in a 256px image.
$w=[int][Math]::Round(($r-$l)*56/256*4)
$h=[int][Math]::Round(($b-$t)*56/256*4)
$g.DrawImage($ref,[Drawing.Rectangle]::new(125-$w/2,247-$h,$w,$h),[Drawing.Rectangle]::FromLTRB($l,$t,$r,$b),[Drawing.GraphicsUnit]::Pixel)
$g.DrawImage($new,[Drawing.Rectangle]::new(122,-81,384,384),0,0,96,96,[Drawing.GraphicsUnit]::Pixel)
$font=[Drawing.Font]::new('Microsoft YaHei',12)
$g.DrawString('实机默认人物',$font,[Drawing.Brushes]::White,45,265)
$g.DrawString('吸收起始姿势',$font,[Drawing.Brushes]::White,250,265)
$out.Save((Join-Path $base '人物尺寸校准对照.png'))
$g.Dispose();$out.Dispose();$ref.Dispose();$new.Dispose();$font.Dispose()

$out=[Drawing.Bitmap]::new(1152,432)
$g=[Drawing.Graphics]::FromImage($out)
$g.Clear([Drawing.Color]::FromArgb(37,48,65))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
$font=[Drawing.Font]::new('Microsoft YaHei',12)
for($i=0;$i -lt 12;$i++){
 $frame=[Drawing.Bitmap]::new((Join-Path $base ('动画单帧/absorb_magic/{0:00}.png' -f $i)))
 $x=($i%6)*192;$y=[int][Math]::Floor($i/6)*216
 $g.DrawImage($frame,[Drawing.Rectangle]::new($x,$y,192,192),0,0,96,96,[Drawing.GraphicsUnit]::Pixel)
 $g.DrawString([string]$i,$font,[Drawing.Brushes]::White,$x+80,$y+192)
 $frame.Dispose()
}
$out.Save((Join-Path $base '吸收动作总览.png'))
$g.Dispose();$out.Dispose();$font.Dispose()
