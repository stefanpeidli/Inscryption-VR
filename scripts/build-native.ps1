param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'bootstrap-native.ps1')
$compiler = Join-Path $root '.dependencies\zig\zig-windows-x86_64-0.13.0\zig.exe'
$outputDirectory = Join-Path $root 'artifacts\native'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$env:ZIG_GLOBAL_CACHE_DIR = Join-Path $root '.dependencies\zig-cache-global'
$env:ZIG_LOCAL_CACHE_DIR = Join-Path $root '.dependencies\zig-cache-local'
& $compiler c++ -target x86-windows-gnu -std=c++17 -shared -O2 -I (Join-Path $root '.dependencies\OpenVR') (Join-Path $root 'native\InscryptionVRBridge.cpp') -o (Join-Path $outputDirectory 'InscryptionVR.Native.dll')
if ($LASTEXITCODE -ne 0) { throw "Native compilation failed with exit code $LASTEXITCODE" }
Write-Output "Built $(Join-Path $outputDirectory 'InscryptionVR.Native.dll')"
