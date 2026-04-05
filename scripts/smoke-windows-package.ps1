[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactZipPath,

    [Parameter(Mandatory = $true)]
    [string]$ExtractDir,

    [string]$LogOutputDir = "",

    [int]$StartupTimeoutSeconds = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "build-common.ps1")

function Copy-FileIfExists {
    param(
        [string]$SourcePath,
        [string]$DestinationDirectory
    )

    if ([string]::IsNullOrWhiteSpace($SourcePath) -or -not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination (Join-Path $DestinationDirectory (Split-Path -Leaf $SourcePath)) -Force
}

$resolvedZipPath = (Resolve-Path -LiteralPath $ArtifactZipPath).Path
$resolvedExtractDir = if (Test-Path -LiteralPath $ExtractDir) { (Resolve-Path -LiteralPath $ExtractDir).Path } else { $ExtractDir }

Reset-Directory -Path $resolvedExtractDir
Expand-Archive -LiteralPath $resolvedZipPath -DestinationPath $resolvedExtractDir -Force

$executable = Get-ChildItem -LiteralPath $resolvedExtractDir -File -Filter "*.exe" |
    Where-Object { $_.Name -notlike "createdump*.exe" } |
    Sort-Object Name |
    Select-Object -First 1

if ($null -eq $executable) {
    throw "Windows smoke test did not find a top-level executable after extraction: $resolvedExtractDir"
}

$stdoutPath = Join-Path $resolvedExtractDir "smoke-stdout.log"
$stderrPath = Join-Path $resolvedExtractDir "smoke-stderr.log"
$logDirectory = Join-Path $resolvedExtractDir "logs"
$latestLogPath = Join-Path $logDirectory "latest.log"
$latestErrorLogPath = Join-Path $logDirectory "latest-error.log"

Remove-PathIfExists -Path $logDirectory
Remove-PathIfExists -Path $stdoutPath
Remove-PathIfExists -Path $stderrPath

try {
    $process = Start-Process `
        -FilePath $executable.FullName `
        -WorkingDirectory $resolvedExtractDir `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru

    Start-Sleep -Seconds $StartupTimeoutSeconds

    $hasExited = $process.HasExited
    $exitCode = if ($hasExited) { $process.ExitCode } else { $null }
    if (-not $hasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }

    $stdoutText = if (Test-Path -LiteralPath $stdoutPath) {
        Get-Content -LiteralPath $stdoutPath -Raw -Encoding UTF8
    } else {
        ""
    }

    $stderrText = if (Test-Path -LiteralPath $stderrPath) {
        Get-Content -LiteralPath $stderrPath -Raw -Encoding UTF8
    } else {
        ""
    }

    $combinedOutput = (($stdoutText, $stderrText) -join [Environment]::NewLine).Trim()
    if ($combinedOutput -match 'No loader found for resource:' -or
        $combinedOutput -match 'Failed loading scene:' -or
        $combinedOutput -match "Can't load dependency:") {
        $excerpt = Get-TextExcerpt -Text $combinedOutput
        throw "Windows package smoke test detected startup resource loading errors.`n$excerpt"
    }

    if ($hasExited -and $exitCode -ne 0) {
        $excerpt = Get-TextExcerpt -Text $combinedOutput
        throw "Windows package smoke test exited early with code $exitCode.`n$excerpt"
    }

    Assert-PathExists -Path $logDirectory -Label "Windows smoke test log directory"
    Assert-PathExists -Path $latestLogPath -Label "Windows smoke test latest log"

    $latestLogText = Get-Content -LiteralPath $latestLogPath -Raw -Encoding UTF8
    if (($latestLogText.IndexOf("[AppRoot]", [System.StringComparison]::OrdinalIgnoreCase) -lt 0) -and
        ($latestLogText.IndexOf("启动应用", [System.StringComparison]::OrdinalIgnoreCase) -lt 0)) {
        $excerpt = Get-TextExcerpt -Text $latestLogText
        throw "Windows package smoke test did not observe AppRoot startup logs in latest.log.`n$excerpt"
    }

    if (-not [string]::IsNullOrWhiteSpace($LogOutputDir)) {
        New-Item -ItemType Directory -Path $LogOutputDir -Force | Out-Null
        Copy-FileIfExists -SourcePath $stdoutPath -DestinationDirectory $LogOutputDir
        Copy-FileIfExists -SourcePath $stderrPath -DestinationDirectory $LogOutputDir
        Copy-FileIfExists -SourcePath $latestLogPath -DestinationDirectory $LogOutputDir
        Copy-FileIfExists -SourcePath $latestErrorLogPath -DestinationDirectory $LogOutputDir
    }

    Write-Host "Windows smoke test passed: $($executable.FullName)" -ForegroundColor Green
    Write-Host "Latest log: $latestLogPath" -ForegroundColor DarkGray
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($LogOutputDir)) {
        New-Item -ItemType Directory -Path $LogOutputDir -Force | Out-Null
        Copy-FileIfExists -SourcePath $stdoutPath -DestinationDirectory $LogOutputDir
        Copy-FileIfExists -SourcePath $stderrPath -DestinationDirectory $LogOutputDir
        Copy-FileIfExists -SourcePath $latestLogPath -DestinationDirectory $LogOutputDir
        Copy-FileIfExists -SourcePath $latestErrorLogPath -DestinationDirectory $LogOutputDir
    }
}
