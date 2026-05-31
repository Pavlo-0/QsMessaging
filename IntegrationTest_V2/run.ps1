[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$logs = Join-Path $root "logs"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$agentProcesses = @()
$pathValue = [Environment]::GetEnvironmentVariable("Path", "Process")

New-Item -ItemType Directory -Path $logs -Force | Out-Null
[Environment]::SetEnvironmentVariable("PATH", $null, "Process")
[Environment]::SetEnvironmentVariable("Path", $pathValue, "Process")
Push-Location $root

try {
    if (-not $NoBuild) {
        Write-Host "Building IntegrationTest_V2..." -ForegroundColor Cyan
        & dotnet build ".\IntegrationTest_V2.sln" "-m:1"
        if ($LASTEXITCODE -ne 0) {
            throw "IntegrationTest_V2 build failed."
        }
    }

    $agents = @(
        "Sender01\IntegrationTestV2.Sender01.csproj",
        "Sender02\IntegrationTestV2.Sender02.csproj",
        "Receiver01\IntegrationTestV2.Receiver01.csproj",
        "Receiver02\IntegrationTestV2.Receiver02.csproj"
    )

    foreach ($agent in $agents) {
        $agentName = [System.IO.Path]::GetFileNameWithoutExtension($agent)
        $outputLog = Join-Path $logs "$stamp-$agentName.out.log"
        $errorLog = Join-Path $logs "$stamp-$agentName.err.log"
        $agentProcesses += Start-Process `
            -FilePath "dotnet" `
            -ArgumentList @("run", "--project", $agent, "--no-build") `
            -RedirectStandardOutput $outputLog `
            -RedirectStandardError $errorLog `
            -WindowStyle Hidden `
            -PassThru
    }

    Write-Host "Starting runner. Agent logs: $logs" -ForegroundColor Cyan
    & dotnet run --project ".\Runner\IntegrationTestV2.Runner.csproj" --no-build -- "Runner:ExitAfterRun=true" "Runner:LogDirectory=..\logs"
    exit $LASTEXITCODE
}
finally {
    foreach ($agentProcess in $agentProcesses) {
        if (-not $agentProcess.HasExited) {
            Stop-Process -Id $agentProcess.Id -Force
        }
    }

    Pop-Location
}
