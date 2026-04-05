[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApkPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageName,

    [string]$LogOutputDir = "",

    [int]$LaunchTimeoutSeconds = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-AdbCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & adb @Arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "adb $($Arguments -join ' ') failed with exit code $LASTEXITCODE.`n$output"
    }

    return $output.Trim()
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Get-ProcessIdForPackage {
    param([string]$PackageName)

    $output = & adb shell pidof $PackageName 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        return ""
    }

    return $output.Trim()
}

if (-not (Get-Command adb -ErrorAction SilentlyContinue)) {
    throw "adb command was not found in PATH."
}

$resolvedApkPath = (Resolve-Path -LiteralPath $ApkPath).Path
$resolvedLogOutputDir = if ([string]::IsNullOrWhiteSpace($LogOutputDir)) {
    Join-Path (Split-Path -Parent $resolvedApkPath) "android-smoke-artifacts"
} else {
    $LogOutputDir
}

$logcatPath = Join-Path $resolvedLogOutputDir "logcat.txt"
$windowDumpPath = Join-Path $resolvedLogOutputDir "window.txt"
$activityDumpPath = Join-Path $resolvedLogOutputDir "activity.txt"
$packageDumpPath = Join-Path $resolvedLogOutputDir "package.txt"

New-Item -ItemType Directory -Path $resolvedLogOutputDir -Force | Out-Null

try {
    Invoke-AdbCommand -Arguments @("wait-for-device") | Out-Null
    Invoke-AdbCommand -Arguments @("devices") | Out-Null
    Invoke-AdbCommand -Arguments @("logcat", "-c") | Out-Null
    Invoke-AdbCommand -Arguments @("install", "-r", $resolvedApkPath) | Out-Null

    $launchOutput = Invoke-AdbCommand -Arguments @("shell", "monkey", "-p", $PackageName, "-c", "android.intent.category.LAUNCHER", "1")
    Write-Host $launchOutput

    $processId = ""
    $deadline = (Get-Date).AddSeconds($LaunchTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $processId = Get-ProcessIdForPackage -PackageName $PackageName
        if (-not [string]::IsNullOrWhiteSpace($processId)) {
            break
        }
    }

    $windowDump = Invoke-AdbCommand -Arguments @("shell", "dumpsys", "window", "windows")
    $activityDump = Invoke-AdbCommand -Arguments @("shell", "dumpsys", "activity", "activities")
    $packageDump = Invoke-AdbCommand -Arguments @("shell", "dumpsys", "package", $PackageName)
    $logcatDump = Invoke-AdbCommand -Arguments @("logcat", "-d", "-v", "time")

    Write-Utf8File -Path $windowDumpPath -Content $windowDump
    Write-Utf8File -Path $activityDumpPath -Content $activityDump
    Write-Utf8File -Path $packageDumpPath -Content $packageDump
    Write-Utf8File -Path $logcatPath -Content $logcatDump

    if ([string]::IsNullOrWhiteSpace($processId)) {
        throw "Android smoke test did not observe a running process for package $PackageName."
    }

    if (($activityDump.IndexOf($PackageName, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) -and
        ($windowDump.IndexOf($PackageName, [System.StringComparison]::OrdinalIgnoreCase) -lt 0)) {
        throw "Android smoke test did not observe the package in activity/window dumps: $PackageName"
    }

    Write-Host "Android smoke test passed: package=$PackageName pid=$processId" -ForegroundColor Green
    Write-Host "Logcat: $logcatPath" -ForegroundColor DarkGray
}
finally {
    try {
        $finalLogcatDump = Invoke-AdbCommand -Arguments @("logcat", "-d", "-v", "time")
        Write-Utf8File -Path $logcatPath -Content $finalLogcatDump
    }
    catch {
    }
}
