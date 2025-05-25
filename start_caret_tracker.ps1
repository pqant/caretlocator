# Check if the service is already running
$process = Get-Process -Name "CaretTracker" -ErrorAction SilentlyContinue

if ($process) {
    Write-Host "Caret Tracker Service is already running with PID: $($process.Id)"
    exit
}

# Get the path of the executable
$debugPath = Join-Path $PSScriptRoot "CaretTracker.Service\bin\Debug\net6.0\win-x64\CaretTracker.exe"
$releasePath = Join-Path $PSScriptRoot "CaretTracker.Service\bin\Release\net6.0\win-x64\CaretTracker.exe"

# Check which executable exists
$exePath = if (Test-Path $debugPath) { $debugPath } elseif (Test-Path $releasePath) { $releasePath } else { $null }

if (-not $exePath) {
    Write-Host "Error: CaretTracker.exe not found in Debug or Release folders."
    Write-Host "Please build the project first with: dotnet publish -c Release -r win-x64 --self-contained true"
    exit 1
}

# Check debug mode from config
$configPath = Join-Path $PSScriptRoot "caret_config.json"
$debugMode = $false
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    $debugMode = $config.debug_mode
}

# Start the service
try {
    if ($debugMode) {
        Write-Host "Starting Caret Tracker Service in DEBUG mode..."
        Start-Process -FilePath $exePath -WindowStyle Normal
    } else {
        Write-Host "Starting Caret Tracker Service in background..."
        Start-Process -FilePath $exePath -WindowStyle Hidden
    }
    Write-Host "Caret Tracker Service started successfully."
} catch {
    Write-Host "Error starting Caret Tracker Service: $_"
    exit 1
} 