# Args: <PPU> <templateMetaPath> <path1> <guid1> <path2> <guid2> ...
$ppu = $args[0]
$tpl = [System.IO.File]::ReadAllText($args[1])
for ($i = 2; $i -lt $args.Count; $i += 2) {
    $path = $args[$i]
    $guid = $args[$i+1]
    $c = $tpl -replace 'guid: a20545073eb5e654c8cd95e86f0490b3', ("guid: " + $guid)
    $c = $c -replace 'spritePixelsToUnits: 100', ('spritePixelsToUnits: ' + $ppu)
    $c = $c -replace 'spriteID: 5e97eb03825dee720800000000000000', ("spriteID: " + $guid)
    [System.IO.File]::WriteAllText($path, $c, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ("meta " + $path)
}
