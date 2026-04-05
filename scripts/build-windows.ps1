[CmdletBinding()]
param(
    [string]$GodotExe = $env:GODOT_EXE,
    [string]$ProjectDir,
    [string]$ExportPreset = "Windows Desktop",
    [string]$VersionName,
    [string]$OutputName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "build-common.ps1")

$ProjectDir = Resolve-ProjectDir -ProjectDir $ProjectDir
$GodotExe = Resolve-GodotExe -GodotExe $GodotExe
$metadata = Get-ProjectMetadata -ProjectDir $ProjectDir

if ([string]::IsNullOrWhiteSpace($VersionName)) {
    $VersionName = $metadata.VersionName
}

if ([string]::IsNullOrWhiteSpace($OutputName)) {
    $OutputName = "TileMatcher-$VersionName-windows-x64.zip"
}

Update-ProjectBuildMetadata -ProjectDir $ProjectDir -VersionName $VersionName

$outputDir = Join-Path $ProjectDir "build/windows"
$stagingDir = Join-Path $outputDir "_staging"
$artifactPath = Join-Path $outputDir $OutputName
$exportBaseName = [regex]::Replace($metadata.ProjectName, '[\\/:*?"<>|]', "")
if ([string]::IsNullOrWhiteSpace($exportBaseName)) {
    $exportBaseName = "TileMatcher"
}

$exportPath = Join-Path $stagingDir "$exportBaseName.exe"

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
    throw "Windows export staging directory is empty: $stagingDir"
}

Write-Step "Package Windows artifact"
Compress-DirectoryToZip -SourceDir $stagingDir -ZipPath $artifactPath
$hashInfo = Write-Sha256File -ArtifactPath $artifactPath

Write-Host ""
Write-Host "Windows build completed: $artifactPath" -ForegroundColor Green
Write-Host "SHA256: $($hashInfo.Sha256)" -ForegroundColor DarkGray
Write-Host "SHA256 file: $($hashInfo.HashPath)" -ForegroundColor DarkGray
Write-Host "Packaged files:" -ForegroundColor DarkGray
foreach ($relativePath in $stagedFiles) {
    Write-Host " - $relativePath" -ForegroundColor DarkGray
}

return
