param(
    [string]$GodotExe = 'D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe',
    [string]$ProjectPath = 'godot',
    [string]$OutputDir,
    [switch]$IncludeEditorLogs,
    [switch]$IncludeRuntimeLogs,
    [switch]$UseMsBuild
)

# Godot / .NET 诊断日志抓取脚本。
# 统一收集 headless 运行日志、dotnet 构建日志，以及可选的编辑器和运行时日志。
$ErrorActionPreference = 'Stop'

# 构造摘要分节标题，便于最终 summary.txt 阅读。
function Write-Section {
    param([string]$Title)
    "`n===== $Title =====`n"
}

# 探测命令是否在 PATH 中可用，主要用于可选的 MSBuild 分支。
function Resolve-OptionalCommand {
    param([string]$Name)

    try {
        $command = Get-Command $Name -ErrorAction Stop
        return $command.Source
    }
    catch {
        return $null
    }
}

# 从日志目录复制最近若干文件到诊断输出目录。
function Copy-RecentFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDir,
        [int]$Count = 5,
        [string]$Prefix = ''
    )

    if (-not (Test-Path -LiteralPath $SourceDir)) {
        return @()
    }

    $copied = @()
    $files = Get-ChildItem -LiteralPath $SourceDir -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First $Count

    foreach ($file in $files) {
        $targetName = if ([string]::IsNullOrWhiteSpace($Prefix)) { $file.Name } else { "$Prefix$file" }
        $destination = Join-Path $DestinationDir $targetName
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        $copied += $destination
    }

    return $copied
}

$godotExePath = (Resolve-Path -LiteralPath $GodotExe).Path
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectName = Split-Path -Leaf $projectRoot
$projectFile = Join-Path $projectRoot 'project.godot'
if (Test-Path -LiteralPath $projectFile) {
    $nameLine = Get-Content $projectFile | Where-Object { $_ -match '^config/name=' } | Select-Object -First 1
    if ($nameLine) {
        $projectName = ($nameLine -replace '^config/name="', '') -replace '"$', ''
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $logsRoot = Join-Path $projectRoot 'logs'
    if (-not (Test-Path -LiteralPath $logsRoot)) {
        New-Item -ItemType Directory -Path $logsRoot | Out-Null
    }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $OutputDir = Join-Path $logsRoot ("diagnostics-" + $timestamp)
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$summaryPath = Join-Path $OutputDir 'summary.txt'
$godotLogPath = Join-Path $OutputDir 'godot-headless.log'
$dotnetLogPath = Join-Path $OutputDir 'dotnet-build.log'
$msbuildLogPath = Join-Path $OutputDir 'msbuild.log'

$summaryLines = [System.Collections.Generic.List[string]]::new()
$summaryLines.Add("Godot executable: $godotExePath")
$summaryLines.Add("Project path: $projectRoot")
$summaryLines.Add("Project name: $projectName")
$summaryLines.Add("Output directory: $OutputDir")

$summaryLines.Add((Write-Section 'Godot Headless Run'))

# 这里有意不使用 PowerShell 管道或 stdout/stderr 重定向去包裹 Godot 进程。
# 在 Windows + Godot 4.6.1 Mono 下，这种抓取方式会触发：
# 1. ERROR: Failed to open 'user://logs/...'
# 2. 随后 headless 进程崩溃
# 改为让 Godot 自己通过 --log-file 落盘，再由脚本读取退出码。
$headlessExitCode = 0
& $godotExePath --headless --path $projectRoot --quit --verbose --log-file $godotLogPath
$headlessExitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }

if (-not (Test-Path -LiteralPath $godotLogPath)) {
    "Godot headless log file was not created." | Set-Content -LiteralPath $godotLogPath -Encoding UTF8
}

if ($headlessExitCode -eq 0) {
    $summaryLines.Add("OK: $godotLogPath")
}
else {
    $summaryLines.Add("FAILED: $godotLogPath")
    $summaryLines.Add("Godot headless exit code: $headlessExitCode")
}

$solutionFile = Get-ChildItem -LiteralPath $projectRoot -Filter *.sln -File | Select-Object -First 1
$projectCsprojFile = Get-ChildItem -LiteralPath $projectRoot -Filter *.csproj -File | Select-Object -First 1
$buildTarget = if ($solutionFile) { $solutionFile.FullName } elseif ($projectCsprojFile) { $projectCsprojFile.FullName } else { $null }

if ($buildTarget) {
    $summaryLines.Add((Write-Section '.NET Build'))
    try {
        dotnet build $buildTarget -nologo -v:minimal *>&1 | Tee-Object -FilePath $dotnetLogPath | Out-Host
        $summaryLines.Add("OK: $dotnetLogPath")
    }
    catch {
        $_ | Out-String | Set-Content -LiteralPath $dotnetLogPath -Encoding UTF8
        $summaryLines.Add("FAILED: $dotnetLogPath")
        $summaryLines.Add($_.Exception.Message)
    }

    if ($UseMsBuild) {
        $msbuildPath = Resolve-OptionalCommand 'MSBuild.exe'
        if ($msbuildPath) {
            $summaryLines.Add((Write-Section 'MSBuild'))
            try {
                & $msbuildPath $buildTarget /nologo /verbosity:minimal *>&1 | Tee-Object -FilePath $msbuildLogPath | Out-Host
                $summaryLines.Add("OK: $msbuildLogPath")
            }
            catch {
                $_ | Out-String | Set-Content -LiteralPath $msbuildLogPath -Encoding UTF8
                $summaryLines.Add("FAILED: $msbuildLogPath")
                $summaryLines.Add($_.Exception.Message)
            }
        }
        else {
            $summaryLines.Add((Write-Section 'MSBuild'))
            $summaryLines.Add('MSBuild.exe not found on PATH.')
        }
    }
}
else {
    $summaryLines.Add((Write-Section '.NET Build'))
    $summaryLines.Add('No .sln or .csproj file found in the project root.')
}

if ($IncludeEditorLogs) {
    $summaryLines.Add((Write-Section 'Editor Logs'))
    $editorLogsDir = Join-Path $env:APPDATA 'Godot\editor_logs'
    if (Test-Path -LiteralPath $editorLogsDir) {
        $copiedLogs = Copy-RecentFiles -SourceDir $editorLogsDir -DestinationDir $OutputDir -Count 3 -Prefix 'editor-'
        foreach ($logPath in $copiedLogs) {
            $summaryLines.Add("COPIED: $logPath")
        }
    }
    else {
        $summaryLines.Add("Editor logs directory not found: $editorLogsDir")
    }
}

if ($IncludeRuntimeLogs) {
    $summaryLines.Add((Write-Section 'Runtime Logs'))
    $runtimeLogsDir = Join-Path $env:APPDATA ("Godot\app_userdata\{0}\logs" -f $projectName)
    if (Test-Path -LiteralPath $runtimeLogsDir) {
        $copiedRuntimeLogs = Copy-RecentFiles -SourceDir $runtimeLogsDir -DestinationDir $OutputDir -Count 5 -Prefix 'runtime-'
        foreach ($logPath in $copiedRuntimeLogs) {
            $summaryLines.Add("COPIED: $logPath")
        }
    }
    else {
        $summaryLines.Add("Runtime logs directory not found: $runtimeLogsDir")
    }

    $summaryLines.Add((Write-Section 'Mono Build Logs'))
    $monoBuildLogsRoot = Join-Path $env:APPDATA 'Godot\mono\build_logs'
    if (Test-Path -LiteralPath $monoBuildLogsRoot) {
        $recentBuildDir = Get-ChildItem -LiteralPath $monoBuildLogsRoot -Directory |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($recentBuildDir) {
            $copiedBuildLogs = Copy-RecentFiles -SourceDir $recentBuildDir.FullName -DestinationDir $OutputDir -Count 5 -Prefix 'mono-build-'
            foreach ($logPath in $copiedBuildLogs) {
                $summaryLines.Add("COPIED: $logPath")
            }
        }
        else {
            $summaryLines.Add("No build log directories found in: $monoBuildLogsRoot")
        }
    }
    else {
        $summaryLines.Add("Mono build logs directory not found: $monoBuildLogsRoot")
    }
}

$summaryLines | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Host "Diagnostics written to: $OutputDir"
Write-Host "Summary: $summaryPath"
