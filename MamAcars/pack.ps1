# pack.ps1 - Build and package the application with Velopack
# Usage: .\pack.ps1

$ErrorActionPreference = "Stop"

# Read version from .csproj
[xml]$csproj = Get-Content "MamAcars.csproj"
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $version) {
    Write-Error "Could not read Version from .csproj"
    exit 1
}

# Read branding configuration
$brandingJson = Get-Content "branding\branding.json" | ConvertFrom-Json
$appId = $brandingJson.AppId

if (-not $appId) {
    Write-Error "Could not read AppId from branding.json"
    exit 1
}

# Read assembly name from branding.props
[xml]$brandingProps = Get-Content "branding\branding.props"
$assemblyName = $brandingProps.Project.PropertyGroup.AssemblyName | Where-Object { $_ } | Select-Object -First 1

if (-not $assemblyName) {
    $assemblyName = $appId
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  App ID:       $appId" -ForegroundColor White
Write-Host "  Assembly:     $assemblyName" -ForegroundColor White
Write-Host "  Version:      $version" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean previous publish
$publishDir = ".\publish"
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous publish..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $publishDir
}

# Publish
Write-Host "Publishing application..." -ForegroundColor Green
dotnet publish MamAcars.csproj -c Release --self-contained -r win-x64 -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed"
    exit 1
}

# Package with Velopack
Write-Host "Packaging with Velopack..." -ForegroundColor Green
vpk pack --packId $appId --packVersion $version --packDir $publishDir --mainExe "$assemblyName.exe"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Packaging failed"
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Package created successfully!" -ForegroundColor Green
Write-Host "  Output: .\Releases\" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan
