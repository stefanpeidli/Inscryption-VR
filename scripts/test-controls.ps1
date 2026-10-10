param([string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts\control-validation'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$executable = Join-Path $output 'ControllerTriggerSmoke.exe'
& $compiler /nologo /target:exe /platform:x86 ('/out:' + $executable) (Join-Path $root 'src\InscryptionVR\ControllerTriggerState.cs') (Join-Path $root 'tools\validation\ControllerTriggerSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Controller trigger test compilation failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Controller trigger tests failed.' }
$routerExecutable = Join-Path $output 'ControllerInputRouterSmoke.exe'
& $compiler /nologo /target:exe /platform:x86 ('/out:' + $routerExecutable) (Join-Path $root 'src\InscryptionVR\ControllerInputRouter.cs') (Join-Path $root 'src\InscryptionVR\ControllerTriggerState.cs') (Join-Path $root 'tools\validation\ControllerInputRouterSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Controller context harness compilation failed.' }
& $routerExecutable
if ($LASTEXITCODE -ne 0) { throw 'Controller context tests failed.' }
$viewExecutable = Join-Path $output 'TableViewPolicySmoke.exe'
& $compiler /nologo /target:exe /platform:x86 ('/out:' + $viewExecutable) (Join-Path $root 'src\InscryptionVR\TableViewPolicy.cs') (Join-Path $root 'tools\validation\TableViewPolicySmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Viewpoint policy harness compilation failed.' }
& $viewExecutable
if ($LASTEXITCODE -ne 0) { throw 'Viewpoint policy tests failed.' }
$layoutExecutable = Join-Path $output 'OpenVrControllerLayoutSmoke.exe'
& $compiler /nologo /target:exe /platform:x86 ('/out:' + $layoutExecutable) (Join-Path $root '.dependencies\OpenVR\openvr_api.cs') (Join-Path $root 'tools\validation\OpenVrControllerLayoutSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'OpenVR controller ABI harness compilation failed.' }
& $layoutExecutable
if ($LASTEXITCODE -ne 0) { throw 'OpenVR controller ABI checks failed.' }
$stickExecutable = Join-Path $output 'StickGestureSmoke.exe'
& $compiler /nologo /target:exe /platform:x86 ('/out:' + $stickExecutable) (Join-Path $root 'src\InscryptionVR\StickGestureState.cs') (Join-Path $root 'tools\validation\StickGestureSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Stick gesture harness compilation failed.' }
& $stickExecutable
if ($LASTEXITCODE -ne 0) { throw 'Stick gesture tests failed.' }
& (Join-Path $root 'tools\validation\check-control-contract.ps1') -GamePath $GamePath
$adapterExecutable = Join-Path $output 'GameCursorAdapterSmoke.exe'
& $compiler /nologo /target:exe /platform:x86 ('/out:' + $adapterExecutable) (Join-Path $root 'tools\validation\GameCursorAdapterSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Production adapter harness compilation failed.' }
& $adapterExecutable (Join-Path $GamePath 'Inscryption_Data\Managed') (Join-Path $root '.dependencies\BepInEx\BepInEx\core') (Join-Path $root 'artifacts\BepInEx\plugins\InscryptionVR\InscryptionVR.dll')
if ($LASTEXITCODE -ne 0) { throw 'Production adapter runtime member resolution failed.' }
