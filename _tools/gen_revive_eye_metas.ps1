# Generate .meta files for ReviveEye_0..11.png based on the existing
# ReviveBossFrame_0.png.meta template, only changing the GUID.

$dir = 'd:\Survivor\Survivor\Assets\Resources\Effects'
$tplPath = Join-Path $dir 'ReviveBossFrame_0.png.meta'
$tpl = Get-Content $tplPath -Raw -Encoding UTF8
$oldGuid = 'a1b2c3d4e5f6a7b8c9d0e1f200000000'

for ($i = 0; $i -lt 12; $i++) {
    # New GUID: distinct prefix to avoid collisions with the old purple-frame metas.
    $newGuid = ('b2c3d4e5f6a7b8c9d0e1f2a3' + ('{0:X8}' -f ($i + 1))).ToLower()
    if ($newGuid.Length -ne 32) { Write-Warning ("guid len mismatch: " + $newGuid); continue }
    $out = $tpl.Replace($oldGuid, $newGuid)
    $dst = Join-Path $dir ("ReviveEye_" + $i + ".png.meta")
    [System.IO.File]::WriteAllText($dst, $out, [System.Text.UTF8Encoding]::new($false))
    Write-Output ("wrote: ReviveEye_" + $i + ".png.meta  guid=" + $newGuid)
}
