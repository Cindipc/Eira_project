$content = Get-Content 'C:\Users\Usuario\Eira_project\Assets\Eira\Scenes\Level1Scene.unity' -Raw
$ids = @('1164352824','1164352825','1164352826','960092914','960092915','1168919903','1168919904','1168919905','1168919906','509127380','1515015477')
$docs = $content -split '(?=^--- !u!)'
$filtered = @()
foreach ($doc in $docs) {
    $skip = $false
    foreach ($id in $ids) {
        if ($doc -match "&" + $id) { $skip = $true; break }
    }
    if (-not $skip) {
        $doc = $doc -replace "-\s*\{fileID: ($($ids -join '|'))\}", ''
        $doc = $doc -replace "m_Father: \{fileID: ($($ids -join '|'))\}", 'm_Father: {fileID: 0}'
        $filtered += $doc
    }
}
$filtered -join '' | Set-Content 'C:\Users\Usuario\Eira_project\Assets\Eira\Scenes\Level1Scene.unity' -Encoding UTF8
Write-Host 'Done'