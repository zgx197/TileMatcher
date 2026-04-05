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

# CI 环境中的 Godot 安装脚本。
# 负责下载指定版本的 .NET 编辑器和 .NET 导出模板，并安装到 Godot 约定目录。

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

function Expand-ArchiveFile {
    param(
        [string]$ArchivePath,
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null

    $archiveToExtract = $ArchivePath
    $temporaryZipPath = $null
    if ([System.IO.Path]::GetExtension($ArchivePath) -ieq ".tpz") {
        $temporaryZipPath = Join-Path ([System.IO.Path]::GetDirectoryName($ArchivePath)) ([System.IO.Path]::GetFileNameWithoutExtension($ArchivePath) + ".zip")
        Copy-Item -LiteralPath $ArchivePath -Destination $temporaryZipPath -Force
        $archiveToExtract = $temporaryZipPath
    }

    try {
        Expand-Archive -LiteralPath $archiveToExtract -DestinationPath $DestinationPath -Force
    }
    finally {
        if ($null -ne $temporaryZipPath -and (Test-Path -LiteralPath $temporaryZipPath)) {
            Remove-Item -LiteralPath $temporaryZipPath -Force -ErrorAction SilentlyContinue
        }
    }
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
$templatesArchiveName = "Godot_v$Version-$ReleaseStatus" + "_mono_export_templates.tpz"
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

Write-Step "Download Godot .NET editor"
Get-DownloadFile -Url $editorDownloadUrl -Path $editorArchivePath

Write-Step "Download Godot .NET export templates"
Get-DownloadFile -Url $templatesDownloadUrl -Path $templatesArchivePath

Write-Step "Extract Godot editor"
Expand-ArchiveFile -ArchivePath $editorArchivePath -DestinationPath $editorExtractRoot

$godotExe = Get-ChildItem -LiteralPath $editorExtractRoot -Recurse -Filter ("Godot_v$Version-$ReleaseStatus" + "_mono_win64.exe") -File | Select-Object -First 1
if ($null -eq $godotExe) {
    throw "Godot executable not found after extraction under: $editorExtractRoot"
}
$godotExePath = $godotExe.FullName

Write-Step "Install Godot .NET export templates"
Expand-ArchiveFile -ArchivePath $templatesArchivePath -DestinationPath $templatesExtractRoot

$versionFile = Get-ChildItem -LiteralPath $templatesExtractRoot -Recurse -Filter "version.txt" -File | Select-Object -First 1
if ($null -eq $versionFile) {
    throw "Godot export templates version file not found under: $templatesExtractRoot"
}

$versionFilePath = $versionFile.FullName
$templatesContentRoot = [System.IO.Path]::GetDirectoryName($versionFilePath)
$versionFileContent = (Get-Content -LiteralPath $versionFilePath -Raw -Encoding UTF8).Trim()

$exportTemplatesRoot = Join-Path $exportTemplatesParentRoot $versionFileContent
Install-TemplatesDirectory -SourceRoot $templatesContentRoot -DestinationRoot $exportTemplatesRoot

if ($versionFileContent -ne $godotVersionLabel) {
    $monoAliasTemplatesRoot = Join-Path $exportTemplatesParentRoot $godotVersionLabel
    Install-TemplatesDirectory -SourceRoot $templatesContentRoot -DestinationRoot $monoAliasTemplatesRoot
    Write-Host "Godot mono templates: $monoAliasTemplatesRoot" -ForegroundColor Green
}

Write-Host ""
Write-Host "Godot executable: $godotExePath" -ForegroundColor Green
Write-Host "Godot templates: $exportTemplatesRoot" -ForegroundColor Green
