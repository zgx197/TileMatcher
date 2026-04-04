param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "tools\TileMatcher.Offline.Tests\TileMatcher.Offline.Tests.csproj"
$summaryPath = Join-Path $repoRoot "artifacts\test-results\offline-tests\summary.json"

Write-Host "[OfflineTests] Running test project..."
& dotnet run --project $projectPath -c $Configuration
$exitCode = $LASTEXITCODE

if (-not (Test-Path $summaryPath)) {
    Write-Host "[OfflineTests] Summary file not found: $summaryPath"
    exit 1
}

$summary = Get-Content $summaryPath -Raw | ConvertFrom-Json
$failed = @($summary.Results | Where-Object { -not $_.Passed })

Write-Host ""
Write-Host "[OfflineTests] Summary"
Write-Host "  Total  : $($summary.TotalCount)"
Write-Host "  Passed : $($summary.PassedCount)"
Write-Host "  Failed : $($summary.FailedCount)"

if ($failed.Count -eq 0) {
    Write-Host "[OfflineTests] Analysis: minimum offline test baseline is stable."
    exit $exitCode
}

Write-Host "[OfflineTests] Analysis: failures detected, offline rules or export format regressed."
foreach ($case in $failed) {
    Write-Host "  - $($case.Name): $($case.Message)"
}

exit $exitCode
