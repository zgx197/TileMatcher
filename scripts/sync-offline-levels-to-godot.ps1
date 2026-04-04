param(
    [string]$SourceDir = "artifacts\mahjong-mvp\runtime-levels",
    [string]$TargetDir = "godot\generated\runtime-levels"
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$sourcePath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $SourceDir))
$targetPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $TargetDir))
$targetLevelsPath = Join-Path $targetPath "levels"

if (-not $targetPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Target path escapes repository root: $targetPath"
}

if (-not (Test-Path $sourcePath -PathType Container)) {
    throw "Offline runtime source directory not found: $sourcePath"
}

$sourceCatalogPath = Join-Path $sourcePath "level-catalog.json"
$sourceLevelsPath = Join-Path $sourcePath "levels"

if (-not (Test-Path $sourceCatalogPath -PathType Leaf)) {
    throw "Offline runtime catalog file not found: $sourceCatalogPath"
}

if (-not (Test-Path $sourceLevelsPath -PathType Container)) {
    throw "Offline runtime levels directory not found: $sourceLevelsPath"
}

New-Item -ItemType Directory -Force -Path $targetPath | Out-Null
New-Item -ItemType Directory -Force -Path $targetLevelsPath | Out-Null

$sourceLevelFiles = Get-ChildItem -Path $sourceLevelsPath -File -Filter *.json | Sort-Object Name
if ($sourceLevelFiles.Count -eq 0) {
    throw "Offline runtime levels directory is empty: $sourceLevelsPath"
}

Get-ChildItem -Path $targetLevelsPath -File -Filter *.json | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Force
}

Copy-Item -LiteralPath $sourceCatalogPath -Destination (Join-Path $targetPath "level-catalog.json") -Force
foreach ($file in $sourceLevelFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $targetLevelsPath $file.Name) -Force
}

Write-Host "[OfflineSync] Source : $sourcePath"
Write-Host "[OfflineSync] Target : $targetPath"
Write-Host "[OfflineSync] Levels : $($sourceLevelFiles.Count)"
Write-Host "[OfflineSync] Catalog: level-catalog.json"
