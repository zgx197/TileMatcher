[CmdletBinding()]
param(
    [string]$GodotExe = $env:GODOT_EXE,
    [string]$ProjectDir,
    [string]$ExportPreset = "Web",
    [string]$VersionName,
    [string]$OutputName
)

# Web 导出脚本。
# 当前项目是 Godot C# 工程，因此这里只保留显式阻断和说明，
# 避免 CI 或本地误以为可以正常产出 Web 版本。
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "build-common.ps1")

$ProjectDir = Resolve-ProjectDir -ProjectDir $ProjectDir
$metadata = Get-ProjectMetadata -ProjectDir $ProjectDir

if ([string]::IsNullOrWhiteSpace($VersionName)) {
    $VersionName = $metadata.VersionName
}

if ([string]::IsNullOrWhiteSpace($OutputName)) {
    $OutputName = "TileMatcher-$VersionName-web.zip"
}

if ($metadata.UsesDotNet) {
    throw @"
Web export is blocked for the current project.

Reason:
- This Godot project uses C#/.NET.
- Official Godot export documentation states that Godot 4 C# projects cannot be exported to Web.

Reference:
- https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_web.html

Requested artifact name:
- $OutputName
"@
}

$GodotExe = Resolve-GodotExe -GodotExe $GodotExe
Update-ProjectBuildMetadata -ProjectDir $ProjectDir -VersionName $VersionName

# 如果未来项目不再依赖 C#，这里会沿用和 Windows 类似的暂存目录打包方式。
$outputDir = Join-Path $ProjectDir "build/web"
$stagingDir = Join-Path $outputDir "_staging"
$artifactPath = Join-Path $outputDir $OutputName
$exportPath = Join-Path $stagingDir "index.html"

Reset-Directory -Path $stagingDir
Remove-PathIfExists -Path $artifactPath
Remove-PathIfExists -Path "$artifactPath.sha256.txt"
Clear-GodotMonoBuildIssueFiles

Invoke-GodotExport `
    -GodotExe $GodotExe `
    -ProjectDir $ProjectDir `
    -ExportPreset $ExportPreset `
    -ExportPath $exportPath `
    -BuildKind "release"

Remove-TransientExportFiles -RootPath $stagingDir

$stagedFiles = Get-RelativeChildPaths -RootPath $stagingDir
if ($stagedFiles.Count -eq 0) {
    throw "Web export staging directory is empty: $stagingDir"
}

Write-Step "Package Web artifact"
Compress-DirectoryToZip -SourceDir $stagingDir -ZipPath $artifactPath
$hashInfo = Write-Sha256File -ArtifactPath $artifactPath

Write-Host ""
Write-Host "Web build completed: $artifactPath" -ForegroundColor Green
Write-Host "SHA256: $($hashInfo.Sha256)" -ForegroundColor DarkGray
Write-Host "SHA256 file: $($hashInfo.HashPath)" -ForegroundColor DarkGray
Write-Host "Packaged files:" -ForegroundColor DarkGray
foreach ($relativePath in $stagedFiles) {
    Write-Host " - $relativePath" -ForegroundColor DarkGray
}

return
