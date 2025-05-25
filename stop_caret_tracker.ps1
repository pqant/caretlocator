# Check if the service is running
$process = Get-Process -Name "CaretTracker" -ErrorAction SilentlyContinue

if (-not $process) {
    Write-Host "Caret Tracker Service is not running."
    exit
}

# Stop the service
try {
    Stop-Process -Name "CaretTracker" -Force
    Write-Host "Caret Tracker Service stopped successfully."
} catch {
    Write-Host "Error stopping Caret Tracker Service: $_"
    exit 1
} 