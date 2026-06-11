# Strip near-white/gray haze from dragon eyes PNG, keep eyeballs intact.
# Logic: pixel is treated as background if it is bright AND nearly grayscale.
# Eye sclera is dark+purple (low brightness OR has color tint) -> kept.
# Iris is colorful gold -> kept.

Add-Type -AssemblyName System.Drawing

$src = 'd:\Survivor\Survivor\generated-images\dragon_eyes_v7\Two_massive_FLAT_ELONGATED_dra_2026-06-09T07-46-05.png'
$dst = 'd:\Survivor\Survivor\Assets\Resources\Effects\ReviveDragonEye.png'

$bmp = [System.Drawing.Bitmap]::FromFile($src)
$w = $bmp.Width
$h = $bmp.Height
$out = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# Lock bits for speed
$rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$srcData = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$dstData = $out.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

$bytes = $w * $h * 4
$buf = New-Object byte[] $bytes
[System.Runtime.InteropServices.Marshal]::Copy($srcData.Scan0, $buf, 0, $bytes)

for ($i = 0; $i -lt $bytes; $i += 4) {
    $b = $buf[$i]
    $g = $buf[$i + 1]
    $r = $buf[$i + 2]
    $a = $buf[$i + 3]

    $mx = [Math]::Max([Math]::Max($r, $g), $b)
    $mn = [Math]::Min([Math]::Min($r, $g), $b)
    $sat = $mx - $mn

    # Background: bright AND low saturation (white/light gray haze)
    if ($mx -gt 180 -and $sat -lt 18) {
        $buf[$i + 3] = 0
    }
    elseif ($mx -gt 130 -and $sat -lt 14) {
        # Soft fade for mid-bright grayish haze
        $alpha = [int](255 * (1.0 - ($mx - 130) / 50.0))
        if ($alpha -lt 0) { $alpha = 0 }
        if ($alpha -gt $a) { $alpha = $a }
        $buf[$i + 3] = [byte]$alpha
    }
}

[System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $dstData.Scan0, $bytes)
$bmp.UnlockBits($srcData)
$out.UnlockBits($dstData)

# Find tight bbox of non-transparent pixels and crop
$minX = $w; $minY = $h; $maxX = 0; $maxY = 0
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $idx = ($y * $w + $x) * 4
        if ($buf[$idx + 3] -gt 8) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

Write-Host "BBox: ($minX,$minY) -> ($maxX,$maxY), size $($maxX-$minX+1) x $($maxY-$minY+1)"

if ($maxX -gt $minX) {
    $cropW = $maxX - $minX + 1
    $cropH = $maxY - $minY + 1
    $cropped = New-Object System.Drawing.Bitmap $cropW, $cropH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gfx = [System.Drawing.Graphics]::FromImage($cropped)
    $srcRect = New-Object System.Drawing.Rectangle $minX, $minY, $cropW, $cropH
    $dstRect = New-Object System.Drawing.Rectangle 0, 0, $cropW, $cropH
    $gfx.DrawImage($out, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    $gfx.Dispose()
    $cropped.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
    $cropped.Dispose()
}
else {
    $out.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
}

$out.Dispose()
$bmp.Dispose()

$info = Get-Item $dst
Write-Host "Saved $($info.FullName) ($($info.Length) bytes)"
