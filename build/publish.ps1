<#
.SYNOPSIS
    Runs the tests, then builds the two release zips into .\publish.

.DESCRIPTION
    - FluxProHWiNFO-<version>-win-x64.zip
        Small single-file exe. Needs the .NET 10 Desktop Runtime installed.
    - FluxProHWiNFO-<version>-win-x64-selfcontained.zip
        Larger single-file exe with the .NET runtime built in. Runs on any Windows 10/11 x64 PC.

    Each zip holds the exe, README.md, LICENSE.txt, THIRD-PARTY-NOTICES.md and licenses\.
    The version comes from Directory.Build.props.

.EXAMPLE
    .\build\publish.ps1
#>
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\FluxProHWiNFO\FluxProHWiNFO.csproj'
$solution = Join-Path $root 'FluxProHWiNFO.slnx'
$outRoot = Join-Path $root 'publish'

[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = @($props.Project.PropertyGroup)[0].Version
if (-not $version) { throw 'Version not found in Directory.Build.props' }

Write-Host "FluxProHWiNFO $version ($Runtime)" -ForegroundColor Cyan

Write-Host 'Running tests...' -ForegroundColor Cyan
dotnet test $solution -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; nothing published.' }

$variants = @(
    @{ Suffix = '';               SelfContained = $false },
    @{ Suffix = '-selfcontained'; SelfContained = $true }
)

New-Item -ItemType Directory -Force $outRoot | Out-Null

foreach ($v in $variants) {
    $name = "FluxProHWiNFO-$version-$Runtime$($v.Suffix)"
    $dir = Join-Path $outRoot $name
    $zip = "$dir.zip"
    Write-Host "Publishing $name..." -ForegroundColor Cyan

    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    if (Test-Path $zip) { Remove-Item $zip -Force }

    $publishArgs = @(
        'publish', $project,
        '-c', 'Release',
        '-r', $Runtime,
        '--self-contained', $(if ($v.SelfContained) { 'true' } else { 'false' }),
        '-o', $dir,
        '--nologo',
        '-p:PublishSingleFile=true',
        '-p:DebugType=none'
    )
    if ($v.SelfContained) {
        # Native runtime pieces go inside the exe too. Deliberately NOT using EnableCompressionInSingleFile:
        # measured 2026-11-23, it shrinks the exe from 111 to 49 MB but triples the always-running tray
        # app's private memory (20 -> 61 MB). The zip compresses the download anyway.
        $publishArgs += '-p:IncludeNativeLibrariesForSelfExtract=true'
    }

    dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed." }

    Copy-Item (Join-Path $root 'README.md') $dir
    # Entry names are written with "/" by hand: Windows PowerShell 5.1's Compress-Archive and
    # ZipFile.CreateFromDirectory both store "\", which breaks folders for non-Windows unzip tools.
    $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem $dir -Recurse -File) {
            $entryName = $file.FullName.Substring($dir.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $archive.Dispose()
    }

    $exe = Get-Item (Join-Path $dir 'FluxProHWiNFO.exe')
    Write-Host ("  exe {0:N1} MB, zip {1:N1} MB -> {2}" -f ($exe.Length / 1MB), ((Get-Item $zip).Length / 1MB), $zip) -ForegroundColor Green
}
