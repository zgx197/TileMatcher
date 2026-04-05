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

function Install-TemplatesDirectory {
    param(
        [string]$SourceRoot,
        [string]$DestinationRoot
    )

    if (Test-Path -LiteralPath $DestinationRoot) {
        Remove-Item -LiteralPath $DestinationRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $SourceRoot -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $DestinationRoot -Recurse -Force
    }
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
$exportTemplatesParentRoot = Join-Path $AppDataRoot "Godot\export_templates"

New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
New-Item -ItemType Directory -Path $editorExtractRoot -Force | Out-Null
New-Item -ItemType Directory -Path $templatesExtractRoot -Force | Out-Null
New-Item -ItemType Directory -Path $exportTemplatesParentRoot -Force | Out-Null

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

$versionFile = Get-ChildItem -LiteralPath $templatesExtractRoot -Recurse -Filter "version.txt" -File | Select-Object -First 1
if ($null -eq $versionFile) {
    throw "Godot export templates version file not found under: $templatesExtractRoot"
}
$versionFilePath = $versionFile.FullName
$templatesContentRoot = [System.IO.Path]::GetDirectoryName($versionFilePath)

$versionFileContent = (Get-Content -LiteralPath $versionFilePath -Raw -Encoding UTF8).Trim()
$exportTemplatesRoot = Join-Path $exportTemplatesParentRoot $versionFileContent
Install-TemplatesDirectory -SourceRoot $templatesContentRoot -DestinationRoot $exportTemplatesRoot

$monoAliasTemplatesRoot = Join-Path $exportTemplatesParentRoot $godotVersionLabel
if ($versionFileContent -ne $godotVersionLabel) {
    Install-TemplatesDirectory -SourceRoot $templatesContentRoot -DestinationRoot $monoAliasTemplatesRoot
}

Write-Host ""
Write-Host "Godot executable: $godotExePath" -ForegroundColor Green
Write-Host "Godot templates: $exportTemplatesRoot" -ForegroundColor Green
if ($versionFileContent -ne $godotVersionLabel) {
    Write-Host "Godot mono templates: $monoAliasTemplatesRoot" -ForegroundColor Green
}
