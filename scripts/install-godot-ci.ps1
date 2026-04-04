[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ReleaseStatus = "stable",
    [string]$InstallRoot = (Join-Path $env:RUNNER_TEMP "godot"),
    [string]$AppDataRoot = $env:APPDATA
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-DownloadFile {
    param(
        [string]$Url,
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        Write-Host "Reuse cached file: $Path" -ForegroundColor DarkGray
        return
    }

    Write-Host "Download: $Url" -ForegroundColor DarkGray
    Invoke-WebRequest -Uri $Url -OutFile $Path
}

function Expand-ZipArchive {
    param(
        [string]$ArchivePath,
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
    Expand-Archive -LiteralPath $ArchivePath -DestinationPath $DestinationPath -Force
}

$releaseTag = "$Version-$ReleaseStatus"
$godotVersionLabel = "$Version.$ReleaseStatus.mono"
$editorArchiveName = "Godot_v$Version-$ReleaseStatus" + "_mono_win64.zip"
$templatesArchiveName = "Godot_v$Version-$ReleaseStatus" + "_export_templates.tpz"
$editorDownloadUrl = "https://github.com/godotengine/godot/releases/download/$releaseTag/$editorArchiveName"
$templatesDownloadUrl = "https://github.com/godotengine/godot/releases/download/$releaseTag/$templatesArchiveName"

$downloadRoot = Join-Path $InstallRoot "downloads"
$editorExtractRoot = Join-Path $InstallRoot "editor"
$templatesExtractRoot = Join-Path $InstallRoot "templates"
$exportTemplatesRoot = Join-Path $AppDataRoot "Godot\export_templates\$godotVersionLabel"

New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
New-Item -ItemType Directory -Path $editorExtractRoot -Force | Out-Null
New-Item -ItemType Directory -Path $templatesExtractRoot -Force | Out-Null

$editorArchivePath = Join-Path $downloadRoot $editorArchiveName
$templatesArchivePath = Join-Path $downloadRoot $templatesArchiveName

Write-Step "Download Godot editor"
Get-DownloadFile -Url $editorDownloadUrl -Path $editorArchivePath

Write-Step "Download Godot export templates"
Get-DownloadFile -Url $templatesDownloadUrl -Path $templatesArchivePath

Write-Step "Extract Godot editor"
Expand-ZipArchive -ArchivePath $editorArchivePath -DestinationPath $editorExtractRoot

$godotExe = Get-ChildItem -LiteralPath $editorExtractRoot -Recurse -Filter ("Godot_v$Version-$ReleaseStatus" + "_mono_win64.exe") -File | Select-Object -First 1
if ($null -eq $godotExe) {
    throw "Godot executable not found after extraction under: $editorExtractRoot"
}
$godotExePath = $godotExe.FullName

Write-Step "Install Godot export templates"
Expand-ZipArchive -ArchivePath $templatesArchivePath -DestinationPath $templatesExtractRoot

if (Test-Path -LiteralPath $exportTemplatesRoot) {
    Remove-Item -LiteralPath $exportTemplatesRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $exportTemplatesRoot -Force | Out-Null
Get-ChildItem -LiteralPath $templatesExtractRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $exportTemplatesRoot -Recurse -Force
}

$versionFile = Get-ChildItem -LiteralPath $exportTemplatesRoot -Recurse -Filter "version.txt" -File | Select-Object -First 1
if ($null -eq $versionFile) {
    throw "Godot export templates version file not found under: $exportTemplatesRoot"
}
$versionFilePath = $versionFile.FullName

$versionFileContent = (Get-Content -LiteralPath $versionFilePath -Raw -Encoding UTF8).Trim()
if ($versionFileContent -ne $godotVersionLabel) {
    throw "Unexpected export templates version. Expected $godotVersionLabel, got $versionFileContent"
}

Write-Host ""
Write-Host "Godot executable: $godotExePath" -ForegroundColor Green
Write-Host "Godot templates: $exportTemplatesRoot" -ForegroundColor Green
