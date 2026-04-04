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

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
