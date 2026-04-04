[CmdletBinding()]
param(
    [string]$GodotExe = $env:GODOT_EXE,
    [string]$ProjectDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "godot"),
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

    Set-RegexValue -Path $exportPresetsPath -Pattern '^version/code=\d+$' -Replacement "version/code=$VersionCode"
    Set-RegexValue -Path $exportPresetsPath -Pattern '^version/name="[^"]*"$' -Replacement "version/name=`"$VersionName`""
    Set-RegexValue -Path $exportPresetsPath -Pattern '^package/unique_name="[^"]*"$' -Replacement "package/unique_name=`"$PackageName`""

    Set-RegexValue -Path $mainManifestPath -Pattern 'android:versionCode="\d+"' -Replacement "android:versionCode=`"$VersionCode`""
    Set-RegexValue -Path $mainManifestPath -Pattern 'android:versionName="[^"]*"' -Replacement "android:versionName=`"$VersionName`""
    Set-RegexValue -Path $mainManifestPath -Pattern 'android:screenOrientation="[^"]*"' -Replacement "android:screenOrientation=`"$ManifestOrientation`""
    Set-RegexValue -Path $debugManifestPath -Pattern 'android:screenOrientation="[^"]*"' -Replacement "android:screenOrientation=`"$ManifestOrientation`""

    Set-RegexValue -Path $projectSettingsPath -Pattern '^package_name="[^"]*"$' -Replacement "package_name=`"$PackageName`""
    Set-RegexValue -Path $projectSettingsPath -Pattern '^version_name="[^"]*"$' -Replacement "version_name=`"$VersionName`""
    Set-RegexValue -Path $projectSettingsPath -Pattern '^version_code=\d+$' -Replacement "version_code=$VersionCode"
    Set-RegexValue -Path $projectSettingsPath -Pattern '^manifest_orientation="[^"]*"$' -Replacement "manifest_orientation=`"$ManifestOrientation`""
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

$outputDir = Join-Path $ProjectDir "build/android"
$unsignedApkPath = Join-Path $outputDir "TileMatcher-unsigned.apk"
$signedApkPath = Join-Path $outputDir $OutputName

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

Update-BuildMetadata `
    -ProjectDir $ProjectDir `
    -PackageName $PackageName `
    -VersionName $VersionName `
    -VersionCode $VersionCode `
    -ManifestOrientation $ManifestOrientation

Write-Step "Run Godot export"
& $GodotExe --headless --path $ProjectDir --export-debug $ExportPreset $unsignedApkPath
$godotExitCode = $LASTEXITCODE
Assert-PathExists -Path $unsignedApkPath -Label "unsigned APK"
if ($godotExitCode -ne 0) {
    # Do not fail immediately. The exported artifact is the real success criterion here.
    Write-Warning "Godot export returned exit code $godotExitCode, but the unsigned APK was generated successfully. Continue with signing."
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
if ($LASTEXITCODE -ne 0) {
    throw "apksigner sign failed with exit code $LASTEXITCODE"
}

Write-Step "Verify signature"
& $apksignerPath verify --verbose $signedApkPath
if ($LASTEXITCODE -ne 0) {
    throw "apksigner verify failed with exit code $LASTEXITCODE"
}

Write-Step "Dump badging"
# This final check is intentionally part of the script rather than an optional manual step.
# A version mismatch discovered here usually means metadata sync regressed earlier in the pipeline.
& $aaptPath dump badging $signedApkPath
if ($LASTEXITCODE -ne 0) {
    throw "aapt dump badging failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Android build completed: $signedApkPath" -ForegroundColor Green
exit 0
