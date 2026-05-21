[CmdletBinding()]
param(
    [switch]$NoInstall,
    [switch]$RequireLib32,
    [string]$X86CompatibleVersion = "1.3.296.0"
)

$ErrorActionPreference = "Stop"

function Test-VulkanSdk([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $hasCoreSdk = (Test-Path -LiteralPath (Join-Path $Path "Include\vulkan\vulkan.h")) -and
                  (Test-Path -LiteralPath (Join-Path $Path "Lib\vulkan-1.lib")) -and
                  (Test-Path -LiteralPath (Join-Path $Path "Bin\glslangValidator.exe"))

    if (-not $hasCoreSdk) {
        return $false
    }

    if ($RequireLib32 -and -not (Test-Path -LiteralPath (Join-Path $Path "Lib32\vulkan-1.lib"))) {
        return $false
    }

    return $true
}

function Find-VulkanSdk {
    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($env:VULKAN_SDK)) {
        $candidates.Add($env:VULKAN_SDK)
    }

    foreach ($root in @(
        (Join-Path $env:LOCALAPPDATA "Programs\VulkanSDK"),
        "C:\VulkanSDK"
    )) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending |
                ForEach-Object { $candidates.Add($_.FullName) }
        }
    }

    foreach ($candidate in $candidates) {
        if (Test-VulkanSdk $candidate) {
            return $candidate
        }
    }

    return $null
}

function Install-VulkanSdkWithChocolatey {
    $choco = Get-Command choco -ErrorAction SilentlyContinue
    if (-not $choco) {
        throw "Vulkan SDK was not found and Chocolatey is unavailable. Install Vulkan SDK or add VULKAN_SDK to the environment."
    }

    Write-Host "Vulkan SDK not found. Installing with Chocolatey..."
    & choco install vulkan-sdk -y --no-progress
    if ($LASTEXITCODE -ne 0) {
        throw "Chocolatey failed to install vulkan-sdk."
    }
}

function Install-VulkanSdkWithLib32 {
    if ([string]::IsNullOrWhiteSpace($X86CompatibleVersion)) {
        throw "X86CompatibleVersion must not be empty when -RequireLib32 is used."
    }

    $installerName = "VulkanSDK-$X86CompatibleVersion-Installer.exe"
    $downloadUrl = "https://sdk.lunarg.com/sdk/download/$X86CompatibleVersion/windows/$installerName"
    $tempRoot = if (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
        $env:RUNNER_TEMP
    } else {
        [System.IO.Path]::GetTempPath()
    }
    $installerPath = Join-Path $tempRoot $installerName

    Write-Host "Vulkan SDK with Lib32 not found. Downloading $downloadUrl"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath

    Write-Host "Installing Vulkan SDK $X86CompatibleVersion with 32-bit components..."
    & $installerPath --accept-licenses --default-answer --confirm-command install com.lunarg.vulkan.32bit
    if ($LASTEXITCODE -ne 0) {
        throw "Vulkan SDK installer failed with exit code $LASTEXITCODE."
    }
}

$sdkPath = Find-VulkanSdk
if (-not $sdkPath) {
    if ($NoInstall) {
        if ($RequireLib32) {
            throw "Vulkan SDK with Lib32\vulkan-1.lib was not found."
        }

        throw "Vulkan SDK was not found."
    }

    if ($RequireLib32) {
        Install-VulkanSdkWithLib32
    }
    else {
        Install-VulkanSdkWithChocolatey
    }

    $sdkPath = Find-VulkanSdk
    if (-not $sdkPath) {
        if ($RequireLib32) {
            throw "Vulkan SDK was installed, but a valid directory with Lib32\vulkan-1.lib could not be located."
        }

        throw "Vulkan SDK was installed, but a valid Vulkan SDK directory could not be located."
    }
}

$sdkBin = Join-Path $sdkPath "Bin"
$env:VULKAN_SDK = $sdkPath
if (($env:Path -split ';') -notcontains $sdkBin) {
    $env:Path = "$sdkBin;$env:Path"
}

if ($env:GITHUB_ENV) {
    Add-Content -LiteralPath $env:GITHUB_ENV -Value "VULKAN_SDK=$sdkPath"
}

if ($env:GITHUB_PATH) {
    Add-Content -LiteralPath $env:GITHUB_PATH -Value $sdkBin
}

Write-Host "Using Vulkan SDK: $sdkPath"
