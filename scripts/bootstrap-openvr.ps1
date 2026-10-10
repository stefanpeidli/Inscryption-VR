param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $root '.dependencies\OpenVR'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$files = @(
    @{ Name = 'openvr_api.dll'; Path = 'bin/win32/openvr_api.dll'; Hash = 'FD100F4F6730B5FB01D644EDCA96C7280A09481F044F61695908FEBA2D9663C6' },
    @{ Name = 'LICENSE'; Path = 'LICENSE'; Hash = 'F56FF606104D4EF18E617921A75C73AD73B5A1A1D70C69590C29DE16919E04AD' },
    @{ Name = 'openvr_api.cs'; Path = 'headers/openvr_api.cs'; Hash = '65E77D3880B70FBBFE10A8DF8124CDFD112A40146C8C88A982C73C6D78A9AECE' }
)
foreach ($file in $files) {
    $target = Join-Path $destination $file.Name
    if (!(Test-Path -LiteralPath $target)) {
        Invoke-WebRequest -Uri ('https://raw.githubusercontent.com/ValveSoftware/openvr/v2.12.14/' + $file.Path) -OutFile $target
    }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.Hash) {
        throw ('OpenVR checksum mismatch: ' + $file.Name)
    }
}
Write-Output 'Official OpenVR v2.12.14 win32 dependency verified.'
