# ============================================================================
# 程序化生成 12 帧 ReviveEnergy_0..11.png （256x256, RGBA 透明背景）
# ============================================================================
# v5 设计：能量场与龙眼**分离**渲染。
#   • 龙眼 = 单张 AI 生成图（Resources/Effects/ReviveDragonEye.png），由 Unity
#           侧的上层 SpriteRenderer 静态显示，仅靠 alpha 调亮度（呼吸效果）。
#   • 能量场 = 本脚本程序化生成的 12 帧（紫雾爬升 → 漩涡向心 → 起爆 → 残辉），
#           **不画任何眼睛**，让上层龙眼直接透出。
#
# 这样三个职责清晰分离：
#   1) 龙眼细节（鳞片、眉骨、虹膜纹理、垂直裂瞳）→ AI 图擅长；
#   2) 多帧连贯能量场（参数随帧索引连续渐变）→ 程序化擅长；
#   3) 反向死亡动画 → Animator 协程驱动；
# 三者运行时同步播放（同一 clipLength 节奏）。
#
# 解决之前 4 个核心问题：
#   1) 背景：32bppArgb 空画布，未绘制像素 alpha=0，背景天然透明。
#   2) 连贯性：12 帧共用同一组渐变参数（fogRise, swirl, burst, dispersal），
#      眼睛已外包给静态 sprite，不存在眼睛抖动问题。
#   3) 龙眼问题：本脚本不画眼睛 → 不会再有"猫眼"风险。
#   4) 像素风：128x128 逻辑画布 → NearestNeighbor 放大到 256x256，
#      硬边块状像素，6 色调色板，无抗锯齿。
# ----------------------------------------------------------------------------
# 调色板：
#   背景        rgba(0,0,0,0)
#   深紫底色    #2A0840
#   主紫        #9B30E8 ← 紫雾、爆炸主色
#   亮品红      #E040FF ← 高亮、核心
#   黄金        #FFC820 ← 与龙眼配合的色调
#   白色核心    #FFFFFF ← 极小高光
# ============================================================================

Add-Type -AssemblyName System.Drawing

$dst       = 'd:\Survivor\Survivor\Assets\Resources\Effects'
$logicSize = 128            # 逻辑画布
$finalSize = 256            # 最终输出 = logicSize × 2

function NewARGB([int]$a, [int]$r, [int]$g, [int]$b) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}
$colDeepPurple = NewARGB 255 42  8   64
$colMainPurple = NewARGB 255 155 48  232
$colMagenta    = NewARGB 255 224 64  255
$colGold       = NewARGB 255 255 200 32
$colWhite      = NewARGB 255 255 255 255

# ============================================================================
# 单帧绘制：纯能量场（无眼睛）
#   fogRise      0..1  紫雾从下往上爬升的高度比例
#   swirlAmount  0..1  螺旋向心强度
#   burstRadius  0..1  爆炸射线半径
#   burstAlpha   0..1  爆炸 alpha
#   dispersal    0..1  残粒子飘散密度
# ============================================================================
function Draw-EnergyFrame {
    param(
        [int]$frameIdx,
        [single]$fogRise,
        [single]$swirlAmount,
        [single]$burstRadius,
        [single]$burstAlpha,
        [single]$dispersal
    )

    $bmp = New-Object System.Drawing.Bitmap($logicSize, $logicSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
    $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))

    # ===== A. 紫雾从底部向上爬 =====
    if ($fogRise -gt 0.01) {
        $rand = New-Object System.Random(1234 + $frameIdx)
        $fogTopY = [int]($logicSize * (1.0 - $fogRise))
        for ($y = $logicSize - 1; $y -ge $fogTopY; $y--) {
            $depth = ($y - $fogTopY) / [Math]::Max(1, $logicSize - $fogTopY)
            $density = 0.15 + 0.55 * $depth
            for ($x = 0; $x -lt $logicSize; $x++) {
                $r = $rand.NextDouble()
                if ($r -gt $density) { continue }
                if ($r -lt $density * 0.5) {
                    $col = $colDeepPurple
                } elseif ($r -lt $density * 0.85) {
                    $col = $colMainPurple
                } else {
                    $col = $colMagenta
                }
                $bmp.SetPixel($x, $y, $col)
            }
        }
    }

    # ===== B. 螺旋向心臂 =====
    if ($swirlAmount -gt 0.05) {
        $cx = [int]($logicSize / 2)
        $cy = [int]($logicSize / 2)
        for ($arm = 0; $arm -lt 6; $arm++) {
            $angleStart = ($arm * [Math]::PI * 2.0) / 6.0
            $rOuter = 60.0
            $rInner = 14.0 + (1.0 - $swirlAmount) * 30.0
            for ($r = $rOuter; $r -gt $rInner; $r -= 1.5) {
                $progress = ($rOuter - $r) / ($rOuter - $rInner)
                $angle = $angleStart + $progress * 2.5 * $swirlAmount
                $px = [int]($cx + $r * [Math]::Cos($angle))
                $py = [int]($cy + $r * [Math]::Sin($angle))
                if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }
                if ($progress -gt 0.6) {
                    $bmp.SetPixel($px, $py, $colMagenta)
                } else {
                    $bmp.SetPixel($px, $py, $colMainPurple)
                }
                if (($px+1) -lt $logicSize) { $bmp.SetPixel($px+1, $py, $colMainPurple) }
            }
        }
    }

    # ===== C. 爆炸放射 =====
    if ($burstRadius -gt 0.05 -and $burstAlpha -gt 0.05) {
        $cx = [int]($logicSize / 2)
        $cy = [int]($logicSize / 2)
        $maxR = [int]($logicSize * 0.5 * $burstRadius)
        $alphaB = [int](255 * $burstAlpha)
        $colBurstMain = NewARGB $alphaB 224 64 255
        $colBurstEdge = NewARGB ([int]($alphaB * 0.7)) 155 48 232
        for ($k = 0; $k -lt 16; $k++) {
            $angle = ($k * [Math]::PI * 2.0) / 16.0
            for ($r = 6; $r -lt $maxR; $r += 1) {
                $thickness = 1
                $relR = $r / [single]$maxR
                if ($relR -gt 0.3 -and $relR -lt 0.7) { $thickness = 2 }
                if ($r -lt 8) { $thickness = 3 }
                $px = [int]($cx + $r * [Math]::Cos($angle))
                $py = [int]($cy + $r * [Math]::Sin($angle))
                $col = if ($relR -lt 0.5) { $colBurstMain } else { $colBurstEdge }
                for ($dx = -$thickness; $dx -le $thickness; $dx++) {
                    for ($dy = -$thickness; $dy -le $thickness; $dy++) {
                        $ax = $px + $dx
                        $ay = $py + $dy
                        if ($ax -lt 0 -or $ax -ge $logicSize -or $ay -lt 0 -or $ay -ge $logicSize) { continue }
                        if (($dx*$dx + $dy*$dy) -le ($thickness*$thickness)) {
                            $bmp.SetPixel($ax, $ay, $col)
                        }
                    }
                }
            }
        }
        # 中心炽白核（爆炸最盛时点亮）
        if ($burstRadius -gt 0.5) {
            for ($dx = -3; $dx -le 3; $dx++) {
                for ($dy = -3; $dy -le 3; $dy++) {
                    if (($dx*$dx + $dy*$dy) -le 9) {
                        $bmp.SetPixel($cx + $dx, $cy + $dy, $colWhite)
                    }
                }
            }
        }
    }

    # ===== D. 残粒子 =====
    if ($dispersal -gt 0.05) {
        $rand3 = New-Object System.Random(9999 + $frameIdx)
        $count = [int](80 * $dispersal)
        $cx = $logicSize / 2.0
        $cy = $logicSize / 2.0
        for ($p = 0; $p -lt $count; $p++) {
            $angle = $rand3.NextDouble() * [Math]::PI * 2.0
            $r = (10.0 + 50.0 * $dispersal) * (0.4 + 0.6 * $rand3.NextDouble())
            $px = [int]($cx + $r * [Math]::Cos($angle))
            $py = [int]($cy + $r * [Math]::Sin($angle))
            if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }
            $col = if ($rand3.NextDouble() -lt 0.5) { $colMainPurple } else { $colMagenta }
            $alphaP = [int](255 * (1.0 - 0.5 * $dispersal))
            $colP = NewARGB $alphaP $col.R $col.G $col.B
            $bmp.SetPixel($px, $py, $colP)
        }
    }

    $g.Dispose()

    # ---- NearestNeighbor 放大到 256 ----
    $final = New-Object System.Drawing.Bitmap($finalSize, $finalSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gf = [System.Drawing.Graphics]::FromImage($final)
    $gf.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $gf.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $gf.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::None
    $gf.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
    $gf.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))
    $gf.DrawImage($bmp, 0, 0, $finalSize, $finalSize)
    $gf.Dispose()
    $bmp.Dispose()

    $outPath = Join-Path $dst ("ReviveEnergy_" + $frameIdx + ".png")
    $final.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $final.Dispose()
    Write-Output ("done: ReviveEnergy_" + $frameIdx + ".png")
}

# ============================================================================
# 12 帧叙事编排（与龙眼 alpha 在 Unity 侧配合）
# ============================================================================
# 帧 | 雾爬升 |  漩涡 | 爆炸R | 爆炸a | 残粒  |  叙事
#  0 | 0.00   | 0.00  | 0.00  | 0.00  | 0.00  | 静谧（仅龙眼弱光初现）
#  1 | 0.00   | 0.00  | 0.00  | 0.00  | 0.00  | 龙眼渐亮
#  2 | 0.05   | 0.00  | 0.00  | 0.00  | 0.00  | 微紫雾起 + 龙眼全开
#  3 | 0.20   | 0.00  | 0.00  | 0.00  | 0.00  | 雾起底部
#  4 | 0.40   | 0.20  | 0.00  | 0.00  | 0.00  | 雾爬中段 + 漩涡萌
#  5 | 0.60   | 0.45  | 0.00  | 0.00  | 0.00  | 漩涡明显
#  6 | 0.80   | 0.70  | 0.00  | 0.00  | 0.00  | 漩涡向心
#  7 | 0.95   | 1.00  | 0.00  | 0.00  | 0.00  | 压缩成核 ★
#  8 | 0.70   | 0.80  | 0.30  | 0.70  | 0.00  | 起爆
#  9 | 0.40   | 0.40  | 0.65  | 1.00  | 0.10  | 爆炸扩张
# 10 | 0.15   | 0.00  | 0.95  | 0.85  | 0.40  | 爆炸全盛 ★
# 11 | 0.05   | 0.00  | 0.50  | 0.30  | 0.85  | 残辉消散

$frames = @(
    @{ idx=0;  fog=0.00; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=1;  fog=0.00; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=2;  fog=0.05; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=3;  fog=0.20; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=4;  fog=0.40; swirl=0.20; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=5;  fog=0.60; swirl=0.45; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=6;  fog=0.80; swirl=0.70; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=7;  fog=0.95; swirl=1.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=8;  fog=0.70; swirl=0.80; bR=0.30; bA=0.70; disp=0.00 },
    @{ idx=9;  fog=0.40; swirl=0.40; bR=0.65; bA=1.00; disp=0.10 },
    @{ idx=10; fog=0.15; swirl=0.00; bR=0.95; bA=0.85; disp=0.40 },
    @{ idx=11; fog=0.05; swirl=0.00; bR=0.50; bA=0.30; disp=0.85 }
)

foreach ($f in $frames) {
    Draw-EnergyFrame -frameIdx $f.idx `
                     -fogRise $f.fog -swirlAmount $f.swirl `
                     -burstRadius $f.bR -burstAlpha $f.bA `
                     -dispersal $f.disp
}

Write-Output "All 12 energy frames generated."
