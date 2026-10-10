param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dependencyRoot = Join-Path $root '.dependencies'
$archive = Join-Path $dependencyRoot 'BepInEx_win_x86_5.4.23.2.zip'
$expectedHash = '7DD51193587A86C11A58530AEEB4789B15763CC6CF2C55E71785CEF4EF214253'
New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null
if (!(Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x86_5.4.23.2.zip' -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'BepInEx archive checksum mismatch. Dependency was not extracted.'
}
Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $dependencyRoot 'BepInEx') -Force
Write-Output 'BepInEx 5.4.23.2 x86 development dependency ready.'
