# Рисует значок MRID6 Helper — фирменный знак MECHREVO (оранжевый шестигранник, разрезанный по диагонали, со ступенчатым вырезом),
# обведённый по фотографии логотипа на крышке ноутбука. Собирает assets\mrid6.ico (PNG-кадры 16…256) и assets\preview.png.
# Запуск: pwsh -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null

# Вершины двух половин в координатах фото (центр знака ≈ 847,762): верхняя-левая и нижняя-правая половины.
$upper = @(@(662,642), @(862,547), @(1007,659), @(806,757), @(798,811), @(668,869), @(657,858))
$lower = @(@(692,883), @(897,787), @(904,730), @(1034,676), @(1044,684), @(1026,891), @(832,976))
$cx = 847.0; $cy = 762.0

function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size * 0.92 / 440.0                     # высота знака ≈ 430 единиц фото → 92 % холста
    $mx = $size / 2.0; $my = $size / 2.0
    $top = [System.Drawing.Color]::FromArgb(255, 255, 156, 24)
    $bot = [System.Drawing.Color]::FromArgb(255, 255, 112, 0)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $size), $top, $bot)
    $pen = New-Object System.Drawing.Pen $bot, ([math]::Max(1.0, $size / 64.0))
    $pen.LineJoin = 'Round'
    foreach ($poly in @($upper, $lower)) {
        $pts = @()
        foreach ($p in $poly) { $pts += New-Object System.Drawing.PointF ($mx + ($p[0] - $cx) * $s), ($my + ($p[1] - $cy) * $s) }
        $arr = [System.Drawing.PointF[]]$pts
        $g.FillPolygon($grad, $arr)
        $g.DrawPolygon($pen, $arr)               # лёгкое скругление углов
    }
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
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$png[$sz].Length); $w.Write([uint32]$offset)
    $offset += $png[$sz].Length
}
foreach ($sz in $sizes) { $w.Write($png[$sz]) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'mrid6.ico'), $out.ToArray())
"ok: " + (Join-Path $assets 'mrid6.ico') + "  (" + $out.Length + " bytes)"
