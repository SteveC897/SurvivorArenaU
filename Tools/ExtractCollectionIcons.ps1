#requires -Version 7
<#
    从 Docs/images/吸血鬼幸存者截图01.png、02.png 抠出收藏界面的武器/被动图标。
    截图里每个格子 103x103：外圈暗紫边距 + 4px 金框 + 4px 橄榄色内斜面，内部是纯黑底。
    做法：
      1. 取格子内部 78x77 的纯黑区域；
      2. 从边缘泛洪，把与边缘连通的黑色背景设为透明（不碰图标内部的深色）；
      3. 清掉四角残留的金框/斜面像素；
      4. 按不透明区域裁边，居中放进固定 80x80 画布；
      5. 写 PNG + Unity Sprite meta。
    产物：Assets/Art/Generated/Icons/*.png
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [int]$CanvasSize = 80
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ProjectRoot) { $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }

# 复用 GenerateSprites.ps1 里的 C()/New-CvFromPixels/Write-SpriteMeta 等函数（不执行生成逻辑）
. (Join-Path $PSScriptRoot 'GenerateSprites.ps1') -NoRun

$OutDir = Join-Path $ProjectRoot 'Assets/Art/Generated/Icons'
$PreviewDir = Join-Path $ProjectRoot 'Docs/Generated'
New-Item -ItemType Directory -Force -Path $OutDir, $PreviewDir | Out-Null

# ---------------------------------------------------------------- geometry
# 01: 3 行 x 8 列；02: 第 1 行 8 个、第 2 行 5 个
$XS = @(7, 133, 258, 383, 509, 634, 759, 884)
$ROWS_01 = @(11, 136, 262)
$ROWS_02 = @(8, 133)
$INSET = 13
$CELL_W = 78
$CELL_H = 77

function Test-Gold([int]$r, [int]$g, [int]$b) {
    return ($r -ge 150 -and $g -ge 120 -and $b -le 150 -and [math]::Abs($r - $g) -le 45)
}

function Test-Bevel([int]$r, [int]$g, [int]$b) {
    return ([math]::Abs($r - 90) -le 30 -and [math]::Abs($g - 90) -le 30 -and $b -ge 20 -and $b -le 80)
}

<#
    返回 @{ px=int[]; size=int; bbox=@(...); tightW; tightH }
#>
function Get-IconCanvas([byte[]]$buf, [int]$stride, [int]$x0, [int]$y0, [int]$cw, [int]$ch, [int]$canvasSize, [bool]$keepBlack) {
    $n = $cw * $ch
    $bg = [bool[]]::new($n)
    $q = [System.Collections.Generic.Queue[int]]::new()

    # 泛洪种子：内部矩形四边上的纯黑像素
    for ($x = 0; $x -lt $cw; $x++) {
        foreach ($y in 0, ($ch - 1)) {
            $i = $y * $cw + $x
            $o = ($y0 + $y) * $stride + ($x0 + $x) * 4
            if ($buf[$o + 2] -le 10 -and $buf[$o + 1] -le 10 -and $buf[$o] -le 10) {
                if (-not $bg[$i]) { $bg[$i] = $true; $q.Enqueue($i) }
            }
        }
    }
    for ($y = 0; $y -lt $ch; $y++) {
        foreach ($x in 0, ($cw - 1)) {
            $i = $y * $cw + $x
            $o = ($y0 + $y) * $stride + ($x0 + $x) * 4
            if ($buf[$o + 2] -le 10 -and $buf[$o + 1] -le 10 -and $buf[$o] -le 10) {
                if (-not $bg[$i]) { $bg[$i] = $true; $q.Enqueue($i) }
            }
        }
    }

    while ($q.Count -gt 0) {
        $i = $q.Dequeue()
        $y = [int][math]::Floor($i / $cw); $x = $i - $y * $cw
        foreach ($d in @(-1, 1, -$cw, $cw)) {
            $j = $i + $d
            if ($j -lt 0 -or $j -ge $n) { continue }
            if ($d -eq -1 -and $x -eq 0) { continue }
            if ($d -eq 1 -and $x -eq $cw - 1) { continue }
            if ($bg[$j]) { continue }
            $ny = [int][math]::Floor($j / $cw); $nx = $j - $ny * $cw
            $o = ($y0 + $ny) * $stride + ($x0 + $nx) * 4
            if ($buf[$o + 2] -le 10 -and $buf[$o + 1] -le 10 -and $buf[$o] -le 10) {
                $bg[$j] = $true; $q.Enqueue($j)
            }
        }
    }

    # 四角 16x16 里残留的金框/斜面
    $corner = 16
    foreach ($cx in 0, ($cw - $corner)) {
        foreach ($cy in 0, ($ch - $corner)) {
            for ($y = 0; $y -lt $corner; $y++) {
                for ($x = 0; $x -lt $corner; $x++) {
                    $i = ($cy + $y) * $cw + ($cx + $x)
                    if ($bg[$i]) { continue }
                    $o = ($y0 + $cy + $y) * $stride + ($x0 + $cx + $x) * 4
                    $r = $buf[$o + 2]; $g = $buf[$o + 1]; $b = $buf[$o]
                    if ((Test-Gold $r $g $b) -or (Test-Bevel $r $g $b)) { $bg[$i] = $true }
                }
            }
        }
    }

    # 裁剪边缘 2px 内混进来的内框线（不同槽位有 1px 偏移）
    for ($y = 0; $y -lt $ch; $y++) {
        for ($x = 0; $x -lt $cw; $x++) {
            if ($x -ge 2 -and $x -lt $cw - 2 -and $y -ge 2 -and $y -lt $ch - 2) { continue }
            $i = $y * $cw + $x
            if ($bg[$i]) { continue }
            $o = ($y0 + $y) * $stride + ($x0 + $x) * 4
            $r = $buf[$o + 2]; $g = $buf[$o + 1]; $b = $buf[$o]
            if ((Test-Gold $r $g $b) -or (Test-Bevel $r $g $b)) { $bg[$i] = $true }
        }
    }

    # 被图案围住的纯黑：多数是环内/镂空处透出的底色（鞭子环内、宝石环中央等），去掉；
    # 只有两本“黑皮书”的黑封皮是图案（weapon_11 / weapon_12），保留。
    if (-not $keepBlack) {
        for ($y = 0; $y -lt $ch; $y++) {
            for ($x = 0; $x -lt $cw; $x++) {
                $i = $y * $cw + $x
                if ($bg[$i]) { continue }
                $o = ($y0 + $y) * $stride + ($x0 + $x) * 4
                if ($buf[$o + 2] -le 10 -and $buf[$o + 1] -le 10 -and $buf[$o] -le 10) { $bg[$i] = $true }
            }
        }
    }

    # 不透明区域的包围盒
    $minX = $cw; $maxX = -1; $minY = $ch; $maxY = -1
    for ($y = 0; $y -lt $ch; $y++) {
        for ($x = 0; $x -lt $cw; $x++) {
            $i = $y * $cw + $x
            if ($bg[$i]) { continue }
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
    if ($maxX -lt 0) { throw "empty icon at $x0,$y0" }

    $tw = $maxX - $minX + 1
    $th = $maxY - $minY + 1
    $ox = [int][math]::Floor(($canvasSize - $tw) / 2)
    $oy = [int][math]::Floor(($canvasSize - $th) / 2)

    $canvas = [int[]]::new($canvasSize * $canvasSize)
    for ($y = 0; $y -lt $th; $y++) {
        for ($x = 0; $x -lt $tw; $x++) {
            $i = ($minY + $y) * $cw + ($minX + $x)
            if ($bg[$i]) { continue }
            $o = ($y0 + $minY + $y) * $stride + ($x0 + $minX + $x) * 4
            $color = [System.Drawing.Color]::FromArgb(255, $buf[$o + 2], $buf[$o + 1], $buf[$o])
            $canvas[($oy + $y) * $canvasSize + ($ox + $x)] = $color.ToArgb()
        }
    }

    return [pscustomobject]@{
        px = $canvas; size = $canvasSize; tightW = $tw; tightH = $th
        bbox = @($minX, $minY, $maxX, $maxY)
    }
}

# ---------------------------------------------------------------- run
$src01 = Join-Path $ProjectRoot 'Docs/images/吸血鬼幸存者截图01.png'
$src02 = Join-Path $ProjectRoot 'Docs/images/吸血鬼幸存者截图02.png'

$jobs = [System.Collections.Generic.List[object]]::new()
$index = 0
foreach ($y0 in $ROWS_01) {
    foreach ($x0 in $XS) {
        $index++
        $jobs.Add([pscustomobject]@{ name = ('weapon_{0:d2}' -f $index); src = $src01; x0 = $x0; y0 = $y0; row = ([array]::IndexOf($ROWS_01, $y0) + 1); col = ([array]::IndexOf($XS, $x0) + 1); category = 'weapon' })
    }
}
$index = 0
foreach ($y0 in $ROWS_02) {
    $cols = if ($y0 -eq $ROWS_02[0]) { $XS } else { $XS[0..4] }
    foreach ($x0 in $cols) {
        $index++
        $jobs.Add([pscustomobject]@{ name = ('passive_{0:d2}' -f $index); src = $src02; x0 = $x0; y0 = $y0; row = ([array]::IndexOf($ROWS_02, $y0) + 1); col = ([array]::IndexOf($XS, $x0) + 1); category = 'passive' })
    }
}

$manifest = [System.Collections.Generic.List[object]]::new()
$maxTight = 0
foreach ($job in $jobs) {
    $img = [System.Drawing.Bitmap]::new($job.src)
    $rect = [System.Drawing.Rectangle]::new(0, 0, $img.Width, $img.Height)
    $bd = $img.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $bd.Stride
    $buf = [byte[]]::new($stride * $img.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $buf, 0, $buf.Length)
    $img.UnlockBits($bd); $img.Dispose()

    $keepBlack = $job.name -in @('weapon_11', 'weapon_12')
    $res = Get-IconCanvas $buf $stride ($job.x0 + $INSET) ($job.y0 + $INSET) $CELL_W $CELL_H $CanvasSize $keepBlack
    if ($res.tightW -gt $maxTight) { $maxTight = $res.tightW }
    if ($res.tightH -gt $maxTight) { $maxTight = $res.tightH }

    $cv = New-CvFromPixels $res.px $res.size $res.size
    $png = Join-Path $OutDir "$($job.name).png"
    $cv.Bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $cv.Bmp.Dispose()

    $rel = "Assets/Art/Generated/Icons/$($job.name).png"
    $guid = Write-SpriteMeta $png $rel 100 @(0, 0, 0, 0)

    $manifest.Add([pscustomobject]@{
        name = $job.name
        file = $rel
        category = $job.category
        row = $job.row
        col = $job.col
        source = "Docs/images/$(Split-Path $job.src -Leaf)"
        bbox = $res.bbox
        tight = "$($res.tightW)x$($res.tightH)"
        size = "$($res.size)x$($res.size)"
        guid = $guid
    })
}

$json = $manifest | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText((Join-Path $OutDir 'icons.manifest.json'), $json, [System.Text.UTF8Encoding]::new($false))

# ---------------------------------------------------------------- preview
function New-IconSheet([object[]]$items, [string]$path, [string]$title) {
    $scale = 2; $cols = 8; $cellW = 176; $cellH = 190; $pad = 16
    $rows = [int][math]::Ceiling($items.Count / $cols)
    $img = [System.Drawing.Bitmap]::new($pad * 2 + $cols * $cellW, $pad * 2 + 30 + $rows * $cellH)
    $g = [System.Drawing.Graphics]::FromImage($img)
    $g.SmoothingMode = 'None'; $g.InterpolationMode = 'NearestNeighbor'; $g.PixelOffsetMode = 'Half'
    $g.Clear([System.Drawing.Color]::FromArgb(255, 18, 16, 26))
    $titleFont = [System.Drawing.Font]::new('Consolas', 16, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $labelFont = [System.Drawing.Font]::new('Consolas', 11, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 232, 194, 90))
    $labelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 190, 188, 205))
    $tileA = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 32, 30, 42))
    $tileB = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 44, 41, 57))
    $g.DrawString($title, $titleFont, $titleBrush, [single]$pad, [single]8)

    for ($i = 0; $i -lt $items.Count; $i++) {
        $it = $items[$i]
        $col = $i % $cols; $row = [int][math]::Floor($i / $cols)
        $cx = $pad + $col * $cellW
        $cy = $pad + 30 + $row * $cellH
        $iw = 80 * $scale; $ih = 80 * $scale
        $sx = $cx + [int](($cellW - $iw) / 2)
        for ($yy = 0; $yy -lt $ih; $yy += 8) {
            for ($xx = 0; $xx -lt $iw; $xx += 8) {
                $brush = if (([int]($xx / 8) + [int]($yy / 8)) % 2 -eq 0) { $tileA } else { $tileB }
                $g.FillRectangle($brush, $sx + $xx, $cy + $yy, [math]::Min(8, $iw - $xx), [math]::Min(8, $ih - $yy))
            }
        }
        $bmp = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot $it.file))
        $g.DrawImage($bmp, [System.Drawing.Rectangle]::new($sx, $cy, $iw, $ih), 0, 0, $bmp.Width, $bmp.Height, [System.Drawing.GraphicsUnit]::Pixel)
        $bmp.Dispose()
        $g.DrawString($it.name, $labelFont, $labelBrush, [single]($cx + 6), [single]($cy + $ih + 4))
    }

    $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $titleFont.Dispose(); $labelFont.Dispose(); $titleBrush.Dispose(); $labelBrush.Dispose()
    $tileA.Dispose(); $tileB.Dispose(); $g.Dispose(); $img.Dispose()
}

$weapons = @($manifest | Where-Object { $_.category -eq 'weapon' })
$passives = @($manifest | Where-Object { $_.category -eq 'passive' })
New-IconSheet $weapons (Join-Path $PreviewDir 'preview_icons_weapons.png') "weapons (screenshot 01)"
New-IconSheet $passives (Join-Path $PreviewDir 'preview_icons_passives.png') "passives (screenshot 02)"

Write-Host ''
$manifest | Group-Object category | ForEach-Object { "$($_.Name): $($_.Count)" }
"max tight icon: ${maxTight}px  (canvas ${CanvasSize}px)"
Write-Host "Icons    -> $OutDir"
Write-Host "Preview  -> $(Join-Path $PreviewDir 'preview_icons_weapons.png')"
Write-Host "Preview  -> $(Join-Path $PreviewDir 'preview_icons_passives.png')"
