# ============================================================================
# 程序化生成 12 帧 ReviveEye_0..11.png （256x256, RGBA 透明背景，统一像素风）
# ============================================================================
# 解决 4 个核心问题：
#   1) 背景：在 32bppArgb 空画布上画，未绘制像素 alpha=0，背景天然透明。
#   2) 连贯性：12 帧共用同一套基础参数，眼眸位置/形状全程不变，只让
#      "眼眸亮度 / 紫雾半径 / 爆炸半径 / 粒子布局" 按帧索引连续变化。
#   3) 锐利金色龙眼（v2 关键修复，明确**不**是猫眼/温顺动物眼）：
#      旧 v1 用的是"杏仁形椭圆 + 圆形瞳孔" → 这其实就是温顺动物眼/猫眼侧面，
#      根本没有龙的辨识符号，所以用户依然觉得像猫眼。v2 改用真·龙眼形态学：
#        a. **多边形折线眼轮廓**（非平滑椭圆）——上睑前段斜下压成尖，
#           后段斜上挑出眼角钩；下睑接近水平直线，与上睑后段会成锐角眼角；
#        b. **垂直窄裂瞳**（2 像素宽，贯穿整个眼高）——这才是 dragon 标志，
#           而且配合厚眉骨 + 鳞片就不会读作猫眼；
#        c. **厚重眉骨/眉鳞**——眼上方一排 3 像素高深紫块，把眼"压"成窄缝；
#        d. **径向虹膜纹理**——从瞳孔向外发 6 条亮金细线（鳞翅龙瞳特征）；
#        e. **眼周鳞片**——眼眶外侧 4 个深紫 2x2 方块暗示鳞甲；
#        f. **眼角金尖**——左眼外侧/右眼外侧斜向延伸的 3 像素金色尖刺；
#        g. 上睑下沿一条白色短弧高光带（湿润反光），和瞳孔内侧 1px 微亮点。
#   4) 像素风：所有图形先在 128x128 低分辨率画布上画"逻辑像素"，最后
#      用 NearestNeighbor 放大到 256x256（每"逻辑像素" = 2x2 屏幕像素），
#      锐利块状像素边缘，无抗锯齿，调色板限定。
# ----------------------------------------------------------------------------
# 调色板（统一所有 12 帧）：
#   背景        rgba(0,0,0,0)
#   深紫底色    #2A0840
#   主紫        #9B30E8 ← 紫雾、爆炸主色
#   亮品红      #E040FF ← 高亮、核心
#   黄金虹膜    #FFC820 ← 龙眼主体
#   亮金高光    #FFEC80 ← 龙眼高光
#   白色核心    #FFFFFF ← 极小高光
# ============================================================================

Add-Type -AssemblyName System.Drawing

$dst       = 'd:\Survivor\Survivor\Assets\Resources\Effects'
$logicSize = 128            # 逻辑画布（小）
$finalSize = 256            # 最终输出（大）= logicSize × 2

# ---- 调色板（ARGB） ----
function NewARGB([int]$a, [int]$r, [int]$g, [int]$b) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}
$colDeepPurple   = NewARGB 255 42  8   64    # #2A0840
$colMainPurple   = NewARGB 255 155 48  232   # #9B30E8
$colMagenta      = NewARGB 255 224 64  255   # #E040FF
$colGold         = NewARGB 255 255 200 32    # #FFC820
$colGoldHi       = NewARGB 255 255 236 128   # #FFEC80
$colWhite        = NewARGB 255 255 255 255

# ---- 龙眼几何参数（全程不变，只调 alpha/尺寸权重）----
# 在 logicSize=128 坐标系中。每只眼宽 24（半宽 12）、高 14（半高 7）：
$leftEyeCx    = 44      # 左眼中心 x（往两边拉开，给眉骨/鳞片留位）
$rightEyeCx   = 84      # 右眼中心 x
$eyeCy        = 58      # 双眼中心 y
$eyeHalfW     = 12      # 眼宽半径（横向）
$eyeHalfH     = 6       # 眼高半径（纵向，比 v1 更窄一格 → 凶狠感）
# 垂直裂瞳：2 像素宽（中间柱），贯穿眼高的 80%（顶/底各留 1 像素虹膜）
$pupilHalfW   = 1       # 瞳孔半宽（实际宽 = 2*1+1 = 3 → 但下面只画 2 列保证细窄）
$pupilHalfH   = 5       # 瞳孔半高（与 eyeHalfH 接近，几乎贯穿）

# ---- 单帧绘制函数 ----
# 参数都按 [0..1] 归一化。在帧脚本里按帧索引推出。
function Draw-Frame {
    param(
        [int]$frameIdx,
        [single]$eyeIntensity,    # 0..1 眼眸亮度（金色明度）
        [single]$mistRise,        # 0..1 紫雾从下往上爬升的高度比例
        [single]$swirlAmount,     # 0..1 螺旋汇聚强度
        [single]$burstRadius,     # 0..1 爆炸半径占画面比
        [single]$burstAlpha,      # 0..1 爆炸 alpha
        [single]$dispersal        # 0..1 残粒子飘散程度
    )

    # 1) 创建 logicSize x logicSize 的 RGBA 透明画布
    $bmp = New-Object System.Drawing.Bitmap($logicSize, $logicSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    # 关闭抗锯齿，强制像素块状
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
    # 透明背景（清空）
    $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))

    # ===== A. 背景紫雾 =====
    # 雾从画面下边沿向上爬升，mistRise=0 完全没有雾，=1 充满整个画面。
    if ($mistRise -gt 0.01) {
        # 紫雾用"伪噪点"实现：在下半区按伪随机分布点亮像素
        $rand = New-Object System.Random(1234 + $frameIdx) # 固定 seed → 帧内确定
        $mistTopY = [int]($logicSize * (1.0 - $mistRise))
        for ($y = $logicSize - 1; $y -ge $mistTopY; $y--) {
            # 从 mistTopY 处稀疏，到底部稠密
            $depth = ($y - $mistTopY) / [Math]::Max(1, $logicSize - $mistTopY)
            $density = 0.15 + 0.55 * $depth   # 0.15..0.70
            for ($x = 0; $x -lt $logicSize; $x++) {
                $r = $rand.NextDouble()
                if ($r -gt $density) { continue }
                # 颜色：底部偏深紫，顶部偏亮紫
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

    # ===== B. 漩涡向中心汇聚（swirlAmount > 0 时画几条向心紫色弧线）=====
    if ($swirlAmount -gt 0.05) {
        $cx = [int]($logicSize / 2)
        $cy = [int]($logicSize / 2)
        $rand2 = New-Object System.Random(7777 + $frameIdx)
        # 6 条螺旋臂，从外向内画
        for ($arm = 0; $arm -lt 6; $arm++) {
            $angleStart = ($arm * [Math]::PI * 2.0) / 6.0
            $rOuter = 60.0
            $rInner = 14.0 + (1.0 - $swirlAmount) * 30.0  # swirl 越强越靠中心
            for ($r = $rOuter; $r -gt $rInner; $r -= 1.5) {
                $progress = ($rOuter - $r) / ($rOuter - $rInner)
                # 螺旋角度：外围慢，内圈急
                $angle = $angleStart + $progress * 2.5 * $swirlAmount
                $px = [int]($cx + $r * [Math]::Cos($angle))
                $py = [int]($cy + $r * [Math]::Sin($angle))
                if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }
                # 内圈用品红（更亮），外圈用主紫
                if ($progress -gt 0.6) {
                    $bmp.SetPixel($px, $py, $colMagenta)
                } else {
                    $bmp.SetPixel($px, $py, $colMainPurple)
                }
                # 给一点厚度
                if ($px+1 -lt $logicSize) { $bmp.SetPixel($px+1, $py, $colMainPurple) }
            }
        }
    }

    # ===== C. 爆炸射线（burstRadius > 0 时画放射状光芒）=====
    if ($burstRadius -gt 0.05 -and $burstAlpha -gt 0.05) {
        $cx = [int]($logicSize / 2)
        $cy = [int]($logicSize / 2)
        $maxR = [int]($logicSize * 0.5 * $burstRadius)
        $alphaB = [int](255 * $burstAlpha)
        $colBurstMain = NewARGB $alphaB 224 64 255   # 品红
        $colBurstEdge = NewARGB ([int]($alphaB * 0.7)) 155 48 232  # 主紫边缘
        # 16 条射线
        for ($k = 0; $k -lt 16; $k++) {
            $angle = ($k * [Math]::PI * 2.0) / 16.0
            for ($r = 6; $r -lt $maxR; $r += 1) {
                # 射线粗细：中段最粗（菱形射线感）
                $thickness = 1
                $relR = $r / [single]$maxR
                if ($relR -gt 0.3 -and $relR -lt 0.7) { $thickness = 2 }
                # 中心实心区
                if ($r -lt 8) { $thickness = 3 }

                $px = [int]($cx + $r * [Math]::Cos($angle))
                $py = [int]($cy + $r * [Math]::Sin($angle))
                # 射线主色：内段品红，外段主紫
                $col = if ($relR -lt 0.5) { $colBurstMain } else { $colBurstEdge }
                # 画粗度
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

    # ===== D. 残粒子飘散（dispersal > 0 时画稀疏粒子）=====
    if ($dispersal -gt 0.05) {
        $rand3 = New-Object System.Random(9999 + $frameIdx)
        $count = [int](80 * $dispersal)
        $cx = $logicSize / 2.0
        $cy = $logicSize / 2.0
        for ($p = 0; $p -lt $count; $p++) {
            $angle = $rand3.NextDouble() * [Math]::PI * 2.0
            # 粒子分布：dispersal 越大越往外飘
            $r = (10.0 + 50.0 * $dispersal) * (0.4 + 0.6 * $rand3.NextDouble())
            $px = [int]($cx + $r * [Math]::Cos($angle))
            $py = [int]($cy + $r * [Math]::Sin($angle))
            if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }
            # 颜色：主紫 / 品红 交替
            $col = if ($rand3.NextDouble() -lt 0.5) { $colMainPurple } else { $colMagenta }
            # alpha 随 dispersal 减弱
            $alphaP = [int](255 * (1.0 - 0.5 * $dispersal))
            $colP = NewARGB $alphaP $col.R $col.G $col.B
            $bmp.SetPixel($px, $py, $colP)
        }
    }

    # ===== E. 龙眼（最重要！全程位置/形状不变，只变亮度）=====
    # 画双眼。eyeIntensity = 0 完全不画，=1 全亮。
    if ($eyeIntensity -gt 0.02) {
        Draw-DragonEye $bmp $leftEyeCx  $eyeCy $eyeIntensity
        Draw-DragonEye $bmp $rightEyeCx $eyeCy $eyeIntensity
    }

    $g.Dispose()

    # ---- 放大到 256x256（NearestNeighbor，保持像素感）----
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

    $outPath = Join-Path $dst ("ReviveEye_" + $frameIdx + ".png")
    $final.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $final.Dispose()
    Write-Output ("done: ReviveEye_" + $frameIdx + ".png  intensity=" + ("{0:F2}" -f $eyeIntensity))
}

# ---- 单只龙眼 ----
# 杏仁外形 + 圆形瞳孔（非猫眼）+ 上睑暗化 + 下方亮金高光
function Draw-DragonEye {
    param(
        [System.Drawing.Bitmap]$bmp,
        [int]$cx,
        [int]$cy,
        [single]$intensity
    )
    # 金色虹膜亮度根据 intensity 调暗
    $iR = [int](255 * $intensity)
    $iG = [int](200 * $intensity)
    $iB = [int](32  * $intensity)
    $colIris    = NewARGB 255 $iR $iG $iB
    $colIrisHi  = NewARGB 255 ([int](255 * $intensity)) ([int](236 * $intensity)) ([int](128 * $intensity))
    $colPupil   = NewARGB 255 0 0 0   # 黑色圆形瞳孔（关键：不是垂直裂瞳！）
    $colSpark   = NewARGB 255 255 255 255

    # 第一步：画杏仁形虹膜外轮廓（横向椭圆 → 龙眼/恶鬼眼，不是圆滚滚人眼）
    for ($y = -$eyeRadiusY; $y -le $eyeRadiusY; $y++) {
        for ($x = -$eyeRadiusX; $x -le $eyeRadiusX; $x++) {
            # 椭圆方程
            $nx = $x / [single]$eyeRadiusX
            $ny = $y / [single]$eyeRadiusY
            $d = $nx * $nx + $ny * $ny
            if ($d -gt 1.0) { continue }

            $px = $cx + $x
            $py = $cy + $y
            if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }

            # 上眼睑暗化：y < -2 处拉黑（让眼睛上半被睑遮蔽，更凶）
            if ($y -lt -3) {
                # 上眼睑黑边
                if ($d -gt 0.6) { $bmp.SetPixel($px, $py, $colPupil); continue }
            }

            # 主虹膜：用渐变（上方稍暗，下方亮金高光）
            if ($y -ge 1) {
                # 下半区：亮金
                $bmp.SetPixel($px, $py, $colIrisHi)
            } else {
                $bmp.SetPixel($px, $py, $colIris)
            }
        }
    }

    # 第二步：圆形瞳孔（黑色）—— 这是"龙眼"vs"猫眼"的关键
    # 如果用垂直裂瞳就会变成蛇眼/猫眼，这里强制圆形
    for ($y = -$pupilRadius; $y -le $pupilRadius; $y++) {
        for ($x = -$pupilRadius; $x -le $pupilRadius; $x++) {
            if (($x*$x + $y*$y) -le ($pupilRadius * $pupilRadius)) {
                $px = $cx + $x
                $py = $cy + $y + $pupilOffsetY
                if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }
                $bmp.SetPixel($px, $py, $colPupil)
            }
        }
    }

    # 第三步：白色高光小点（瞳孔上方，2x2，让眼睛"活"起来）
    if ($intensity -gt 0.5) {
        $sx = $cx - 1
        $sy = $cy - 2 + $pupilOffsetY
        for ($dy = 0; $dy -lt 2; $dy++) {
            for ($dx = 0; $dx -lt 2; $dx++) {
                $px = $sx + $dx
                $py = $sy + $dy
                if ($px -lt 0 -or $px -ge $logicSize -or $py -lt 0 -or $py -ge $logicSize) { continue }
                $bmp.SetPixel($px, $py, $colSpark)
            }
        }
    }

    # 第四步：眼角金色尖角（杏仁形向外侧延伸 1 像素，强化龙眼锐利感）
    $tipL = $cx - $eyeRadiusX - 1
    $tipR = $cx + $eyeRadiusX + 1
    if ($tipL -ge 0)            { $bmp.SetPixel($tipL, $cy, $colIris) }
    if ($tipR -lt $logicSize)   { $bmp.SetPixel($tipR, $cy, $colIris) }
}

# ============================================================================
# 12 帧叙事编排（参数全部连续渐变 → 动画自然过渡）
# ============================================================================
# 帧 |  眼亮 | 雾爬升 |  漩涡 | 爆炸R |爆炸a | 残粒
#  0 | 0.30  | 0.00   | 0.00  | 0.00  | 0.00 | 0.00   眼眸初现（弱光）
#  1 | 0.55  | 0.00   | 0.00  | 0.00  | 0.00 | 0.00   双眸渐亮
#  2 | 0.85  | 0.05   | 0.00  | 0.00  | 0.00 | 0.00   全开 + 微雾
#  3 | 1.00  | 0.20   | 0.00  | 0.00  | 0.00 | 0.00   雾起（底部）
#  4 | 1.00  | 0.40   | 0.20  | 0.00  | 0.00 | 0.00   雾爬中段 + 漩涡萌发
#  5 | 1.00  | 0.60   | 0.45  | 0.00  | 0.00 | 0.00   雾盖大半 + 漩涡明显
#  6 | 1.00  | 0.80   | 0.70  | 0.00  | 0.00 | 0.00   漩涡向心
#  7 | 1.00  | 0.95   | 1.00  | 0.00  | 0.00 | 0.00   压缩成核（最压抑）
#  8 | 1.00  | 0.70   | 0.80  | 0.30  | 0.70 | 0.00   起爆（核心点燃 + 内射线）
#  9 | 1.00  | 0.40   | 0.40  | 0.65  | 1.00 | 0.10   爆炸扩张
# 10 | 0.85  | 0.15   | 0.00  | 0.95  | 0.85 | 0.40   爆炸全盛 ★
# 11 | 0.55  | 0.05   | 0.00  | 0.50  | 0.30 | 0.85   残辉消散

$frames = @(
    @{ idx=0;  eye=0.30; mist=0.00; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=1;  eye=0.55; mist=0.00; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=2;  eye=0.85; mist=0.05; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=3;  eye=1.00; mist=0.20; swirl=0.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=4;  eye=1.00; mist=0.40; swirl=0.20; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=5;  eye=1.00; mist=0.60; swirl=0.45; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=6;  eye=1.00; mist=0.80; swirl=0.70; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=7;  eye=1.00; mist=0.95; swirl=1.00; bR=0.00; bA=0.00; disp=0.00 },
    @{ idx=8;  eye=1.00; mist=0.70; swirl=0.80; bR=0.30; bA=0.70; disp=0.00 },
    @{ idx=9;  eye=1.00; mist=0.40; swirl=0.40; bR=0.65; bA=1.00; disp=0.10 },
    @{ idx=10; eye=0.85; mist=0.15; swirl=0.00; bR=0.95; bA=0.85; disp=0.40 },
    @{ idx=11; eye=0.55; mist=0.05; swirl=0.00; bR=0.50; bA=0.30; disp=0.85 }
)

foreach ($f in $frames) {
    Draw-Frame -frameIdx $f.idx -eyeIntensity $f.eye -mistRise $f.mist `
               -swirlAmount $f.swirl -burstRadius $f.bR -burstAlpha $f.bA `
               -dispersal $f.disp
}

Write-Output "All 12 procedural frames generated."
