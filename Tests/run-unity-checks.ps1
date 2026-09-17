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
# Legacy checks explicitly test SampleScene; preserve the user's current exported room.
$exports = @{}
foreach ($name in @('sample.room', 'sample.audio', 'sample.props')) {
    $path = Join-Path $projectPath ('Assets/StreamingAssets/' + $name)
    $exports[$path] = [IO.File]::ReadAllBytes($path)
}
try {
foreach ($method in @('ArchitectureChecks.ExportSample', 'ArchitectureChecks.Run', 'KeyDoorChecks.Run',
    'RoomAudioExportChecks.Run', 'RoomAudioRangeChecks.Run', 'DreamcastAudioTrimChecks.Run',
    'RoomPropExportChecks.Run', 'RoomLightingChecks.Run', 'RoomPhysicsSetup.BuildAndCheck',
    'RoomPhysicsInputChecks.Run')) {
    $logPath = Join-Path $logDirectory ($method + '.log')
    $arguments = '-batchmode -nographics -projectPath "' + $projectPath + '" -executeMethod ' + $method + ' -logFile "' + $logPath + '"'
    $testProcess = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $testProcess.WaitForExit(180000)) {
        Stop-Process -Id $testProcess.Id
        throw "Unity check timed out: $method. Log: $logPath"
    }
    if ($testProcess.ExitCode -ne 0) { throw "Unity check failed: $method. Log: $logPath" }
    Write-Output "$method passed. Log: $logPath"
}
} finally {
    foreach ($path in $exports.Keys) { [IO.File]::WriteAllBytes($path, $exports[$path]) }
}
Write-Output "All Unity checks passed. Logs: $logDirectory"
