[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [string]$Generator,

    [ValidateSet("x64", "x86")]
    [string]$Architecture = "x64",

    [switch]$PruneBuildDirectory
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$srcDir = Join-Path $repoRoot "third_party\ncnn_src\src"
$buildDir = if ($Architecture -eq "x64") {
    Join-Path $srcDir "build"
} else {
    Join-Path $srcDir "build-$Architecture"
}
$backendStateScript = Join-Path $scriptRoot "backend-state.ps1"
. $backendStateScript
$runtimeEngineDir = Get-BackendRuntimeDir -RepoRoot $repoRoot -Architecture $Architecture
$exeTarget = Join-Path $runtimeEngineDir "realesrgan-ncnn-vulkan.exe"

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

    Assert-SafeBuildDirectory
    Write-Host $Reason
    Remove-Item -LiteralPath $buildDir -Recurse -Force
}

function Assert-SafeBuildDirectory {
    $resolvedSrcDir = [System.IO.Path]::GetFullPath($srcDir).TrimEnd('\')
    $resolvedBuildDir = [System.IO.Path]::GetFullPath($buildDir).TrimEnd('\')
    $expectedLeafName = if ($Architecture -eq "x64") { "build" } else { "build-$Architecture" }
    $actualLeafName = Split-Path -Leaf $resolvedBuildDir

    if ($actualLeafName -ne $expectedLeafName -or
        -not $resolvedBuildDir.StartsWith($resolvedSrcDir + "\", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected backend build directory: $resolvedBuildDir"
    }
}

$generatorName = Resolve-VisualStudioGenerator
$generatorPlatform = if ($Architecture -eq "x64") { "x64" } else { "Win32" }
Write-Host "Using CMake generator: $generatorName ($generatorPlatform)"

function Resolve-VulkanLibraryPath {
    param([ValidateSet("x64", "x86")] [string]$TargetArchitecture)

    if ([string]::IsNullOrWhiteSpace($env:VULKAN_SDK)) {
        throw "VULKAN_SDK is not set. Install Vulkan SDK or set VULKAN_SDK before building the backend."
    }

    $relativeLibraryPath = if ($TargetArchitecture -eq "x64") {
        "Lib\vulkan-1.lib"
    } else {
        "Lib32\vulkan-1.lib"
    }
    $libraryPath = Join-Path $env:VULKAN_SDK $relativeLibraryPath

    if (-not (Test-Path -LiteralPath $libraryPath -PathType Leaf)) {
        throw "Vulkan import library for $TargetArchitecture was not found: $libraryPath. Install a Vulkan SDK that includes $relativeLibraryPath before building this architecture."
    }

    return $libraryPath
}

$vulkanLibraryPath = Resolve-VulkanLibraryPath -TargetArchitecture $Architecture

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

    if (-not $resetReason -and $cacheContent -match 'Vulkan_LIBRARY:FILEPATH=(.+)') {
        $cachedVulkanLibrary = $Matches[1].Trim().Replace('/', '\')
        $expectedVulkanLibrary = $vulkanLibraryPath.Replace('/', '\')
        if (-not $cachedVulkanLibrary.Equals($expectedVulkanLibrary, [System.StringComparison]::OrdinalIgnoreCase)) {
            $resetReason = "CMake cache uses Vulkan library '$cachedVulkanLibrary', expected '$expectedVulkanLibrary'."
        }
    }

    if ($resetReason) {
        Clear-BuildDirectory $resetReason
        New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
    }
}

if (-not (Test-Path -LiteralPath $cacheFile)) {
    Write-Host "Configuring CMake..."
    $cmakeConfigureArgs = @(
        "-S", $srcDir,
        "-B", $buildDir,
        "-G", $generatorName,
        "-A", $generatorPlatform,
        "-DCMAKE_POLICY_VERSION_MINIMUM=3.10"
    )
    $cmakeConfigureArgs += "-DVulkan_LIBRARY=$vulkanLibraryPath"

    & cmake `
        @cmakeConfigureArgs
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
New-Item -ItemType Directory -Force -Path $runtimeEngineDir | Out-Null
Write-Host "Copying $exeSource -> $exeTarget"
Copy-Item -LiteralPath $exeSource -Destination $exeTarget -Force

function Find-VisualStudioRedistFile {
    param(
        [string]$FileName,
        [ValidateSet("x64", "x86")]
        [string]$TargetArchitecture
    )

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
        $vsInstallPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($vsInstallPath)) {
            $redistRoot = Join-Path $vsInstallPath "VC\Redist\MSVC"
            if (Test-Path -LiteralPath $redistRoot) {
                $candidate = Get-ChildItem -LiteralPath $redistRoot -Recurse -File -Filter $FileName -ErrorAction SilentlyContinue |
                    Where-Object {
                        $normalized = $_.FullName.Replace('/', '\')
                        $normalized -match "\\$TargetArchitecture\\" -and
                        $normalized -notmatch "\\onecore\\" -and
                        $normalized -notmatch "\\debug_nonredist\\"
                    } |
                    Sort-Object FullName -Descending |
                    Select-Object -First 1

                if ($candidate) {
                    return $candidate.FullName
                }
            }
        }
    }

    return $null
}

foreach ($runtimeFileName in @("vcomp140.dll")) {
    $runtimeSource = Find-VisualStudioRedistFile -FileName $runtimeFileName -TargetArchitecture $Architecture
    if ($runtimeSource) {
        $runtimeTarget = Join-Path $runtimeEngineDir $runtimeFileName
        Write-Host "Copying $runtimeSource -> $runtimeTarget"
        Copy-Item -LiteralPath $runtimeSource -Destination $runtimeTarget -Force
    }
    elseif (-not (Test-Path -LiteralPath (Join-Path $runtimeEngineDir $runtimeFileName) -PathType Leaf)) {
        throw "$runtimeFileName for $Architecture was not found. Install Visual Studio C++ build tools with OpenMP runtime or copy the matching runtime DLL into $runtimeEngineDir."
    }
}

$nonRedistributableRuntime = Join-Path $runtimeEngineDir "vcomp140d.dll"
if (Test-Path -LiteralPath $nonRedistributableRuntime -PathType Leaf) {
    Write-Host "Removing non-redistributable debug runtime from backend artifacts: $nonRedistributableRuntime"
    Remove-Item -LiteralPath $nonRedistributableRuntime -Force
}

$fingerprint = Write-BackendBuildFingerprint -RepoRoot $repoRoot -Configuration $Configuration -Architecture $Architecture
Write-Host "Backend build fingerprint: $($fingerprint.sourceFingerprint)"
Write-Host "Backend build complete: $exeTarget"

if ($PruneBuildDirectory) {
    Clear-BuildDirectory "Pruning backend build directory..."
}
