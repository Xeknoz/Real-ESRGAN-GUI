[CmdletBinding()]
param(
    [string]$Version,

    [string]$FileVersion,

    [string]$ProductVersion,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

$launcherRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $launcherRoot)
$sourcePath = Join-Path $launcherRoot "Launcher.c"
$resourcePath = Join-Path $launcherRoot "Launcher.rc"
$binDir = Join-Path $launcherRoot "bin"
$objDir = Join-Path $launcherRoot "obj"
$outputPath = Join-Path $binDir "Launcher.exe"
$objectPath = Join-Path $objDir "Launcher.obj"
$generatedHeaderPath = Join-Path $objDir "Launcher.version.h"
$generatedResourcePath = Join-Path $objDir "Launcher.generated.rc"
$compiledResourcePath = Join-Path $objDir "Launcher.res"
$fingerprintPath = Join-Path $objDir "Launcher.buildfingerprint.json"

function ConvertTo-RcNumericVersion([string]$Value) {
    if ($Value -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Launcher FileVersion must be a four-part numeric version, got '$Value'."
    }

    return (($Value -split '\.') | ForEach-Object { [int]$_ }) -join ","
}

function ConvertTo-RcString([string]$Value) {
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

function ConvertTo-CWideString([string]$Value) {
    return 'L"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"'
}

function Get-Sha256ForText([string]$Text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join "")
    }
    finally {
        $sha.Dispose()
    }
}

function Get-Sha256ForFile([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if ([string]::IsNullOrWhiteSpace($FileVersion) -or [string]::IsNullOrWhiteSpace($ProductVersion)) {
    $versionScript = Join-Path $repoRoot "scripts\version.ps1"
    if (-not (Test-Path -LiteralPath $versionScript)) {
        throw "Version metadata was not provided and version.ps1 was not found."
    }

    . $versionScript
    $appVersion = Resolve-AppVersion -RepoRoot $repoRoot -VersionOverride $Version

    if ([string]::IsNullOrWhiteSpace($FileVersion)) {
        $FileVersion = $appVersion.FileVersion
    }

    if ([string]::IsNullOrWhiteSpace($ProductVersion)) {
        $ProductVersion = $appVersion.InformationalVersion
    }
}

New-Item -ItemType Directory -Force -Path $binDir, $objDir | Out-Null
$numericVersion = ConvertTo-RcNumericVersion $FileVersion
$fileVersionText = ConvertTo-RcString $FileVersion
$productVersionText = ConvertTo-RcString $ProductVersion
$displayVersion = if ($ProductVersion.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) {
    $ProductVersion
} else {
    "v$ProductVersion"
}
$generatedHeaderContent = @"
#pragma once
#define LAUNCHER_DISPLAY_VERSION $(ConvertTo-CWideString $displayVersion)
"@ + [Environment]::NewLine

$baseResource = Get-Content -LiteralPath $resourcePath -Raw
$versionResource = @"

1 VERSIONINFO
 FILEVERSION $numericVersion
 PRODUCTVERSION $numericVersion
 FILEFLAGSMASK 0x3fL
 FILEFLAGS 0x0L
 FILEOS 0x40004L
 FILETYPE 0x1L
 FILESUBTYPE 0x0L
BEGIN
    BLOCK "StringFileInfo"
    BEGIN
        BLOCK "040904B0"
        BEGIN
            VALUE "CompanyName", "Xeknoz\0"
            VALUE "FileDescription", "Real-ESRGAN GUI Launcher\0"
            VALUE "FileVersion", "$fileVersionText\0"
            VALUE "InternalName", "Launcher.exe\0"
            VALUE "OriginalFilename", "Launcher.exe\0"
            VALUE "ProductName", "Real-ESRGAN GUI\0"
            VALUE "ProductVersion", "$productVersionText\0"
        END
    END
    BLOCK "VarFileInfo"
    BEGIN
        VALUE "Translation", 0x0409, 1200
    END
END
"@

$generatedResourceContent = $baseResource.TrimEnd() + [Environment]::NewLine + $versionResource + [Environment]::NewLine
$inputParts = New-Object System.Collections.Generic.List[string]
$inputParts.Add("schema=1")
$inputParts.Add("fileVersion=$FileVersion")
$inputParts.Add("productVersion=$ProductVersion")
$inputParts.Add("displayVersion=$displayVersion")
$inputParts.Add("source=$(Get-Sha256ForFile $sourcePath)")
$inputParts.Add("resource=$(Get-Sha256ForText $generatedResourceContent)")
$inputParts.Add("versionHeader=$(Get-Sha256ForText $generatedHeaderContent)")
foreach ($relativePath in @("resource.h", "app.ico")) {
    $inputPath = Join-Path $launcherRoot $relativePath
    if (Test-Path -LiteralPath $inputPath -PathType Leaf) {
        $inputParts.Add("${relativePath}=$(Get-Sha256ForFile $inputPath)")
    }
}
$fingerprint = Get-Sha256ForText ($inputParts -join "`n")

if (-not $Force -and
    (Test-Path -LiteralPath $outputPath -PathType Leaf) -and
    (Test-Path -LiteralPath $fingerprintPath -PathType Leaf)) {
    try {
        $previous = Get-Content -LiteralPath $fingerprintPath -Raw | ConvertFrom-Json
        if ($previous.fingerprint -eq $fingerprint) {
            Write-Host "Launcher unchanged; skipping launcher build."
            Write-Host "Launcher version: $ProductVersion ($FileVersion)"
            return
        }
    }
    catch {
        Write-Host "Launcher build fingerprint is invalid; rebuilding launcher."
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($generatedHeaderPath, $generatedHeaderContent, $utf8NoBom)
[System.IO.File]::WriteAllText($generatedResourcePath, $generatedResourceContent, $utf8NoBom)

# Locate vswhere.exe: try PATH first, then known install locations
$vswhere = Get-Command vswhere -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $vswhere) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe")
        (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            $vswhere = $candidate
            break
        }
    }
}
if (-not $vswhere) {
    throw "vswhere.exe not found. Install Visual Studio Build Tools or Visual Studio with C++ tools."
}

$vsInstallPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ([string]::IsNullOrWhiteSpace($vsInstallPath)) {
    throw "MSVC x64 build tools were not found."
}

$vcvarsPath = Join-Path $vsInstallPath "VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path -LiteralPath $vcvarsPath)) {
    throw "vcvars64.bat not found: $vcvarsPath"
}

$vswhereDir = Split-Path -Parent $vswhere
$command = @(
    "set `"PATH=%PATH%;$vswhereDir`""
    "call `"$vcvarsPath`" >nul"
    "rc.exe /nologo /i `"$launcherRoot`" /fo `"$compiledResourcePath`" `"$generatedResourcePath`""
    "cl.exe /nologo /utf-8 /O2 /W4 /I `"$objDir`" `"$sourcePath`" `"$compiledResourcePath`" /Fo`"$objectPath`" /Fe`"$outputPath`" /link user32.lib gdi32.lib dwmapi.lib advapi32.lib"
) -join " && "

cmd.exe /d /s /c $command
if ($LASTEXITCODE -ne 0) {
    throw "Launcher build failed with exit code $LASTEXITCODE."
}

$fingerprintJson = ([ordered]@{
    schema = 1
    fingerprint = $fingerprint
    fileVersion = $FileVersion
    productVersion = $ProductVersion
    displayVersion = $displayVersion
} | ConvertTo-Json -Depth 3) + [Environment]::NewLine
[System.IO.File]::WriteAllText($fingerprintPath, $fingerprintJson, $utf8NoBom)

Write-Host "Built $outputPath"
Write-Host "Launcher version: $ProductVersion ($FileVersion)"
Write-Host "Launcher splash version: $displayVersion"
