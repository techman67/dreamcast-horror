$ErrorActionPreference = "Stop"

$repo = "C:\Users\Administrator\Documents\ChatGPT\dreamcast-horror"
$cpp  = Join-Path $repo "Game\Compatibility\StaticCollisionBackend.cpp"

Copy-Item -LiteralPath $cpp -Destination "$cpp.bak9b" -Force
Write-Host "Backed up to $cpp.bak9b" -ForegroundColor DarkGray

$text = Get-Content -LiteralPath $cpp -Raw

# Match the escape hatch, tolerant of any indentation.
$pattern = '(?m)^\s*if \(otherAxis <= boxMinOther \|\|\s*\r?\n\s*otherAxis >= boxMaxOther\) \{\s*\r?\n\s*\r?\n\s*return desiredAxis;\s*\r?\n\s*\}'

if ($text -notmatch $pattern) {
    Write-Host "Escape hatch not found by regex. Dumping surrounding lines:" -ForegroundColor Yellow
    $lines = $text -split "`r?`n"
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match 'otherAxis') {
            $s = [Math]::Max(0, $i - 2)
            $e = [Math]::Min($lines.Length - 1, $i + 6)
            for ($j = $s; $j -le $e; $j++) {
                Write-Host ("{0,4}: {1}" -f $j, $lines[$j])
            }
            Write-Host ""
        }
    }
    throw "Stopping so you can paste the dump."
}

$replacement = @"
if (otherAxis <= boxMinOther + 0.01f ||
        otherAxis >= boxMaxOther - 0.01f) {

        return desiredAxis;
    }
"@

$newText = [regex]::Replace($text, $pattern, $replacement, 1)

if ($newText -eq $text) {
    throw "Regex matched but replace produced no change."
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($cpp, $newText, $utf8)

Write-Host "Patched StaticCollisionBackend.cpp" -ForegroundColor Green

$verify = Get-Content -LiteralPath $cpp -Raw
if ($verify -match 'boxMinOther \+ 0\.01f') {
    Write-Host "  OK  boundary tolerance present" -ForegroundColor DarkGray
}
if ($verify -match 'boxMaxOther - 0\.01f') {
    Write-Host "  OK  boundary tolerance present (max)" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Rebuild:"
Write-Host "  cd $repo"
Write-Host "  .\Unity\Dreamcast-Horror\build_unity_plugin.bat"
