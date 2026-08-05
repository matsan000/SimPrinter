<#
.SYNOPSIS
    Publishes SimPrinter and builds a versioned MSI installer.

.DESCRIPTION
    Reads <Version> from SimPrinter.csproj (the single source of truth for the app's
    version), republishes the self-contained app, and builds installer\SimPrinter-<version>.msi.

    Because the MSI's UpgradeCode is fixed (never change it in Product.wxs), installing a
    newer version's MSI over an existing install automatically upgrades in place - no
    manual uninstall needed. Just bump <Version> in SimPrinter.csproj before running this.

.EXAMPLE
    .\build-installer.ps1
#>

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$csprojPath = Join-Path $root "src\SimPrinter\SimPrinter.csproj"
$publishDir = Join-Path $root "publish"
$installerDir = Join-Path $root "installer"

# --- Read version from the csproj (single source of truth) ---
[xml]$csproj = Get-Content $csprojPath
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    throw "Could not find <Version> in SimPrinter.csproj"
}
Write-Host "Building installer for SimPrinter v$version" -ForegroundColor Cyan

# --- Make sure SimPrinter isn't running (locks publish output files) ---
$running = Get-Process -Name "SimPrinter" -ErrorAction SilentlyContinue
if ($running) {
    throw "SimPrinter.exe is currently running. Close it before building, so the publish step can overwrite its files."
}

# --- Publish (self-contained, win-x64) ---
# The publish folder is harvested wholesale into the MSI (Product.wxs: publish\**), so it must
# only ever contain exactly what dotnet publish produces. Wiping it first guarantees that -
# otherwise a stray file dropped in there by anything else (a manual copy, another tool) would
# silently ride along into the installer forever, since dotnet publish only adds/overwrites,
# it never removes files that don't belong.
if (Test-Path $publishDir) {
    Write-Host "Cleaning publish folder..." -ForegroundColor Cyan
    Remove-Item -Recurse -Force $publishDir
}

Write-Host "Publishing..." -ForegroundColor Cyan
Push-Location $root
try {
    dotnet publish $csprojPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
}
finally {
    Pop-Location
}

# --- Build the MSI ---
$outputMsi = Join-Path $installerDir "SimPrinter-$version.msi"
Write-Host "Building MSI -> $outputMsi" -ForegroundColor Cyan

$env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"
Push-Location $installerDir
try {
    wix build -arch x64 -ext WixToolset.UI.wixext -d ProductVersion=$version -o $outputMsi Product.wxs
    if ($LASTEXITCODE -ne 0) { throw "wix build failed" }
}
finally {
    Pop-Location
}

Write-Host "Done: $outputMsi" -ForegroundColor Green
