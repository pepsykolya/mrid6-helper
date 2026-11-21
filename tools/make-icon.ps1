# Рисует значок MRID6 Helper (дракон 蛟龙 на тёмной плитке) и собирает assets\mrid6.ico (PNG-кадры 16…256) + preview.png.
# Запуск: pwsh -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function Bez($p0, $p1, $p2, $p3, $t) {
    $u = 1 - $t
    ,@(($u*$u*$u*$p0[0] + 3*$u*$u*$t*$p1[0] + 3*$u*$t*$t*$p2[0] + $t*$t*$t*$p3[0]),
       ($u*$u*$u*$p0[1] + 3*$u*$u*$t*$p1[1] + 3*$u*$t*$t*$p2[1] + $t*$t*$t*$p3[1]))
}
function Lerp([double]$a, [double]$b, [double]$t) { $a + ($b - $a) * $t }
function Rgb($r, $g, $b, $a = 255) { [System.Drawing.Color]::FromArgb($a, [int]$r, [int]$g, [int]$b) }

# Осевая линия тела дракона (в координатах холста 256×256): хвост слева внизу → петля → голова справа вверху
$segs = @(
    @(@(34,186), @(52,238), @(132,244), @(146,196)),
    @(@(146,196), @(156,150), @(98,142), @(108,100)),
    @(@(108,100), @(114,70), @(150,70), @(186,50))
)
$pts = New-Object System.Collections.Generic.List[object]
foreach ($s in $segs) { for ($i = 0; $i -lt 70; $i++) { $pts.Add((Bez $s[0] $s[1] $s[2] $s[3] ($i / 70.0))) } }
$pts.Add((Bez $segs[2][0] $segs[2][1] $segs[2][2] $segs[2][3] 1.0))
$n = $pts.Count

function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    # плитка
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 52; $d = $r * 2
    $path.AddArc(4, 4, $d, $d, 180, 90); $path.AddArc(252 - $d, 4, $d, $d, 270, 90)
    $path.AddArc(252 - $d, 252 - $d, $d, $d, 0, 90); $path.AddArc(4, 252 - $d, $d, $d, 90, 90); $path.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 256, 256), (Rgb 30 36 52), (Rgb 8 10 16))
    $g.FillPath($bg, $path)
    $pen = New-Object System.Drawing.Pen (Rgb 0 168 232 200), 5
    $g.DrawPath($pen, $path)

    # лёгкое свечение за драконом
    $glow = New-Object System.Drawing.Drawing2D.GraphicsPath; $glow.AddEllipse(30, 30, 200, 200)
    $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush $glow
    $pg.CenterColor = Rgb 0 150 220 70; $pg.SurroundColors = @((Rgb 0 150 220 0))
    $g.FillPath($pg, $glow)

    $rm = if ($size -le 48) { 1.45 } else { 1.0 }
    $g.ScaleTransform(0.74, 0.74); $g.TranslateTransform(14, 70)

    # шипы вдоль спины (рисуем до тела, чтобы тело их перекрывало у основания)
    $orange = New-Object System.Drawing.SolidBrush (Rgb 255 160 40)
    for ($k = 10; $k -lt $n - 12; $k += 9) {
        $s = $k / ($n - 1.0)
        $p = $pts[$k]; $q = $pts[$k + 1]; $pp = $pts[$k - 1]
        $tx = $q[0] - $pp[0]; $ty = $q[1] - $pp[1]; $len = [math]::Sqrt($tx*$tx + $ty*$ty); $tx /= $len; $ty /= $len
        $nx = $ty; $ny = -$tx                       # нормаль слева от направления движения (внешняя сторона петли)
        $rad = $rm * $(if ($s -lt 0.55) { 3 + 12 * [math]::Pow([math]::Min(1, $s / 0.55), 0.8) } else { 15 - 3 * ($s - 0.55) / 0.45 })
        $h = 9 + 5 * [math]::Sin($s * 3.14159)
        $bx = $p[0] + $nx * ($rad - 2); $by = $p[1] + $ny * ($rad - 2)
        $tri = @(
            (New-Object System.Drawing.PointF ($bx - $tx * 5), ($by - $ty * 5)),
            (New-Object System.Drawing.PointF ($bx + $tx * 5), ($by + $ty * 5)),
            (New-Object System.Drawing.PointF ($bx + $nx * $h - $tx * 6), ($by + $ny * $h - $ty * 6)))
        $g.FillPolygon($orange, [System.Drawing.PointF[]]$tri)
    }

    # тело: круги с убывающим к хвосту радиусом и градиентом цвета
    for ($k = 0; $k -lt $n; $k++) {
        $s = $k / ($n - 1.0)
        $rad = $rm * $(if ($s -lt 0.55) { 3 + 12 * [math]::Pow([math]::Min(1, $s / 0.55), 0.8) } else { 15 - 3 * ($s - 0.55) / 0.45 })
        $c = Rgb (Lerp 20 90 $s) (Lerp 130 240 $s) (Lerp 255 215 $s)
        $br = New-Object System.Drawing.SolidBrush $c
        $g.FillEllipse($br, $pts[$k][0] - $rad, $pts[$k][1] - $rad, $rad * 2, $rad * 2)
        $br.Dispose()
    }
    # блик на теле
    $hl = New-Object System.Drawing.Pen (Rgb 200 255 255 90), 3
    for ($k = 12; $k -lt $n - 14; $k += 1) {
        $s = $k / ($n - 1.0); $rad = $rm * $(if ($s -lt 0.55) { 3 + 12 * [math]::Pow([math]::Min(1, $s / 0.55), 0.8) } else { 15 - 3 * ($s - 0.55) / 0.45 })
        $p = $pts[$k]; $q = $pts[$k + 1]; $pp = $pts[$k - 1]
        $tx = $q[0] - $pp[0]; $ty = $q[1] - $pp[1]; $len = [math]::Sqrt($tx*$tx + $ty*$ty); $tx /= $len; $ty /= $len
        $bx = $p[0] + $ty * $rad * 0.45; $by = $p[1] - $tx * $rad * 0.45
        $g.FillEllipse((New-Object System.Drawing.SolidBrush (Rgb 210 255 255 70)), $bx - 1.3, $by - 1.3, 2.6, 2.6)
    }

    # голова
    $e = $pts[$n - 1]; $e0 = $pts[$n - 6]
    $ang = [math]::Atan2($e[1] - $e0[1], $e[0] - $e0[0])
    $ca = [math]::Cos($ang); $sa = [math]::Sin($ang)
    $hs = if ($size -le 48) { 1.6 } else { 1.35 }
    function W($lx, $ly) { New-Object System.Drawing.PointF ($e[0] + $hs * ($lx * $ca - $ly * $sa)), ($e[1] + $hs * ($lx * $sa + $ly * $ca)) }
    $head = @((W -6 -14), (W 16 -14), (W 36 -9), (W 54 -2), (W 52 4), (W 38 6), (W 28 12), (W 8 15), (W -8 12))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush (Rgb 110 245 225)), [System.Drawing.PointF[]]$head)
    $g.DrawPolygon((New-Object System.Drawing.Pen (Rgb 20 90 120), 2), [System.Drawing.PointF[]]$head)
    # рога
    $horn1 = @((W 0 -13), (W -22 -38), (W -28 -33), (W -12 -12))
    $horn2 = @((W 12 -14), (W -6 -42), (W -11 -37), (W 2 -13))
    $g.FillPolygon($orange, [System.Drawing.PointF[]]$horn1); $g.FillPolygon((New-Object System.Drawing.SolidBrush (Rgb 255 190 70)), [System.Drawing.PointF[]]$horn2)
    # глаз
    $eye = W 14 -4
    $g.FillEllipse([System.Drawing.Brushes]::White, $eye.X - 6, $eye.Y - 6, 12, 12)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (Rgb 20 20 40)), $eye.X - 2.6, $eye.Y - 2.6, 5.2, 5.2)
    # усы-«барбели»
    $wp = New-Object System.Drawing.Pen (Rgb 255 160 40), 4
    $wp.StartCap = 'Round'; $wp.EndCap = 'Round'
    $g.DrawBezier($wp, (W 50 0), (W 66 8), (W 78 -6), (W 92 6))
    $g.DrawBezier($wp, (W 48 4), (W 62 18), (W 74 10), (W 84 22))
    # жемчужина впереди
    $pr = W 98 -8
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (Rgb 255 210 90)), $pr.X - 12, $pr.Y - 12, 24, 24)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (Rgb 255 245 200)), $pr.X - 7, $pr.Y - 8, 9, 9)

    $g.Dispose()
    return $bmp
}

$sizes = 256, 128, 64, 48, 32, 24, 16
$png = @{}
foreach ($sz in $sizes) {
    $b = Render $sz
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $png[$sz] = $ms.ToArray()
    if ($sz -eq 256) { $b.Save((Join-Path $assets 'preview.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $b.Dispose()
}

# ICO с PNG-кадрами
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $w.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz }))); $w.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$png[$sz].Length); $w.Write([uint32]$offset)
    $offset += $png[$sz].Length
}
foreach ($sz in $sizes) { $w.Write($png[$sz]) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'mrid6.ico'), $out.ToArray())
"ok: " + (Join-Path $assets 'mrid6.ico') + "  (" + $out.Length + " bytes)"
