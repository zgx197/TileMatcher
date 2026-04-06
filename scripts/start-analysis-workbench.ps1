param(
  [switch]$NoOpen,
  [int]$Port = 3100
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$browserUrl = "http://127.0.0.1:$Port/"
$healthUrl = "http://127.0.0.1:$Port/api/health"

function Test-WorkbenchHealth {
  param(
    [string]$Url
  )

  try {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2
    return $response.StatusCode -eq 200
  } catch {
    return $false
  }
}

function Open-WorkbenchBrowser {
  param(
    [string]$Url
  )

  if ($NoOpen) {
    return
  }

  try {
    Start-Process $Url | Out-Null
  } catch {
    Write-Host "[analysis:start] Browser auto-open skipped: $($_.Exception.Message)"
  }
}

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
  Write-Host "[analysis:start] Node.js was not found in PATH."
  exit 1
}

if (Test-WorkbenchHealth -Url $healthUrl) {
  Write-Host "[analysis:start] Reusing running workbench: $browserUrl"
  Open-WorkbenchBrowser -Url $browserUrl
  exit 0
}

Write-Host "[analysis:start] Starting TileMatcher workbench..."

$serverCommand = "Set-Location -LiteralPath '$repoRoot'; `$env:PORT='$Port'; node app/server/server.js"
$process = Start-Process `
  -FilePath powershell `
  -ArgumentList "-NoExit", "-NoProfile", "-Command", $serverCommand `
  -WindowStyle Minimized `
  -PassThru

for ($attempt = 0; $attempt -lt 60; $attempt += 1) {
  Start-Sleep -Seconds 1

  if ($process.HasExited) {
    Write-Host "[analysis:start] Server process exited early."
    exit 1
  }

  if (Test-WorkbenchHealth -Url $healthUrl) {
    Write-Host "[analysis:start] Workbench ready: $browserUrl"
    Open-WorkbenchBrowser -Url $browserUrl
    exit 0
  }
}

Write-Host "[analysis:start] Startup timed out. Try node app/server/server.js manually."
exit 1
