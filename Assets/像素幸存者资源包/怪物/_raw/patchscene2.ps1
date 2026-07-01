$p = "d:\Survivor\Survivor\Assets\Scenes\SampleScene.unity"
$t = [System.IO.File]::ReadAllText($p)
$nl = "`n"
$search = "  slimePrefab: {fileID: 3331652924953612050, guid: 42b53bffe7bf493d8b5e24e185de5015," + $nl + "    type: 3}" + $nl
$insert = "  wolfBossPrefab: {fileID: 3331652924953612050, guid: 6215e29cbe7d4c9eaff46ae17d27ef66," + $nl + "    type: 3}" + $nl
if ($t.Contains("wolfBossPrefab:")) { Write-Output "ALREADY PRESENT"; return }
if ($t.Contains($search)) {
    $t = $t.Replace($search, $search + $insert)
    [System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output "OK inserted wolfBossPrefab"
} else {
    Write-Output "SEARCH NOT FOUND"
}
