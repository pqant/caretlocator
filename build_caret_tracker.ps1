# Set error action preference
$ErrorActionPreference = "Stop"

# Define colors for output
$successColor = "Green"
$errorColor = "Red"
$infoColor = "Yellow"

# Function to write colored output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    else {
        $input | Write-Output
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

# Check if service is running and stop it
$process = Get-Process -Name "CaretTracker" -ErrorAction SilentlyContinue
if ($process) {
    Write-ColorOutput $infoColor "Service is running. Stopping it before build..."
    & "$PSScriptRoot\stop_caret_tracker.ps1"
    # Wait a bit to ensure the service is fully stopped
    Start-Sleep -Seconds 2
}

# Clean previous builds
Write-ColorOutput $infoColor "Cleaning previous builds..."
Remove-Item -Path "CaretTracker.Service\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "CaretTracker.Service\obj" -Recurse -Force -ErrorAction SilentlyContinue

# Restore packages
Write-ColorOutput $infoColor "Restoring NuGet packages..."
dotnet restore "CaretTracker.Service\CaretTracker.Service.csproj"
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput $errorColor "Failed to restore packages!"
    exit 1
}

# Build in Release mode
Write-ColorOutput $infoColor "Building in Release mode..."
dotnet build "CaretTracker.Service\CaretTracker.Service.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput $errorColor "Build failed!"
    exit 1
}

# Publish self-contained executable
Write-ColorOutput $infoColor "Publishing self-contained executable..."
dotnet publish "CaretTracker.Service\CaretTracker.Service.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput $errorColor "Publish failed!"
    exit 1
}

# Verify the executable exists
$exePath = "CaretTracker.Service\bin\Release\net6.0\win-x64\CaretTracker.exe"
if (Test-Path $exePath) {
    Write-ColorOutput $successColor "Build completed successfully!"
    Write-ColorOutput $successColor "Executable created at: $exePath"
} else {
    Write-ColorOutput $errorColor "Executable not found at expected location!"
    exit 1
} 