param([ValidateSet('run', 'check', 'build', 'dreamcast', 'package')][string]$Action = 'run')
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'scripts/run.sh'
$linuxPath = (& wsl.exe -d Ubuntu -- wslpath -a $scriptPath.Replace('\', '/')).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the Simulant runner in Ubuntu.' }
& wsl.exe -d Ubuntu -- bash $linuxPath $Action
if ($LASTEXITCODE -ne 0) { throw "Simulant $Action failed (exit $LASTEXITCODE)." }
