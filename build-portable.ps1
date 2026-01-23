# Build script for Virtual Desktop Overlay
# Creates a portable single-file executable

param(
    [string]$Configuration = "Release",
    [switch]$CreateZip = $false,
    [string]$Version = "1.0.0",
    [ValidateSet("SelfContained", "FrameworkDependent")]
    [string]$DeploymentType = "SelfContained"
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Virtual Desktop Overlay - Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean -c $Configuration

# Build based on deployment type
Write-Host ""
if ($DeploymentType -eq "SelfContained") {
    Write-Host "Building SELF-CONTAINED single-file executable..." -ForegroundColor Yellow
    Write-Host "(No .NET runtime required, larger file size ~80-100MB)" -ForegroundColor Cyan
    dotnet publish -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true
} else {
    Write-Host "Building FRAMEWORK-DEPENDENT executable..." -ForegroundColor Yellow
    Write-Host "(Requires .NET 10 Desktop Runtime, smaller file size ~500KB)" -ForegroundColor Cyan
    dotnet publish -c $Configuration --self-contained false
}

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Get publish path based on deployment type
if ($DeploymentType -eq "SelfContained") {
    $publishPath = "bin\$Configuration\net10.0-windows\win-x64\publish"
} else {
    $publishPath = "bin\$Configuration\net10.0-windows\publish"
}

Write-Host ""
Write-Host "Build successful!" -ForegroundColor Green
Write-Host "Output location: $publishPath" -ForegroundColor Green

# List files
Write-Host ""
Write-Host "Published files:" -ForegroundColor Yellow
Get-ChildItem $publishPath | Format-Table Name, Length -AutoSize

# Create ZIP if requested
if ($CreateZip) {
    $deploymentSuffix = if ($DeploymentType -eq "SelfContained") { "standalone" } else { "framework-dependent" }
    $zipName = "VirtualDesktopOverlay-v$Version-$deploymentSuffix-win-x64.zip"
    $zipPath = "bin\$zipName"
    
    Write-Host ""
    Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
    
    # Remove old zip if exists
    if (Test-Path $zipPath) {
        Remove-Item $zipPath
    }
    
    # Create ZIP
    Compress-Archive -Path "$publishPath\VirtualDesktopOverlay.exe" -DestinationPath $zipPath
    
    Write-Host "ZIP created: $zipPath" -ForegroundColor Green
    
    # Show ZIP size
    $zipSize = (Get-Item $zipPath).Length
    $zipSizeMB = [math]::Round($zipSize / 1MB, 2)
    Write-Host "ZIP size: $zipSizeMB MB" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Build complete!" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run: $publishPath\VirtualDesktopOverlay.exe" -ForegroundColor White
