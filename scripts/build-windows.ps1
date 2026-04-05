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

function Resolve-WindowsDataDirCandidate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SearchRoot,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedName
    )

    $candidates = @(Get-ChildItem -LiteralPath $SearchRoot -Recurse -Directory -Filter $ExpectedName -ErrorAction SilentlyContinue |
        ForEach-Object {
            $directory = $_
            $parentDir = Split-Path -Parent $directory.FullName
            $companionExe = Get-ChildItem -LiteralPath $parentDir -File -Filter "*.exe" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1
            $companionPck = Get-ChildItem -LiteralPath $parentDir -File -Filter "*.pck" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                Select-Object -First 1

            [pscustomobject]@{
                FullName = $directory.FullName
                HasCompanionExport = ($null -ne $companionExe) -and ($null -ne $companionPck)
                ParentDir = $parentDir
                DirectoryDepth = ($directory.FullName -split '[\\/]').Count
            }
        } |
        Sort-Object @{ Expression = { if ($_.HasCompanionExport) { 0 } else { 1 } } }, DirectoryDepth, FullName)

    if ($candidates.Count -eq 0) {
        return $null
    }

    return $candidates[0].FullName
}

function Resolve-WindowsDataDirFromExportOutput {
    param(
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]]$OutputLines,

        [Parameter(Mandatory = $true)]
        [string]$ProjectDir,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedName
    )

    $ansiEscapePattern = "`e\\[[0-9;]*[A-Za-z]"

    foreach ($line in @($OutputLines)) {
        if ([string]::IsNullOrWhiteSpace($line) -or ($line -notmatch $ExpectedName)) {
            continue
        }

        $sanitizedLine = [regex]::Replace($line, $ansiEscapePattern, "")
        $match = [regex]::Match($sanitizedLine, "res://(?<relative>.*?" + [regex]::Escape($ExpectedName) + ")")
        if (-not $match.Success) {
            continue
        }

        $relativePath = $match.Groups["relative"].Value.Trim().Replace("/", "\")
        if ([string]::IsNullOrWhiteSpace($relativePath)) {
            continue
        }

        $candidatePath = Join-Path $ProjectDir $relativePath
        if (Test-Path -LiteralPath $candidatePath) {
            return (Resolve-Path -LiteralPath $candidatePath).Path
        }
    }

    return $null
}

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
$expectedPckPath = Join-Path $stagingDir "$exportBaseName.pck"
$expectedDataDirName = "data_${exportBaseName}_windows_x86_64"
$expectedDataDirPath = Join-Path $stagingDir $expectedDataDirName

Reset-Directory -Path $stagingDir
Remove-PathIfExists -Path $artifactPath
Remove-PathIfExists -Path "$artifactPath.sha256.txt"
Clear-GodotMonoBuildIssueFiles

$exportResult = Invoke-GodotExport `
    -GodotExe $GodotExe `
    -ProjectDir $ProjectDir `
    -ExportPreset $ExportPreset `
    -ExportPath $exportPath `
    -BuildKind "release"

Wait-ForStableFile -Path $expectedPckPath
Remove-TransientExportFiles -RootPath $stagingDir

Assert-PathExists -Path $exportPath -Label "Windows executable"
Assert-PathExists -Path $expectedPckPath -Label "Windows PCK"

if ($metadata.UsesDotNet -and -not (Test-Path -LiteralPath $expectedDataDirPath)) {
    $dataDirCandidate = Resolve-WindowsDataDirFromExportOutput `
        -OutputLines @($exportResult.OutputLines) `
        -ProjectDir $ProjectDir `
        -ExpectedName $expectedDataDirName

    if ([string]::IsNullOrWhiteSpace($dataDirCandidate)) {
        $dataDirCandidate = Resolve-WindowsDataDirCandidate `
            -SearchRoot $outputDir `
            -ExpectedName $expectedDataDirName
    }

    if ([string]::IsNullOrWhiteSpace($dataDirCandidate)) {
        throw "Windows .NET export data directory was not produced: $expectedDataDirName"
    }

    $resolvedCandidatePath = (Resolve-Path -LiteralPath $dataDirCandidate).Path
    if ($resolvedCandidatePath -ine $expectedDataDirPath) {
        Copy-Item -LiteralPath $resolvedCandidatePath -Destination $expectedDataDirPath -Recurse -Force
    }
}

if ($metadata.UsesDotNet) {
    Assert-PathExists -Path $expectedDataDirPath -Label "Windows .NET runtime data directory"
}

$stagedFiles = Get-RelativeChildPaths -RootPath $stagingDir
if ($stagedFiles.Count -eq 0) {
    throw "Windows export staging directory is empty: $stagingDir"
}

$topLevelEntries = @(Get-ChildItem -LiteralPath $stagingDir -Force | Sort-Object Name | ForEach-Object {
    if ($_.PSIsContainer) {
        "$($_.Name)\"
    } else {
        $_.Name
    }
})

Write-Step "Package Windows artifact"
Compress-DirectoryToZip -SourceDir $stagingDir -ZipPath $artifactPath
$hashInfo = Write-Sha256File -ArtifactPath $artifactPath

Write-Host ""
Write-Host "Windows build completed: $artifactPath" -ForegroundColor Green
Write-Host "SHA256: $($hashInfo.Sha256)" -ForegroundColor DarkGray
Write-Host "SHA256 file: $($hashInfo.HashPath)" -ForegroundColor DarkGray
Write-Host "Top-level package entries:" -ForegroundColor DarkGray
foreach ($entry in $topLevelEntries) {
    Write-Host " - $entry" -ForegroundColor DarkGray
}
Write-Host "Total packaged files: $($stagedFiles.Count)" -ForegroundColor DarkGray
