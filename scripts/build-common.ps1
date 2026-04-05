Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:DefaultGodotExe = "D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe"

function Write-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-ScriptRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return $PSScriptRoot
    }

    if ($MyInvocation.MyCommand.Path) {
        return Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    throw "Unable to resolve script root."
}

function Get-RepoRoot {
    return Split-Path -Parent (Get-ScriptRoot)
}

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }
}

function Resolve-ProjectDir {
    param([string]$ProjectDir)

    $resolved = if ([string]::IsNullOrWhiteSpace($ProjectDir)) {
        Join-Path (Get-RepoRoot) "godot"
    } else {
        $ProjectDir
    }

    Assert-PathExists -Path $resolved -Label "Godot project directory"
    return (Resolve-Path -LiteralPath $resolved).Path
}

function Resolve-GodotExe {
    param([string]$GodotExe)

    $resolved = if (-not [string]::IsNullOrWhiteSpace($GodotExe)) {
        $GodotExe
    } elseif (-not [string]::IsNullOrWhiteSpace($env:GODOT_EXE)) {
        $env:GODOT_EXE
    } else {
        $script:DefaultGodotExe
    }

    Assert-PathExists -Path $resolved -Label "Godot executable"
    return (Resolve-Path -LiteralPath $resolved).Path
}

function Resolve-GodotNuGetSource {
    param([string]$GodotExe)

    if (-not [string]::IsNullOrWhiteSpace($env:GODOT_NUGET_SOURCE) -and (Test-Path -LiteralPath $env:GODOT_NUGET_SOURCE)) {
        return (Resolve-Path -LiteralPath $env:GODOT_NUGET_SOURCE).Path
    }

    $godotExePath = Resolve-GodotExe -GodotExe $GodotExe
    $godotRoot = Split-Path -Parent $godotExePath
    $candidate = Join-Path $godotRoot "GodotSharp\Tools\nupkgs"
    Assert-PathExists -Path $candidate -Label "Godot NuGet source"
    return (Resolve-Path -LiteralPath $candidate).Path
}

function Set-GodotNuGetSourceEnvironment {
    param([string]$GodotExe)

    $resolvedSource = Resolve-GodotNuGetSource -GodotExe $GodotExe
    $env:GODOT_NUGET_SOURCE = $resolvedSource
    return $resolvedSource
}

function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Remove-PathIfExists -Path $Path
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Set-RegexValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$Replacement
    )

    $content = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $options = [System.Text.RegularExpressions.RegexOptions]::Multiline
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($content, $Pattern, $options)) {
        throw "Failed to find pattern in file: $Path`nPattern: $Pattern"
    }

    $updated = [System.Text.RegularExpressions.Regex]::Replace($content, $Pattern, $Replacement, $options)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $updated, $utf8NoBom)
}

function Get-ProjectMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectDir
    )

    $projectSettingsPath = Join-Path $ProjectDir "project.godot"
    Assert-PathExists -Path $projectSettingsPath -Label "project.godot"

    $projectSettings = Get-Content -LiteralPath $projectSettingsPath -Raw -Encoding UTF8

    function Get-MatchValue {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Pattern,

            [Parameter(Mandatory = $true)]
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

    $usesDotNet =
        ($projectSettings -match '^\[dotnet\]\r?$') -or
        ($projectSettings -match 'config/features=PackedStringArray\([^\)]*"C#"')

    return [pscustomobject]@{
        ProjectName = Get-MatchValue -Pattern '^\s*config/name="([^"]+)"\r?$' -Label "config/name"
        VersionName = Get-MatchValue -Pattern '^\s*version_name="([^"]+)"\r?$' -Label "version_name"
        VersionCode = [int](Get-MatchValue -Pattern '^\s*version_code=(\d+)\r?$' -Label "version_code")
        PackageName = Get-MatchValue -Pattern '^\s*package_name="([^"]+)"\r?$' -Label "package_name"
        ManifestOrientation = Get-MatchValue -Pattern '^\s*manifest_orientation="([^"]+)"\r?$' -Label "manifest_orientation"
        UsesDotNet = $usesDotNet
    }
}

function Update-ProjectBuildMetadata {
    param(
        [Parameter(Mandatory = $true)]
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

function Wait-ForStableFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [int]$TimeoutSeconds = 60,

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

function Clear-GodotMonoBuildIssueFiles {
    $godotMonoBuildLogsRoot = Join-Path $env:APPDATA "Godot\mono\build_logs"
    if (-not (Test-Path -LiteralPath $godotMonoBuildLogsRoot)) {
        return
    }

    $issueFiles = @(Get-ChildItem -LiteralPath $godotMonoBuildLogsRoot -Recurse -Filter "msbuild_issues.csv" -File -ErrorAction SilentlyContinue)
    foreach ($issueFile in $issueFiles) {
        try {
            [System.IO.File]::SetAttributes($issueFile.FullName, [System.IO.FileAttributes]::Normal)
            Remove-Item -LiteralPath $issueFile.FullName -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to clear stale Godot Mono build issue file: $($issueFile.FullName)"
        }
    }
}

function Invoke-GodotExport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$GodotExe,

        [Parameter(Mandatory = $true)]
        [string]$ProjectDir,

        [Parameter(Mandatory = $true)]
        [string]$ExportPreset,

        [Parameter(Mandatory = $true)]
        [string]$ExportPath,

        [ValidateSet("debug", "release")]
        [string]$BuildKind = "release"
    )

    $exportDirectory = Split-Path -Parent $ExportPath
    if (-not [string]::IsNullOrWhiteSpace($exportDirectory)) {
        New-Item -ItemType Directory -Path $exportDirectory -Force | Out-Null
    }

    Set-GodotNuGetSourceEnvironment -GodotExe $GodotExe | Out-Null

    $exportFlag = if ($BuildKind -eq "debug") { "--export-debug" } else { "--export-release" }

    $capturedOutput = New-Object System.Collections.Generic.List[string]
    $stdoutPath = Join-Path $env:TEMP ("godot-export-" + [guid]::NewGuid().ToString("N") + ".stdout.log")
    $stderrPath = Join-Path $env:TEMP ("godot-export-" + [guid]::NewGuid().ToString("N") + ".stderr.log")
    $argumentList = @(
        "--headless"
        "--path"
        $ProjectDir
        $exportFlag
        $ExportPreset
        $ExportPath
    )
    $quotedArgumentString = ($argumentList | ForEach-Object {
            if ($_ -match '[\s"]') {
                '"' + ($_ -replace '"', '\"') + '"'
            } else {
                $_
            }
        }) -join ' '

    Write-Step "Run Godot export ($ExportPreset / $BuildKind)"
    try {
        $process = Start-Process `
            -FilePath $GodotExe `
            -ArgumentList $quotedArgumentString `
            -WorkingDirectory $ProjectDir `
            -NoNewWindow `
            -Wait `
            -PassThru `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath

        foreach ($outputPath in @($stdoutPath, $stderrPath)) {
            if (-not (Test-Path -LiteralPath $outputPath)) {
                continue
            }

            Get-Content -LiteralPath $outputPath -Encoding UTF8 | ForEach-Object {
                $line = [string]$_
                $capturedOutput.Add($line)
                Write-Host $line
            }
        }

        $exitCode = $process.ExitCode
    }
    finally {
        foreach ($tempPath in @($stdoutPath, $stderrPath)) {
            if (Test-Path -LiteralPath $tempPath) {
                Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($exitCode -ne 0) {
        throw "Godot export failed with exit code $exitCode for preset '$ExportPreset'."
    }

    Wait-ForStableFile -Path $ExportPath

    return [pscustomobject]@{
        ExportPath = $ExportPath
        OutputLines = [string[]]$capturedOutput.ToArray()
    }
}

function Compress-DirectoryToZip {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,

        [Parameter(Mandatory = $true)]
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

function Get-TextExcerpt {
    param(
        [string]$Text,
        [int]$MaxChars = 4000
    )

    if ([string]::IsNullOrEmpty($Text)) {
        return ""
    }

    if ($Text.Length -le $MaxChars) {
        return $Text.Trim()
    }

    return $Text.Substring($Text.Length - $MaxChars).Trim()
}

function Invoke-WindowsExportSmokeTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [int]$StartupTimeoutSeconds = 8
    )

    Assert-PathExists -Path $ExecutablePath -Label "Windows executable for smoke test"
    Assert-PathExists -Path $WorkingDirectory -Label "Windows smoke test working directory"

    $stdoutPath = Join-Path $env:TEMP ("tilematcher-windows-smoke-" + [guid]::NewGuid().ToString("N") + ".stdout.log")
    $stderrPath = Join-Path $env:TEMP ("tilematcher-windows-smoke-" + [guid]::NewGuid().ToString("N") + ".stderr.log")
    $logsDirectory = Join-Path $WorkingDirectory "logs"
    $latestLogPath = Join-Path $logsDirectory "latest.log"

    Remove-PathIfExists -Path $logsDirectory

    try {
        $process = Start-Process `
            -FilePath $ExecutablePath `
            -WorkingDirectory $WorkingDirectory `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -PassThru

        Start-Sleep -Seconds $StartupTimeoutSeconds

        $hasExited = $process.HasExited
        $exitCode = if ($hasExited) { $process.ExitCode } else { $null }
        if (-not $hasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit()
        }

        $stdoutText = if (Test-Path -LiteralPath $stdoutPath) {
            Get-Content -LiteralPath $stdoutPath -Raw -Encoding UTF8
        } else {
            ""
        }

        $stderrText = if (Test-Path -LiteralPath $stderrPath) {
            Get-Content -LiteralPath $stderrPath -Raw -Encoding UTF8
        } else {
            ""
        }

        $combinedOutput = (($stdoutText, $stderrText) -join [Environment]::NewLine).Trim()

        if ($combinedOutput -match 'No loader found for resource:' -or
            $combinedOutput -match 'Failed loading scene:' -or
            $combinedOutput -match "Can't load dependency:") {
            $excerpt = Get-TextExcerpt -Text $combinedOutput
            throw "Windows smoke test detected startup resource loading errors.`n$excerpt"
        }

        if ($hasExited -and $exitCode -ne 0) {
            $excerpt = Get-TextExcerpt -Text $combinedOutput
            throw "Windows smoke test exited early with code $exitCode.`n$excerpt"
        }

        Assert-PathExists -Path $logsDirectory -Label "Windows smoke test log directory"
        Assert-PathExists -Path $latestLogPath -Label "Windows smoke test latest log"

        return [pscustomobject]@{
            LatestLogPath = $latestLogPath
            StdoutText = $stdoutText
            StderrText = $stderrText
            ExitedEarly = $hasExited
            ExitCode = $exitCode
        }
    }
    finally {
        foreach ($path in @($stdoutPath, $stderrPath)) {
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function Write-Sha256File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArtifactPath
    )

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
