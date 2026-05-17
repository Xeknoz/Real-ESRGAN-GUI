[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [switch]$ForceBackend,

    [switch]$ForceModels,

    [switch]$PruneBackendBuildDirectory,

    [string]$BackendGenerator,

    [string]$ModelArchive,

    [string]$ModelDownloadUrl,

    [string]$Version,

    [ValidateSet("x64", "x86")]
    [string]$Architecture = "x64",

    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$backendScript = Join-Path $scriptRoot "build-backend.ps1"
$backendStateScript = Join-Path $scriptRoot "backend-state.ps1"
$modelScript = Join-Path $scriptRoot "build-models.ps1"
$distScript = Join-Path $scriptRoot "build-dist.ps1"
. $backendStateScript

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

$defaultOutputDir = Join-Path (Join-Path "artifacts" "portable") $Architecture
$resolvedOutputDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputDir)) { $defaultOutputDir } else { $OutputDir }) -BasePath $repoRoot

Write-Host "========== Step 1/3: Backend =========="
$backendStatus = Get-BackendBuildStatus -RepoRoot $repoRoot -Configuration $Configuration -Architecture $Architecture
if ($ForceBackend -or -not $backendStatus.IsCurrent) {
    if ($ForceBackend) {
        Write-Host "Building backend because -ForceBackend was specified."
    }
    else {
        Write-Host "Building backend because $($backendStatus.Reason)."
    }

    $backendArgs = @{
        Configuration = $Configuration
        Clean = $Clean
        Architecture = $Architecture
    }
    if (-not [string]::IsNullOrWhiteSpace($BackendGenerator)) {
        $backendArgs["Generator"] = $BackendGenerator
    }
    if ($PruneBackendBuildDirectory) {
        $backendArgs["PruneBuildDirectory"] = $true
    }

    & $backendScript @backendArgs
}
else {
    Write-Host "Backend unchanged; skipping backend build."
}

Write-Host ""
Write-Host "========== Step 2/3: Models =========="
$modelArgs = @{
    Clean = $Clean
    Force = $ForceModels
}
if (-not [string]::IsNullOrWhiteSpace($ModelArchive)) {
    $modelArgs["SourceArchive"] = $ModelArchive
}
if (-not [string]::IsNullOrWhiteSpace($ModelDownloadUrl)) {
    $modelArgs["DownloadUrl"] = $ModelDownloadUrl
}

& $modelScript @modelArgs

Write-Host ""
Write-Host "========== Step 3/3: Build Dist =========="
$distArgs = @{
    Configuration = $Configuration
    Clean = $Clean
    Architecture = $Architecture
    OutputDir = $resolvedOutputDir
}
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $distArgs["Version"] = $Version
}

& $distScript @distArgs

Write-Host ""
Write-Host "========== Build All Complete =========="
Write-Host "Architecture: $Architecture"
Write-Host "Output: $resolvedOutputDir"
Write-Host "  Launcher.exe        - Native splash launcher"
Write-Host "  Real-ESRGAN GUI.exe - WPF frontend"
Write-Host "  engine\              - Backend + models + runtime DLLs"
