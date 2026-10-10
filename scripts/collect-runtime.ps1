param([string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $root ('artifacts\runtime-snapshots\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$playerDirectory = Join-Path $env:USERPROFILE 'AppData\LocalLow\Daniel Mullins Games\Inscryption'
$sources = @(
    (Join-Path $GamePath 'BepInEx\LogOutput.log'),
    (Join-Path $GamePath 'BepInEx\config\local.inscryption.vr.cfg'),
    (Join-Path $playerDirectory 'Player.log'),
    (Join-Path $playerDirectory 'Player-prev.log')
)
$copied = @()
foreach ($source in $sources) {
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $destination
        $copied += [IO.Path]::GetFileName($source)
    }
}
$pluginDirectory = Join-Path $GamePath 'BepInEx\plugins\InscryptionVR'
$manifest = @()
foreach ($name in @('InscryptionVR.dll','InscryptionVR.Native.dll','openvr_api.dll')) {
    $path = Join-Path $pluginDirectory $name
    if (Test-Path -LiteralPath $path) {
        $entry = [ordered]@{ File = $name; Sha256 = (Get-FileHash -LiteralPath $path).Hash }
        if ($name -eq 'InscryptionVR.dll') { $entry.Version = [Reflection.AssemblyName]::GetAssemblyName($path).Version.ToString() }
        $manifest += $entry
    }
}
$result = [ordered]@{
    CapturedUtc = [DateTime]::UtcNow.ToString('o')
    GameRunning = [bool](Get-Process -Name Inscryption -ErrorAction SilentlyContinue)
    Files = $copied
    InstalledPlugins = $manifest
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding UTF8
Write-Output "Runtime snapshot: $destination"
Write-Output 'Local logs/config only; no save files are collected.'
