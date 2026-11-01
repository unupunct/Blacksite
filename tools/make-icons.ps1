# Generates the BLACKSITE logo as PNGs (all sizes), a Windows .ico and Android launcher icons.
param([string]$Root = (Split-Path $PSScriptRoot -Parent))
Add-Type -AssemblyName System.Drawing
function New-Logo([int]$size, [bool]$round = $false) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $s = [single]$size
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0,0)), ([System.Drawing.PointF]::new($s,$s)), ([System.Drawing.Color]::FromArgb(255,4,10,22)), ([System.Drawing.Color]::FromArgb(255,2,4,10))
    if ($round) { $g.FillEllipse($bgBrush, 0, 0, $s, $s) } else {
        $r = $s * 0.18; $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc(0,0,$r*2,$r*2,180,90); $path.AddArc($s-$r*2,0,$r*2,$r*2,270,90); $path.AddArc($s-$r*2,$s-$r*2,$r*2,$r*2,0,90); $path.AddArc(0,$s-$r*2,$r*2,$r*2,90,90); $path.CloseFigure()
        $g.FillPath($bgBrush, $path)
    }
    $cx = $s/2; $cy = $s/2
    $cyan = [System.Drawing.Color]::FromArgb(255,0,220,255)
    foreach ($k in 0..2) {
        $rad = $s * (0.40 - 0.09*$k)
        $a = 255 - $k*50
        $w = [Math]::Max(1.0, $s * (0.032 - 0.004*$k))
        $glow = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40,0,220,255)), ($w*3.2)
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($a,0,220,255)), $w
        $start = 20 + $k*70
        foreach ($seg in 0..2) {
            $g.DrawArc($glow, $cx-$rad, $cy-$rad, $rad*2, $rad*2, ($start + $seg*120), 80)
            $g.DrawArc($pen, $cx-$rad, $cy-$rad, $rad*2, $rad*2, ($start + $seg*120), 80)
        }
    }
    $core = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,190,245,255))
    $cr = $s*0.07
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70,0,220,255))), $cx-$cr*2, $cy-$cr*2, $cr*4, $cr*4)
    $g.FillEllipse($core, $cx-$cr, $cy-$cr, $cr*2, $cr*2)
    $cp = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(160,0,220,255)), ([Math]::Max(1.0,$s*0.012))
    $g.DrawLine($cp, $cx, $s*0.06, $cx, $s*0.16); $g.DrawLine($cp, $cx, $s*0.84, $cx, $s*0.94)
    $g.DrawLine($cp, $s*0.06, $cy, $s*0.16, $cy); $g.DrawLine($cp, $s*0.84, $cy, $s*0.94, $cy)
    $g.Dispose(); return $bmp
}
function Save-Png($bmp, $path) { New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null; $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose() }

# ICO with PNG-compressed frames
$sizes = 16,24,32,48,64,128,256
$ms = New-Object System.IO.MemoryStream; $bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$blobs = @()
foreach ($sz in $sizes) { $b = New-Logo $sz; $t = New-Object System.IO.MemoryStream; $b.Save($t,[System.Drawing.Imaging.ImageFormat]::Png); $blobs += ,$t.ToArray(); $b.Dispose() }
$offset = 6 + 16*$sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$blobs[$i].Length); $bw.Write([uint32]$offset); $offset += $blobs[$i].Length
}
foreach ($bl in $blobs) { $bw.Write($bl) }
$bw.Flush()
New-Item -ItemType Directory -Force "$Root\src\Blacksite.Desktop\Assets" | Out-Null
[System.IO.File]::WriteAllBytes("$Root\src\Blacksite.Desktop\Assets\blacksite.ico", $ms.ToArray())
Save-Png (New-Logo 512) "$Root\src\Blacksite.UI\Assets\logo.png"
Save-Png (New-Logo 512) "$Root\Documentation\logo.png"

# Android launcher icons
$dens = @{ 'mdpi'=48; 'hdpi'=72; 'xhdpi'=96; 'xxhdpi'=144; 'xxxhdpi'=192 }
foreach ($d in $dens.Keys) {
    $res = "$Root\src\Blacksite.Android\Resources\mipmap-$d"
    Save-Png (New-Logo $dens[$d]) "$res\ic_launcher.png"
    Save-Png (New-Logo $dens[$d] $true) "$res\ic_launcher_round.png"
}
"icons done"
