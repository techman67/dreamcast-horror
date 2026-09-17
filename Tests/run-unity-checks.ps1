param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Save and close Unity before running the batch checks.'
}
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw 'Set -UnityEditor to your Unity 6000.6.0f1 executable.' }
$repositoryPath = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repositoryPath 'Unity/Dreamcast-Horror'
$logDirectory = Join-Path $env:TEMP ('dreamcast-unity-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logDirectory | Out-Null
foreach ($check in @('ArchitectureChecks', 'KeyDoorChecks', 'RoomAudioExportChecks',
    'RoomAudioRangeChecks', 'DreamcastAudioTrimChecks', 'RoomPropExportChecks', 'RoomLightingChecks')) {
    $logPath = Join-Path $logDirectory ($check + '.log')
    $arguments = '-batchmode -nographics -projectPath "' + $projectPath + '" -executeMethod ' + $check + '.Run -logFile "' + $logPath + '"'
    $testProcess = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $testProcess.WaitForExit(180000)) {
        Stop-Process -Id $testProcess.Id
        throw "Unity check timed out: $check. Log: $logPath"
    }
    if ($testProcess.ExitCode -ne 0) { throw "Unity check failed: $check. Log: $logPath" }
    Write-Output "$check passed. Log: $logPath"
}
Write-Output "All Unity checks passed. Logs: $logDirectory"
