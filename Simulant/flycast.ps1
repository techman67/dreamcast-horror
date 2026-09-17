param([string]$FlycastPath = (Join-Path $PSScriptRoot '..\FlyCast\flycast.exe'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $FlycastPath -PathType Leaf)) {
    throw "Flycast was not found at $FlycastPath. Pass -FlycastPath with its location."
}
$emulator = (Resolve-Path -LiteralPath $FlycastPath).Path
& (Join-Path $PSScriptRoot 'run.ps1') package
$disc = Join-Path $PSScriptRoot '..\FlyCast\dreamcast-horror.cdi'
Start-Process -FilePath $emulator -ArgumentList ('"' + $disc + '"') -WorkingDirectory (Split-Path $emulator)
