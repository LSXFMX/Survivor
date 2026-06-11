# Convert 12 AI-generated black-bg PNGs (1024x1024) under
# Assets/Resources/Effects/ReviveEye_0..11.png into 256x256 transparent-bg PNGs.
# Strategy:
#   1. Resize 1024 -> 256 with HighQualityBicubic.
#   2. For each pixel, alpha = max(R,G,B), then divide RGB by (alpha/255)
#      to reverse the premultiplied-on-black contamination, so the color
#      stays vivid while the black bg becomes fully transparent.
# No external deps. Works with built-in .NET GDI+.

Add-Type -AssemblyName System.Drawing

$dir = 'd:\Survivor\Survivor\Assets\Resources\Effects'
$dstSize = 256

for ($i = 0; $i -lt 12; $i++) {
    $path = Join-Path $dir ("ReviveEye_" + $i + ".png")
    if (-not (Test-Path $path)) {
        Write-Warning ("missing: " + $path)
        continue
    }

    $src = New-Object System.Drawing.Bitmap($path)

    $small = New-Object System.Drawing.Bitmap($dstSize, $dstSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($small)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $dstSize, $dstSize)
    $g.Dispose()
    $src.Dispose()

    $rect = New-Object System.Drawing.Rectangle(0, 0, $dstSize, $dstSize)
    $data = $small.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $data.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

    for ($p = 0; $p -lt $bytes.Length; $p += 4) {
        $b = $bytes[$p]
        $gn = $bytes[$p + 1]
        $r = $bytes[$p + 2]
        $maxc = $r
        if ($gn -gt $maxc) { $maxc = $gn }
        if ($b -gt $maxc) { $maxc = $b }

        if ($maxc -lt 8) {
            $bytes[$p]     = 0
            $bytes[$p + 1] = 0
            $bytes[$p + 2] = 0
            $bytes[$p + 3] = 0
        } else {
            # Keep original RGB; alpha = max channel value.
            # This preserves the original hue (no white-wash on dim pixels)
            # and naturally fades dark pixels via low alpha. Black bg goes
            # to alpha=0 cleanly because pure-black RGB is already (0,0,0).
            $bytes[$p + 3] = $maxc
        }
    }

    [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
    $small.UnlockBits($data)

    $small.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $small.Dispose()
    Write-Output ("done: ReviveEye_" + $i + ".png")
}

Write-Output "All 12 frames converted to 256x256 with transparent background."
