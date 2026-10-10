param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dependencyRoot = Join-Path $root '.dependencies'
$compilerArchive = Join-Path $dependencyRoot 'zig-windows-x86_64-0.13.0.zip'
$compilerHash = 'D859994725EF9402381E557C60BB57497215682E355204D754EE3DF75EE3C158'
New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null
if (!(Test-Path -LiteralPath $compilerArchive)) {
    Invoke-WebRequest -Uri 'https://ziglang.org/download/0.13.0/zig-windows-x86_64-0.13.0.zip' -OutFile $compilerArchive
}
if ((Get-FileHash -LiteralPath $compilerArchive).Hash -ne $compilerHash) { throw 'Portable compiler checksum mismatch.' }
$compiler = Join-Path $dependencyRoot 'zig\zig-windows-x86_64-0.13.0\zig.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    Expand-Archive -LiteralPath $compilerArchive -DestinationPath (Join-Path $dependencyRoot 'zig') -Force
}
$headers = @(
    @{Name='openvr.h'; Hash='0C217387163B6E2B7712A9BB650085F4DC8546041270BB3E2D726901A025DC93'},
    @{Name='openvr_capi.h'; Hash='D58A2B149E434B2B8EE25CB4253CA6157FEED8092F621E78ECFED6846D35D3E4'}
)
New-Item -ItemType Directory -Path (Join-Path $dependencyRoot 'OpenVR') -Force | Out-Null
foreach ($header in $headers) {
    $path = Join-Path $dependencyRoot ('OpenVR\' + $header.Name)
    if (!(Test-Path -LiteralPath $path)) {
        Invoke-WebRequest -Uri ('https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/headers/' + $header.Name) -OutFile $path
    }
    if ((Get-FileHash -LiteralPath $path).Hash -ne $header.Hash) { throw ('OpenVR header checksum mismatch: ' + $header.Name) }
}
Write-Output 'Native build dependencies verified (portable Zig 0.13.0, OpenVR v2.12.14).'
