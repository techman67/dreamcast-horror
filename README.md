# Dreamcast Horror

Engine-neutral C++ gameplay, developed through a Unity Windows host. Sega
Dreamcast is the target; the Simulant backend and hardware budgets remain to be
validated. See [the architecture](Docs/compatibility-api.md) and
[the room authoring workflow](Docs/room-format.md).

From the repository root in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/run-tests.ps1
.\Unity\Dreamcast-Horror\build_unity_plugin.bat --non-interactive
```

Rebuild the DLL after every C++ change; Unity does not compile native sources.
The build requires Visual Studio C++ x64 tools. Native tests compile all current
Game sources without Unity and leave their binaries in a unique temporary folder.
Pass `-AddressSanitizer` to the test script for instrumented memory-safety checks.

For Unity integration checks (Unity 6000.6.0f1, project closed in other editors):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -nographics -projectPath "$PWD\Unity\Dreamcast-Horror" -executeMethod ArchitectureChecks.Run -logFile "$env:TEMP\dreamcast-checks.log"
```

The check exits with code 0 on success and 1 on failure. Its log contains
`ARCHITECTURE CHECKS PASSED` when scene, export and native integration checks pass.
It does not substitute for a visual playthrough or Dreamcast hardware testing.
