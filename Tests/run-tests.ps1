param([switch]$AddressSanitizer)

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) { throw 'Visual Studio C++ tools were not found.' }
$testDirectory = Join-Path $env:TEMP ('dreamcast-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$sources = Get-ChildItem (Join-Path $repoPath 'Game') -Recurse -Filter '*.cpp'
$sourceArguments = ($sources | ForEach-Object { '"' + $_.FullName + '"' }) -join ' '
$includes = @('Game/Core', 'Game/Compatibility', 'Game/Gameplay', 'Tests') | ForEach-Object { '/I"' + (Join-Path $repoPath $_) + '"' }
$diagnostics = if ($AddressSanitizer) { '/fsanitize=address /Zi ' } else { '' }
$lines = @('@echo off', ('call "' + $vsPath + '\VC\Auxiliary\Build\vcvars64.bat" >nul'), 'if not "%errorlevel%"=="0" exit /b 1')
foreach ($test in Get-ChildItem $PSScriptRoot -Filter 'test_*.cpp') {
    $executable = Join-Path $testDirectory ($test.BaseName + '.exe')
    $lines += 'cl /nologo /std:c++17 /EHsc /W4 /WX /UNDEBUG ' + $diagnostics + ($includes -join ' ') + ' ' + $sourceArguments + ' "' + $test.FullName + '" /Fe:"' + $executable + '"'
    $lines += 'if not "%errorlevel%"=="0" exit /b 1'
    $lines += 'pushd "' + $repoPath + '"'
    $lines += '"' + $executable + '"'
    $lines += 'if not "%errorlevel%"=="0" exit /b 1'
    $lines += 'popd'
}
$batch = Join-Path $testDirectory 'run.bat'
[IO.File]::WriteAllLines($batch, $lines)
Push-Location $testDirectory
try {
    & $batch
    if ($LASTEXITCODE -ne 0) { throw "Native tests failed. Artifacts: $testDirectory" }
} finally { Pop-Location }
Write-Output "All native tests passed. Artifacts: $testDirectory"
