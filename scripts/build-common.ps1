# 公共构建辅助函数。
# 统一提供路径解析、Godot 导出、产物打包和元数据同步等基础能力，
# 让各平台脚本只关注各自差异。
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:DefaultGodotExe = "D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# 解析当前脚本所在目录，兼容被 dot-source 或直接执行两种调用方式。
function Get-ScriptRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return $PSScriptRoot
    }

    if ($MyInvocation.MyCommand.Path) {
        return Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    throw "Unable to resolve script root."
}

# 解析仓库根目录。
function Get-RepoRoot {
    $scriptRoot = Get-ScriptRoot
    return Split-Path -Parent $scriptRoot
}

# 解析 Godot 工程目录，未显式传参时默认落到仓库内 `godot/`。
function Resolve-ProjectDir {
    param([string]$ProjectDir)

    if (-not [string]::IsNullOrWhiteSpace($ProjectDir)) {
        return $ProjectDir
    }

    return (Join-Path (Get-RepoRoot) "godot")
}

# 解析 Godot 可执行文件路径，优先级为显式参数、环境变量、默认本地路径。
function Resolve-GodotExe {
    param([string]$GodotExe)

    if ([string]::IsNullOrWhiteSpace($GodotExe)) {
        if (-not [string]::IsNullOrWhiteSpace($env:GODOT_EXE)) {
            $GodotExe = $env:GODOT_EXE
        } else {
            $GodotExe = $script:DefaultGodotExe
        }
    }

    Assert-PathExists -Path $GodotExe -Label "Godot executable"
    return $GodotExe
}

# 断言目标路径存在，不存在时给出带语义标签的错误。
function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

# 如果路径存在则删除，供导出前清理旧产物使用。
function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

# 重建目录，确保目录内容是全新的。
function Reset-Directory {
    param([string]$Path)

    Remove-PathIfExists -Path $Path
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

# 用正则精确替换配置文件中的单个键值，避免手写整文件模板。
function Set-RegexValue {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Replacement
    )

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

# 从 `project.godot` 提取当前构建需要的项目元数据。
function Get-ProjectMetadata {
    param([string]$ProjectDir)

    $projectSettingsPath = Join-Path $ProjectDir "project.godot"
    Assert-PathExists -Path $projectSettingsPath -Label "project.godot"

    $projectSettings = Get-Content -LiteralPath $projectSettingsPath -Raw -Encoding UTF8

    function Get-MatchValue {
        param(
            [string]$Pattern,
            [string]$Label,
            [string]$DefaultValue = ""
        )

        $match = [regex]::Match(
            $projectSettings,
            $Pattern,
            [System.Text.RegularExpressions.RegexOptions]::Multiline)

        if ($match.Success) {
            return $match.Groups[1].Value
        }

        if ($PSBoundParameters.ContainsKey("DefaultValue")) {
            return $DefaultValue
        }

        throw "Failed to resolve $Label from project.godot"
    }

    $usesDotNet = $projectSettings -match '^\[dotnet\]\r?$' -or $projectSettings -match 'config/features=PackedStringArray\([^\)]*"C#"'

    return [pscustomobject]@{
        ProjectName = Get-MatchValue -Pattern '^\s*config/name="([^"]+)"\r?$' -Label "config/name"
        VersionName = Get-MatchValue -Pattern '^\s*version_name="([^"]+)"\r?$' -Label "version_name"
        VersionCode = [int](Get-MatchValue -Pattern '^\s*version_code=(\d+)\r?$' -Label "version_code")
        PackageName = Get-MatchValue -Pattern '^\s*package_name="([^"]+)"\r?$' -Label "package_name"
        ManifestOrientation = Get-MatchValue -Pattern '^\s*manifest_orientation="([^"]+)"\r?$' -Label "manifest_orientation"
        UsesDotNet = $usesDotNet
    }
}

# 统一回写版本号、包名和方向设置，保证脚本与导出元数据一致。
function Update-ProjectBuildMetadata {
    param(
        [string]$ProjectDir,
        [string]$VersionName,
        [Nullable[int]]$VersionCode = $null,
        [string]$PackageName,
        [string]$ManifestOrientation
    )

    $projectSettingsPath = Join-Path $ProjectDir "project.godot"
    Assert-PathExists -Path $projectSettingsPath -Label "project.godot"

    if (-not [string]::IsNullOrWhiteSpace($VersionName)) {
        Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*version_name="[^"]*"\r?$' -Replacement "version_name=`"$VersionName`""
    }

    if ($PSBoundParameters.ContainsKey("VersionCode") -and $null -ne $VersionCode) {
        Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*version_code=\d+\r?$' -Replacement "version_code=$VersionCode"
    }

    if (-not [string]::IsNullOrWhiteSpace($PackageName)) {
        Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*package_name="[^"]*"\r?$' -Replacement "package_name=`"$PackageName`""
    }

    if (-not [string]::IsNullOrWhiteSpace($ManifestOrientation)) {
        Set-RegexValue -Path $projectSettingsPath -Pattern '^\s*manifest_orientation="[^"]*"\r?$' -Replacement "manifest_orientation=`"$ManifestOrientation`""
    }
}

# 等待导出文件稳定，避免刚生成就被后续打包步骤读取半成品。
function Wait-ForStableFile {
    param(
        [string]$Path,
        [int]$TimeoutSeconds = 30,
        [int]$StableSeconds = 2
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastFingerprint = ""
    $stableSince = $null

    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            $item = Get-Item -LiteralPath $Path
            $fingerprint = "$($item.Length)|$($item.LastWriteTimeUtc.Ticks)"

            if ($fingerprint -eq $lastFingerprint) {
                if ($null -ne $stableSince -and ((Get-Date) - $stableSince).TotalSeconds -ge $StableSeconds) {
                    return
                }
            } else {
                $lastFingerprint = $fingerprint
                $stableSince = Get-Date
            }
        }

        Start-Sleep -Milliseconds 500
    }

    Assert-PathExists -Path $Path -Label "Export artifact"
}

# 清理 Godot 导出过程遗留的临时文件，减少误打包风险。
function Remove-TransientExportFiles {
    param([string]$RootPath)

    if (-not (Test-Path -LiteralPath $RootPath)) {
        return
    }

    Get-ChildItem -LiteralPath $RootPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -eq ".tmp" -or $_.Name -like "*.truncated*.tmp" } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
        }
}

# 清理 Godot Mono 曾生成的旧问题日志，避免误判本次构建结果。
function Clear-GodotMonoBuildIssueFiles {
    $godotMonoBuildLogsRoot = Join-Path $env:APPDATA "Godot\mono\build_logs"
    if (-not (Test-Path -LiteralPath $godotMonoBuildLogsRoot)) {
        return
    }

    Get-ChildItem -LiteralPath $godotMonoBuildLogsRoot -Recurse -Filter "msbuild_issues.csv" -ErrorAction SilentlyContinue |
        ForEach-Object {
            try {
                [System.IO.File]::SetAttributes($_.FullName, [System.IO.FileAttributes]::Normal)
                Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop
            }
            catch {
                Write-Warning "Failed to clear stale Godot Mono build issue file: $($_.FullName)"
            }
        }
}

# 执行一次标准 Godot headless 导出，并验证目标产物已稳定落盘。
function Invoke-GodotExport {
    param(
        [string]$GodotExe,
        [string]$ProjectDir,
        [string]$ExportPreset,
        [string]$ExportPath,
        [ValidateSet("debug", "release")]
        [string]$BuildKind = "release"
    )

    $exportDirectory = Split-Path -Parent $ExportPath
    if (-not [string]::IsNullOrWhiteSpace($exportDirectory)) {
        New-Item -ItemType Directory -Path $exportDirectory -Force | Out-Null
    }

    $exportFlag = if ($BuildKind -eq "debug") { "--export-debug" } else { "--export-release" }

    Write-Step "Run Godot export ($ExportPreset / $BuildKind)"
    & $GodotExe --headless --path $ProjectDir $exportFlag $ExportPreset $ExportPath
    $exitCode = if ($null -ne (Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue)) {
        [int]$global:LASTEXITCODE
    } else {
        0
    }

    if ($exitCode -ne 0) {
        throw "Godot export failed with exit code $exitCode for preset '$ExportPreset'."
    }

    Wait-ForStableFile -Path $ExportPath
}

# 把导出暂存目录压缩成 zip 产物。
function Compress-DirectoryToZip {
    param(
        [string]$SourceDir,
        [string]$ZipPath
    )

    Assert-PathExists -Path $SourceDir -Label "Staging directory"

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    Remove-PathIfExists -Path $ZipPath

    $zipDirectory = Split-Path -Parent $ZipPath
    if (-not [string]::IsNullOrWhiteSpace($zipDirectory)) {
        New-Item -ItemType Directory -Path $zipDirectory -Force | Out-Null
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $SourceDir,
        $ZipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
}

# 为产物生成 SHA256 摘要文件，便于 CI 上传和人工校验。
function Write-Sha256File {
    param([string]$ArtifactPath)

    Assert-PathExists -Path $ArtifactPath -Label "Artifact"

    $hash = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $hashPath = "$ArtifactPath.sha256.txt"
    "$hash  $(Split-Path -Leaf $ArtifactPath)" | Set-Content -LiteralPath $hashPath -Encoding ASCII

    return [pscustomobject]@{
        ArtifactPath = $ArtifactPath
        HashPath = $hashPath
        Sha256 = $hash
    }
}

# 归一化命令行传入的构建目标列表，并校验是否属于支持的平台集合。
function Convert-ToTargetList {
    param([string[]]$Targets)

    $allowedTargets = @("android", "web", "windows")
    $resolvedTargets = New-Object System.Collections.Generic.List[string]

    foreach ($entry in $Targets) {
        if ([string]::IsNullOrWhiteSpace($entry)) {
            continue
        }

        foreach ($part in ($entry -split ",")) {
            $target = $part.Trim().ToLowerInvariant()
            if ([string]::IsNullOrWhiteSpace($target)) {
                continue
            }

            if ($allowedTargets -notcontains $target) {
                throw "Unsupported target '$target'. Allowed values: android, web, windows."
            }

            if (-not $resolvedTargets.Contains($target)) {
                $resolvedTargets.Add($target)
            }
        }
    }

    return [string[]]$resolvedTargets.ToArray()
}

# 列出某个目录下所有文件的相对路径，用于产物摘要输出。
function Get-RelativeChildPaths {
    param([string]$RootPath)

    if (-not (Test-Path -LiteralPath $RootPath)) {
        return @()
    }

    $resolvedRoot = (Resolve-Path -LiteralPath $RootPath).Path
    $rootUri = New-Object System.Uri(($resolvedRoot.TrimEnd('\') + '\'))

    return Get-ChildItem -LiteralPath $RootPath -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $childUri = New-Object System.Uri($_.FullName)
            [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($childUri).ToString()).Replace('/', '\')
        }
}
