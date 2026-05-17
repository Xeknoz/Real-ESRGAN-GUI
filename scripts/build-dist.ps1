[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [string]$Version,

    [ValidateSet("x64", "x86")]
    [string]$Architecture = "x64",

    [string]$OutputDir
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

$appVersion = Resolve-AppVersion -RepoRoot $repoRoot -VersionOverride $Version
$runtimeIdentifier = if ($Architecture -eq "x64") { "win-x64" } else { "win-x86" }
$backendRuntimeDir = Get-BackendRuntimeDir -RepoRoot $repoRoot -Architecture $Architecture
$modelArtifactDir = Join-Path (Join-Path $repoRoot "artifacts") "models"
$defaultOutputDir = Join-Path (Join-Path "artifacts" "portable") $Architecture
$distDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputDir)) { $defaultOutputDir } else { $OutputDir }) -BasePath $repoRoot
$licenseRoot = Join-Path $distDir "licenses"
$architectureMarkerPath = Join-Path $distDir "ARCHITECTURE.txt"

$requiredPayload = @(
    @{ Root = $backendRuntimeDir; RelativePath = "realesrgan-ncnn-vulkan.exe" },
    @{ Root = $backendRuntimeDir; RelativePath = "vcomp140.dll" },
    @{ Root = $backendRuntimeDir; RelativePath = "vcomp140d.dll" },
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

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $runtimeIdentifier,
    "--self-contained", "true",
    "-o", $distDir,
    "-p:Platform=$Architecture",
    "-p:Version=$($appVersion.PackageVersion)",
    "-p:AssemblyVersion=$($appVersion.AssemblyVersion)",
    "-p:FileVersion=$($appVersion.FileVersion)",
    "-p:InformationalVersion=$($appVersion.InformationalVersion)",
    "-p:IncludeSourceRevisionInInformationalVersion=false",
    "-p:SourceRevisionId=",
    "-p:ApplicationManifest=$generatedManifestPath"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Write-Host "[3/4] Copying launcher into portable output..."
Copy-Item -LiteralPath $launcherExe -Destination (Join-Path $distDir "Launcher.exe") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $distDir "engine") | Out-Null
foreach ($runtimeFileName in @("realesrgan-ncnn-vulkan.exe", "vcomp140.dll", "vcomp140d.dll")) {
    Copy-Item -LiteralPath (Join-Path $backendRuntimeDir $runtimeFileName) -Destination (Join-Path $distDir "engine\$runtimeFileName") -Force
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
    $architectureMarkerPath,
    $Architecture + [Environment]::NewLine,
    $utf8NoBom)

Write-Host "[4/4] Copying license notices into portable output..."
New-Item -ItemType Directory -Force -Path $licenseRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $distDir "LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md") -Destination (Join-Path $distDir "THIRD_PARTY_NOTICES.md") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "licenses\Real-ESRGAN-LICENSE.txt") -Destination (Join-Path $licenseRoot "Real-ESRGAN-LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "third_party\ncnn_src\LICENSE") -Destination (Join-Path $licenseRoot "Real-ESRGAN-ncnn-vulkan-Enhanced-LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "third_party\ncnn_src\src\ncnn\LICENSE.txt") -Destination (Join-Path $licenseRoot "ncnn-LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "third_party\ncnn_src\src\libwebp\COPYING") -Destination (Join-Path $licenseRoot "libwebp-COPYING.txt") -Force

Write-Host ""
Write-Host "Build complete: $distDir"
