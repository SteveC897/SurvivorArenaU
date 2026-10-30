#requires -Version 7
<#
    程序化生成 2D 像素风 sprite（PNG + Unity .meta）。
    用法：pwsh -File Tools/GenerateSprites.ps1
    产物：Assets/Art/Generated/*.png 与同名 .meta（Sprite 导入、Point 过滤、PPU 按资源设定）
          Docs/Generated/preview_sprites.png（8 倍放大预览，仅供查看，不进 Assets）
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$OutDir,
    [string]$PreviewDir,
    [switch]$NoRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not $ProjectRoot) { $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
if (-not $OutDir)     { $OutDir     = Join-Path $ProjectRoot 'Assets/Art/Generated' }
if (-not $PreviewDir) { $PreviewDir = Join-Path $ProjectRoot 'Docs/Generated' }

New-Item -ItemType Directory -Force -Path $OutDir, $PreviewDir | Out-Null

# ---------------------------------------------------------------- palette
$C = @{
    Outline     = '#140f1e'
    Skin        = '#f0c39a'
    Eye         = '#2a1a24'
    Mouth       = '#b2705a'
    Hair        = '#33203f'
    HairLight   = '#4a3057'
    Hat         = '#4c2b6b'
    HatLight    = '#6f4494'
    HatDark     = '#33204a'
    HatBand     = '#e0b24a'
    Robe        = '#2f7f8f'
    RobeLight   = '#48a2ac'
    RobeDark    = '#1f5a66'
    Cape        = '#5a3f72'
    Gold        = '#e8c25a'
    Wood        = '#8a5a33'
    Boot        = '#2a1f33'
    Belt        = '#b8893a'

    BatBody     = '#3b2f4d'
    BatWing     = '#6a4b85'
    BatEye      = '#ff4d5e'
    Fang        = '#efe6d0'

    BatRedWing  = '#b23a42'
    BatRedBody  = '#6a1f2b'
    BatRedEye   = '#ffd24a'

    BoltOuter   = '#ff7a2f'
    BoltMid     = '#ffc43d'
    BoltCore    = '#fff6d0'

    BoltBlueOuter = '#2f6dff'
    BoltBlueMid   = '#62b6ff'
    BoltBlueCore  = '#eef8ff'

    Gem         = '#45e0ea'
    GemLight    = '#b6fbff'
    GemDark     = '#1f8f9e'

    GemGreen      = '#4bdd7a'
    GemGreenLight = '#c9ffdd'
    GemGreenDark  = '#1f8f4a'

    GemRed        = '#ff3b4a'
    GemRedLight   = '#ffd2d6'
    GemRedDark    = '#a31223'

    Chicken     = '#c9782f'
    ChickenDark = '#8f4a1c'
    ChickenLite = '#e8a04f'
    Bone        = '#efe6d0'

    PanelFill   = '#1a1430'
    PanelInner  = '#241b3e'
    GoldTrim    = '#c9a24a'
    GoldTrimHi  = '#e8c987'

    Grass       = '#2b4630'
    GrassDark   = '#223a27'
    GrassLite   = '#3f6444'
}

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }

# ---------------------------------------------------------------- canvas / primitives
function New-Cv([int]$w, [int]$h) {
    $bmp = [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $g.Clear([System.Drawing.Color]::Transparent)
    [pscustomobject]@{ W = $w; H = $h; Bmp = $bmp; G = $g }
}

function Fill-Rect($cv, [string]$hex, [int]$x, [int]$y, [int]$w, [int]$h) {
    $b = [System.Drawing.SolidBrush]::new((C $hex))
    $cv.G.FillRectangle($b, $x, $y, $w, $h)
    $b.Dispose()
}

function Fill-Ellipse($cv, [string]$hex, [int]$x, [int]$y, [int]$w, [int]$h) {
    $b = [System.Drawing.SolidBrush]::new((C $hex))
    $cv.G.FillEllipse($b, $x, $y, $w, $h)
    $b.Dispose()
}

function Fill-Circle($cv, [string]$hex, [int]$cx, [int]$cy, [int]$r) {
    Fill-Ellipse $cv $hex ($cx - $r) ($cy - $r) (2 * $r) (2 * $r)
}

function Fill-Poly($cv, [string]$hex, [int[]]$xy) {
    $n = [int]($xy.Length / 2)
    $p = [System.Drawing.Point[]]::new($n)
    for ($i = 0; $i -lt $n; $i++) {
        $p[$i] = [System.Drawing.Point]::new($xy[2 * $i], $xy[2 * $i + 1])
    }
    $b = [System.Drawing.SolidBrush]::new((C $hex))
    $cv.G.FillPolygon($b, $p)
    $b.Dispose()
}

function Draw-Line($cv, [string]$hex, [int]$x1, [int]$y1, [int]$x2, [int]$y2, [int]$thick = 1) {
    $pen = [System.Drawing.Pen]::new((C $hex), $thick)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Flat
    $cv.G.DrawLine($pen, $x1, $y1, $x2, $y2)
    $pen.Dispose()
}

# ---------------------------------------------------------------- post-processing
function Get-Pixels($cv) {
    $px = [int[]]::new($cv.W * $cv.H)
    for ($y = 0; $y -lt $cv.H; $y++) {
        for ($x = 0; $x -lt $cv.W; $x++) {
            $px[$y * $cv.W + $x] = $cv.Bmp.GetPixel($x, $y).ToArgb()
        }
    }
    return , $px
}

function Set-Pixels($cv, [int[]]$px) {
    for ($y = 0; $y -lt $cv.H; $y++) {
        for ($x = 0; $x -lt $cv.W; $x++) {
            $cv.Bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($px[$y * $cv.W + $x]))
        }
    }
}

function Mix([int]$argb, [int]$target, [double]$amount) {
    $a = ($argb -shr 24) -band 0xFF
    $r = ($argb -shr 16) -band 0xFF
    $g = ($argb -shr 8) -band 0xFF
    $b = $argb -band 0xFF
    $tr = ($target -shr 16) -band 0xFF
    $tg = ($target -shr 8) -band 0xFF
    $tb = $target -band 0xFF
    $r = [int][math]::Round($r + ($tr - $r) * $amount)
    $g = [int][math]::Round($g + ($tg - $g) * $amount)
    $b = [int][math]::Round($b + ($tb - $b) * $amount)
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b).ToArgb()
}

<#
    像素美术收尾：
      1) 4/8 邻域描边（画在剪影外侧，不缩形）
      2) 内侧斜面：左上受光提亮，右下背光压暗
#>
function Add-PixelStyle($cv, [string]$outlineHex, [double]$lit = 0.18, [double]$shade = 0.20, [switch]$NoBevel) {
    $w = $cv.W; $h = $cv.H
    $src = Get-Pixels $cv
    $outline = (C $outlineHex).ToArgb()
    $dst = [int[]]::new($src.Length)
    [Array]::Copy($src, $dst, $src.Length)

    $solid = [bool[]]::new($src.Length)
    for ($i = 0; $i -lt $src.Length; $i++) { $solid[$i] = (($src[$i] -shr 24) -band 0xFF) -gt 0 }

    # bevel
    if (-not $NoBevel) {
        for ($y = 0; $y -lt $h; $y++) {
            for ($x = 0; $x -lt $w; $x++) {
                $i = $y * $w + $x
                if (-not $solid[$i]) { continue }
                $upT = ($y -eq 0) -or (-not $solid[($y - 1) * $w + $x])
                $leftT = ($x -eq 0) -or (-not $solid[$y * $w + ($x - 1)])
                $dnT = ($y -eq $h - 1) -or (-not $solid[($y + 1) * $w + $x])
                $rtT = ($x -eq $w - 1) -or (-not $solid[$y * $w + ($x + 1)])
                if ($dnT -or $rtT) { $dst[$i] = Mix $src[$i] 0x000000 $shade }
                elseif ($upT -or $leftT) { $dst[$i] = Mix $src[$i] 0xFFFFFF $lit }
            }
        }
    }

    # outline outside silhouette (8-neighbour, so diagonals stay connected)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $i = $y * $w + $x
            if ($solid[$i]) { continue }
            $touch = $false
            for ($dy = -1; $dy -le 1 -and -not $touch; $dy++) {
                for ($dx = -1; $dx -le 1; $dx++) {
                    if ($dx -eq 0 -and $dy -eq 0) { continue }
                    $nx = $x + $dx; $ny = $y + $dy
                    if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h) { continue }
                    if ($solid[$ny * $w + $nx]) { $touch = $true; break }
                }
            }
            if ($touch) { $dst[$i] = $outline }
        }
    }

    Set-Pixels $cv $dst
    $cv.G.Dispose()
}

# ---------------------------------------------------------------- sprites
function Build-Player([int]$bob = 0, [int]$step = 0) {
    $cv = New-Cv 32 32
    $y = $bob
    $sw = $step   # -1 / 0 / +1：长袍与斗篷的摆动方向

    # shoulder cape behind body
    Fill-Poly $cv $C.Cape 12, (17 + $y), 20, (17 + $y), 24, (27 + $y), 8, (27 + $y)
    Fill-Poly $cv $C.Cape 8, (26 + $y), 24, (26 + $y), 23, (29 + $y), 9, (29 + $y)

    # hair behind head
    Fill-Ellipse $cv $C.Hair 9 (8 + $y) 14 14

    # boots stay on the ground while the body bobs: absolute y, no +$y
    if ($step -eq 0) {
        Fill-Rect $cv $C.Boot 10 27 4 3
        Fill-Rect $cv $C.Boot 18 27 4 3
    } elseif ($step -lt 0) {
        Fill-Rect $cv $C.Boot 18 27 3 2
    } else {
        Fill-Rect $cv $C.Boot 10 27 3 2
    }

    # robe (hem dips toward the leading foot and lifts on the trailing side)
    $hemLx = 9
    $hemRx = 23
    if ($step -eq 0) {
        $hemLy = 28 + $y; $hemRy = 28 + $y
    } else {
        $hemLy = 27 - $sw + $y
        $hemRy = 27 + $sw + $y
    }
    Fill-Poly $cv $C.Robe 12, (17 + $y), 20, (17 + $y), $hemRx, $hemRy, $hemLx, $hemLy
    Fill-Poly $cv $C.RobeDark 13, (17 + $y), 19, (17 + $y), 18, (21 + $y), 14, (21 + $y)
    Fill-Rect $cv $C.Belt 11 (22 + $y) 10 2
    Fill-Rect $cv $C.Gold 15 (22 + $y) 2 2

    # fabric fold on the trailing side
    if ($step -lt 0) {
        Draw-Line $cv $C.RobeDark 18 (23 + $y) 21 (27 + $y) 1
    } elseif ($step -gt 0) {
        Draw-Line $cv $C.RobeDark 14 (23 + $y) 11 (27 + $y) 1
    }

    # leading boot, in front of the hem and overlapping it
    if ($step -lt 0) {
        Fill-Rect $cv $C.Boot 8 28 4 3
    } elseif ($step -gt 0) {
        Fill-Rect $cv $C.Boot 20 28 4 3
    }

    # head
    Fill-Ellipse $cv $C.Skin 11 (9 + $y) 10 10
    Fill-Rect $cv $C.Hair 10 (12 + $y) 2 6
    Fill-Rect $cv $C.Hair 20 (12 + $y) 2 6

    # face
    Fill-Rect $cv $C.Eye 13 (14 + $y) 2 2
    Fill-Rect $cv $C.Eye 18 (14 + $y) 2 2
    Fill-Rect $cv '#ffffff' 13 (14 + $y) 1 1
    Fill-Rect $cv '#ffffff' 18 (14 + $y) 1 1
    Fill-Rect $cv $C.Mouth 15 (17 + $y) 2 1

    # witch hat: drooping brim + tall crown with a bent tip
    Fill-Poly $cv $C.HatDark 2, (13 + $y), 6, (9 + $y), 16, (8 + $y), 26, (9 + $y), 30, (13 + $y), 26, (14 + $y), 16, (13 + $y), 6, (14 + $y)
    Fill-Ellipse $cv $C.Hat 4 (8 + $y) 24 5
    Fill-Poly $cv $C.Hat 10, (12 + $y), 12, (6 + $y), 15, (2 + $y), 17, (1 + $y), 19, (5 + $y), 21, (12 + $y)
    Fill-Poly $cv $C.HatLight 13, (11 + $y), 14, (6 + $y), 16, (3 + $y), 16, (11 + $y)
    Fill-Rect $cv $C.HatBand 11 (10 + $y) 11 2

    # left arm
    Fill-Poly $cv $C.Robe 7, (18 + $y), 10, (18 + $y), 9, (22 + $y), 7, (23 + $y)
    Fill-Circle $cv $C.Skin 6 (23 + $y) 2

    # right arm holding wand
    Fill-Poly $cv $C.Robe 19, (19 + $y), 24, (18 + $y), 25, (21 + $y), 21, (22 + $y)
    Fill-Circle $cv $C.Skin 25 (19 + $y) 2

    # wand
    Draw-Line $cv $C.Wood 24 (22 + $y) 29 (10 + $y) 2
    Fill-Circle $cv $C.Gold 29 (9 + $y) 2
    Fill-Rect $cv $C.BoltCore 29 (8 + $y) 1 1

    Add-PixelStyle $cv $C.Outline
    return $cv
}

function Build-Bat([string]$wing, [string]$body, [string]$eye) {
    if (-not $wing) { $wing = $C.BatWing }
    if (-not $body) { $body = $C.BatBody }
    if (-not $eye) { $eye = $C.BatEye }
    $cv = New-Cv 24 24

    # wings (left, then mirrored): shoulder at x=10, scalloped trailing edge
    $left = @(
        10, 10, 7, 7, 4, 7, 2, 9, 4, 10, 1, 12, 4, 13, 2, 16, 6, 14, 6, 18, 9, 16, 9, 19, 11, 15
    )
    Fill-Poly $cv $wing $left
    $right = @()
    for ($i = 0; $i -lt $left.Length; $i += 2) {
        $right += (24 - $left[$i]); $right += $left[$i + 1]
    }
    Fill-Poly $cv $wing $right

    # wing membrane ribs
    Draw-Line $cv $body 10 12 3 9 1
    Draw-Line $cv $body 14 12 21 9 1

    # body + head
    Fill-Ellipse $cv $body 9 (10) 6 9
    Fill-Ellipse $cv $body 9 (6) 6 6

    # ears
    Fill-Poly $cv $body 9, 8, 9, 3, 12, 6
    Fill-Poly $cv $body 15, 8, 15, 3, 12, 6

    # eyes + fangs
    Fill-Rect $cv $eye 10 9 1 2
    Fill-Rect $cv $eye 13 9 1 2
    Fill-Rect $cv $C.Fang 10 12 1 1
    Fill-Rect $cv $C.Fang 13 12 1 1

    Add-PixelStyle $cv $C.Outline -shade 0.24
    return $cv
}

function Build-Bolt([string]$outer, [string]$mid, [string]$core) {
    if (-not $outer) { $outer = $C.BoltOuter }
    if (-not $mid) { $mid = $C.BoltMid }
    if (-not $core) { $core = $C.BoltCore }
    $cv = New-Cv 16 16
    Fill-Circle $cv $outer 8 8 6
    Fill-Circle $cv $mid 8 8 4
    Fill-Circle $cv $core 8 8 2
    # four-point sparkle
    Fill-Rect $cv $mid 7 1 2 2
    Fill-Rect $cv $mid 7 13 2 2
    Fill-Rect $cv $mid 1 7 2 2
    Fill-Rect $cv $mid 13 7 2 2
    Add-PixelStyle $cv $C.Outline -lit 0.22 -shade 0.18
    return $cv
}

function Build-Gem([string]$base, [string]$light, [string]$dark) {
    if (-not $base) { $base = $C.Gem }
    if (-not $light) { $light = $C.GemLight }
    if (-not $dark) { $dark = $C.GemDark }
    $cv = New-Cv 12 12
    Fill-Poly $cv $base 6, 1, 11, 5, 6, 11, 1, 5
    Fill-Poly $cv $dark 6, 1, 11, 5, 6, 5
    Fill-Poly $cv $light 6, 1, 1, 5, 6, 5
    Fill-Rect $cv '#ffffff' 4 3 1 2
    Add-PixelStyle $cv $C.Outline -lit 0.12 -shade 0.16
    return $cv
}

function Build-Chicken() {
    $cv = New-Cv 20 20
    # drumstick: bone knobs + shaft on the left, tapering meat to the right
    Fill-Poly $cv $C.Chicken 5, 8, 8, 6, 15, 5, 18, 10, 17, 15, 12, 17, 7, 15
    Fill-Ellipse $cv $C.Chicken 6 5 13 12
    Fill-Ellipse $cv $C.ChickenLite 9 6 7 4
    Fill-Ellipse $cv $C.ChickenDark 8 13 9 4
    Fill-Rect $cv $C.Bone 2 8 6 4
    Fill-Ellipse $cv $C.Bone 1 6 4 4
    Fill-Ellipse $cv $C.Bone 1 11 4 4
    Fill-Rect $cv $C.ChickenDark 9 12 5 1
    Add-PixelStyle $cv $C.Outline -lit 0.16 -shade 0.18
    return $cv
}

function Build-Panel() {
    $cv = New-Cv 48 48
    Fill-Rect $cv $C.PanelFill 0 0 48 48
    Fill-Rect $cv $C.PanelInner 4 4 40 40
    # gold frame (2 px, drawn as rects to keep corners crisp)
    $t = 2; $off = 2
    Fill-Rect $cv $C.GoldTrim $off $off 44 $t
    Fill-Rect $cv $C.GoldTrim $off (46 - $t) 44 $t
    Fill-Rect $cv $C.GoldTrim $off $off $t 44
    Fill-Rect $cv $C.GoldTrim (46 - $t) $off $t 44
    # highlight on top/left of frame
    Fill-Rect $cv $C.GoldTrimHi $off $off 44 1
    Fill-Rect $cv $C.GoldTrimHi $off $off 1 44
    # corner diamonds
    foreach ($cx in 8, 40) {
        foreach ($cy in 8, 40) {
            Fill-Poly $cv $C.GoldTrimHi $cx, ($cy - 3), ($cx + 3), $cy, $cx, ($cy + 3), ($cx - 3), $cy
        }
    }
    Add-PixelStyle $cv $C.Outline -NoBevel
    return $cv
}

function Build-Grass() {
    $cv = New-Cv 32 32
    Fill-Rect $cv $C.Grass 0 0 32 32
    $rnd = [System.Random]::new(20261022)
    # grass clumps, drawn with wrapped copies so the tile stays seamless
    for ($i = 0; $i -lt 70; $i++) {
        $x = $rnd.Next(0, 32); $yy = $rnd.Next(1, 28)
        $hgt = $rnd.Next(2, 5)
        $col = if ($rnd.NextDouble() -lt 0.55) { $C.GrassLite } else { $C.GrassDark }
        $wide = $rnd.NextDouble() -lt 0.4
        foreach ($ox in -32, 0, 32) {
            foreach ($oy in -32, 0, 32) {
                Fill-Rect $cv $col ($x + $ox) ($yy + $oy) 1 $hgt
                if ($wide) { Fill-Rect $cv $col ($x + 1 + $ox) ($yy + 1 + $oy) 1 ($hgt - 1) }
            }
        }
    }
    # a few darker patches + pebbles
    foreach ($p in @(@(5, 9), @(21, 4), @(14, 22), @(27, 18))) {
        Fill-Ellipse $cv $C.GrassDark ($p[0] - 3) ($p[1] - 1) 6 3
    }
    foreach ($p in @(@(11, 6), @(24, 25), @(3, 20))) {
        Fill-Rect $cv '#3a3c45' $p[0] $p[1] 1 1
        Fill-Rect $cv '#2a2c33' ($p[0] + 1) ($p[1] + 1) 1 1
    }
    Add-PixelStyle $cv $C.Outline -NoBevel
    return $cv
}

# 标题字：用系统字体的字形路径描边 + 金色渐变，再按 pixel 倍降采样成像素风
function Build-Title([string]$text, [string]$fontName = 'Georgia', [int]$fontPixels = 96, [int]$pixel = 3) {
    try { $family = [System.Drawing.FontFamily]::new($fontName) }
    catch { $family = [System.Drawing.FontFamily]::new('Segoe UI') }

    $em = [single]($fontPixels * $pixel)
    $fmt = [System.Drawing.StringFormat]::new()
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddString($text, $family, [int][System.Drawing.FontStyle]::Bold, $em, [System.Drawing.PointF]::new(0, 0), $fmt)
    $bounds = $path.GetBounds()
    $family.Dispose(); $fmt.Dispose()

    $pad = 10 * $pixel
    $shx = 8 * $pixel
    $shy = 10 * $pixel
    $stroke = 4 * $pixel     # pen width; half of it stays outside the glyph
    $W = [int][math]::Ceiling($bounds.Width + 2 * $pad + $shx + $stroke)
    $H = [int][math]::Ceiling($bounds.Height + 2 * $pad + $shy + $stroke)

    $mat = [System.Drawing.Drawing2D.Matrix]::new()
    $mat.Translate(-$bounds.X + $pad + $stroke / 2, -$bounds.Y + $pad)
    $path.Transform($mat)
    $mat.Dispose()

    $big = [System.Drawing.Bitmap]::new($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # hard drop shadow
    $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(150, 0, 0, 0))
    $g.TranslateTransform($shx, $shy)
    $g.FillPath($shadow, $path)
    $g.ResetTransform()
    $shadow.Dispose()

    # dark outline
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 30, 18, 10), [single]$stroke)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $path)
    $pen.Dispose()

    # gold gradient fill (top pale gold -> bottom ochre, matching ui_panel's trim)
    $rect = [System.Drawing.RectangleF]::new(0, [single]$pad, [single]$W, [single]$bounds.Height)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $rect, (C '#fbeab4'), (C '#8a5518'), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $blend = [System.Drawing.Drawing2D.ColorBlend]::new(3)
    $blend.Colors[0] = (C '#fbeab4')
    $blend.Colors[1] = (C '#dfb25c')
    $blend.Colors[2] = (C '#8a5518')
    $blend.Positions[0] = 0.0
    $blend.Positions[1] = 0.55
    $blend.Positions[2] = 1.0
    $brush.InterpolationColors = $blend
    $g.FillPath($brush, $path)
    $brush.Dispose()

    # faint light rim on the upper half to fake a bevel
    $g.SetClip([System.Drawing.RectangleF]::new(0, [single]($pad), [single]$W, [single]($bounds.Height * 0.45)))
    $rim = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(110, 255, 246, 208), [single](1.5 * $pixel))
    $rim.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($rim, $path)
    $rim.Dispose()
    $g.ResetClip()

    # read back the supersampled bitmap
    $rectI = [System.Drawing.Rectangle]::new(0, 0, $W, $H)
    $bd = $big.LockBits($rectI, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $bd.Stride
    $buf = [byte[]]::new($stride * $H)
    [System.Runtime.InteropServices.Marshal]::Copy($bd.Scan0, $buf, 0, $buf.Length)
    $big.UnlockBits($bd)
    $g.Dispose(); $big.Dispose(); $path.Dispose()

    # box-downsample to the pixel-art grid, keeping alpha-weighted colour
    $mw = [int][math]::Floor($W / $pixel); $mh = [int][math]::Floor($H / $pixel)
    $px = [int[]]::new($mw * $mh)
    $need = 128 * $pixel * $pixel
    for ($y = 0; $y -lt $mh; $y++) {
        for ($x = 0; $x -lt $mw; $x++) {
            $sa = 0; $sr = 0; $sg = 0; $sb = 0
            for ($dy = 0; $dy -lt $pixel; $dy++) {
                $row = ($y * $pixel + $dy) * $stride
                for ($dx = 0; $dx -lt $pixel; $dx++) {
                    $o = $row + ($x * $pixel + $dx) * 4
                    $a = $buf[$o + 3]
                    $sa += $a
                    $sr += $buf[$o + 2] * $a
                    $sg += $buf[$o + 1] * $a
                    $sb += $buf[$o] * $a
                }
            }
            if ($sa -lt $need) { continue }
            $r = [int][math]::Round($sr / $sa); $gg = [int][math]::Round($sg / $sa); $b = [int][math]::Round($sb / $sa)
            $px[$y * $mw + $x] = [System.Drawing.Color]::FromArgb(255, $r, $gg, $b).ToArgb()
        }
    }
    return New-CvFromPixels $px $mw $mh
}

function New-CvFromPixels([int[]]$argb, [int]$w, [int]$h) {
    $bmp = [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rect = [System.Drawing.Rectangle]::new(0, 0, $w, $h)
    $bd = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $bd.Stride
    $bytes = [byte[]]::new($stride * $h)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $argb[$y * $w + $x]
            $o = $y * $stride + $x * 4
            $bytes[$o] = $c -band 0xFF
            $bytes[$o + 1] = ($c -shr 8) -band 0xFF
            $bytes[$o + 2] = ($c -shr 16) -band 0xFF
            $bytes[$o + 3] = ($c -shr 24) -band 0xFF
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $bd.Scan0, $bytes.Length)
    $bmp.UnlockBits($bd)
    return [pscustomobject]@{ W = $w; H = $h; Bmp = $bmp; G = $null }
}

# ---------------------------------------------------------------- export
function Get-MetaGuid([string]$seed) {
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $hash = $md5.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($seed))
    $md5.Dispose()
    return (($hash | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Write-SpriteMeta([string]$path, [string]$relPath, [int]$ppu, [int[]]$border, [switch]$Tiled) {
    $wrap = if ($Tiled) { 0 } else { 1 }   # 0 = Repeat, 1 = Clamp
    $bx, $by, $bz, $bw = $border
    $metaPath = "$path.meta"

    # Unity rewrites the meta on import (adds spriteID, bumps serializedVersion).
    # If it already exists, patch only the settings we own so GUID/spriteID survive
    # and existing scene references keep working.
    if (Test-Path $metaPath) {
        $text = [System.IO.File]::ReadAllText($metaPath)
        $text = [regex]::Replace($text, 'spritePixelsToUnits: \d+', "spritePixelsToUnits: $ppu")
        $text = [regex]::Replace($text, 'spriteBorder: \{x: \d+, y: \d+, z: \d+, w: \d+\}', "spriteBorder: {x: $bx, y: $by, z: $bz, w: $bw}")
        $text = [regex]::Replace($text, 'wrapU: \d+', "wrapU: $wrap")
        $text = [regex]::Replace($text, 'wrapV: \d+', "wrapV: $wrap")
        $text = [regex]::Replace($text, 'wrapW: \d+', "wrapW: $wrap")
        $text = [regex]::Replace($text, 'filterMode: \d+', 'filterMode: 0')
        $text = [regex]::Replace($text, 'textureType: \d+', 'textureType: 8')
        $text = [regex]::Replace($text, 'alphaIsTransparency: \d+', 'alphaIsTransparency: 1')
        $text = [regex]::Replace($text, 'textureCompression: \d+', 'textureCompression: 0')
        [System.IO.File]::WriteAllText($metaPath, $text, [System.Text.UTF8Encoding]::new($false))
        return [regex]::Match($text, 'guid: ([0-9a-f]{32})').Groups[1].Value
    }

    $guid = Get-MetaGuid $relPath
    $yaml = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: $wrap
    wrapV: $wrap
    wrapW: $wrap
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: $ppu
  spriteBorder: {x: $bx, y: $by, z: $bz, w: $bw}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 00000000000000000000000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    [System.IO.File]::WriteAllText($metaPath, $yaml, [System.Text.UTF8Encoding]::new($false))
    return $guid
}

function Save-Sprite($cv, [string]$name, [int]$ppu, [int[]]$border = @(0, 0, 0, 0), [switch]$Tiled) {
    $png = Join-Path $OutDir "$name.png"
    $cv.Bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $cv.Bmp.Dispose()
    $guid = Write-SpriteMeta $png "Assets/Art/Generated/$name.png" $ppu $border -Tiled:$Tiled
    [pscustomobject]@{
        name = $name
        file = "Assets/Art/Generated/$name.png"
        width = $cv.W
        height = $cv.H
        ppu = $ppu
        border = $border
        guid = $guid
    }
}

# ---------------------------------------------------------------- run
if (-not $NoRun) {
    Write-Host 'Generating sprites...'
    $manifest = [System.Collections.Generic.List[object]]::new()

    $manifest.Add((Save-Sprite (Build-Player 0 0) 'player_imelda_idle_0' 32))
    $manifest.Add((Save-Sprite (Build-Player -1 0) 'player_imelda_idle_1' 32))
    $manifest.Add((Save-Sprite (Build-Player 0 -1) 'player_imelda_walk_0' 32))
    $manifest.Add((Save-Sprite (Build-Player -1 1) 'player_imelda_walk_1' 32))
    $manifest.Add((Save-Sprite (Build-Bat) 'enemy_bat' 24))
    $manifest.Add((Save-Sprite (Build-Bat $C.BatRedWing $C.BatRedBody $C.BatRedEye) 'enemy_bat_red' 24))
    $manifest.Add((Save-Sprite (Build-Bolt) 'proj_bolt' 16))
    $manifest.Add((Save-Sprite (Build-Bolt $C.BoltBlueOuter $C.BoltBlueMid $C.BoltBlueCore) 'proj_bolt_blue' 16))
    $manifest.Add((Save-Sprite (Build-Gem) 'pickup_xp_gem' 12))
    $manifest.Add((Save-Sprite (Build-Gem $C.GemGreen $C.GemGreenLight $C.GemGreenDark) 'pickup_xp_gem_green' 12))
    $manifest.Add((Save-Sprite (Build-Gem $C.GemRed $C.GemRedLight $C.GemRedDark) 'pickup_xp_gem_red' 12))
    $manifest.Add((Save-Sprite (Build-Chicken) 'pickup_chicken' 20))
    $manifest.Add((Save-Sprite (Build-Panel) 'ui_panel' 48 @(12, 12, 12, 12)))
    $manifest.Add((Save-Sprite (Build-Grass) 'tile_grass' 32 @(0, 0, 0, 0) -Tiled))
    $manifest.Add((Save-Sprite (Build-Title 'Survivor Arena' 'Georgia' 96 3) 'title_survivor_arena' 100))

    # manifest for scripts/tools that want sprite metrics
    $json = $manifest | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'sprites.manifest.json'), $json, [System.Text.UTF8Encoding]::new($false))

    # ---------------------------------------------------------------- preview
    function New-Preview([object[]]$entries, [string]$path, [int]$Scale = 8, [switch]$Plain) {
        $scale = $Scale; $gap = 18; $labelH = 26; $pad = 16
        $totalW = $pad * 2
        foreach ($e in $entries) { $totalW += $e.width * $scale + $gap }
        $maxH = ($entries | ForEach-Object { $_.height } | Measure-Object -Maximum).Maximum * $scale
        $img = [System.Drawing.Bitmap]::new($totalW, $maxH + $labelH + $pad * 2 + 8)
        $g = [System.Drawing.Graphics]::FromImage($img)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $g.Clear([System.Drawing.Color]::FromArgb(255, 18, 16, 26))

        $font = [System.Drawing.Font]::new('Consolas', 10, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $textBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 190, 188, 205))
        $tileA = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 32, 30, 42))
        $tileB = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 42, 39, 55))

        $x = $pad
        foreach ($e in $entries) {
            $sw = $e.width * $scale; $sh = $e.height * $scale
            $top = $pad + ($maxH - $sh)
            if (-not $Plain) {
                for ($cy = 0; $cy -lt $sh; $cy += 8) {
                    for ($cx = 0; $cx -lt $sw; $cx += 8) {
                        $brush = if ((($cx / 8) + ($cy / 8)) % 2 -eq 0) { $tileA } else { $tileB }
                        $g.FillRectangle($brush, $x + $cx, $top + $cy, [math]::Min(8, $sw - $cx), [math]::Min(8, $sh - $cy))
                    }
                }
            }
            $bmp = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot $e.file))
            $g.DrawImage($bmp, [System.Drawing.Rectangle]::new($x, $top, $sw, $sh), 0, 0, $e.width, $e.height, [System.Drawing.GraphicsUnit]::Pixel)
            $bmp.Dispose()
            $g.DrawString($e.name, $font, $textBrush, [single]$x, [single]($pad + $maxH + 8))
            $x += $sw + $gap
        }

        $img.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $font.Dispose(); $textBrush.Dispose(); $tileA.Dispose(); $tileB.Dispose(); $g.Dispose(); $img.Dispose()
    }

    $small = @($manifest | Where-Object { $_.name -ne 'title_survivor_arena' })
    $title = @($manifest | Where-Object { $_.name -eq 'title_survivor_arena' })
    $chars = @($manifest | Where-Object { $_.name -match '^(player_imelda|enemy_bat)' })
    $items = @($manifest | Where-Object { $_.name -match '^(proj_bolt|pickup_)' })
    New-Preview $small (Join-Path $PreviewDir 'preview_sprites.png')
    New-Preview $chars (Join-Path $PreviewDir 'preview_characters.png') -Scale 10
    New-Preview $items (Join-Path $PreviewDir 'preview_items.png') -Scale 12
    New-Preview $title (Join-Path $PreviewDir 'preview_title.png') -Scale 1 -Plain

    Write-Host ''
    $manifest | Format-Table name, width, height, ppu, border -AutoSize
    Write-Host "Sprites  -> $OutDir"
    Write-Host "Preview  -> $(Join-Path $PreviewDir 'preview_sprites.png')"
}
