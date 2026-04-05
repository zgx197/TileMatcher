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

# Android 导出与签名脚本。
# 负责同步版本元数据、准备 Godot Android 模板资源、执行导出并完成签名校验。
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

# 解析脚本目录，兼容直接执行和被其他脚本调用两种模式。
function Get-ScriptRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return $PSScriptRoot
    }

    if ($MyInvocation.MyCommand.Path) {
        return Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    throw "Unable to resolve script root."
}

# 断言关键路径存在，否则尽早失败。
function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Resolve-GodotNuGetSource {
    param([string]$GodotExe)

    if (-not [string]::IsNullOrWhiteSpace($env:GODOT_NUGET_SOURCE) -and (Test-Path -LiteralPath $env:GODOT_NUGET_SOURCE)) {
        return (Resolve-Path -LiteralPath $env:GODOT_NUGET_SOURCE).Path
    }

    Assert-PathExists -Path $GodotExe -Label "Godot executable"
    $godotRoot = Split-Path -Parent $GodotExe
    $candidate = Join-Path $godotRoot "GodotSharp\Tools\nupkgs"
    Assert-PathExists -Path $candidate -Label "Godot NuGet source"
    return (Resolve-Path -LiteralPath $candidate).Path
}

function Set-GodotNuGetSourceEnvironment {
    param([string]$GodotExe)

    $resolvedSource = Resolve-GodotNuGetSource -GodotExe $GodotExe
    $env:GODOT_NUGET_SOURCE = $resolvedSource
    return $resolvedSource
}

# 用正则同步替换配置文件中的单个键值。
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

# 以 UTF-8 无 BOM 写文件，避免 Godot/Gradle 解析到 BOM。
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

# 从 project.godot 中读取应用显示名称。
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

# 在导出模板目录下定位最新的 `android_source.zip`。
function Resolve-GodotAndroidSourceArchive {
    $exportTemplatesRoot = Join-Path $env:APPDATA "Godot\export_templates"
    Assert-PathExists -Path $exportTemplatesRoot -Label "Godot export templates directory"

    $archive = Get-ChildItem -LiteralPath $exportTemplatesRoot -Recurse -Filter "android_source.zip" -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $archive) {
        throw "android_source.zip was not found under: $exportTemplatesRoot"
    }

    return $archive.FullName
}

# 从 zip 模板中解压单个条目到目标位置。
function Expand-ZipEntryToFile {
    param(
        [System.IO.Compression.ZipArchive]$Archive,
        [string]$EntryPath,
        [string]$DestinationPath
    )

    $entry = $Archive.GetEntry($EntryPath)
    if ($null -eq $entry) {
        throw "Zip entry not found: $EntryPath"
    }

    $destinationDirectory = Split-Path -Parent $DestinationPath
    if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    $entryStream = $entry.Open()
    try {
        $destinationStream = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $entryStream.CopyTo($destinationStream)
        }
        finally {
            $destinationStream.Dispose()
        }
    }
    finally {
        $entryStream.Dispose()
    }
}

# 确保 Android 导出模板里的 AAR 已经落到项目所需位置。
function Ensure-GodotAndroidTemplateAars {
    param([string]$ProjectDir)

    Write-Step "Prepare Godot Android template archives"

    $debugAarPath = Join-Path $ProjectDir "android/build/libs/debug/godot-lib.template_debug.aar"
    $releaseAarPath = Join-Path $ProjectDir "android/build/libs/release/godot-lib.template_release.aar"

    if ((Test-Path -LiteralPath $debugAarPath) -and (Test-Path -LiteralPath $releaseAarPath)) {
        return
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $androidSourceArchivePath = Resolve-GodotAndroidSourceArchive
    $archive = [System.IO.Compression.ZipFile]::OpenRead($androidSourceArchivePath)
    try {
        if (-not (Test-Path -LiteralPath $debugAarPath)) {
            Expand-ZipEntryToFile `
                -Archive $archive `
                -EntryPath "libs/debug/godot-lib.template_debug.aar" `
                -DestinationPath $debugAarPath
        }

        if (-not (Test-Path -LiteralPath $releaseAarPath)) {
            Expand-ZipEntryToFile `
                -Archive $archive `
                -EntryPath "libs/release/godot-lib.template_release.aar" `
                -DestinationPath $releaseAarPath
        }
    }
    finally {
        $archive.Dispose()
    }
}

# 生成 Android 模板在干净环境中缺失的最小资源文件。
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

# 解析 Android SDK 中可用的最新 build-tools 目录。
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

# 统一读取原生命令退出码，避免不同调用路径下行为不一致。
function Get-NativeExitCode {
    $lastExitCodeVariable = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
    if ($null -eq $lastExitCodeVariable) {
        return 0
    }

    return [int]$lastExitCodeVariable.Value
}

# 输出日志文件尾部摘要，便于 CI 快速定位失败点。
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

function Assert-TextContains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Label
    )

    if ([string]::IsNullOrWhiteSpace($Text) -or ($Text.IndexOf($Expected, [System.StringComparison]::OrdinalIgnoreCase) -lt 0)) {
        throw "$Label did not contain expected text: $Expected"
    }
}

function Get-ZipEntryNames {
    param([string]$ArchivePath)

    Assert-PathExists -Path $ArchivePath -Label "Archive"
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
    try {
        return [string[]]@($archive.Entries | Select-Object -ExpandProperty FullName)
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-ZipContainsEntry {
    param(
        [string[]]$EntryNames,
        [string]$ExpectedEntry,
        [string]$Label
    )

    if (-not ($EntryNames -contains $ExpectedEntry)) {
        throw "$Label is missing required archive entry: $ExpectedEntry"
    }
}

function Assert-ZipContainsPrefix {
    param(
        [string[]]$EntryNames,
        [string]$ExpectedPrefix,
        [string]$Label
    )

    $match = $EntryNames | Where-Object { $_.StartsWith($ExpectedPrefix, [System.StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
    if ($null -eq $match) {
        throw "$Label is missing required archive content under: $ExpectedPrefix"
    }
}

function Validate-AndroidApkArtifact {
    param(
        [string]$ApkPath,
        [string]$AaptPath,
        [string]$PackageName,
        [string]$VersionName,
        [int]$VersionCode,
        [string]$ManifestOrientation
    )

    Write-Step "Validate Android APK contents"

    Assert-PathExists -Path $ApkPath -Label "Android APK"
    Assert-PathExists -Path $AaptPath -Label "aapt"

    $badgingOutput = & $AaptPath dump badging $ApkPath | Out-String
    $badgingExitCode = Get-NativeExitCode
    if ($badgingExitCode -ne 0) {
        throw "aapt dump badging failed with exit code $badgingExitCode"
    }

    Assert-TextContains -Text $badgingOutput -Expected "package: name='$PackageName'" -Label "APK badging"
    Assert-TextContains -Text $badgingOutput -Expected "versionCode='$VersionCode'" -Label "APK badging"
    Assert-TextContains -Text $badgingOutput -Expected "versionName='$VersionName'" -Label "APK badging"
    Assert-TextContains -Text $badgingOutput -Expected "native-code: 'arm64-v8a'" -Label "APK badging"

    if ($ManifestOrientation -eq "portrait") {
        Assert-TextContains -Text $badgingOutput -Expected "uses-feature: name='android.hardware.screen.portrait'" -Label "APK badging"
    }

    if ($ManifestOrientation -eq "landscape") {
        Assert-TextContains -Text $badgingOutput -Expected "uses-feature: name='android.hardware.screen.landscape'" -Label "APK badging"
    }

    $entryNames = Get-ZipEntryNames -ArchivePath $ApkPath
    Assert-ZipContainsEntry -EntryNames $entryNames -ExpectedEntry "lib/arm64-v8a/libgodot_android.so" -Label "Android APK"
    Assert-ZipContainsEntry -EntryNames $entryNames -ExpectedEntry "assets/project.binary" -Label "Android APK"
    Assert-ZipContainsEntry -EntryNames $entryNames -ExpectedEntry "assets/_cl_" -Label "Android APK"
    Assert-ZipContainsEntry -EntryNames $entryNames -ExpectedEntry "assets/.godot/mono/publish/arm64/TileMatcher.dll" -Label "Android APK"
    Assert-ZipContainsEntry -EntryNames $entryNames -ExpectedEntry "assets/scripts/app/AppRoot.cs" -Label "Android APK"
    Assert-ZipContainsEntry -EntryNames $entryNames -ExpectedEntry "assets/scenes/app/AppRoot.tscn.remap" -Label "Android APK"
    Assert-ZipContainsPrefix -EntryNames $entryNames -ExpectedPrefix "META-INF/" -Label "Android APK"
}

function Invoke-GodotExport {
    param(
        [string]$GodotExe,
        [string]$ProjectDir,
        [string]$ExportPreset,
        [string]$UnsignedApkPath,
        [string]$FallbackApkPath,
        [int]$PollIntervalSeconds = 2,
        [int]$ArtifactStableSeconds = 8,
        [int]$HeartbeatSeconds = 15,
        [int]$MaxWaitSeconds = 900
    )

    $stdoutLogPath = Join-Path $env:TEMP "tilematcher-godot-export-stdout.log"
    $stderrLogPath = Join-Path $env:TEMP "tilematcher-godot-export-stderr.log"
    $exportStartedAt = Get-Date

    Set-GodotNuGetSourceEnvironment -GodotExe $GodotExe | Out-Null

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
    $stableArtifactPath = $null
    $completedByFreshFallbackArtifact = $false
    $timedOut = $false
    $lastHeartbeatAt = Get-Date

    function Get-FreshArtifactCandidate {
        param(
            [string[]]$Paths,
            [datetime]$StartedAt
        )

        foreach ($path in $Paths) {
            if ([string]::IsNullOrWhiteSpace($path)) {
                continue
            }

            if (-not (Test-Path -LiteralPath $path)) {
                continue
            }

            $item = Get-Item -LiteralPath $path
            if (($item.LastWriteTime -lt $StartedAt) -or ($item.Length -le 0)) {
                continue
            }

            return $item
        }

        return $null
    }

    while (-not $process.HasExited) {
        Start-Sleep -Seconds $PollIntervalSeconds

        $now = Get-Date
        $elapsedSeconds = [int](($now - $exportStartedAt).TotalSeconds)
        if (($now - $lastHeartbeatAt).TotalSeconds -ge $HeartbeatSeconds) {
            $artifactStatus = "none"
            $artifactCandidate = Get-FreshArtifactCandidate `
                -Paths @($UnsignedApkPath, $FallbackApkPath) `
                -StartedAt $exportStartedAt
            if ($null -ne $artifactCandidate) {
                $artifactStatus = "$($artifactCandidate.FullName) ($([math]::Round($artifactCandidate.Length / 1MB, 2)) MB)"
            }

            Write-Host "Godot export is still running... elapsed ${elapsedSeconds}s, latest artifact: $artifactStatus" -ForegroundColor DarkGray
            $lastHeartbeatAt = $now
        }

        if ($elapsedSeconds -ge $MaxWaitSeconds) {
            try {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
            catch {
            }

            $timedOut = $true
            break
        }

        $artifactCandidate = Get-FreshArtifactCandidate `
            -Paths @($UnsignedApkPath, $FallbackApkPath) `
            -StartedAt $exportStartedAt
        if ($null -eq $artifactCandidate) {
            continue
        }

        $currentFingerprint = "$($artifactCandidate.FullName)|$($artifactCandidate.Length)|$($artifactCandidate.LastWriteTimeUtc.Ticks)"
        if ($currentFingerprint -ne $artifactFingerprint) {
            $artifactFingerprint = $currentFingerprint
            $stableArtifactPath = $artifactCandidate.FullName
            $artifactStableSince = $now
            continue
        }

        if (($null -ne $artifactStableSince) -and (($now - $artifactStableSince).TotalSeconds -ge $ArtifactStableSeconds)) {
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

    if ($timedOut) {
        Write-Warning "Godot export exceeded the wait limit (${MaxWaitSeconds}s). The process was stopped so the script can surface the current logs instead of appearing hung."
        return [pscustomobject]@{
            ExitCode = 1
            StdoutLogPath = $stdoutLogPath
            StderrLogPath = $stderrLogPath
            CompletedByFreshFallbackArtifact = $false
            StableArtifactPath = $stableArtifactPath
            TimedOut = $true
        }
    }

    if ($completedByFreshFallbackArtifact) {
        Write-Warning "Godot export did not exit cleanly after producing a stable APK artifact. The process was stopped and the build will continue from: $stableArtifactPath"
        return [pscustomobject]@{
            ExitCode = 0
            StdoutLogPath = $stdoutLogPath
            StderrLogPath = $stderrLogPath
            CompletedByFreshFallbackArtifact = $true
            StableArtifactPath = $stableArtifactPath
            TimedOut = $false
        }
    }

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdoutLogPath = $stdoutLogPath
        StderrLogPath = $stderrLogPath
        CompletedByFreshFallbackArtifact = $false
        StableArtifactPath = $stableArtifactPath
        TimedOut = $false
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
Ensure-GodotAndroidTemplateAars -ProjectDir $ProjectDir

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
    $unsignedHash = (Get-FileHash -LiteralPath $unsignedApkPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $unsignedHashPath = "$unsignedApkPath.sha256.txt"
    "$unsignedHash  $(Split-Path -Leaf $unsignedApkPath)" | Set-Content -LiteralPath $unsignedHashPath -Encoding ASCII
    Write-Host ""
    Write-Host "Unsigned export completed: $unsignedApkPath" -ForegroundColor Green
    Write-Host "SHA256: $unsignedHash" -ForegroundColor DarkGray
    Write-Host "SHA256 file: $unsignedHashPath" -ForegroundColor DarkGray
    return
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

Validate-AndroidApkArtifact `
    -ApkPath $signedApkPath `
    -AaptPath $aaptPath `
    -PackageName $PackageName `
    -VersionName $VersionName `
    -VersionCode $VersionCode `
    -ManifestOrientation $ManifestOrientation

$signedHash = (Get-FileHash -LiteralPath $signedApkPath -Algorithm SHA256).Hash.ToUpperInvariant()
$signedHashPath = "$signedApkPath.sha256.txt"
"$signedHash  $(Split-Path -Leaf $signedApkPath)" | Set-Content -LiteralPath $signedHashPath -Encoding ASCII

Write-Host ""
Write-Host "Android build completed: $signedApkPath" -ForegroundColor Green
Write-Host "SHA256: $signedHash" -ForegroundColor DarkGray
Write-Host "SHA256 file: $signedHashPath" -ForegroundColor DarkGray
return
