[CmdletBinding()]
param(
    [string]$GodotExe = $env:GODOT_EXE,
    [string]$ProjectDir,
    [string[]]$Targets = @("android", "windows"),
    [string]$VersionName,
    [Nullable[int]]$VersionCode = $null,
    [string]$PackageName,
    [string]$ManifestOrientation,
    [string]$AndroidSdkRoot = $env:ANDROID_SDK_ROOT,
    [string]$JavaHome = $env:JAVA_HOME,
    [string]$KeystorePath = $env:ANDROID_DEBUG_KEYSTORE,
    [string]$KeystorePassword = "android",
    [string]$KeyAlias = "androiddebugkey",
    [string]$KeyPassword = "android",
    [switch]$SkipSigning
)

# 三平台统一构建入口。
# 负责解析版本与目标参数，调用各平台脚本，并输出统一的产物摘要。
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "build-common.ps1")

$ProjectDir = Resolve-ProjectDir -ProjectDir $ProjectDir
$GodotExe = Resolve-GodotExe -GodotExe $GodotExe
$metadata = Get-ProjectMetadata -ProjectDir $ProjectDir
[string[]]$resolvedTargets = @(Convert-ToTargetList -Targets $Targets)

if ($resolvedTargets.Count -eq 0) {
    throw "No build targets were resolved. Pass -Targets android,windows or -Targets web explicitly."
}

if ([string]::IsNullOrWhiteSpace($VersionName)) {
    $VersionName = $metadata.VersionName
}

if (-not $PSBoundParameters.ContainsKey("VersionCode") -or $null -eq $VersionCode) {
    $VersionCode = $metadata.VersionCode
}

if ([string]::IsNullOrWhiteSpace($PackageName)) {
    $PackageName = $metadata.PackageName
}

if ([string]::IsNullOrWhiteSpace($ManifestOrientation)) {
    $ManifestOrientation = $metadata.ManifestOrientation
}

Update-ProjectBuildMetadata `
    -ProjectDir $ProjectDir `
    -VersionName $VersionName `
    -VersionCode $VersionCode `
    -PackageName $PackageName `
    -ManifestOrientation $ManifestOrientation

$scriptRoot = Get-ScriptRoot
$results = New-Object System.Collections.Generic.List[object]

foreach ($target in $resolvedTargets) {
    Write-Step "Build target: $target"

    switch ($target) {
        "android" {
            $androidParams = @{
                GodotExe = $GodotExe
                ProjectDir = $ProjectDir
                VersionName = $VersionName
                VersionCode = [int]$VersionCode
                PackageName = $PackageName
                ManifestOrientation = $ManifestOrientation
                AndroidSdkRoot = $AndroidSdkRoot
                JavaHome = $JavaHome
                KeystorePath = $KeystorePath
                KeystorePassword = $KeystorePassword
                KeyAlias = $KeyAlias
                KeyPassword = $KeyPassword
            }

            if ($SkipSigning) {
                $androidParams.SkipSigning = $true
            }

            & (Join-Path $scriptRoot "build-android.ps1") @androidParams

            $artifactPath = if ($SkipSigning) {
                Join-Path $ProjectDir "build/android/TileMatcher-unsigned.apk"
            } else {
                Join-Path $ProjectDir "build/android/TileMatcher-$VersionName-android-debug.apk"
            }

            $hashPath = "$artifactPath.sha256.txt"
            Assert-PathExists -Path $artifactPath -Label "Android artifact"
            Assert-PathExists -Path $hashPath -Label "Android SHA256 file"

            $results.Add([pscustomobject]@{
                Target = "android"
                ArtifactPath = $artifactPath
                HashPath = $hashPath
            })
        }

        "windows" {
            & (Join-Path $scriptRoot "build-windows.ps1") `
                -GodotExe $GodotExe `
                -ProjectDir $ProjectDir `
                -VersionName $VersionName

            $artifactPath = Join-Path $ProjectDir "build/windows/TileMatcher-$VersionName-windows-x64.zip"
            $hashPath = "$artifactPath.sha256.txt"
            Assert-PathExists -Path $artifactPath -Label "Windows artifact"
            Assert-PathExists -Path $hashPath -Label "Windows SHA256 file"

            $results.Add([pscustomobject]@{
                Target = "windows"
                ArtifactPath = $artifactPath
                HashPath = $hashPath
            })
        }

        "web" {
            & (Join-Path $scriptRoot "build-web.ps1") `
                -GodotExe $GodotExe `
                -ProjectDir $ProjectDir `
                -VersionName $VersionName

            $artifactPath = Join-Path $ProjectDir "build/web/TileMatcher-$VersionName-web.zip"
            $hashPath = "$artifactPath.sha256.txt"
            Assert-PathExists -Path $artifactPath -Label "Web artifact"
            Assert-PathExists -Path $hashPath -Label "Web SHA256 file"

            $results.Add([pscustomobject]@{
                Target = "web"
                ArtifactPath = $artifactPath
                HashPath = $hashPath
            })
        }
    }
}

# 汇总本次构建成功产物，便于本地和 CI 统一读取。
Write-Step "Build summary"
Write-Host "Version: $VersionName" -ForegroundColor DarkGray
Write-Host "Targets: $($resolvedTargets -join ', ')" -ForegroundColor DarkGray
foreach ($result in $results) {
    Write-Host ""
    Write-Host "[$($result.Target)]" -ForegroundColor Green
    Write-Host "Artifact: $($result.ArtifactPath)" -ForegroundColor DarkGray
    Write-Host "SHA256:   $($result.HashPath)" -ForegroundColor DarkGray
}

return
