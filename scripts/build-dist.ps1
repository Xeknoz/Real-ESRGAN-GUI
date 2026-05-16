[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\RealESRGAN-GUI\RealESRGAN-GUI.csproj"
$launcherBuildScript = Join-Path $repoRoot "src\Launcher\build.ps1"
$launcherExe = Join-Path $repoRoot "src\Launcher\bin\Launcher.exe"
$distDir = Join-Path $repoRoot "dist"
$runtimeRoot = Join-Path $repoRoot "runtime"
$licenseRoot = Join-Path $distDir "licenses"

$requiredPayload = @(
    "input.jpg",
    "engine\realesrgan-ncnn-vulkan.exe",
    "engine\vcomp140.dll",
    "engine\vcomp140d.dll",
    "engine\models\realesr-animevideov3-x2.bin",
    "engine\models\realesr-animevideov3-x2.param",
    "engine\models\realesr-animevideov3-x3.bin",
    "engine\models\realesr-animevideov3-x3.param",
    "engine\models\realesr-animevideov3-x4.bin",
    "engine\models\realesr-animevideov3-x4.param",
    "engine\models\realesrgan-x4plus-anime.bin",
    "engine\models\realesrgan-x4plus-anime.param",
    "engine\models\realesrgan-x4plus.bin",
    "engine\models\realesrgan-x4plus.param"
)

foreach ($relativePath in $requiredPayload) {
    $fullPath = Join-Path $runtimeRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Missing runtime payload file: $relativePath"
    }
}

if ($Clean -and (Test-Path -LiteralPath $distDir)) {
    Remove-Item -LiteralPath $distDir -Recurse -Force
}

Write-Host "[1/4] Building native launcher..."
& $launcherBuildScript

Write-Host "[2/4] Publishing WPF application..."
dotnet publish $projectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $distDir

Write-Host "[3/4] Copying launcher into dist..."
Copy-Item -LiteralPath $launcherExe -Destination (Join-Path $distDir "Launcher.exe") -Force

Write-Host "[4/4] Copying license notices into dist..."
New-Item -ItemType Directory -Force -Path $licenseRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination (Join-Path $distDir "LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md") -Destination (Join-Path $distDir "THIRD_PARTY_NOTICES.md") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "third_party\ncnn_src\LICENSE") -Destination (Join-Path $licenseRoot "Real-ESRGAN-ncnn-vulkan-Enhanced-LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "third_party\ncnn_src\src\ncnn\LICENSE.txt") -Destination (Join-Path $licenseRoot "ncnn-LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "third_party\ncnn_src\src\libwebp\COPYING") -Destination (Join-Path $licenseRoot "libwebp-COPYING.txt") -Force

Write-Host ""
Write-Host "Build complete: $distDir"
