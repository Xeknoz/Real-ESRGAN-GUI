[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64", "x86")]
    [string[]]$Architecture = @("x64", "x86"),

    [switch]$NoClean,

    [switch]$SkipInstaller,

    [switch]$ForceBackend,

    [switch]$ForceModels,

    [switch]$ForceRestore,

    [switch]$KeepBackendBuildDirectory,

    [string]$BackendGenerator,

    [string]$ModelArchive,

    [string]$ModelDownloadUrl,

    [string]$Version,

    [string]$InnoSetupCompilerPath,

    [string]$PortableRoot,

    [string]$InstallerOutputDir,

    [switch]$BuildEnigma,

    [string]$EnigmaConsolePath,

    [string]$EnigmaOutputDir,

    [switch]$EnigmaCompressFiles,

    [switch]$NoStopRunningApps
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$buildAllScript = Join-Path $scriptRoot "build-all.ps1"
$buildInstallerScript = Join-Path $scriptRoot "build-installer.ps1"
$buildEnigmaScript = Join-Path $scriptRoot "build-enigma.ps1"

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

    throw "ISCC.exe was not found. Install Inno Setup 6 or pass -InnoSetupCompilerPath. Use -SkipInstaller to build portable folders only."
}

function Close-RunningAppProcesses {
    $processes = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -in @("Launcher", "Real-ESRGAN GUI") })

    if ($processes.Count -eq 0) {
        return
    }

    if ($NoStopRunningApps) {
        $names = ($processes | ForEach-Object { "$($_.ProcessName) ($($_.Id))" }) -join ", "
        throw "Close running app processes before building release artifacts: $names"
    }

    Write-Host "Closing running app processes that can lock portable output..."
    foreach ($process in $processes) {
        Stop-Process -Id $process.Id -Force
    }
}

function Add-SwitchArgument {
    param(
        [hashtable]$Arguments,
        [string]$Name,
        [bool]$Enabled
    )

    if ($Enabled) {
        $Arguments[$Name] = $true
    }
}

function Add-StringArgument {
    param(
        [hashtable]$Arguments,
        [string]$Name,
        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        $Arguments[$Name] = $Value
    }
}

function Assert-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing ${Description}: $Path"
    }
}

function Get-UniqueArchitectures {
    $result = @()
    foreach ($item in $Architecture) {
        if ($result -notcontains $item) {
            $result += $item
        }
    }

    return $result
}

$clean = -not $NoClean
$pruneBackendBuildDirectory = -not $KeepBackendBuildDirectory
$architectures = @(Get-UniqueArchitectures)
$defaultPortableRoot = if ($SkipInstaller -and -not $BuildEnigma) { "artifacts\portable" } else { "artifacts\intermediate\portable" }
$resolvedPortableRoot = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($PortableRoot)) { $defaultPortableRoot } else { $PortableRoot }) -BasePath $repoRoot
$resolvedInstallerOutputDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($InstallerOutputDir)) { "artifacts\installers" } else { $InstallerOutputDir }) -BasePath $repoRoot
$resolvedEnigmaOutputDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($EnigmaOutputDir)) { "artifacts\portable-enigma" } else { $EnigmaOutputDir }) -BasePath $repoRoot
$isccPath = $null

if (-not $SkipInstaller) {
    $isccPath = Resolve-InnoCompilerPath -ExplicitPath $InnoSetupCompilerPath
    Write-Host "Using Inno Setup: $isccPath"
}

Close-RunningAppProcesses

$outputs = New-Object System.Collections.Generic.List[object]

for ($index = 0; $index -lt $architectures.Count; $index++) {
    $arch = $architectures[$index]
    $step = $index + 1
    Write-Host ""
    Write-Host "========== Release $step/$($architectures.Count): $arch =========="

    $portableDir = Join-Path $resolvedPortableRoot $arch

    if ($SkipInstaller) {
        $buildArgs = @{
            Configuration = $Configuration
            Architecture = $arch
            OutputDir = $portableDir
        }
        Add-SwitchArgument -Arguments $buildArgs -Name "Clean" -Enabled $clean
        Add-SwitchArgument -Arguments $buildArgs -Name "ForceBackend" -Enabled $ForceBackend
        Add-SwitchArgument -Arguments $buildArgs -Name "ForceModels" -Enabled $ForceModels
        Add-SwitchArgument -Arguments $buildArgs -Name "ForceRestore" -Enabled $ForceRestore
        Add-SwitchArgument -Arguments $buildArgs -Name "PruneBackendBuildDirectory" -Enabled $pruneBackendBuildDirectory
        Add-StringArgument -Arguments $buildArgs -Name "BackendGenerator" -Value $BackendGenerator
        Add-StringArgument -Arguments $buildArgs -Name "ModelArchive" -Value $ModelArchive
        Add-StringArgument -Arguments $buildArgs -Name "ModelDownloadUrl" -Value $ModelDownloadUrl
        Add-StringArgument -Arguments $buildArgs -Name "Version" -Value $Version

        & $buildAllScript @buildArgs
    }
    else {
        $installerArgs = @{
            Configuration = $Configuration
            Architecture = $arch
            DistDir = $portableDir
            OutputDir = $resolvedInstallerOutputDir
            InnoSetupCompilerPath = $isccPath
        }
        Add-SwitchArgument -Arguments $installerArgs -Name "Clean" -Enabled $clean
        Add-SwitchArgument -Arguments $installerArgs -Name "ForceBackend" -Enabled $ForceBackend
        Add-SwitchArgument -Arguments $installerArgs -Name "ForceModels" -Enabled $ForceModels
        Add-SwitchArgument -Arguments $installerArgs -Name "ForceRestore" -Enabled $ForceRestore
        Add-SwitchArgument -Arguments $installerArgs -Name "PruneBackendBuildDirectory" -Enabled $pruneBackendBuildDirectory
        Add-StringArgument -Arguments $installerArgs -Name "BackendGenerator" -Value $BackendGenerator
        Add-StringArgument -Arguments $installerArgs -Name "ModelArchive" -Value $ModelArchive
        Add-StringArgument -Arguments $installerArgs -Name "ModelDownloadUrl" -Value $ModelDownloadUrl
        Add-StringArgument -Arguments $installerArgs -Name "Version" -Value $Version

        & $buildInstallerScript @installerArgs
    }

    $architectureMarkerPath = Join-Path $portableDir "ARCHITECTURE.txt"
    Assert-RequiredFile -Path (Join-Path $portableDir "Launcher.exe") -Description "$arch Launcher.exe"
    Assert-RequiredFile -Path (Join-Path $portableDir "Real-ESRGAN GUI.exe") -Description "$arch Real-ESRGAN GUI.exe"
    Assert-RequiredFile -Path (Join-Path $portableDir "engine\realesrgan-ncnn-vulkan.exe") -Description "$arch backend executable"
    Assert-RequiredFile -Path (Join-Path $portableDir "VERSION.txt") -Description "$arch VERSION.txt"
    Assert-RequiredFile -Path (Join-Path $portableDir "CHANNEL.txt") -Description "$arch CHANNEL.txt"
    Assert-RequiredFile -Path $architectureMarkerPath -Description "$arch ARCHITECTURE.txt"

    $distArchitecture = (Get-Content -LiteralPath $architectureMarkerPath -TotalCount 1).Trim()
    if ($distArchitecture -ne $arch) {
        throw "Portable output architecture is '$distArchitecture', expected '$arch': $portableDir"
    }

    $installerPath = $null
    if (-not $SkipInstaller) {
        $installerPath = Join-Path $resolvedInstallerOutputDir "Real-ESRGAN-GUI-Setup-$arch.exe"
        Assert-RequiredFile -Path $installerPath -Description "$arch installer"
    }

    $enigmaPath = $null
    if ($BuildEnigma) {
        $enigmaArgs = @{
            Configuration = $Configuration
            Architecture = $arch
            DistDir = $portableDir
            OutputDir = $resolvedEnigmaOutputDir
            SkipDistBuild = $true
        }
        Add-SwitchArgument -Arguments $enigmaArgs -Name "Clean" -Enabled $clean
        Add-SwitchArgument -Arguments $enigmaArgs -Name "CompressFiles" -Enabled $EnigmaCompressFiles
        Add-StringArgument -Arguments $enigmaArgs -Name "EnigmaConsolePath" -Value $EnigmaConsolePath

        & $buildEnigmaScript @enigmaArgs

        $enigmaPath = Join-Path $resolvedEnigmaOutputDir "Real-ESRGAN-GUI-Portable-$arch.exe"
        Assert-RequiredFile -Path $enigmaPath -Description "$arch Enigma portable executable"
    }

    $outputs.Add([pscustomobject]@{
        Architecture = $arch
        Portable = $portableDir
        Installer = $installerPath
        EnigmaPortable = $enigmaPath
    })
}

Write-Host ""
Write-Host "========== Release Artifacts Complete =========="
foreach ($output in $outputs) {
    Write-Host "Architecture: $($output.Architecture)"
    Write-Host "  Portable : $($output.Portable)"
    if ($output.Installer) {
        Write-Host "  Installer: $($output.Installer)"
    }
    if ($output.EnigmaPortable) {
        Write-Host "  Enigma   : $($output.EnigmaPortable)"
    }
}
