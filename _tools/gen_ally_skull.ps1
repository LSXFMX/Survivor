Add-Type -AssemblyName System.Drawing

$W = 32
$H = 32

# 32×32 像素图（'.'=透明, '#'=深紫黑描边, 'B'=骨主色, 'b'=骨阴影, 'H'=高光,
# 'E'=洞穴/眼/口的发光紫粉, 'R'=青色 rim, 'X'=深紫描边外侧再加一圈黑使骷髅更立体）
$art = @(
    "................................",
    "................................",
    "................................",
    "............RRRRRR..............",
    "..........RR######RR............",
    "........RR##BBBBBB##RR..........",
    ".......R##BBBBBBBBBB##R.........",
    "......R##BBBBHHHHBBBB##R........",
    ".....R##BBBHHHHHHHHBBBB#R.......",
    ".....R#BBBHHEEEEEEHHHBBB#R......",
    "....R#BBBHHEE####EE##EHHBB#R....",
    "....R#BBBHHE######E##EHHBB#R....",
    "....R#BBBHHEE####EE##EHHBB#R....",
    "....R#BBBHHHEEEEEEEEHHHBBB#R....",
    "....R#BBBBBHHEEHHEEHHBBBBB#R....",
    "....R#BBBBBHHHE##EHHBBBBB#R.....",
    ".....R#BBBBBHHE##EHHBBBBB#R.....",
    "......R##BBBBHEE##EHBBBB##R.....",
    ".......R##BBBBE####EBBB##R......",
    "........R##BBBE####EBB##R.......",
    ".........R##BBE#EE#EBB#R........",
    "..........R##BBEEEEBB#R.........",
    "...........R##BBBBBB#R..........",
    "............RR####RR............",
    "............R#BBBB#R............",
    "...R##R....R#BBBBBB#R....R##R...",
    "..R#BB#R..R#BBBBBBBB#R..R#BB#R..",
    ".R#BBBB#RR#BBBBBBBBBB#RR#BBBB#R.",
    "R#BBBBBB##BBBB####BBBB##BBBBBB#R",
    "R#BBBBBBBBBB##....##BBBBBBBBBB#R",
    ".R#BBBBBBBB##......##BBBBBBBB#R.",
    "..R##BBBB##............##BBBB##R"
)

if ($art.Length -ne $H) { throw "rows $($art.Length) != $H" }
foreach ($row in $art) { if ($row.Length -ne $W) { throw "bad row len $($row.Length)" } }

# 调色板 (A R G B)
$pal = @{}
$pal['.'] = [System.Drawing.Color]::FromArgb(0,   0,   0,   0)
$pal['#'] = [System.Drawing.Color]::FromArgb(255, 28,  6,   38)
$pal['B'] = [System.Drawing.Color]::FromArgb(255, 185, 120, 230)
$pal['b'] = [System.Drawing.Color]::FromArgb(255, 140, 78,  200)
$pal['H'] = [System.Drawing.Color]::FromArgb(255, 235, 200, 255)
$pal['E'] = [System.Drawing.Color]::FromArgb(255, 255, 110, 245)
$pal['R'] = [System.Drawing.Color]::FromArgb(220, 140, 235, 240)

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

# 升采样到 256×256（NearestNeighbor 保持像素硬边缘）
$out = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($out)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::None
$g.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, 256, 256))

$path = 'd:\Survivor\Survivor\Assets\Resources\UI\AllySkullMark.png'
$out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $out.Dispose(); $bmp.Dispose()

# 验证: 角落像素应当 A=0
$verify = [System.Drawing.Bitmap]::FromFile($path)
$corner = $verify.GetPixel(0, 0)
$mid    = $verify.GetPixel(128, 128)
Write-Output "saved $path"
Write-Output "corner(0,0) A=$($corner.A) R=$($corner.R) G=$($corner.G) B=$($corner.B)"
Write-Output "mid(128,128) A=$($mid.A) R=$($mid.R) G=$($mid.G) B=$($mid.B)"
$verify.Dispose()
