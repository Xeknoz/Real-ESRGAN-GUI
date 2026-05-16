[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [switch]$ForceBackend,

    [string]$BackendGenerator,

    [string]$Version
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$backendScript = Join-Path $scriptRoot "build-backend.ps1"
$backendStateScript = Join-Path $scriptRoot "backend-state.ps1"
$distScript = Join-Path $scriptRoot "build-dist.ps1"
. $backendStateScript

Write-Host "========== Step 1/2: Backend =========="
$backendStatus = Get-BackendBuildStatus -RepoRoot $repoRoot -Configuration $Configuration
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
    }
    if (-not [string]::IsNullOrWhiteSpace($BackendGenerator)) {
        $backendArgs["Generator"] = $BackendGenerator
    }

    & $backendScript @backendArgs
}
else {
    Write-Host "Backend unchanged; skipping backend build."
}

Write-Host ""
Write-Host "========== Step 2/2: Build Dist =========="
$distArgs = @{
    Configuration = $Configuration
    Clean = $Clean
}
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $distArgs["Version"] = $Version
}

& $distScript @distArgs

Write-Host ""
Write-Host "========== Build All Complete =========="
Write-Host "Output: $repoRoot\dist\"
Write-Host "  Launcher.exe        - Native splash launcher"
Write-Host "  Real-ESRGAN GUI.exe - WPF frontend"
Write-Host "  engine\              - Backend + models + runtime DLLs"
