$ErrorActionPreference = "Stop"

$repo = "C:\Users\Administrator\Documents\ChatGPT\dreamcast-horror"
$cpp  = Join-Path $repo "Game\Compatibility\StaticCollisionBackend.cpp"

if (-not (Test-Path -LiteralPath $cpp)) {
    throw "StaticCollisionBackend.cpp not found at: $cpp"
}

Copy-Item -LiteralPath $cpp -Destination "$cpp.bak3" -Force
Write-Host "Backed up to $cpp.bak3" -ForegroundColor DarkGray

$text = Get-Content -LiteralPath $cpp -Raw

# --- Circle normal gate: fire whenever near OR inside, not just at exact surface. ---

$oldCircleGate = "if (dist >= r - 0.01f) {"
$newCircleGate = "if (dist < r + 0.1f) {"

if (-not $text.Contains($oldCircleGate)) {
    Write-Host "Circle gate already patched or not found. Skipping." -ForegroundColor Yellow
}
else {
    $text = $text.Replace($oldCircleGate, $newCircleGate)
    Write-Host "Patched circle normal gate." -ForegroundColor Green
}

# --- Box touch window: widen from 0.01 to 0.05. ---

$oldTouch = "const float touch =" + [Environment]::NewLine + "                0.01f;"
$newTouch = "const float touch =" + [Environment]::NewLine + "                0.05f;"

if (-not $text.Contains($oldTouch)) {
    Write-Host "Box touch already patched or not found. Skipping." -ForegroundColor Yellow
}
else {
    $text = $text.Replace($oldTouch, $newTouch)
    Write-Host "Patched box touch window." -ForegroundColor Green
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($cpp, $text, $utf8)

Write-Host ""
Write-Host "Verifying..." -ForegroundColor Cyan

$verify = Get-Content -LiteralPath $cpp -Raw

if ($verify.Contains("if (dist < r + 0.1f) {")) {
    Write-Host "  OK  circle normal gate" -ForegroundColor DarkGray
}

if ($verify.Contains("0.05f;")) {
    Write-Host "  OK  box touch window" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
