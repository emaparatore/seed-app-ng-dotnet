param(
    [ValidateSet("up", "down", "logs", "restart")]
    [string]$Action = "up"
)

$ComposeFile = "docker-compose.dev.yml"
$DockerDir = Join-Path $PSScriptRoot "..\docker"

if (-not (Test-Path (Join-Path $DockerDir $ComposeFile))) {
    Write-Error "Compose file not found: $DockerDir\$ComposeFile"
    exit 1
}

Push-Location $DockerDir
try {
    switch ($Action) {
        "up" {
            docker compose -f $ComposeFile up
        }
        "down" {
            docker compose -f $ComposeFile down
        }
        "logs" {
            docker compose -f $ComposeFile logs -f
        }
        "restart" {
            docker compose -f $ComposeFile down
            docker compose -f $ComposeFile up
        }
    }
}
finally {
    Pop-Location
}
