[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [string]$Version,

    [ValidateSet("x64", "x86")]
    [string]$Architecture = "x64",

    [string]$OutputDir,

    [switch]$ForceRestore
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\Real-ESRGAN-GUI\RealESRGAN-GUI.csproj"
$manifestTemplatePath = Join-Path $repoRoot "src\Real-ESRGAN-GUI\app.manifest"
$generatedManifestPath = Join-Path $repoRoot "src\Real-ESRGAN-GUI\obj\generated\app.manifest"
$launcherBuildScript = Join-Path $repoRoot "src\Launcher\build.ps1"
$launcherExe = Join-Path $repoRoot "src\Launcher\bin\Launcher.exe"
$versionScript = Join-Path $scriptRoot "version.ps1"
$backendStateScript = Join-Path $scriptRoot "backend-state.ps1"
. $versionScript
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

function Copy-LicenseFile {
    param(
        [string]$SourcePath,
        [string]$DestinationFileName
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        throw "Missing license notice: $SourcePath"
    }

    Copy-Item -LiteralPath $SourcePath -Destination (Join-Path $licenseRoot $DestinationFileName) -Force
}

function Resolve-DotNetRoot {
    $candidates = @()
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($dotnetCommand -and -not [string]::IsNullOrWhiteSpace($dotnetCommand.Source)) {
        $candidates += Split-Path -Parent $dotnetCommand.Source
    }
    if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT)) {
        $candidates += $env:DOTNET_ROOT
    }
    $candidates += @(
        (Join-Path $env:ProgramFiles "dotnet"),
        (Join-Path ${env:ProgramFiles(x86)} "dotnet")
    )

    foreach ($candidate in $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique) {
        if ((Test-Path -LiteralPath (Join-Path $candidate "LICENSE.txt") -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $candidate "ThirdPartyNotices.txt") -PathType Leaf)) {
            return $candidate
        }
    }

    throw "Unable to locate .NET LICENSE.txt and ThirdPartyNotices.txt. Install a full .NET SDK/runtime before building a self-contained distribution."
}

function Resolve-VisualStudioRedistNotice {
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
            $candidate = Join-Path $vsInstallPath "Licenses\1033\Redist.txt"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }

    throw "Unable to locate Visual Studio Redist.txt. Install Visual Studio C++ build tools before packaging the OpenMP runtime."
}

function Test-NuGetPackageCacheForRuntime {
    param(
        [string]$PackagesPath,
        [string]$RuntimeIdentifier
    )

    if ([string]::IsNullOrWhiteSpace($PackagesPath) -or
        -not (Test-Path -LiteralPath $PackagesPath -PathType Container)) {
        return $false
    }

    $requiredPackages = @(
        "microsoft.netcore.app.runtime.$RuntimeIdentifier",
        "microsoft.windowsdesktop.app.runtime.$RuntimeIdentifier",
        "microsoft.aspnetcore.app.runtime.$RuntimeIdentifier"
    )

    foreach ($packageName in $requiredPackages) {
        $packageRoot = Join-Path $PackagesPath $packageName
        if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
            return $false
        }

        $packageVersion = Get-ChildItem -LiteralPath $packageRoot -Directory -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $packageVersion) {
            return $false
        }
    }

    return $true
}

function Resolve-NuGetPackageCache {
    param([string]$RuntimeIdentifier)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        $candidates += $env:NUGET_PACKAGES
    }
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $candidates += Join-Path $env:USERPROFILE ".nuget\packages"
    }

    $dotNetUserProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    if (-not [string]::IsNullOrWhiteSpace($dotNetUserProfile)) {
        $candidates += Join-Path $dotNetUserProfile ".nuget\packages"
    }
    if (-not [string]::IsNullOrWhiteSpace($env:HOMEDRIVE) -and -not [string]::IsNullOrWhiteSpace($env:HOMEPATH)) {
        $candidates += Join-Path ($env:HOMEDRIVE + $env:HOMEPATH) ".nuget\packages"
    }

    foreach ($candidate in $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique) {
        $fullPath = [System.IO.Path]::GetFullPath($candidate)
        if (Test-NuGetPackageCacheForRuntime -PackagesPath $fullPath -RuntimeIdentifier $RuntimeIdentifier) {
            return $fullPath
        }
    }

    return $null
}

function Use-NuGetPackageCache {
    param([string]$RuntimeIdentifier)

    $packagesPath = Resolve-NuGetPackageCache -RuntimeIdentifier $RuntimeIdentifier
    if ([string]::IsNullOrWhiteSpace($packagesPath)) {
        Write-Host "No local NuGet package cache with $RuntimeIdentifier runtime packs was found; dotnet may restore from configured sources."
        return $null
    }

    if ($env:NUGET_PACKAGES -ne $packagesPath) {
        $env:NUGET_PACKAGES = $packagesPath
        Write-Host "Using local NuGet package cache: $packagesPath"
    }

    return $packagesPath
}

function Test-DotNetRestoreAssets {
    param(
        [string]$AssetsPath,
        [string]$ProjectPath,
        [string]$RuntimeIdentifier,
        [string]$PackagesPath
    )

    if (-not (Test-Path -LiteralPath $AssetsPath -PathType Leaf)) {
        return $false
    }

    $assetsFile = Get-Item -LiteralPath $AssetsPath
    $projectFile = Get-Item -LiteralPath $ProjectPath
    if ($assetsFile.LastWriteTimeUtc -lt $projectFile.LastWriteTimeUtc) {
        return $false
    }

    try {
        $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json
        $targets = @($assets.targets.PSObject.Properties.Name)
    }
    catch {
        return $false
    }

    $errors = @($assets.logs | Where-Object { $_.level -eq "Error" })
    if ($errors.Count -gt 0) {
        return $false
    }

    if ($targets -notcontains "net9.0-windows/$RuntimeIdentifier") {
        return $false
    }

    if (-not [string]::IsNullOrWhiteSpace($PackagesPath)) {
        $normalizedPackagesPath = [System.IO.Path]::GetFullPath($PackagesPath).TrimEnd('\')
        $packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
        $hasPackageFolder = $false
        foreach ($packageFolder in $packageFolders) {
            $normalizedPackageFolder = [System.IO.Path]::GetFullPath($packageFolder).TrimEnd('\')
            if ($normalizedPackageFolder.Equals($normalizedPackagesPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                $hasPackageFolder = $true
                break
            }
        }

        if (-not $hasPackageFolder) {
            return $false
        }
    }

    return $true
}

$appVersion = Resolve-AppVersion -RepoRoot $repoRoot -VersionOverride $Version
$runtimeIdentifier = if ($Architecture -eq "x64") { "win-x64" } else { "win-x86" }
$restoreAssetsPath = Join-Path (Split-Path -Parent $projectPath) "obj\project.assets.json"
$backendRuntimeDir = Get-BackendRuntimeDir -RepoRoot $repoRoot -Architecture $Architecture
$modelArtifactDir = Join-Path (Join-Path $repoRoot "artifacts") "models"
$defaultOutputDir = Join-Path (Join-Path "artifacts" "portable") $Architecture
$distDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputDir)) { $defaultOutputDir } else { $OutputDir }) -BasePath $repoRoot
$licenseRoot = Join-Path $distDir "licenses"
$architectureMarkerPath = Join-Path $distDir "ARCHITECTURE.txt"
$packageKindMarkerPath = Join-Path $distDir "PACKAGE_KIND.txt"
$nuGetPackagesPath = Use-NuGetPackageCache -RuntimeIdentifier $runtimeIdentifier

$requiredPayload = @(
    @{ Root = $backendRuntimeDir; RelativePath = "realesrgan-ncnn-vulkan.exe" },
    @{ Root = $backendRuntimeDir; RelativePath = "vcomp140.dll" },
    @{ Root = $modelArtifactDir; RelativePath = "realesr-animevideov3-x2.bin" },
    @{ Root = $modelArtifactDir; RelativePath = "realesr-animevideov3-x2.param" },
    @{ Root = $modelArtifactDir; RelativePath = "realesr-animevideov3-x3.bin" },
    @{ Root = $modelArtifactDir; RelativePath = "realesr-animevideov3-x3.param" },
    @{ Root = $modelArtifactDir; RelativePath = "realesr-animevideov3-x4.bin" },
    @{ Root = $modelArtifactDir; RelativePath = "realesr-animevideov3-x4.param" },
    @{ Root = $modelArtifactDir; RelativePath = "realesrgan-x4plus-anime.bin" },
    @{ Root = $modelArtifactDir; RelativePath = "realesrgan-x4plus-anime.param" },
    @{ Root = $modelArtifactDir; RelativePath = "realesrgan-x4plus.bin" },
    @{ Root = $modelArtifactDir; RelativePath = "realesrgan-x4plus.param" }
)

foreach ($payload in $requiredPayload) {
    $fullPath = Join-Path $payload.Root $payload.RelativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Missing generated build artifact: $fullPath"
    }
}

if ($Clean -and (Test-Path -LiteralPath $distDir)) {
    if (-not (Test-PathUnderRoot -Path $distDir -Root $repoRoot)) {
        throw "Refusing to clean output directory outside repository: $distDir"
    }

    Remove-Item -LiteralPath $distDir -Recurse -Force
}
elseif ((Test-Path -LiteralPath $architectureMarkerPath -PathType Leaf) -and
    ((Get-Content -LiteralPath $architectureMarkerPath -TotalCount 1).Trim() -ne $Architecture)) {
    throw "Existing portable output architecture does not match '$Architecture'. Re-run with -Clean before switching architectures."
}

Write-Host "[1/4] Building native launcher..."
& $launcherBuildScript `
    -FileVersion $appVersion.FileVersion `
    -ProductVersion $appVersion.InformationalVersion `
    -DisplayVersion $appVersion.DisplayVersion `
    -Architecture $Architecture

Write-Host "[2/4] Publishing WPF application..."
Write-Host "App version: $($appVersion.VersionNumber) ($($appVersion.Channel), $($appVersion.Source), $Architecture)"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $generatedManifestPath) | Out-Null
$manifestContent = Get-Content -LiteralPath $manifestTemplatePath -Raw
$manifestContent = $manifestContent -replace '(<assemblyIdentity version=")[^"]+(" name="Real-ESRGAN\.GUI"/>)', "`${1}$($appVersion.AssemblyVersion)`${2}"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($generatedManifestPath, $manifestContent, $utf8NoBom)

if ($ForceRestore -or -not (Test-DotNetRestoreAssets -AssetsPath $restoreAssetsPath -ProjectPath $projectPath -RuntimeIdentifier $runtimeIdentifier -PackagesPath $nuGetPackagesPath)) {
    Write-Host "Restoring .NET assets..."
    & dotnet restore $projectPath -r $runtimeIdentifier -p:Platform=$Architecture -p:AppChannel=$($appVersion.Channel) -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed."
    }
}
else {
    Write-Host "Using existing .NET restore assets; publishing without restore."
}

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $runtimeIdentifier,
    "--self-contained", "true",
    "--no-restore",
    "-o", $distDir,
    "-p:Platform=$Architecture",
    "-p:Version=$($appVersion.PackageVersion)",
    "-p:AssemblyVersion=$($appVersion.AssemblyVersion)",
    "-p:FileVersion=$($appVersion.FileVersion)",
    "-p:InformationalVersion=$($appVersion.InformationalVersion)",
    "-p:IncludeSourceRevisionInInformationalVersion=false",
    "-p:SourceRevisionId=",
    "-p:AppChannel=$($appVersion.Channel)",
    "-p:ApplicationManifest=$generatedManifestPath"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Write-Host "[3/4] Copying launcher into portable output..."
Copy-Item -LiteralPath $launcherExe -Destination (Join-Path $distDir "Launcher.exe") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $distDir "engine") | Out-Null
foreach ($runtimeFileName in @("realesrgan-ncnn-vulkan.exe", "vcomp140.dll")) {
    Copy-Item -LiteralPath (Join-Path $backendRuntimeDir $runtimeFileName) -Destination (Join-Path $distDir "engine\$runtimeFileName") -Force
}
$nonRedistributableRuntime = Join-Path $distDir "engine\vcomp140d.dll"
if (Test-Path -LiteralPath $nonRedistributableRuntime -PathType Leaf) {
    Remove-Item -LiteralPath $nonRedistributableRuntime -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $distDir "engine\models") | Out-Null
foreach ($modelFile in Get-ChildItem -LiteralPath $modelArtifactDir -File | Where-Object { $_.Extension -in ".bin", ".param" }) {
    Copy-Item -LiteralPath $modelFile.FullName -Destination (Join-Path $distDir "engine\models\$($modelFile.Name)") -Force
}

[System.IO.File]::WriteAllText(
    (Join-Path $distDir "VERSION.txt"),
    $appVersion.VersionNumber + [Environment]::NewLine,
    $utf8NoBom)
[System.IO.File]::WriteAllText(
    (Join-Path $distDir "CHANNEL.txt"),
    $appVersion.Channel + [Environment]::NewLine,
    $utf8NoBom)
[System.IO.File]::WriteAllText(
    $packageKindMarkerPath,
    "portable" + [Environment]::NewLine,
    $utf8NoBom)
[System.IO.File]::WriteAllText(
    $architectureMarkerPath,
    $Architecture + [Environment]::NewLine,
    $utf8NoBom)

Write-Host "[4/4] Copying license notices into portable output..."
New-Item -ItemType Directory -Force -Path $licenseRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $distDir "LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md") -Destination (Join-Path $distDir "THIRD_PARTY_NOTICES.md") -Force
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "licenses\Real-ESRGAN-LICENSE.txt") -DestinationFileName "Real-ESRGAN-LICENSE.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "third_party\ncnn_src\LICENSE") -DestinationFileName "Real-ESRGAN-ncnn-vulkan-Enhanced-LICENSE.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "licenses\dirent-MIT-LICENSE.txt") -DestinationFileName "dirent-MIT-LICENSE.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "licenses\stb-LICENSE.txt") -DestinationFileName "stb-LICENSE.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "third_party\ncnn_src\src\ncnn\LICENSE.txt") -DestinationFileName "ncnn-LICENSE.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "third_party\ncnn_src\src\ncnn\glslang\LICENSE.txt") -DestinationFileName "glslang-LICENSE.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "third_party\ncnn_src\src\libwebp\COPYING") -DestinationFileName "libwebp-COPYING.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "third_party\ncnn_src\src\libwebp\PATENTS") -DestinationFileName "libwebp-PATENTS.txt"
Copy-LicenseFile -SourcePath (Join-Path $repoRoot "third_party\ncnn_src\src\ncnn\python\pybind11\LICENSE") -DestinationFileName "pybind11-LICENSE.txt"

$dotNetRoot = Resolve-DotNetRoot
Copy-LicenseFile -SourcePath (Join-Path $dotNetRoot "LICENSE.txt") -DestinationFileName "Microsoft-dotnet-LICENSE.txt"
Copy-LicenseFile -SourcePath (Join-Path $dotNetRoot "ThirdPartyNotices.txt") -DestinationFileName "Microsoft-dotnet-ThirdPartyNotices.txt"
Copy-LicenseFile -SourcePath (Resolve-VisualStudioRedistNotice) -DestinationFileName "Microsoft-Visual-Cpp-Redistributable-Redist.txt"

Write-Host ""
Write-Host "Build complete: $distDir"
