$ErrorActionPreference = "Stop"

$repo = "C:\Users\Administrator\Documents\ChatGPT\dreamcast-horror"
$cpp  = Join-Path $repo "Game\Compatibility\StaticCollisionBackend.cpp"

if (-not (Test-Path -LiteralPath $cpp)) {
    throw "StaticCollisionBackend.cpp not found at: $cpp"
}

Copy-Item -LiteralPath $cpp -Destination "$cpp.bak9" -Force
Write-Host "Backed up to $cpp.bak9" -ForegroundColor DarkGray

$text = Get-Content -LiteralPath $cpp -Raw

$oldBlock = @"
    if (otherAxis <= boxMinOther ||
        otherAxis >= boxMaxOther) {

        return desiredAxis;
    }
"@

$newBlock = @"
    // If the player is at or outside the box's span on the
    // OTHER axis, movement along THIS axis is unblocked.
    //
    // The original code used a strict comparison, which
    // failed at corners: float precision would put the
    // player a few millionths of a unit inside the boundary,
    // making <= evaluate false, and the escape hatch would
    // close exactly where we most need it.
    //
    // A small tolerance restores the intended behavior at
    // corners without measurably changing flat-face collision.
    const float boundaryTolerance = 0.01f;

    if (otherAxis <= boxMinOther + boundaryTolerance ||
        otherAxis >= boxMaxOther - boundaryTolerance) {

        return desiredAxis;
    }
"@

if (-not $text.Contains($oldBlock)) {
    throw "Could not find the escape hatch block. Pattern did not match."
}

$text = $text.Replace($oldBlock, $newBlock)

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($cpp, $text, $utf8)

Write-Host "Patched StaticCollisionBackend.cpp" -ForegroundColor Green

$verify = Get-Content -LiteralPath $cpp -Raw

if ($verify -match "boundaryTolerance") {
    Write-Host "  OK  boundary tolerance present" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Rebuild:"
Write-Host "  cd $repo"
Write-Host "  .\Unity\Dreamcast-Horror\build_unity_plugin.bat"
