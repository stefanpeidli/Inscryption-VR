param([string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'bootstrap.ps1')
& (Join-Path $PSScriptRoot 'bootstrap-openvr.ps1')
& (Join-Path $PSScriptRoot 'build.ps1') -GamePath $GamePath
$stage = Join-Path $root 'artifacts\diagnostic-package'
Expand-Archive -LiteralPath (Join-Path $root '.dependencies\BepInEx_win_x86_5.4.23.2.zip') -DestinationPath $stage -Force
$pluginDirectory = Join-Path $stage 'BepInEx\plugins\InscryptionVR'
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'artifacts\BepInEx\plugins\InscryptionVR\InscryptionVR.dll') -Destination $pluginDirectory -Force
Copy-Item -LiteralPath (Join-Path $root '.dependencies\OpenVR\openvr_api.dll') -Destination $pluginDirectory -Force
Copy-Item -LiteralPath (Join-Path $root '.dependencies\OpenVR\LICENSE') -Destination (Join-Path $pluginDirectory 'OpenVR-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\first-runtime-test.md') -Destination (Join-Path $stage 'INSCRYPTION-VR-TEST.md') -Force
$archive = Join-Path $root 'artifacts\InscryptionVR-diagnostic-x86.zip'
$packageItems = @(Get-ChildItem -LiteralPath $stage -Force | ForEach-Object { $_.FullName })
Compress-Archive -LiteralPath $packageItems -DestinationPath $archive -Force
Write-Output "Diagnostic package: $archive"
