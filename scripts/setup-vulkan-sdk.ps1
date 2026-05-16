[CmdletBinding()]
param(
    [switch]$NoInstall
)

$ErrorActionPreference = "Stop"

function Test-VulkanSdk([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    return (Test-Path -LiteralPath (Join-Path $Path "Include\vulkan\vulkan.h")) -and
           (Test-Path -LiteralPath (Join-Path $Path "Lib\vulkan-1.lib")) -and
           (Test-Path -LiteralPath (Join-Path $Path "Bin\glslangValidator.exe"))
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

$sdkPath = Find-VulkanSdk
if (-not $sdkPath) {
    if ($NoInstall) {
        throw "Vulkan SDK was not found."
    }

    $choco = Get-Command choco -ErrorAction SilentlyContinue
    if (-not $choco) {
        throw "Vulkan SDK was not found and Chocolatey is unavailable. Install Vulkan SDK or add VULKAN_SDK to the environment."
    }

    Write-Host "Vulkan SDK not found. Installing with Chocolatey..."
    & choco install vulkan-sdk -y --no-progress
    if ($LASTEXITCODE -ne 0) {
        throw "Chocolatey failed to install vulkan-sdk."
    }

    $sdkPath = Find-VulkanSdk
    if (-not $sdkPath) {
        throw "vulkan-sdk was installed, but a valid Vulkan SDK directory could not be located."
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
