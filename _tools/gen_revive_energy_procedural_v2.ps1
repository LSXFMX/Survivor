# ============================================================================
# 程序化生成 12 帧 ReviveEnergy_0..11.png （384x384, RGBA 透明背景）— v2 精修
# ============================================================================
# v2 相对 v1 的精致化升级：
#   • 逻辑画布 192×192（v1 是 128），细节翻倍；最终 384×384 输出。
#   • 紫雾粒子密度 +50%，加入"亮粒+暗粒"二级分布，每颗粒子是 1-2px 方块，
#     不再是单点—像素颗粒感更强、密度更高。
#   • 螺旋臂 6→8 道，每道双线（主线 + 拖尾），每线沿臂走时间隔散落小亮粒，
#     形成"流体星尘"质感。
#   • 爆炸射线 16→24 道，长射线 + 16 道短反向小射线（交错），中心增加六芒辉光层。
#   • 残粒子 80→220 颗，分大/中/小三档（3px/2px/1px），颜色四色（深紫、亮紫、品红、金）。
#   • 新增"花瓣气流"层：高潮帧前后绕中心旋转的 5 片紫色羽状粒子云，加强汇聚感。
# ============================================================================

Add-Type -AssemblyName System.Drawing

$dst       = 'd:\Survivor\Survivor\Assets\Resources\Effects'
$logicSize = 192            # 逻辑画布
$finalSize = 384            # 最终输出 = logicSize × 2

function NewARGB([int]$a, [int]$r, [int]$g, [int]$b) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}
$colDeepPurple = NewARGB 255 42  8   64
$colMidPurple  = NewARGB 255 95  24  155
$colMainPurple = NewARGB 255 155 48  232
$colMagenta    = NewARGB 255 224 64  255
$colHotPink    = NewARGB 255 255 120 255
$colGold       = NewARGB 255 255 200 32
$colWhite      = NewARGB 255 255 255 255

# 安全 setPixel（自动边界检查 + 小方块绘制）
function PutBlock {
    param(
        [System.Drawing.Bitmap]$bmp,
        [int]$x, [int]$y,
        [int]$size,
        [System.Drawing.Color]$col
    )
    for ($dx = 0; $dx -lt $size; $dx++) {
        for ($dy = 0; $dy -lt $size; $dy++) {
            $ax = $x + $dx
            $ay = $y + $dy
            if ($ax -ge 0 -and $ax -lt $bmp.Width -and $ay -ge 0 -and $ay -lt $bmp.Height) {
                $bmp.SetPixel($ax, $ay, $col)
            }
        }
    }
}

function Draw-EnergyFrame {
    param(
        [int]$frameIdx,
        [single]$fogRise,
        [single]$swirlAmount,
        [single]$burstRadius,
        [single]$burstAlpha,
        [single]$dispersal,
        [single]$petals       # 花瓣气流强度 0..1
    )

    $bmp = New-Object System.Drawing.Bitmap($logicSize, $logicSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
    $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))

    # ===== A. 紫雾从底部向上爬（密度翻倍 + 二级亮粒） =====
    if ($fogRise -gt 0.01) {
        $rand = New-Object System.Random(1234 + $frameIdx)
        $fogTopY = [int]($logicSize * (1.0 - $fogRise))
        for ($y = $logicSize - 1; $y -ge $fogTopY; $y--) {
            $depth = ($y - $fogTopY) / [Math]::Max(1, $logicSize - $fogTopY)
            # 密度更高
            $density = 0.30 + 0.55 * $depth
            for ($x = 0; $x -lt $logicSize; $x++) {
                $r = $rand.NextDouble()
                if ($r -gt $density) { continue }
                if ($r -lt $density * 0.35) {
                    $col = $colDeepPurple
                } elseif ($r -lt $density * 0.7) {
                    $col = $colMidPurple
                } elseif ($r -lt $density * 0.9) {
                    $col = $colMainPurple
                } else {
                    $col = $colMagenta
                }
                $bmp.SetPixel($x, $y, $col)
            }
            # 二级亮粒：每行随机撒 1-3 颗 2x2 亮品红颗粒
            $sparkCount = [int]([Math]::Floor($density * 3.0))
            for ($s = 0; $s -lt $sparkCount; $s++) {
                $sx = $rand.Next(0, $logicSize - 1)
                if ($rand.NextDouble() -lt 0.5) {
                    PutBlock $bmp $sx $y 2 $colHotPink
                } else {
                    PutBlock $bmp $sx $y 1 $colMagenta
                }
            }
        }
    }

    # ===== B. 螺旋向心臂（8 道双线 + 流体亮点） =====
    if ($swirlAmount -gt 0.05) {
        $cx = [int]($logicSize / 2)
        $cy = [int]($logicSize / 2)
        $rand2 = New-Object System.Random(5678 + $frameIdx)
        $armCount = 8
        for ($arm = 0; $arm -lt $armCount; $arm++) {
            $angleStart = ($arm * [Math]::PI * 2.0) / $armCount
            $rOuter = 90.0
            $rInner = 18.0 + (1.0 - $swirlAmount) * 40.0
            for ($r = $rOuter; $r -gt $rInner; $r -= 1.0) {
                $progress = ($rOuter - $r) / ($rOuter - $rInner)
                $angle = $angleStart + $progress * 3.0 * $swirlAmount

                # 主线
                $px = [int]($cx + $r * [Math]::Cos($angle))
                $py = [int]($cy + $r * [Math]::Sin($angle))
                if ($px -ge 0 -and $px -lt $logicSize -and $py -ge 0 -and $py -lt $logicSize) {
                    if ($progress -gt 0.7) {
                        $bmp.SetPixel($px, $py, $colHotPink)
                    } elseif ($progress -gt 0.4) {
                        $bmp.SetPixel($px, $py, $colMagenta)
                    } else {
                        $bmp.SetPixel($px, $py, $colMainPurple)
                    }
                }
                # 拖尾线（角度偏移）
                $angleTail = $angle - 0.15
                $tx = [int]($cx + ($r - 1.5) * [Math]::Cos($angleTail))
                $ty = [int]($cy + ($r - 1.5) * [Math]::Sin($angleTail))
                if ($tx -ge 0 -and $tx -lt $logicSize -and $ty -ge 0 -and $ty -lt $logicSize) {
                    $bmp.SetPixel($tx, $ty, $colMidPurple)
                }
                # 流体亮粒：每隔几个点随机撒亮粒
                if ($rand2.NextDouble() -lt 0.15) {
                    $sparkAngle = $angle + ($rand2.NextDouble() - 0.5) * 0.3
                    $sparkR = $r + ($rand2.NextDouble() - 0.5) * 3.0
                    $spx = [int]($cx + $sparkR * [Math]::Cos($sparkAngle))
                    $spy = [int]($cy + $sparkR * [Math]::Sin($sparkAngle))
                    if ($spx -ge 0 -and $spx -lt $logicSize -and $spy -ge 0 -and $spy -lt $logicSize) {
                        PutBlock $bmp $spx $spy 2 $colHotPink
                    }
                }
            }
        }
    }

    # ===== C. 花瓣气流（5 片旋转的紫色羽状云） =====
    if ($petals -gt 0.05) {
        $cx = [int]($logicSize / 2)
        $cy = [int]($logicSize / 2)
        $rand4 = New-Object System.Random(7777 + $frameIdx)
        $petalCount = 5
        $rotation = $frameIdx * 0.4   # 每帧旋转 0.4 弧度
        $maxR = 80.0 * $petals
        for ($p = 0; $p -lt $petalCount; $p++) {
            $petalAngle = ($p * [Math]::PI * 2.0) / $petalCount + $rotation
            # 每片花瓣 = 沿 petalAngle 的椭圆云
            $cloudCount = [int](40 * $petals)
            for ($c = 0; $c -lt $cloudCount; $c++) {
                # 椭圆参数：r 从 0 到 maxR 分布，宽度（垂直于 petalAngle）随 r 增大
                $rLocal = $rand4.NextDouble() * $maxR
                $widthHere = (0.25 + 0.5 * ($rLocal / $maxR)) * $maxR * 0.4
                $perp = ($rand4.NextDouble() - 0.5) * 2.0 * $widthHere
                # 局部坐标 -> 全局
                $cosA = [Math]::Cos($petalAngle)
                $sinA = [Math]::Sin($petalAngle)
                $gx = $cx + $rLocal * $cosA - $perp * $sinA
                $gy = $cy + $rLocal * $sinA + $perp * $cosA
                $px = [int]$gx
                $py = [int]$gy
                if ($px -ge 0 -and $px -lt $logicSize -and $py -ge 0 -and $py -lt $logicSize) {
                    $col = if ($rand4.NextDouble() -lt 0.5) { $colMidPurple } else { $colMainPurple }
                    $bmp.SetPixel($px, $py, $col)
                }
            }
        }
    }

    # ===== D. 爆炸放射（24 主射线 + 16 短反射线 + 六芒辉光核） =====
    if ($burstRadius -gt 0.05 -and $burstAlpha -gt 0.05) {
        $cx = [int]($logicSize / 2)
        $cy = [int]($logicSize / 2)
        $maxR = [int]($logicSize * 0.5 * $burstRadius)
        $alphaB = [int](255 * $burstAlpha)
        $colBurstCore = NewARGB $alphaB 255 120 255
        $colBurstMain = NewARGB $alphaB 224 64 255
        $colBurstEdge = NewARGB ([int]($alphaB * 0.7)) 155 48 232
        $colBurstOuter = NewARGB ([int]($alphaB * 0.5)) 95 24 155

        # 主射线 24 道
        for ($k = 0; $k -lt 24; $k++) {
            $angle = ($k * [Math]::PI * 2.0) / 24.0
            for ($r = 8; $r -lt $maxR; $r += 1) {
                $thickness = 1
                $relR = $r / [single]$maxR
                if ($relR -gt 0.25 -and $relR -lt 0.7) { $thickness = 2 }
                if ($r -lt 10) { $thickness = 3 }
                $px = [int]($cx + $r * [Math]::Cos($angle))
                $py = [int]($cy + $r * [Math]::Sin($angle))
                $col = if ($relR -lt 0.35) { $colBurstCore }
                       elseif ($relR -lt 0.65) { $colBurstMain }
                       elseif ($relR -lt 0.85) { $colBurstEdge }
                       else { $colBurstOuter }
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
        # 短反向射线 16 道（错开角度）
        $shortR = [int]($maxR * 0.55)
        for ($k = 0; $k -lt 16; $k++) {
            $angle = (($k + 0.5) * [Math]::PI * 2.0) / 16.0
            for ($r = 12; $r -lt $shortR; $r += 1) {
                $px = [int]($cx + $r * [Math]::Cos($angle))
                $py = [int]($cy + $r * [Math]::Sin($angle))
                $col = if (($r - 12) -lt 8) { $colBurstMain } else { $colBurstEdge }
                if ($px -ge 0 -and $px -lt $logicSize -and $py -ge 0 -and $py -lt $logicSize) {
                    $bmp.SetPixel($px, $py, $col)
                    if (($px+1) -lt $logicSize) { $bmp.SetPixel($px+1, $py, $col) }
                }
            }
        }
        # 中心炽白核（爆炸最盛时点亮）+ 六芒辉光层
        if ($burstRadius -gt 0.4) {
            for ($dx = -5; $dx -le 5; $dx++) {
                for ($dy = -5; $dy -le 5; $dy++) {
                    if (($dx*$dx + $dy*$dy) -le 25) {
                        $bmp.SetPixel($cx + $dx, $cy + $dy, $colWhite)
                    }
                }
            }
            # 六芒辉光（六个方向的小光斑）
            for ($k = 0; $k -lt 6; $k++) {
                $angle = ($k * [Math]::PI * 2.0) / 6.0
                $hx = [int]($cx + 8 * [Math]::Cos($angle))
                $hy = [int]($cy + 8 * [Math]::Sin($angle))
                PutBlock $bmp ($hx - 1) ($hy - 1) 3 $colWhite
            }
        }
    }

    # ===== E. 残粒子（220 颗，三档大小 + 四色） =====
    if ($dispersal -gt 0.05) {
        $rand3 = New-Object System.Random(9999 + $frameIdx)
        $count = [int](220 * $dispersal)
        $cx = $logicSize / 2.0
        $cy = $logicSize / 2.0
        for ($p = 0; $p -lt $count; $p++) {
            $angle = $rand3.NextDouble() * [Math]::PI * 2.0
            $r = (12.0 + 75.0 * $dispersal) * (0.3 + 0.7 * $rand3.NextDouble())
            $px = [int]($cx + $r * [Math]::Cos($angle))
            $py = [int]($cy + $r * [Math]::Sin($angle))
            if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }

            $sizePick = $rand3.NextDouble()
            $blockSize = if ($sizePick -lt 0.55) { 1 } elseif ($sizePick -lt 0.9) { 2 } else { 3 }

            $colorPick = $rand3.NextDouble()
            $alphaP = [int](255 * (1.0 - 0.4 * $dispersal))
            if ($colorPick -lt 0.35) {
                $col = NewARGB $alphaP 155 48 232    # 主紫
            } elseif ($colorPick -lt 0.7) {
                $col = NewARGB $alphaP 224 64 255    # 品红
            } elseif ($colorPick -lt 0.9) {
                $col = NewARGB $alphaP 255 120 255   # 亮粉
            } else {
                $col = NewARGB $alphaP 255 200 32    # 金（少量）
            }
            PutBlock $bmp $px $py $blockSize $col
        }
    }

    $g.Dispose()

    # ---- NearestNeighbor 放大到 finalSize ----
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
# 12 帧叙事编排（v2）
# ============================================================================
# 帧 | 雾   | 漩涡 | 爆R  | 爆a  | 残粒 | 花瓣 | 叙事
#  0 |0.00  |0.00  |0.00  |0.00  |0.00  |0.00  | 静谧（仅龙眼弱光初现）
#  1 |0.05  |0.10  |0.00  |0.00  |0.00  |0.20  | 微紫雾起 + 花瓣萌动
#  2 |0.15  |0.20  |0.00  |0.00  |0.00  |0.35  | 龙眼全开 + 紫雾加厚
#  3 |0.30  |0.35  |0.00  |0.00  |0.00  |0.50  | 雾爬底部
#  4 |0.50  |0.55  |0.00  |0.00  |0.00  |0.70  | 雾爬中段 + 漩涡明
#  5 |0.70  |0.75  |0.00  |0.00  |0.00  |0.85  | 漩涡向心
#  6 |0.85  |0.90  |0.00  |0.00  |0.00  |0.95  | 压缩收紧
#  7 |0.95  |1.00  |0.00  |0.00  |0.00  |1.00  | 压缩成核 ★
#  8 |0.70  |0.80  |0.35  |0.75  |0.00  |0.70  | 起爆
#  9 |0.40  |0.40  |0.70  |1.00  |0.20  |0.40  | 爆炸扩张
# 10 |0.15  |0.00  |0.98  |0.85  |0.55  |0.15  | 爆炸全盛 ★
# 11 |0.05  |0.00  |0.55  |0.30  |0.95  |0.05  | 残辉消散

$frames = @(
    @{ idx=0;  fog=0.00; swirl=0.00; bR=0.00; bA=0.00; disp=0.00; pet=0.00 },
    @{ idx=1;  fog=0.05; swirl=0.10; bR=0.00; bA=0.00; disp=0.00; pet=0.20 },
    @{ idx=2;  fog=0.15; swirl=0.20; bR=0.00; bA=0.00; disp=0.00; pet=0.35 },
    @{ idx=3;  fog=0.30; swirl=0.35; bR=0.00; bA=0.00; disp=0.00; pet=0.50 },
    @{ idx=4;  fog=0.50; swirl=0.55; bR=0.00; bA=0.00; disp=0.00; pet=0.70 },
    @{ idx=5;  fog=0.70; swirl=0.75; bR=0.00; bA=0.00; disp=0.00; pet=0.85 },
    @{ idx=6;  fog=0.85; swirl=0.90; bR=0.00; bA=0.00; disp=0.00; pet=0.95 },
    @{ idx=7;  fog=0.95; swirl=1.00; bR=0.00; bA=0.00; disp=0.00; pet=1.00 },
    @{ idx=8;  fog=0.70; swirl=0.80; bR=0.35; bA=0.75; disp=0.00; pet=0.70 },
    @{ idx=9;  fog=0.40; swirl=0.40; bR=0.70; bA=1.00; disp=0.20; pet=0.40 },
    @{ idx=10; fog=0.15; swirl=0.00; bR=0.98; bA=0.85; disp=0.55; pet=0.15 },
    @{ idx=11; fog=0.05; swirl=0.00; bR=0.55; bA=0.30; disp=0.95; pet=0.05 }
)

foreach ($f in $frames) {
    Draw-EnergyFrame -frameIdx $f.idx `
                     -fogRise $f.fog -swirlAmount $f.swirl `
                     -burstRadius $f.bR -burstAlpha $f.bA `
                     -dispersal $f.disp -petals $f.pet
}

Write-Output "All 12 energy frames (v2 refined) generated."
