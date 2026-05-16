#requires -Version 5.1
<#
.SYNOPSIS
    Real-ESRGAN batch image upscaling script

.DESCRIPTION
    Auto-detects script directory, supports custom input/output paths,
    model selection, scale factor, and thread count.
    Provides interactive model selection menu, auto-creates directories,
    and checks for input files before processing.

.PARAMETER InputDir
    Input images directory. Defaults to "%USERPROFILE%\Pictures\Real-ESRGAN_Input".

.PARAMETER OutputDir
    Output images directory. Defaults to "%USERPROFILE%\Pictures\Real-ESRGAN_Output".

.PARAMETER Model
    Model name. Valid values:
    - realesrgan-x4plus (general photos, default)
    - realesrgan-x4plus-anime (anime/illustrations)
    - realesr-animevideov3-x2 (anime video x2)
    - realesr-animevideov3-x3 (anime video x3)
    - realesr-animevideov3-x4 (anime video x4)

.PARAMETER Scale
    Upscaling factor. Defaults to model's preset value.

.PARAMETER Threads
    CPU thread count. Defaults to auto (0).

.PARAMETER OpenInput
    Auto-open input directory. Enabled by default.

.PARAMETER OpenOutput
    Auto-open output directory. Enabled by default.

.PARAMETER NoWait
    Skip pre-start pause and run immediately.

.EXAMPLE
    .\Start_Real-ESRGAN.ps1
    Run with defaults, interactive model selection

.EXAMPLE
    .\Start_Real-ESRGAN.ps1 -InputDir "D:\Photos\Raw" -OutputDir "D:\Photos\Upscaled" -Model "realesrgan-x4plus"

.EXAMPLE
    .\Start_Real-ESRGAN.ps1 -Model "realesr-animevideov3-x4" -Scale 4 -Threads 4 -NoWait
#>

[CmdletBinding()]
param(
    [Alias("i")]
    [string]$InputDir = "",

    [Alias("o")]
    [string]$OutputDir = "",

    [Alias("n")]
    [ValidateSet("realesrgan-x4plus", "realesrgan-x4plus-anime",
                 "realesr-animevideov3-x2", "realesr-animevideov3-x3", "realesr-animevideov3-x4", "")]
    [string]$Model = "",

    [Alias("s")]
    [ValidateRange(1, 4)]
    [int]$Scale = 0,

    [Alias("t")]
    [ValidateRange(0, 64)]
    [int]$Threads = 0,

    [switch]$OpenInput = $true,

    [switch]$OpenOutput = $true,

    [switch]$NoWait
)

# --- Init ---
$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Get-Location
}

# Default paths (using %USERPROFILE% for cross-machine compatibility)
if ([string]::IsNullOrWhiteSpace($InputDir)) {
    $InputDir = Join-Path $env:USERPROFILE "Pictures\Real-ESRGAN_Input"
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $env:USERPROFILE "Pictures\Real-ESRGAN_Output"
}

# Resolve to absolute paths
$InputDir = [System.IO.Path]::GetFullPath($InputDir)
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)

# --- Check executable ---
$exePath = Join-Path $scriptDir "engine\realesrgan-ncnn-vulkan.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "[ERROR] realesrgan-ncnn-vulkan.exe not found. Please make sure the engine folder exists." -ForegroundColor Red
    exit 1
}

# --- Model selection (interactive if not specified) ---
$modelOptions = @{
    "1" = @{ Name = "realesrgan-x4plus";         Desc = "General photos / realistic (x4)"; DefaultScale = 4 }
    "2" = @{ Name = "realesrgan-x4plus-anime";   Desc = "Anime / illustrations (x4)";      DefaultScale = 4 }
    "3" = @{ Name = "realesr-animevideov3-x2";  Desc = "Anime video optimized (x2)";      DefaultScale = 2 }
    "4" = @{ Name = "realesr-animevideov3-x3";  Desc = "Anime video optimized (x3)";      DefaultScale = 3 }
    "5" = @{ Name = "realesr-animevideov3-x4";  Desc = "Anime video optimized (x4)";      DefaultScale = 4 }
}

if ([string]::IsNullOrWhiteSpace($Model)) {
    Write-Host ""
    Write-Host "=== Select Upscaling Model ===" -ForegroundColor Cyan
    foreach ($key in ($modelOptions.Keys | Sort-Object)) {
        Write-Host "  [$key] $($modelOptions[$key].Desc)"
    }
    Write-Host ""
    $choice = Read-Host "Enter option (1-5, default 1)"
    if ([string]::IsNullOrWhiteSpace($choice) -or -not $modelOptions.ContainsKey($choice)) {
        $choice = "1"
    }
    $Model = $modelOptions[$choice].Name
    if ($Scale -eq 0) {
        $Scale = $modelOptions[$choice].DefaultScale
    }
} else {
    if ($Scale -eq 0) {
        $found = $modelOptions.Values | Where-Object { $_.Name -eq $Model } | Select-Object -First 1
        if ($found) {
            $Scale = $found.DefaultScale
        } else {
            $Scale = 4
        }
    }
}

# --- Auto-create directories ---
if (-not (Test-Path $InputDir)) {
    New-Item -ItemType Directory -Path $InputDir -Force | Out-Null
    Write-Host "[INFO] Created input directory: $InputDir" -ForegroundColor Yellow
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "[INFO] Created output directory: $OutputDir" -ForegroundColor Yellow
}

# --- Check input files ---
$supportedExt = @("*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tif", "*.tiff")
[array]$inputFiles = Get-ChildItem -Path "$InputDir\*" -Include $supportedExt -File -ErrorAction SilentlyContinue

if ($inputFiles.Count -eq 0) {
    Write-Host ""
    Write-Host "[WARNING] No supported image files found in input directory." -ForegroundColor Yellow
    Write-Host "          Supported: png, jpg, jpeg, bmp, webp, tif, tiff" -ForegroundColor Yellow
    Write-Host "          Input dir: $InputDir" -ForegroundColor Yellow
    Write-Host ""
    $createSample = Read-Host "Copy sample image (input.jpg) for testing? [Y/n]"
    if ($createSample -eq "" -or $createSample -match "^[Yy]") {
        $samplePath = Join-Path $scriptDir "input.jpg"
        if (Test-Path $samplePath) {
            Copy-Item $samplePath $InputDir -Force
            Write-Host "[INFO] Copied sample image to input directory." -ForegroundColor Green
            [array]$inputFiles = Get-ChildItem -Path "$InputDir\*" -Include $supportedExt -File
        } else {
            Write-Host "[ERROR] Sample image input.jpg not found." -ForegroundColor Red
            exit 1
        }
    } else {
        exit 0
    }
}

Write-Host ""
Write-Host "=== Configuration ===" -ForegroundColor Cyan
Write-Host "  Input dir : $InputDir"
Write-Host "  Output dir: $OutputDir"
Write-Host "  Model     : $Model"
Write-Host "  Scale     : $Scale"
Write-Host "  Files     : $($inputFiles.Count)"
Write-Host ""

# --- Open input directory ---
if ($OpenInput) {
    Write-Host "[INFO] Opening input directory..." -ForegroundColor Green
    Start-Process explorer.exe -ArgumentList $InputDir
}

# --- Wait for user confirmation ---
if (-not $NoWait) {
    Write-Host "Press Enter to start (or type q to quit)..." -ForegroundColor Cyan -NoNewline
    $confirm = Read-Host
    if ($confirm -eq "q" -or $confirm -eq "Q") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# --- Build command ---
$cmdArgs = @("-i", $InputDir, "-o", $OutputDir, "-n", $Model)
if ($Scale -gt 0) {
    $cmdArgs += @("-s", $Scale)
}
if ($Threads -gt 0) {
    $cmdArgs += @("-t", $Threads)
}

Write-Host "[START] Executing: realesrgan-ncnn-vulkan.exe $cmdArgs" -ForegroundColor Cyan
Write-Host ""

# --- Execute ---
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $process = Start-Process -FilePath $exePath -ArgumentList $cmdArgs -NoNewWindow -Wait -PassThru
    $exitCode = $process.ExitCode
} catch {
    Write-Host "[ERROR] Execution failed: $_" -ForegroundColor Red
    exit 1
}
$stopwatch.Stop()

# --- Result ---
Write-Host ""
if ($exitCode -eq 0) {
    [array]$outputFiles = Get-ChildItem -Path "$OutputDir\*" -Include $supportedExt -File -ErrorAction SilentlyContinue
    Write-Host "[DONE] Processing complete!" -ForegroundColor Green
    Write-Host "       Duration : $($stopwatch.Elapsed.ToString('mm\:ss'))"
    Write-Host "       Output   : $($outputFiles.Count) file(s)"
    if ($OpenOutput) {
        Start-Process explorer.exe -ArgumentList $OutputDir
    }
} else {
    Write-Host "[ERROR] Processing failed, exit code: $exitCode" -ForegroundColor Red
    exit $exitCode
}
