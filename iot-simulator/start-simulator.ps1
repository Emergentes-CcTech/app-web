# Helper script to launch IoT simulator locally in PowerShell
Write-Host "=== IoBuild IoT Simulator ===" -ForegroundColor Cyan

$env:MQTT_HOST = if ($env:MQTT_HOST) { $env:MQTT_HOST } else { "localhost" }
$env:MQTT_PORT = if ($env:MQTT_PORT) { $env:MQTT_PORT } else { "1883" }

Write-Host "Connecting to MQTT broker at ${env:MQTT_HOST}:${env:MQTT_PORT}..." -ForegroundColor Yellow

# Check dependencies
try {
    python -c "import paho.mqtt" 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Installing dependencies from requirements.txt..." -ForegroundColor Gray
        pip install -r "$PSScriptRoot\requirements.txt"
    }
} catch {
    Write-Host "Please ensure python and pip are installed." -ForegroundColor Red
}

python "$PSScriptRoot\simulator.py"
