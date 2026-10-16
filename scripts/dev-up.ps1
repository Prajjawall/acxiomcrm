$ErrorActionPreference = "Stop"

Write-Host "Starting local SQL Server container via docker compose..."
if (-not (Test-Path .env) -and (Test-Path .env.example)) {
    Copy-Item .env.example .env
}

docker compose up -d db

Write-Host "Waiting for database to become healthy..."
while ($true) {
    $status = docker compose ps db --format "{{.Health}}"
    if ($status -eq "healthy") {
        break
    }
    Start-Sleep -Seconds 2
}

Write-Host "SQL Server is healthy on port 1433."
