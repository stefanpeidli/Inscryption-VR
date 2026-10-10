param(
    [string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ',
    [string]$BepInExRoot
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$BepInExRoot) { $BepInExRoot = Join-Path $root '.dependencies\BepInEx\BepInEx' }
$managed = Join-Path $GamePath 'Inscryption_Data\Managed'
$bep = Join-Path $BepInExRoot 'core\BepInEx.dll'
if (!(Test-Path -LiteralPath $bep)) { throw 'Missing BepInEx.dll. Run scripts/bootstrap.ps1 first or specify -BepInExRoot.' }
if (!(Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) { throw 'GamePath must point to the installed Inscryption game.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$output = Join-Path $root 'artifacts\BepInEx\plugins\InscryptionVR'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\InscryptionVR') -Filter '*.cs' -Recurse | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' } | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw 'No plugin source files found.' }
$bindings = Join-Path $root '.dependencies\OpenVR\openvr_api.cs'
if (!(Test-Path -LiteralPath $bindings)) { throw 'Run scripts/bootstrap-openvr.ps1 to fetch official managed input bindings.' }
$sources += $bindings
$referencesGame = Join-Path $managed 'Assembly-CSharp.dll'
$references = @($bep)
$references += $referencesGame
$references += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' | ForEach-Object { $_.FullName })
$references += @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'netstandard.dll') | ForEach-Object { Join-Path $managed $_ }
$arguments = @('/nologo', '/noconfig', '/target:library', '/platform:x86', '/optimize+', '/nostdlib+', ('/out:' + (Join-Path $output 'InscryptionVR.dll')))
$arguments += $references | ForEach-Object { '/reference:' + $_ }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
Write-Output "Built $(Join-Path $output 'InscryptionVR.dll')"
