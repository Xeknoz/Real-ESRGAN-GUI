[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [switch]$SkipDistBuild,

    [switch]$ForceBackend,

    [switch]$ForceModels,

    [switch]$ForceRestore,

    [switch]$PruneBackendBuildDirectory,

    [string]$BackendGenerator,

    [string]$ModelArchive,

    [string]$ModelDownloadUrl,

    [string]$Version,

    [ValidateSet("x64", "x86")]
    [string]$Architecture = "x64",

    [string]$InnoSetupCompilerPath,

    [string]$DistDir,

    [string]$OutputDir,

    [string]$OutputBaseFilename
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$buildAllScript = Join-Path $scriptRoot "build-all.ps1"
$versionScript = Join-Path $scriptRoot "version.ps1"
$installerScript = Join-Path $repoRoot "packaging\windows\RealESRGAN-GUI.iss"
. $versionScript

function Resolve-FullPath {
    param(
        [string]$Path,
        [string]$BasePath
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Test-PathUnderRoot {
    param(
        [string]$Path,
        [string]$Root
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $normalizedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    return $normalizedPath.Equals($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalizedPath.StartsWith($normalizedRoot + "\", [System.StringComparison]::OrdinalIgnoreCase)
}

function Resolve-InnoCompilerPath {
    param([string]$ExplicitPath)

    $explicit = Resolve-FullPath -Path $ExplicitPath -BasePath $repoRoot
    if ($explicit) {
        if (-not (Test-Path -LiteralPath $explicit -PathType Leaf)) {
            throw "Inno Setup compiler was not found: $explicit"
        }

        return $explicit
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidatePaths = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            return $candidatePath
        }
    }

    throw "ISCC.exe was not found. Install Inno Setup 6 or pass -InnoSetupCompilerPath."
}

$appVersion = Resolve-AppVersion -RepoRoot $repoRoot -VersionOverride $Version
$installerDisplayVersion = if ($appVersion.Channel -eq "dev") {
    "$($appVersion.VersionNumber) Dev"
} else {
    $appVersion.VersionNumber
}
$resolvedOutputBaseFilename = if ([string]::IsNullOrWhiteSpace($OutputBaseFilename)) {
    "Real-ESRGAN-GUI-Setup-$Architecture"
} else {
    $OutputBaseFilename
}
$defaultDistDir = Join-Path (Join-Path "artifacts" "portable") $Architecture
$distDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($DistDir)) { $defaultDistDir } else { $DistDir }) -BasePath $repoRoot
$architectureMarkerPath = Join-Path $distDir "ARCHITECTURE.txt"
$resolvedOutputDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputDir)) { "artifacts\installers" } else { $OutputDir }) -BasePath $repoRoot

if ($Clean -and (Test-Path -LiteralPath $resolvedOutputDir)) {
    if (-not (Test-PathUnderRoot -Path $resolvedOutputDir -Root $repoRoot)) {
        throw "Refusing to clean output directory outside repository: $resolvedOutputDir"
    }

    $installerOutputPath = Join-Path $resolvedOutputDir "$resolvedOutputBaseFilename.exe"
    if (Test-Path -LiteralPath $installerOutputPath -PathType Leaf) {
        Remove-Item -LiteralPath $installerOutputPath -Force
    }
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

if (-not $SkipDistBuild) {
    $buildArgs = @{
        Configuration = $Configuration
        Clean = $Clean
        Architecture = $Architecture
        OutputDir = $distDir
    }
    if ($ForceBackend) {
        $buildArgs["ForceBackend"] = $true
    }
    if ($ForceModels) {
        $buildArgs["ForceModels"] = $true
    }
    if ($ForceRestore) {
        $buildArgs["ForceRestore"] = $true
    }
    if ($PruneBackendBuildDirectory) {
        $buildArgs["PruneBackendBuildDirectory"] = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($BackendGenerator)) {
        $buildArgs["BackendGenerator"] = $BackendGenerator
    }
    if (-not [string]::IsNullOrWhiteSpace($ModelArchive)) {
        $buildArgs["ModelArchive"] = $ModelArchive
    }
    if (-not [string]::IsNullOrWhiteSpace($ModelDownloadUrl)) {
        $buildArgs["ModelDownloadUrl"] = $ModelDownloadUrl
    }
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $buildArgs["Version"] = $Version
    }

    & $buildAllScript @buildArgs
}
else {
    Write-Host "Skipping portable output build because -SkipDistBuild was specified."
}

$requiredDistFiles = @(
    "Launcher.exe",
    "Real-ESRGAN GUI.exe",
    "engine\realesrgan-ncnn-vulkan.exe",
    "VERSION.txt",
    "CHANNEL.txt",
    "PACKAGE_KIND.txt"
)

foreach ($relativePath in $requiredDistFiles) {
    $fullPath = Join-Path $distDir $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Missing portable output file required for installer: $relativePath"
    }
}

if (-not (Test-Path -LiteralPath $architectureMarkerPath -PathType Leaf)) {
    throw "portable output architecture marker is missing. Rebuild with scripts\build-all.ps1 before using -SkipDistBuild."
}

$distArchitecture = (Get-Content -LiteralPath $architectureMarkerPath -TotalCount 1).Trim()
if ($distArchitecture -ne $Architecture) {
    throw "portable output architecture is '$distArchitecture', expected '$Architecture'. Rebuild with -Clean or choose the matching -Architecture."
}

$isccPath = Resolve-InnoCompilerPath -ExplicitPath $InnoSetupCompilerPath
$isccArgs = @(
    "/Qp",
    "/DAppVersion=$($appVersion.VersionNumber)",
    "/DAppFileVersion=$($appVersion.FileVersion)",
    "/DAppDisplayVersion=$installerDisplayVersion",
    "/DAppArchitecture=$Architecture",
    "/DAppSourceDir=$distDir",
    "/O$resolvedOutputDir",
    "/F$resolvedOutputBaseFilename"
)
$isccArgs += $installerScript

Write-Host "Building unsigned installer with Inno Setup..."
& $isccPath @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed."
}

$installerPath = Join-Path $resolvedOutputDir "$resolvedOutputBaseFilename.exe"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Installer output was not found: $installerPath"
}

Write-Host ""
Write-Host "Unsigned installer complete: $installerPath"
