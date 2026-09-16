$ErrorActionPreference = "Stop"

$repo = "C:\Users\Administrator\Documents\ChatGPT\dreamcast-horror"
$cpp  = Join-Path $repo "Game\Compatibility\StaticCollisionBackend.cpp"

if (-not (Test-Path -LiteralPath $cpp)) {
    throw "StaticCollisionBackend.cpp not found at: $cpp"
}

Copy-Item -LiteralPath $cpp -Destination "$cpp.bak4" -Force
Write-Host "Backed up to $cpp.bak4" -ForegroundColor DarkGray

$text = Get-Content -LiteralPath $cpp -Raw

$before = $text.Length

# Allow corners: extend each face's span check by "touch".
$text = $text.Replace(
    "position.z > minZ &&",
    "position.z >= minZ - touch &&")

$text = $text.Replace(
    "position.z < maxZ)",
    "position.z <= maxZ + touch)")

$text = $text.Replace(
    "position.x > minX &&",
    "position.x >= minX - touch &&")

$text = $text.Replace(
    "position.x < maxX)",
    "position.x <= maxX + touch)")

if ($text.Length -eq $before) {
    throw "No changes were made. Pattern did not match."
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($cpp, $text, $utf8)

Write-Host "Patched corner handling." -ForegroundColor Green

$verify = Get-Content -LiteralPath $cpp -Raw

if ($verify -match 'position\.z >= minZ - touch') {
    Write-Host "  OK  X-face spans allow corners" -ForegroundColor DarkGray
}
if ($verify -match 'position\.x >= minX - touch') {
    Write-Host "  OK  Z-face spans allow corners" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Rebuild:" -ForegroundColor Cyan
Write-Host "  cd $repo"
Write-Host "  .\Unity\Dreamcast-Horror\build_unity_plugin.bat"
