[CmdletBinding()]
param(
    [string] $InstallRoot,
    [switch] $ForceDownload
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = Split-Path -Parent $PSScriptRoot

function Read-Json([string] $Path) {
    [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
}

$globalJson = Read-Json (Join-Path $repo 'global.json')
$version = [string] $globalJson.sdk.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "global.json: sdk.version '$version' is not major.minor.patch." }
if ([string] $globalJson.sdk.rollForward -ne 'disable') { throw "global.json: sdk.rollForward must be 'disable' so the pinned SDK is the one used." }

$pinPath = Join-Path $PSScriptRoot 'dotnet-sdk.json'
$pin = Read-Json $pinPath
if ([string] $pin.version -ne $version) { throw "tools/dotnet-sdk.json pins $($pin.version), but global.json asks for $version." }
if ([string] $pin.url -notmatch "^https://builds\.dotnet\.microsoft\.com/dotnet/Sdk/$([regex]::Escape($version))/dotnet-sdk-$([regex]::Escape($version))-win-x64\.zip$") {
    throw "tools/dotnet-sdk.json: url '$($pin.url)' is not the official win-x64 zip of SDK $version."
}
if ([string] $pin.sha512 -notmatch '^[0-9a-f]{128}$') { throw 'tools/dotnet-sdk.json: sha512 must be 128 lowercase hexadecimal digits.' }

function Test-SdkListed([string] $Dotnet) {
    $listed = @(& $Dotnet --list-sdks 2>$null)
    if ($LASTEXITCODE -ne 0) { return $false }
    return @($listed | Where-Object { $_ -match "^$([regex]::Escape($version)) \[" }).Count -gt 0
}

if (-not $ForceDownload) {
    $system = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($system -and (Test-SdkListed $system.Source)) {
        Write-Output ".NET SDK ${version}: installed at $(Split-Path -Parent $system.Source)"
        return
    }
}

if (-not $InstallRoot) {
    $InstallRoot = if ($env:PANELCLADDING_TOOLCHAIN_ROOT) { $env:PANELCLADDING_TOOLCHAIN_ROOT }
    elseif ($env:LOCALAPPDATA) { Join-Path $env:LOCALAPPDATA 'PanelCladding\toolchain' }
    else { Join-Path $repo 'artifacts\toolchain' }
}
$target = Join-Path $InstallRoot "dotnet-sdk-$version"
$dotnet = Join-Path $target 'dotnet.exe'

if (-not (Test-Path -LiteralPath (Join-Path $target "sdk\$version") -PathType Container)) {
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    $archive = Join-Path $InstallRoot "dotnet-sdk-$version-win-x64.zip"
    $staging = "$target.partial"
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Write-Output ".NET SDK ${version}: downloading $($pin.url)"
    Invoke-WebRequest -Uri $pin.url -OutFile $archive -UseBasicParsing
    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash.ToLowerInvariant()
    if ($actual -ne $pin.sha512) {
        Remove-Item -LiteralPath $archive -Force
        throw ".NET SDK ${version}: SHA-512 of the download is $actual, but tools/dotnet-sdk.json pins $($pin.sha512)."
    }
    Write-Output ".NET SDK ${version}: SHA-512 matches the pinned checksum"
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($archive, $staging)
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    Move-Item -LiteralPath $staging -Destination $target
    Remove-Item -LiteralPath $archive -Force
}

if (-not (Test-SdkListed $dotnet)) { throw ".NET SDK ${version}: $dotnet does not list the SDK." }
$env:DOTNET_ROOT = $target
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:PATH = "$target;$env:PATH"
Write-Output ".NET SDK ${version}: using $target"
