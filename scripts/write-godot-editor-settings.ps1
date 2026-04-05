[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$JavaHome,
    [Parameter(Mandatory = $true)]
    [string]$AndroidSdkRoot,
    [Parameter(Mandatory = $true)]
    [string]$DebugKeystorePath,
    [string]$DebugKeystorePassword = "android",
    [string]$SettingsPath = (Join-Path $env:APPDATA "Godot\editor_settings-4.6.tres")
)

# 生成 Godot 编辑器导出所需的 Android 编辑器设置文件。
# 主要用于 CI 或新环境快速落一份最小可用配置。
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# 把 Windows 路径转换成 Godot 配置文件可安全写入的字符串字面量。
function Convert-ToGodotStringLiteral {
    param([string]$Value)

    return $Value.Replace("\", "\\").Replace('"', '\"')
}

$settingsDirectory = Split-Path -Parent $SettingsPath
New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null

$javaHomeValue = Convert-ToGodotStringLiteral -Value $JavaHome
$androidSdkRootValue = Convert-ToGodotStringLiteral -Value $AndroidSdkRoot
$debugKeystorePathValue = Convert-ToGodotStringLiteral -Value ($DebugKeystorePath.Replace("\", "/"))
$debugKeystorePasswordValue = Convert-ToGodotStringLiteral -Value $DebugKeystorePassword

$settingsContent = @"
[gd_resource type="EditorSettings" format=3]

[resource]
export/android/debug_keystore = "$debugKeystorePathValue"
export/android/debug_keystore_pass = "$debugKeystorePasswordValue"
export/android/java_sdk_path = "$javaHomeValue"
export/android/android_sdk_path = "$androidSdkRootValue"
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($SettingsPath, $settingsContent, $utf8NoBom)

Write-Host "Godot editor settings written: $SettingsPath" -ForegroundColor Green
