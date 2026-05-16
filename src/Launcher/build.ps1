[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$launcherRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $launcherRoot "Launcher.c"
$resourcePath = Join-Path $launcherRoot "Launcher.rc"
$binDir = Join-Path $launcherRoot "bin"
$objDir = Join-Path $launcherRoot "obj"
$outputPath = Join-Path $binDir "Launcher.exe"
$objectPath = Join-Path $objDir "Launcher.obj"
$compiledResourcePath = Join-Path $objDir "Launcher.res"

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

New-Item -ItemType Directory -Force -Path $binDir, $objDir | Out-Null

$vswhereDir = Split-Path -Parent $vswhere
$command = @(
    "set `"PATH=%PATH%;$vswhereDir`""
    "call `"$vcvarsPath`" >nul"
    "rc.exe /nologo /fo `"$compiledResourcePath`" `"$resourcePath`""
    "cl.exe /nologo /utf-8 /O2 /W4 `"$sourcePath`" `"$compiledResourcePath`" /Fo`"$objectPath`" /Fe`"$outputPath`" /link user32.lib gdi32.lib dwmapi.lib advapi32.lib"
) -join " && "

cmd.exe /d /s /c $command
if ($LASTEXITCODE -ne 0) {
    throw "Launcher build failed with exit code $LASTEXITCODE."
}

Write-Host "Built $outputPath"
