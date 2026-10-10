param(
    [string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ',
    [switch]$EnableOpenVRProbe,
    [switch]$EnableStereoRenderer,
    [switch]$EnableControllerPointer
)
$ErrorActionPreference = 'Stop'
if ($EnableOpenVRProbe -and $EnableStereoRenderer) { throw 'Select either the connection probe or stereo renderer.' }
if ($EnableControllerPointer -and (!$EnableStereoRenderer -or $EnableOpenVRProbe)) { throw 'Controller pointer requires the stereo renderer and no connection probe.' }
if (Get-Process -Name Inscryption -ErrorAction SilentlyContinue) {
    throw 'Close Inscryption before installing the update.'
}
$root = Split-Path -Parent $PSScriptRoot
$candidatePlugin = Join-Path $root 'artifacts\BepInEx\plugins\InscryptionVR\InscryptionVR.dll'
if (Test-Path -LiteralPath $candidatePlugin) {
    if ([Reflection.AssemblyName]::GetAssemblyName($candidatePlugin).Version.ToString() -eq '0.2.1.0') {
        throw 'Version 0.2.1 crashed during headset testing and has been withdrawn. Do not install this rejected build.'
    }
}
$loader = Join-Path $GamePath 'BepInEx\core\BepInEx.dll'
if (!(Test-Path -LiteralPath $loader)) { throw 'Install the x86 BepInEx loader before using this plugin update script.' }
$target = Join-Path $GamePath 'BepInEx\plugins\InscryptionVR'
$configPath = Join-Path $GamePath 'BepInEx\config\local.inscryption.vr.cfg'
$backup = Join-Path $root ('artifacts\installed-backups\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
$files = @(
    @{ Source = (Join-Path $root 'artifacts\BepInEx\plugins\InscryptionVR\InscryptionVR.dll'); Name = 'InscryptionVR.dll' },
    @{ Source = (Join-Path $root '.dependencies\OpenVR\openvr_api.dll'); Name = 'openvr_api.dll' },
    @{ Source = (Join-Path $root '.dependencies\OpenVR\LICENSE'); Name = 'OpenVR-LICENSE.txt' }
)
if ($EnableStereoRenderer) {
    if ($EnableControllerPointer) {
        $nativeCandidate = Join-Path $root 'artifacts\native\InscryptionVR.Native.dll'
        if ((Get-FileHash -LiteralPath $nativeCandidate).Hash -ne 'EA4C2B919B19F668D0B8CAD0DB1E6220A0AE6D93357432B5307EC3171D91642E') {
            throw 'Controller update must retain the verified 0.2.0 native renderer binary.'
        }
    }
    $files += @{ Source = (Join-Path $root 'artifacts\native\InscryptionVR.Native.dll'); Name = 'InscryptionVR.Native.dll' }
}
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath $file.Source)) { throw ('Missing built dependency: ' + $file.Source) }
}
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination (Join-Path $backup 'InscryptionVR') -Recurse }
if (Test-Path -LiteralPath $configPath) { Copy-Item -LiteralPath $configPath -Destination $backup }
$logPath = Join-Path $GamePath 'BepInEx\LogOutput.log'
if (Test-Path -LiteralPath $logPath) { Copy-Item -LiteralPath $logPath -Destination $backup }
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($file in $files) {
    $installed = Join-Path $target $file.Name
    Copy-Item -LiteralPath $file.Source -Destination $installed -Force
    if ((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $file.Source).Hash) {
        throw ('Installed file verification failed: ' + $installed)
    }
}
if ($EnableOpenVRProbe -or $EnableStereoRenderer) {
    $config = if (Test-Path -LiteralPath $configPath) { [IO.File]::ReadAllText($configPath) } else { "[Experimental]`r`n" }
    $settings = @{
        EnableOpenVRProbe = $EnableOpenVRProbe.IsPresent.ToString().ToLowerInvariant()
        EnableStereoRenderer = $EnableStereoRenderer.IsPresent.ToString().ToLowerInvariant()
        EnableLegacyXR = 'false'
    }
    foreach ($setting in $settings.GetEnumerator()) {
        $pattern = '(?m)^' + [regex]::Escape($setting.Key) + '\s*=.*$'
        $line = $setting.Key + ' = ' + $setting.Value
        if ($config -match $pattern) {
            $config = [regex]::Replace($config, $pattern, $line)
        } elseif ($config -match '(?m)^\[Experimental\]\s*$') {
            $config = [regex]::Replace($config, '(?m)^\[Experimental\][ \t]*\r?$', "[Experimental]`r`n" + $line)
        } else {
            $config += "`r`n[Experimental]`r`n" + $line + "`r`n"
        }
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $configPath) -Force | Out-Null
    [IO.File]::WriteAllText($configPath, $config, (New-Object Text.UTF8Encoding($false)))
    $saved = [IO.File]::ReadAllText($configPath)
    foreach ($setting in $settings.GetEnumerator()) {
        if ($saved -notmatch ('(?m)^' + $setting.Key + ' = ' + $setting.Value + '\r?$')) {
            throw ('Installed config verification failed: ' + $setting.Key)
        }
    }
}
if ($EnableControllerPointer) {
    $config = [IO.File]::ReadAllText($configPath)
    if ($config -match '(?m)^EnableControllerPointer\s*=') {
        $config = [regex]::Replace($config, '(?m)^EnableControllerPointer\s*=.*$', 'EnableControllerPointer = true')
    } elseif ($config -match '(?m)^\[Input\]\s*$') {
        $config = [regex]::Replace($config, '(?m)^\[Input\][ \t]*\r?$', "[Input]`r`nEnableControllerPointer = true")
    } else {
        $config += "`r`n[Input]`r`nEnableControllerPointer = true`r`n"
    }
    [IO.File]::WriteAllText($configPath, $config, (New-Object Text.UTF8Encoding($false)))
    if ([IO.File]::ReadAllText($configPath) -notmatch '(?m)^EnableControllerPointer = true\r?$') { throw 'Controller config verification failed.' }
}
Write-Output "Installed plugin and verified file hashes. Backup: $backup"
Write-Output "OpenVR probe enabled: $EnableOpenVRProbe"
Write-Output "Stereo renderer enabled: $EnableStereoRenderer"
Write-Output "Controller pointer enabled: $EnableControllerPointer"
