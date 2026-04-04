[CmdletBinding()]
param(
    [string]$GodotExe = $env:GODOT_EXE,
    [string]$ProjectDir,
    [string]$ExportPreset = "Android",
    [string]$PackageName = "com.zgx197.tilematcher",
    [string]$VersionName = "0.1.5",
    [int]$VersionCode = 6,
    [ValidateSet("portrait", "landscape")]
    [string]$ManifestOrientation = "portrait",
    [string]$KeystorePath = $env:ANDROID_DEBUG_KEYSTORE,
    [string]$KeystorePassword = "android",
    [string]$KeyAlias = "androiddebugkey",
    [string]$KeyPassword = "android",
    [string]$AndroidSdkRoot = $env:ANDROID_SDK_ROOT,
    [string]$JavaHome = $env:JAVA_HOME,
    [string]$OutputName,
    [switch]$SkipSigning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
# We handle native process exit codes explicitly because:
# 1. Godot headless export may leave a non-zero exit code even when the APK is produced.
# 2. We only want to fail the pipeline after checking whether the expected artifact exists.
$PSNativeCommandUseErrorActionPreference = $false

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-ScriptRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return $PSScriptRoot
    }

    if ($MyInvocation.MyCommand.Path) {
        return Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    throw "Unable to resolve script root."
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Set-RegexValue {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Replacement
    )

    # These config files are consumed by Godot directly. We therefore:
    # 1. read the whole file as raw text,
    # 2. replace only the exact key we own,
    # 3. write it back as UTF-8 without BOM to avoid preset parsing issues.
    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $options = [System.Text.RegularExpressions.RegexOptions]::Multiline
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($content, $Pattern, $options)) {
        throw "Failed to find pattern in file: $Path`nPattern: $Pattern"
    }

    $updated = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        $Pattern,
        $Replacement,
        $options)

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $updated, $utf8NoBom)
}

function Write-Utf8NoBomFile {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Get-ProjectDisplayName {
    param([string]$ProjectDir)

    $projectSettingsPath = Join-Path $ProjectDir "project.godot"
    Assert-PathExists -Path $projectSettingsPath -Label "project.godot"

    $projectSettings = Get-Content -LiteralPath $projectSettingsPath -Raw -Encoding UTF8
    $match = [regex]::Match(
        $projectSettings,
        '^\s*config/name="([^"]+)"\r?$',
        [System.Text.RegularExpressions.RegexOptions]::Multiline)

    if (-not $match.Success) {
        throw "Failed to resolve application display name from project.godot"
    }

    return $match.Groups[1].Value
}

function Ensure-AndroidExportResources {
    param(
        [string]$ProjectDir,
        [string]$ProjectDisplayName
    )

    Write-Step "Prepare Android export resources"

    $valuesDirectory = Join-Path $ProjectDir "android/build/res/values"
    $mipmapDirectory = Join-Path $ProjectDir "android/build/res/mipmap"
    $projectNamePath = Join-Path $valuesDirectory "godot_project_name_string.xml"
    $themesPath = Join-Path $valuesDirectory "themes.xml"
    $iconBackgroundPath = Join-Path $mipmapDirectory "icon_background.xml"

    # In clean CI checkouts these generated files do not exist yet, but the
    # Android source template still references them during Gradle packaging.
    # We materialize the minimum required files here so local builds and
    # hosted-runner builds follow the same deterministic path.
    $escapedDisplayName = [System.Security.SecurityElement]::Escape($ProjectDisplayName)
    $projectNameXml = @"
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="godot_project_name_string">$escapedDisplayName</string>
</resources>
"@

    $themesXml = @"
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <style name="GodotAppMainTheme" parent="@android:style/Theme.DeviceDefault.NoActionBar">
        <item name="android:windowSwipeToDismiss">false</item>
        <item name="android:windowIsTranslucent">false</item>
        <item name="android:windowBackground">#000000</item>
    </style>

    <style name="GodotAppSplashTheme" parent="Theme.SplashScreen">
        <item name="android:windowSplashScreenBackground">@mipmap/icon_background</item>
        <item name="windowSplashScreenAnimatedIcon">@mipmap/icon_foreground</item>
        <item name="postSplashScreenTheme">@style/GodotAppMainTheme</item>
        <item name="android:windowIsTranslucent">false</item>
    </style>
</resources>
"@

    $iconBackgroundXml = @"
<?xml version="1.0" encoding="utf-8"?>
<shape xmlns:android="http://schemas.android.com/apk/res/android" android:shape="rectangle">
    <solid android:color="#000000" />
</shape>
"@

    Write-Utf8NoBomFile -Path $projectNamePath -Content $projectNameXml
    Write-Utf8NoBomFile -Path $themesPath -Content $themesXml
    Write-Utf8NoBomFile -Path $iconBackgroundPath -Content $iconBackgroundXml
}

function Resolve-BuildTool {
    param([string]$AndroidSdkRoot)

    $buildToolsRoot = Join-Path $AndroidSdkRoot "build-tools"
    Assert-PathExists -Path $buildToolsRoot -Label "Android build-tools directory"

    $latest = Get-ChildItem -LiteralPath $buildToolsRoot -Directory |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "No Android build-tools versions were found."
    }

    return $latest.FullName
}

function Get-NativeExitCode {
    $lastExitCodeVariable = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
    if ($null -eq $lastExitCodeVariable) {
        return 0
    }

    return [int]$lastExitCodeVariable.Value
}

function Write-LogExcerpt {
    param(
        [string]$Path,
        [string]$Label,
        [int]$TailCount = 120
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    Write-Host ""
    Write-Host "[$Label] $Path" -ForegroundColor DarkGray
    Get-Content -LiteralPath $Path -Tail $TailCount
}

function Invoke-GodotExport {
    param(
        [string]$GodotExe,
        [string]$ProjectDir,
        [string]$ExportPreset,
        [string]$UnsignedApkPath,
        [string]$FallbackApkPath
    )

    $stdoutLogPath = Join-Path $env:TEMP "tilematcher-godot-export-stdout.log"
    $stderrLogPath = Join-Path $env:TEMP "tilematcher-godot-export-stderr.log"
    $exportStartedAt = Get-Date

    Remove-Item -LiteralPath $stdoutLogPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrLogPath -Force -ErrorAction SilentlyContinue

    $process = Start-Process `
        -FilePath $GodotExe `
        -ArgumentList @("--headless", "--path", $ProjectDir, "--export-debug", $ExportPreset, $UnsignedApkPath) `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardOutput $stdoutLogPath `
        -RedirectStandardError $stderrLogPath

    $artifactStableSince = $null
    $artifactFingerprint = $null
    $completedByFreshFallbackArtifact = $false

    while (-not $process.HasExited) {
        Start-Sleep -Seconds 2

        if ([string]::IsNullOrWhiteSpace($FallbackApkPath) -or (-not (Test-Path -LiteralPath $FallbackApkPath))) {
            continue
        }

        $fallbackItem = Get-Item -LiteralPath $FallbackApkPath
        if (($fallbackItem.LastWriteTime -lt $exportStartedAt) -or ($fallbackItem.Length -le 0)) {
            continue
        }

        $currentFingerprint = "$($fallbackItem.Length)|$($fallbackItem.LastWriteTimeUtc.Ticks)"
        if ($currentFingerprint -ne $artifactFingerprint) {
            $artifactFingerprint = $currentFingerprint
            $artifactStableSince = Get-Date
            continue
        }

        if (($null -ne $artifactStableSince) -and (((Get-Date) - $artifactStableSince).TotalSeconds -ge 8)) {
            try {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
            catch {
            }

            $completedByFreshFallbackArtifact = $true
            break
        }
    }

    if (-not $process.HasExited) {
        $process.WaitForExit()
    }

    if ($completedByFreshFallbackArtifact) {
        Write-Warning "Godot export did not exit cleanly after producing a fresh Gradle APK. The process was stopped and the build will continue from the generated artifact."
        return [pscustomobject]@{
            ExitCode = 0
            StdoutLogPath = $stdoutLogPath
            StderrLogPath = $stderrLogPath
            CompletedByFreshFallbackArtifact = $true
        }
    }

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdoutLogPath = $stdoutLogPath
        StderrLogPath = $stderrLogPath
        CompletedByFreshFallbackArtifact = $false
    }
}

function Update-BuildMetadata {
    param(
        [string]$ProjectDir,
        [string]$PackageName,
        [string]$VersionName,
        [int]$VersionCode,
        [string]$ManifestOrientation
    )

    # Keep every build metadata source in sync.
    # If these files diverge, the package name/version shown by:
    # - Godot export
    # - Android manifest
    # - in-game DEBUG panel
    # may stop matching and make debugging much harder.
    Write-Step "Sync build metadata"

    $exportPresetsPath = Join-Path $ProjectDir "export_presets.cfg"
    $mainManifestPath = Join-Path $ProjectDir "android/build/src/main/AndroidManifest.xml"
    $debugManifestPath = Join-Path $ProjectDir "android/build/src/debug/AndroidManifest.xml"
    $projectSettingsPath = Join-Path $ProjectDir "project.godot"

    Assert-PathExists -Path $exportPresetsPath -Label "export_presets.cfg"
    Assert-PathExists -Path $mainManifestPath -Label "main AndroidManifest.xml"
    Assert-PathExists -Path $debugManifestPath -Label "debug AndroidManifest.xml"
    Assert-PathExists -Path $projectSettingsPath -Label "project.godot"

    Set-RegexValue -Path $exportPresetsPath -Pattern '^\s*version/code=\d+\r?$' -Replacement "version/code=$VersionCode"
    Set-RegexValue -Path $exportPresetsPath -Pattern '^\s*version/name="[^"]*"\r?$' -Replacement "version/name=`"$VersionName`""
    Set-RegexValue -Path $exportPresetsPath -Pattern '^\s*package/unique_name="[^"]*"\r?$' -Replacement "package/unique_name=`"$PackageName`""

    Set-RegexValue -Path $mainManifestPath -Pattern 'android:versionCode="\d+"' -Replacement "android:versionCode=`"$VersionCode`""
    Set-RegexValue -Path $mainManifestPath -Pattern 'android:versionName="[^"]*"' -Replacement "android:versionName=`"$VersionName`""
    Set-RegexValue -Path $mainManifestPath -Pattern 'android:screenOrientation="[^"]*"' -Replacement "android:screenOrientation=`"$ManifestOrientation`""
    Set-RegexValue -Path $debugManifestPath -Pattern 'android:screenOrientation="[^"]*"' -Replacement "android:screenOrientation=`"$ManifestOrientation`""

    Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*package_name="[^"]*"\r?$' -Replacement "package_name=`"$PackageName`""
    Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*version_name="[^"]*"\r?$' -Replacement "version_name=`"$VersionName`""
    Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*version_code=\d+\r?$' -Replacement "version_code=$VersionCode"
    Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*manifest_orientation="[^"]*"\r?$' -Replacement "manifest_orientation=`"$ManifestOrientation`""
}

function Resolve-GradleUserHome {
    param([string]$ProjectDir)

    $projectGradleHome = Join-Path $ProjectDir "android/build/.gradle"
    New-Item -ItemType Directory -Path $projectGradleHome -Force | Out-Null

    Get-ChildItem -LiteralPath $projectGradleHome -Recurse -Include "*.lck", "*.part" -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
    }

    $volatileCacheDirs = @(
        (Join-Path $projectGradleHome "caches\8.11.1\groovy-dsl"),
        (Join-Path $projectGradleHome "caches\8.11.1\transforms"),
        (Join-Path $projectGradleHome "daemon\8.11.1")
    )

    foreach ($volatileCacheDir in $volatileCacheDirs) {
        if (Test-Path -LiteralPath $volatileCacheDir) {
            Remove-Item -LiteralPath $volatileCacheDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    return $projectGradleHome
}

function Clear-GodotMonoBuildIssueFiles {
    $godotMonoBuildLogsRoot = Join-Path $env:APPDATA "Godot\mono\build_logs"
    if (-not (Test-Path -LiteralPath $godotMonoBuildLogsRoot)) {
        return
    }

    Get-ChildItem -LiteralPath $godotMonoBuildLogsRoot -Recurse -Filter "msbuild_issues.csv" -ErrorAction SilentlyContinue | ForEach-Object {
        $issueFile = $_
        try {
            [System.IO.File]::SetAttributes($issueFile.FullName, [System.IO.FileAttributes]::Normal)
            Remove-Item -LiteralPath $issueFile.FullName -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to clear stale Godot Mono build issue file: $($issueFile.FullName)"
        }
    }
}

function Remove-GodotDiagnosticArtifacts {
    param([string]$ProjectDir)

    $logsRoot = Join-Path $ProjectDir "logs"
    if (-not (Test-Path -LiteralPath $logsRoot)) {
        return
    }

    Get-ChildItem -LiteralPath $logsRoot -Directory -Filter "diagnostics-*" -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-GodotExportFallbackApkPath {
    param([string]$ProjectDir)

    $fallbackApkPath = Join-Path $ProjectDir "android/build/build/outputs/apk/mono/debug/android_monoDebug.apk"
    if (Test-Path -LiteralPath $fallbackApkPath) {
        return $fallbackApkPath
    }

    return $null
}

function Remove-StaleGodotExportArtifacts {
    param([string]$ProjectDir)

    $stalePaths = @(
        (Join-Path $ProjectDir "android/build/build/outputs/apk/mono/debug/android_monoDebug.apk"),
        (Join-Path $ProjectDir "android/build/build/outputs/apk/mono/debug/output-metadata.json")
    )

    foreach ($stalePath in $stalePaths) {
        if (Test-Path -LiteralPath $stalePath) {
            Remove-Item -LiteralPath $stalePath -Force -ErrorAction SilentlyContinue
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ProjectDir)) {
    $scriptRoot = Get-ScriptRoot
    $repoRoot = Split-Path -Parent $scriptRoot
    $ProjectDir = Join-Path $repoRoot "godot"
}

if ([string]::IsNullOrWhiteSpace($GodotExe)) {
    $GodotExe = "D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe"
}

if ([string]::IsNullOrWhiteSpace($AndroidSdkRoot)) {
    throw "Missing AndroidSdkRoot. Pass -AndroidSdkRoot or set ANDROID_SDK_ROOT."
}

if ([string]::IsNullOrWhiteSpace($JavaHome)) {
    throw "Missing JavaHome. Pass -JavaHome or set JAVA_HOME."
}

if ([string]::IsNullOrWhiteSpace($OutputName)) {
    $OutputName = "TileMatcher-$VersionName-android-debug.apk"
}

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $KeystorePath = Join-Path $env:APPDATA "Godot\keystores\debug.keystore"
}

Assert-PathExists -Path $GodotExe -Label "Godot executable"
Assert-PathExists -Path $ProjectDir -Label "Godot project directory"
Assert-PathExists -Path $AndroidSdkRoot -Label "Android SDK"
Assert-PathExists -Path $JavaHome -Label "JDK"

$buildToolsDir = Resolve-BuildTool -AndroidSdkRoot $AndroidSdkRoot
$apksignerPath = Join-Path $buildToolsDir "apksigner.bat"
$aaptPath = Join-Path $buildToolsDir "aapt.exe"

Assert-PathExists -Path $apksignerPath -Label "apksigner"
Assert-PathExists -Path $aaptPath -Label "aapt"

$env:JAVA_HOME = $JavaHome
$env:ANDROID_SDK_ROOT = $AndroidSdkRoot
$env:ANDROID_HOME = $AndroidSdkRoot
$gradleUserHome = Resolve-GradleUserHome -ProjectDir $ProjectDir
New-Item -ItemType Directory -Path $gradleUserHome -Force | Out-Null
$env:GRADLE_USER_HOME = $gradleUserHome

$outputDir = Join-Path $ProjectDir "build/android"
$unsignedApkPath = Join-Path $outputDir "TileMatcher-unsigned.apk"
$signedApkPath = Join-Path $outputDir $OutputName

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
if (Test-Path -LiteralPath $unsignedApkPath) {
    Remove-Item -LiteralPath $unsignedApkPath -Force
}

if (Test-Path -LiteralPath $signedApkPath) {
    Remove-Item -LiteralPath $signedApkPath -Force
}

Clear-GodotMonoBuildIssueFiles
Remove-GodotDiagnosticArtifacts -ProjectDir $ProjectDir
Remove-StaleGodotExportArtifacts -ProjectDir $ProjectDir

Update-BuildMetadata `
    -ProjectDir $ProjectDir `
    -PackageName $PackageName `
    -VersionName $VersionName `
    -VersionCode $VersionCode `
    -ManifestOrientation $ManifestOrientation

$projectDisplayName = Get-ProjectDisplayName -ProjectDir $ProjectDir
Ensure-AndroidExportResources `
    -ProjectDir $ProjectDir `
    -ProjectDisplayName $projectDisplayName

Write-Step "Run Godot export"
$exportStartedAt = Get-Date
$fallbackApkPath = Join-Path $ProjectDir "android/build/build/outputs/apk/mono/debug/android_monoDebug.apk"
$godotExportResult = Invoke-GodotExport `
    -GodotExe $GodotExe `
    -ProjectDir $ProjectDir `
    -ExportPreset $ExportPreset `
    -UnsignedApkPath $unsignedApkPath `
    -FallbackApkPath $fallbackApkPath
$godotExitCode = [int]$godotExportResult.ExitCode

if (-not (Test-Path -LiteralPath $unsignedApkPath)) {
    $existingFallbackApkPath = Resolve-GodotExportFallbackApkPath -ProjectDir $ProjectDir
    if (-not [string]::IsNullOrWhiteSpace($existingFallbackApkPath)) {
        $fallbackItem = Get-Item -LiteralPath $existingFallbackApkPath
        if ($fallbackItem.LastWriteTime -lt $exportStartedAt) {
            throw "Godot export did not produce a fresh Gradle APK. Refusing to reuse stale fallback artifact: $existingFallbackApkPath"
        }

        Write-Warning "Godot did not copy the unsigned APK to the final export path. Reusing the fresh Gradle output instead: $existingFallbackApkPath"
        Copy-Item -LiteralPath $existingFallbackApkPath -Destination $unsignedApkPath -Force
    }
}

if (-not (Test-Path -LiteralPath $unsignedApkPath)) {
    Write-LogExcerpt -Path $godotExportResult.StdoutLogPath -Label "Godot export stdout"
    Write-LogExcerpt -Path $godotExportResult.StderrLogPath -Label "Godot export stderr"
}

Assert-PathExists -Path $unsignedApkPath -Label "unsigned APK"
if ($godotExitCode -ne 0) {
    Write-LogExcerpt -Path $godotExportResult.StdoutLogPath -Label "Godot export stdout"
    Write-LogExcerpt -Path $godotExportResult.StderrLogPath -Label "Godot export stderr"
    # Do not fail immediately. The exported artifact is the real success criterion here.
    Write-Warning "Godot export returned exit code $godotExitCode, but the unsigned APK was generated successfully. Continue with signing."
}

if ($godotExportResult.CompletedByFreshFallbackArtifact) {
    Write-Host "Godot export logs were captured and the build continued from the fresh Gradle APK." -ForegroundColor DarkGray
}

if ($SkipSigning) {
    Write-Step "Skip signing and show badging"
    & $aaptPath dump badging $unsignedApkPath
    Write-Host ""
    Write-Host "Unsigned export completed: $unsignedApkPath" -ForegroundColor Green
    exit 0
}

Assert-PathExists -Path $KeystorePath -Label "keystore"

Write-Step "Copy and sign APK"
# Sign a copied file instead of the raw Godot export output so that:
# 1. the unsigned artifact remains available for comparison/debugging,
# 2. the final distributable file has a stable name for local use and CI upload.
Copy-Item -LiteralPath $unsignedApkPath -Destination $signedApkPath -Force
& $apksignerPath sign `
    --ks $KeystorePath `
    --ks-pass "pass:$KeystorePassword" `
    --ks-key-alias $KeyAlias `
    --key-pass "pass:$KeyPassword" `
    $signedApkPath
$signExitCode = Get-NativeExitCode
if ($signExitCode -ne 0) {
    throw "apksigner sign failed with exit code $signExitCode"
}

Write-Step "Verify signature"
& $apksignerPath verify --verbose $signedApkPath
$verifyExitCode = Get-NativeExitCode
if ($verifyExitCode -ne 0) {
    throw "apksigner verify failed with exit code $verifyExitCode"
}

Write-Step "Dump badging"
# This final check is intentionally part of the script rather than an optional manual step.
# A version mismatch discovered here usually means metadata sync regressed earlier in the pipeline.
& $aaptPath dump badging $signedApkPath
$badgingExitCode = Get-NativeExitCode
if ($badgingExitCode -ne 0) {
    throw "aapt dump badging failed with exit code $badgingExitCode"
}

Write-Host ""
Write-Host "Android build completed: $signedApkPath" -ForegroundColor Green
exit 0
