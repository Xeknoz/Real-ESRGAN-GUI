[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [string]$Generator
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$srcDir = Join-Path $repoRoot "third_party\ncnn_src\src"
$buildDir = Join-Path $srcDir "build"
$runtimeEngineDir = Join-Path $repoRoot "runtime\engine"
$exeTarget = Join-Path $runtimeEngineDir "realesrgan-ncnn-vulkan.exe"
$backendStateScript = Join-Path $scriptRoot "backend-state.ps1"
. $backendStateScript

# Verify cmake is available
$cmake = Get-Command cmake -ErrorAction SilentlyContinue
if (-not $cmake) {
    throw "cmake not found on PATH. Install CMake or add it to PATH (e.g. from Visual Studio Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin)."
}

$cmakeVersion = & cmake --version | Select-Object -First 1
Write-Host "Using $cmakeVersion"

function Resolve-VisualStudioGenerator {
    if (-not [string]::IsNullOrWhiteSpace($Generator)) {
        return $Generator
    }

    $vswhere = Get-Command vswhere -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $vswhere) {
        foreach ($candidate in @(
            (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
            (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
        )) {
            if (Test-Path -LiteralPath $candidate) {
                $vswhere = $candidate
                break
            }
        }
    }

    if ($vswhere) {
        $installVersion = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationVersion
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($installVersion)) {
            if ($installVersion.StartsWith("18.", [StringComparison]::Ordinal)) {
                return "Visual Studio 18 2026"
            }

            if ($installVersion.StartsWith("17.", [StringComparison]::Ordinal)) {
                return "Visual Studio 17 2022"
            }
        }
    }

    $capabilitiesJson = & cmake -E capabilities
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to query CMake generator capabilities."
    }

    $capabilities = $capabilitiesJson | ConvertFrom-Json
    $available = @($capabilities.generators | ForEach-Object { $_.name })
    foreach ($candidate in @("Visual Studio 18 2026", "Visual Studio 17 2022")) {
        if ($available -contains $candidate) {
            return $candidate
        }
    }

    throw "No supported Visual Studio CMake generator was found. Install Visual Studio 2022 or newer with C++ build tools."
}

function Clear-BuildDirectory([string]$Reason) {
    if (-not (Test-Path -LiteralPath $buildDir)) {
        return
    }

    Write-Host $Reason
    Remove-Item -LiteralPath $buildDir -Recurse -Force
}

$generatorName = Resolve-VisualStudioGenerator
$generatorPlatform = "x64"
Write-Host "Using CMake generator: $generatorName ($generatorPlatform)"

if ($Clean) {
    Clear-BuildDirectory "Cleaning build directory..."
}

New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$cacheFile = Join-Path $buildDir "CMakeCache.txt"
if (Test-Path -LiteralPath $cacheFile) {
    $cacheContent = Get-Content -LiteralPath $cacheFile -Raw
    $resetReason = $null

    if ($cacheContent -match 'CMAKE_HOME_DIRECTORY:INTERNAL=(.+)') {
        $cachedSrcDir = $Matches[1].Trim().Replace('/', '\')
        $normalizedSrcDir = $srcDir.Replace('/', '\')
        if ($cachedSrcDir -ne $normalizedSrcDir) {
            $resetReason = "CMake cache points to stale source path: $cachedSrcDir"
        }
    }

    if (-not $resetReason -and $cacheContent -match 'CMAKE_GENERATOR:INTERNAL=(.+)') {
        $cachedGenerator = $Matches[1].Trim()
        if ($cachedGenerator -ne $generatorName) {
            $resetReason = "CMake cache uses generator '$cachedGenerator', expected '$generatorName'."
        }
    }

    if (-not $resetReason -and $cacheContent -match 'CMAKE_GENERATOR_PLATFORM:INTERNAL=(.*)') {
        $cachedPlatform = $Matches[1].Trim()
        if ($cachedPlatform -ne $generatorPlatform) {
            $resetReason = "CMake cache uses platform '$cachedPlatform', expected '$generatorPlatform'."
        }
    }

    if ($resetReason) {
        Clear-BuildDirectory $resetReason
        New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
    }
}

if (-not (Test-Path -LiteralPath $cacheFile)) {
    Write-Host "Configuring CMake..."
    & cmake `
        -S $srcDir `
        -B $buildDir `
        -G $generatorName `
        -A $generatorPlatform `
        "-DCMAKE_POLICY_VERSION_MINIMUM=3.5"
    if ($LASTEXITCODE -ne 0) {
        throw "CMake configuration failed."
    }
}

# Build
Write-Host "Building backend ($Configuration)..."
& cmake --build $buildDir --config $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Backend build failed."
}

function Find-BackendExecutable {
    $candidates = @(
        (Join-Path $buildDir "$Configuration\realesrgan-ncnn-vulkan.exe"),
        (Join-Path $buildDir "realesrgan-ncnn-vulkan.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $found = Get-ChildItem -LiteralPath $buildDir -Recurse -File -Filter "realesrgan-ncnn-vulkan.exe" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($found) {
        return $found.FullName
    }

    throw "Build succeeded but realesrgan-ncnn-vulkan.exe was not found under: $buildDir"
}

$exeSource = Find-BackendExecutable
Write-Host "Copying $exeSource -> $exeTarget"
Copy-Item -LiteralPath $exeSource -Destination $exeTarget -Force

$fingerprint = Write-BackendBuildFingerprint -RepoRoot $repoRoot -Configuration $Configuration
Write-Host "Backend build fingerprint: $($fingerprint.sourceFingerprint)"
Write-Host "Backend build complete: $exeTarget"
