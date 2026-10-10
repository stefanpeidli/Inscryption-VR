param([string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'bootstrap-openvr.ps1')
& (Join-Path $PSScriptRoot 'build.ps1') -GamePath $GamePath
$stage = Join-Path $root 'artifacts\openvr-update'
$pluginDirectory = Join-Path $stage 'BepInEx\plugins\InscryptionVR'
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'artifacts\BepInEx\plugins\InscryptionVR\InscryptionVR.dll') -Destination $pluginDirectory -Force
Copy-Item -LiteralPath (Join-Path $root '.dependencies\OpenVR\openvr_api.dll') -Destination $pluginDirectory -Force
Copy-Item -LiteralPath (Join-Path $root '.dependencies\OpenVR\LICENSE') -Destination (Join-Path $pluginDirectory 'OpenVR-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\openvr-probe.md') -Destination (Join-Path $stage 'OPENVR-PROBE.md') -Force
$archive = Join-Path $root 'artifacts\InscryptionVR-openvr-probe-update-x86.zip'
$packageItems = @(Get-ChildItem -LiteralPath $stage -Force | ForEach-Object { $_.FullName })
Compress-Archive -LiteralPath $packageItems -DestinationPath $archive -Force
Write-Output "OpenVR update package: $archive"
