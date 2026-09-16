$ErrorActionPreference = "Stop"

$repo = "C:\Users\Administrator\Documents\ChatGPT\dreamcast-horror"
$cpp  = Join-Path $repo "Game\Compatibility\StaticCollisionBackend.cpp"

if (-not (Test-Path -LiteralPath $cpp)) {
    throw "StaticCollisionBackend.cpp not found at: $cpp"
}

Copy-Item -LiteralPath $cpp -Destination "$cpp.bak10" -Force
Write-Host "Backed up to $cpp.bak10" -ForegroundColor DarkGray

$text = Get-Content -LiteralPath $cpp -Raw

# --- Fix 1: corner normal reach. Must cover sqrt(2) * playerRadius. ---
$oldReach = "const float reach = m_playerRadius + touch;"
$newReach = "const float reach = m_playerRadius * 1.6f + touch;"

if ($text.Contains($oldReach)) {
    $text = $text.Replace($oldReach, $newReach)
    Write-Host "  Patched corner normal reach." -ForegroundColor Green
}
elseif ($text.Contains($newReach)) {
    Write-Host "  Corner normal reach already patched." -ForegroundColor Yellow
}
else {
    Write-Host "  WARNING: reach line not found by exact match." -ForegroundColor Yellow
    Write-Host "  Searching for any 'const float reach = ...' line:" -ForegroundColor Yellow
    $text -split "`r?`n" | Where-Object { $_ -match 'reach\s*=' } |
        ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    throw "Stopping so you can paste the reach line."
}

# --- Fix 2: escape hatch boundary tolerance (in resolveBoxAxis). ---
$oldEscape = "if (otherAxis <= boxMinOther ||`r`n        otherAxis >= boxMaxOther) {"
$newEscape = "if (otherAxis <= boxMinOther + 0.01f ||`r`n        otherAxis >= boxMaxOther - 0.01f) {"

if ($text.Contains($oldEscape)) {
    $text = $text.Replace($oldEscape, $newEscape)
    Write-Host "  Patched escape hatch tolerance." -ForegroundColor Green
}
elseif ($text.Contains("boxMinOther + 0.01f")) {
    Write-Host "  Escape hatch tolerance already patched." -ForegroundColor Yellow
}
else {
    Write-Host "  WARNING: escape hatch not found by exact match." -ForegroundColor Yellow
    $text -split "`r?`n" | Where-Object { $_ -match 'otherAxis' } |
        ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($cpp, $text, $utf8)

Write-Host ""
Write-Host "Verify:"
$verify = Get-Content -LiteralPath $cpp -Raw
if ($verify -match 'm_playerRadius \* 1\.6f \+ touch') {
    Write-Host "  OK  corner normal reach" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Rebuild:"
Write-Host "  cd $repo"
Write-Host "  .\Unity\Dreamcast-Horror\build_unity_plugin.bat"
