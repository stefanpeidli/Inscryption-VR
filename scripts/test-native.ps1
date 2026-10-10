param([switch]$WithdrawnTimingExperiment)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $root '.dependencies\zig\zig-windows-x86_64-0.13.0\zig.exe'
$directory = Join-Path $root 'artifacts\native-validation'
$bridge = Join-Path $root 'artifacts\native\InscryptionVR.Native.dll'
if (!(Test-Path -LiteralPath $bridge)) { throw 'Build native renderer first with scripts/build-native.ps1.' }
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$env:ZIG_GLOBAL_CACHE_DIR = Join-Path $root '.dependencies\zig-cache-global'
$env:ZIG_LOCAL_CACHE_DIR = Join-Path $root '.dependencies\zig-cache-local'
& $compiler c++ -target x86-windows-gnu -std=c++17 -shared -O2 -I (Join-Path $root '.dependencies\OpenVR') (Join-Path $root 'tools\validation\native\MockOpenVR.cpp') -o (Join-Path $directory 'MockOpenVR.dll')
if ($LASTEXITCODE -ne 0) { throw 'Mock provider compilation failed.' }
$fixture = if ($WithdrawnTimingExperiment) { 'BridgeSmoke.cpp' } else { 'BridgeBaselineSmoke.cpp' }
& $compiler c++ -target x86-windows-gnu -std=c++17 -O2 -I (Join-Path $root '.dependencies\OpenVR') (Join-Path $root ('tools\validation\native\' + $fixture)) -o (Join-Path $directory 'BridgeSmoke.exe')
if ($LASTEXITCODE -ne 0) { throw 'Native smoke harness compilation failed.' }
& (Join-Path $directory 'BridgeSmoke.exe') $bridge (Join-Path $directory 'MockOpenVR.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native bridge validation failed.' }
