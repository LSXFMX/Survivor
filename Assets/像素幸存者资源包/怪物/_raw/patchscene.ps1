$p = "d:\Survivor\Survivor\Assets\Scenes\SampleScene.unity"
$t = [System.IO.File]::ReadAllText($p)
$nl = "`n"
$search = "  batPrefab: {fileID: 3001585549931319299, guid: adad04921e4089941805a10cb073a13a," + $nl + "    type: 3}" + $nl
$insert = "  wolfPrefab: {fileID: 3331652924953612050, guid: 31c3e5784aca4efcb139f046fbd4f716," + $nl + "    type: 3}" + $nl + "  slimePrefab: {fileID: 3331652924953612050, guid: 42b53bffe7bf493d8b5e24e185de5015," + $nl + "    type: 3}" + $nl
if ($t.Contains($search)) {
    $t = $t.Replace($search, $search + $insert)
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($p, $t, $enc)
    Write-Output "OK inserted"
} else {
    Write-Output "SEARCH NOT FOUND"
}
