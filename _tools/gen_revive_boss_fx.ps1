# =============================================================================
# 亡者领域 · Boss 复活特效（6 帧像素序列）
#
# 输出：Assets/Resources/Effects/ReviveBossFrame_0..5.png（各 32×32 像素 → 升采样到 256×256）
#
# 设计概念
# --------
#   "亡灵符文阵 + 灵魂之手破土"。配色与 AllySkullMark 同源（紫气主题），方便玩家把
#   "复活特效"与"被控制的友军徽记"产生视觉联想。
#
#   • Frame 0：地面一道紫色弧线裂开，下方透出微光（符文阵召唤前兆）
#   • Frame 1：六芒符文阵展开（外环 + 中心五角星），紫光初亮
#   • Frame 2：符文阵全亮，中心冒出灵魂烟柱
#   • Frame 3：灵魂烟柱拉到最高，外环开始扩散
#   • Frame 4：外环爆开成散射粒子，中心一只发光的紫色掌印
#   • Frame 5：粒子飘散收尾，掌印淡化（最后一帧 alpha 整体降低，便于淡出）
#
# 调色板
# ------
#   '.'=透明  '#'=极深紫描边  'B'=亡者紫基底  'b'=深紫阴影
#   'H'=白色高光  'E'=粉紫发光  'R'=外环青绿 rim  'Y'=灵魂烟柱浅紫白
#
# 用法：PowerShell -ExecutionPolicy Bypass -File gen_revive_boss_fx.ps1
# =============================================================================
Add-Type -AssemblyName System.Drawing

$W = 32
$H = 32
$OutDir = 'd:\Survivor\Survivor\Assets\Resources\Effects'
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

# 共享调色板
$pal = @{}
$pal['.'] = [System.Drawing.Color]::FromArgb(0,   0,   0,   0)
$pal['#'] = [System.Drawing.Color]::FromArgb(255, 28,  6,   38)   # 极深紫黑描边
$pal['B'] = [System.Drawing.Color]::FromArgb(255, 176, 70,  224)  # 亡者紫主色
$pal['b'] = [System.Drawing.Color]::FromArgb(255, 110, 40,  170)  # 紫阴影
$pal['H'] = [System.Drawing.Color]::FromArgb(255, 245, 220, 255)  # 白色高光
$pal['E'] = [System.Drawing.Color]::FromArgb(255, 255, 130, 248)  # 粉紫发光
$pal['R'] = [System.Drawing.Color]::FromArgb(220, 150, 235, 240)  # 外环青绿 rim
$pal['Y'] = [System.Drawing.Color]::FromArgb(255, 220, 180, 255)  # 灵魂烟柱浅紫白

# ---- 6 帧像素图 ----
# Frame 0：地裂前兆
$frames = @()

$frames += ,@(
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    ".............EEE................",
    "..........EE#####EE.............",
    "........E##bBBBBBBb##E..........",
    "......E##bBBBBBBBBBBBb##E.......",
    "....E#bBBBBBBBBBBBBBBBBBbb#E....",
    "..E#bBBBBBBBBBBBBBBBBBBBBBBb#E..",
    "..E#bbbBBBBBBBBBBBBBBBBBBBbbb#E.",
    "...E##bbBBBBBBBBBBBBBBBBBbbb##E.",
    ".....E####bbbbBBBBBBBBbbbb####E.",
    "........EEE####bbbbbb####EEE...."
)

# Frame 1：六芒符文阵展开
$frames += ,@(
    "................................",
    "................................",
    "................................",
    "................................",
    "................................",
    "...............HH...............",
    "...............HH...............",
    "..............HEEH..............",
    "..R...........HEEH...........R..",
    ".R.RR........H.EE.H........RR.R.",
    "R.....RR.....H.EE.H.....RR.....R",
    "R.......RRR.HH.EE.HH.RRR.......R",
    ".R.........RR.HEEH.RR.........R.",
    "..RR........RRRHHRRR........RR..",
    "....RR.....RR.RHHR.RR.....RR....",
    ".....RRR..RR..RHHR..RR..RRR.....",
    ".......RRRR...RHHR...RRRR.......",
    "......RR.R....RHHR....R.RR......",
    "....RR...RR..RR..RR..RR...RR....",
    "..RR......RRRR....RRRR......RR..",
    ".R..........R......R..........R.",
    ".R.................R..........R.",
    "..............EEEEE.............",
    "...........EE##bBb##EE..........",
    ".........E##bBBBBBBBb##E........",
    ".......E#bBBBBBBBBBBBBBb#E......",
    "....E##bBBBBBBBBBBBBBBBBBb##E...",
    "..E#bbBBBBBBBBBBBBBBBBBBBBBbb#E.",
    "..E#bbbBBBBBBBBBBBBBBBBBBBbbb#E.",
    "....E####bbbbBBBBBBBBbbbb####E..",
    "........EEE####bbbbbb####EEE....",
    "..............EEEEEE............"
)

# Frame 2：符文阵全亮 + 灵魂烟柱初现
$frames += ,@(
    "................................",
    "................................",
    "..............YYY...............",
    "..............YHY...............",
    ".............YYHYY..............",
    ".............YHEHY..............",
    "............YYEEEYY.............",
    "............YHE#EHY.............",
    ".RR.........YHE#EHY.........RR..",
    "R..RRR......YEE#EEY......RRR..R.",
    "R....RRR....YEE#EEY....RRR....R.",
    ".R.....RRRR.YE###EY.RRRR.....R..",
    ".RR.......RRYEEEEYRR.......RR...",
    "..RRRR.....RREEEERR.....RRRR....",
    "....RRRR..RRR.HH.RRR..RRRR......",
    ".....RRRRRR...HH...RRRRRR.......",
    "......RRRRRRR.HH.RRRRRRR........",
    ".....RRRR..RR.HH.RR..RRRR.......",
    "....RRRR....RR##RR....RRRR......",
    "..RRRR.......RRRR.......RRRR....",
    ".RR..........EEEE..........RR...",
    "R..........EE####EE..........R..",
    ".........EE##bBBb##EE...........",
    "........E#bBBBBBBBBb#E..........",
    "......E##bBBBHHHHBBBb##E........",
    "....E##bBBBBBHHHHBBBBBb##E......",
    "...E#bbBBBBBBBBBBBBBBBBbb#E.....",
    "..E#bbbBBBBBBBBBBBBBBBBBbbb#E...",
    "..E#bbbBBBBBBBBBBBBBBBBBBbbb#E..",
    "....E####bbbbBBBBBBBBbbbb####E..",
    "........EEE####bbbbbb####EEE....",
    "..............EEEEEE............"
)

# Frame 3：灵魂烟柱拉满 + 外环扩散
$frames += ,@(
    "..............YYY...............",
    ".............YYHYY..............",
    ".............YHEHY..............",
    "............YYEEEYY.............",
    "............YHE#EHY.............",
    "...........YHEE#EEHY............",
    "...........YEE##EEY.............",
    "..........YYE####EYY............",
    "..........YEEE##EEEY............",
    "..........YEE####EEY............",
    "R.........YHE####EHY.........R..",
    ".R.......YHEE####EEHY.......R...",
    "..R.....YHEEE####EEEHY.....R....",
    "...R...YYEEE######EEEYY...R.....",
    "....R..YEEE########EEEY..R......",
    ".....R.YEEEE######EEEEY.R.......",
    "......RYEEEEE####EEEEEYR........",
    ".....R.YYEEEEEEEEEEEEYY.R.......",
    "....R...YYEEEEEEEEEEYY...R......",
    "...R.....YYEEEEEEEEYY.....R.....",
    "..R.......YYEEEEEEYY.......R....",
    ".R.........YYEEEEYY.........R...",
    "R...........YEEEEY...........R..",
    "............YYEEYY..............",
    "...........E##bBb##E............",
    ".........E##bBBBBBBb##E.........",
    ".......E#bBBBBBBBBBBBBb#E.......",
    "....E##bBBBBBBBBBBBBBBBBBb##E...",
    "..E#bbBBBBBBBBBBBBBBBBBBBBBbb#E.",
    "..E#bbbBBBBBBBBBBBBBBBBBBBbbb#E.",
    "....E####bbbbBBBBBBBBbbbb####E..",
    "........EEE####bbbbbb####EEE...."
)

# Frame 4：外环炸开 + 中心紫色掌印
$frames += ,@(
    "R.............YYY.............R.",
    ".R...........YEHEY...........R..",
    "..R..........YHEHY..........R...",
    "...RR........YEEEY........RR....",
    ".....R........YEY........R......",
    "......RR......YEY......RR.......",
    "........RR....YEY....RR.........",
    "..........RR..YEY..RR...........",
    "............RRRRRR..............",
    "...........R..YEY..R............",
    "..........R..YEEEY..R...........",
    ".........R..YE###EY..R..........",
    "........R..YEE###EEY..R.........",
    ".......R..YEE#####EEY..R........",
    "......R..YEE#######EEY..R.......",
    ".....R..YEE##HHHHH##EEY..R......",
    "....R..YEE##H#####H##EEY..R.....",
    "...R..YEE##H#######H##EEY..R....",
    "..R..YEE##H#H#####H#H##EEY..R...",
    "..R.YE##H#H##HHHHH##H#H##EY.R...",
    "..R.YE#H#H##HHHHHHH##H#H#EY.R...",
    "..R.YE#H##HH#######HH##H#EY.R...",
    "...RYE#H#HH#########HH#H#EYR....",
    "....YE#HH##HHHHHHHHH##HH#EY.....",
    "....YE#H#HHHHHHHHHHHHH#H#EY.....",
    "....YEE#HHHHHHHHHHHHHHH#EEY.....",
    ".....YEE##HHHHHHHHHHH##EEY......",
    "......YYE##HHHHHHHHH##EYY.......",
    ".......YYE####HHHHH####EYY......",
    "........YYEE#########EEYY.......",
    ".........YYEEE######EEEYY.......",
    "...........YYEEEEEEEEYY........."
)

# Frame 5：粒子飘散收尾（最稀疏，整体淡化）—— 每行严格 32 字符
$frames += ,@(
    "R..............................R",
    "..R..........................R..",
    "....R......R...........R......R.",
    "................................",
    "....R.....YYY......YYY......R...",
    "..........YHY......YHY..........",
    "R.........YEY......YEY.........R",
    "..........YEY......YEY..........",
    "....R.....YYY......YYY.....R....",
    "................................",
    ".......R.........R.........R....",
    "................................",
    "..............YEEEY.............",
    ".............YEE#EEY............",
    "............YEE###EEY...........",
    "...........YEE##H##EEY..........",
    "..........YEE#HHHHH#EEY.........",
    ".........YEE#HH###HH#EEY........",
    "........YEE#H#######H#EEY.......",
    "........YE##H#######H##EY.......",
    "........YE#H##HHHHH##H#EY.......",
    "........YE#H##HHHHH##H#EY.......",
    ".........YE##HHHHHHH##EY........",
    ".........YEE#HHHHHHH#EEY........",
    "..........YEE##HHH##EEY.........",
    "...........YEE#####EEY..........",
    "............YEE###EEY...........",
    ".............YEEEEEY............",
    "..............YYYYY.............",
    "...............YYY..............",
    "................................",
    "................................"
)

if ($frames.Count -ne 6) { throw "expected 6 frames, got $($frames.Count)" }

for ($f = 0; $f -lt $frames.Count; $f++) {
    $art = $frames[$f]
    if ($art.Length -ne $H) { throw "frame $f rows $($art.Length) != $H" }
    foreach ($row in $art) { if ($row.Length -ne $W) { throw "frame $f bad row len $($row.Length)" } }

    $bmp = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $H; $y++) {
        $row = $art[$y]
        for ($x = 0; $x -lt $W; $x++) {
            $ch = $row[$x]
            $c = $pal[[string]$ch]
            if ($null -eq $c) { $c = $pal['.'] }
            $bmp.SetPixel($x, $y, $c)
        }
    }

    # 升采样到 256×256 NearestNeighbor 保留硬像素边缘
    $out = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($out)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, 256, 256))

    $path = Join-Path $OutDir ("ReviveBossFrame_{0}.png" -f $f)
    $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $out.Dispose(); $bmp.Dispose()
    Write-Output ("saved {0}" -f $path)
}

Write-Output "done. 6 frames generated."
