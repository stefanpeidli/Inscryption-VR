param(
    [string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ',
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$baseline = Join-Path $root 'artifacts\installed-backups\20261009-032516-0022'
$baselinePlugins = Join-Path $baseline 'InscryptionVR'
$expected = @{
    'InscryptionVR.dll' = '59E02E294857574BD333D7A8E6AD54872F1EB545E1C616E58B497C785ACFE788'
    'InscryptionVR.Native.dll' = 'EA4C2B919B19F668D0B8CAD0DB1E6220A0AE6D93357432B5307EC3171D91642E'
    'openvr_api.dll' = 'FD100F4F6730B5FB01D644EDCA96C7280A09481F044F61695908FEBA2D9663C6'
    'OpenVR-LICENSE.txt' = 'F56FF606104D4EF18E617921A75C73AD73B5A1A1D70C69590C29DE16919E04AD'
}
foreach ($name in $expected.Keys) {
    $source = Join-Path $baselinePlugins $name
    if (!(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source).Hash -ne $expected[$name]) {
        throw ('Verified baseline missing or modified: ' + $name)
    }
}
$sourceConfig = Join-Path $baseline 'local.inscryption.vr.cfg'
if (!(Test-Path -LiteralPath $sourceConfig)) { throw 'Baseline configuration is missing.' }
$config = [IO.File]::ReadAllText($sourceConfig)
foreach ($line in @('EnableStereoRenderer = true', 'EnableOpenVRProbe = false', 'EnableLegacyXR = false')) {
    if ($config -notmatch ('(?m)^' + [regex]::Escape($line) + '\r?$')) { throw ('Unexpected baseline configuration: ' + $line) }
}
if ($VerifyOnly) { Write-Output 'PASS: user-verified 0.2.0 rollback files and configuration are available. No game files changed.'; return }
if (Get-Process -Name Inscryption -ErrorAction SilentlyContinue) { throw 'Close Inscryption before restoring the baseline.' }
$destination = Join-Path $GamePath 'BepInEx\plugins\InscryptionVR'
$configPath = Join-Path $GamePath 'BepInEx\config\local.inscryption.vr.cfg'
$backup = Join-Path $root ('artifacts\installed-backups\before-baseline-restore-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if (Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination (Join-Path $backup 'InscryptionVR') -Recurse }
if (Test-Path -LiteralPath $configPath) { Copy-Item -LiteralPath $configPath -Destination $backup }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($name in $expected.Keys) {
    $installed = Join-Path $destination $name
    Copy-Item -LiteralPath (Join-Path $baselinePlugins $name) -Destination $installed -Force
    if ((Get-FileHash -LiteralPath $installed).Hash -ne $expected[$name]) { throw ('Rollback verification failed: ' + $name) }
}
New-Item -ItemType Directory -Path (Split-Path -Parent $configPath) -Force | Out-Null
Copy-Item -LiteralPath $sourceConfig -Destination $configPath -Force
if ((Get-FileHash -LiteralPath $configPath).Hash -ne (Get-FileHash -LiteralPath $sourceConfig).Hash) { throw 'Rollback configuration verification failed.' }
Write-Output "Restored verified 0.2.0 renderer. Previous installation backed up at: $backup"
