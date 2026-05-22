[CmdletBinding()]
param(
    [ValidateSet("x64", "x86")]
    [string[]]$Architecture = @("x64", "x86"),

    [string]$InstallerRoot,

    [string]$EnigmaRoot,

    [switch]$Clean,

    [switch]$RequireInstallers,

    [switch]$RequireEnigma
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

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

function ConvertTo-RelativePath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-PathUnderRoot -Path $fullPath -Root $repoRoot)) {
        throw "Release asset is outside repository: $fullPath"
    }

    $rootPath = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + "\"
    $rootUri = New-Object System.Uri($rootPath)
    $pathUri = New-Object System.Uri($fullPath)
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace("/", "\")
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

function Remove-LegacyReleaseAssetsDirectory {
    $legacyOutputDir = Join-Path $repoRoot "artifacts\release-assets"
    if (-not (Test-Path -LiteralPath $legacyOutputDir)) {
        return
    }

    if (-not (Test-PathUnderRoot -Path $legacyOutputDir -Root $repoRoot)) {
        throw "Refusing to clean legacy release-assets directory outside repository: $legacyOutputDir"
    }

    Remove-Item -LiteralPath $legacyOutputDir -Recurse -Force
}

function Add-Asset {
    param(
        [System.Collections.Generic.List[object]]$Assets,
        [string]$SourcePath,
        [string]$Kind,
        [string]$Architecture
    )

    Assert-RequiredFile -Path $SourcePath -Description "$Architecture $Kind"
    $Assets.Add([pscustomobject]@{
        Architecture = $Architecture
        Kind = $Kind
        Path = (ConvertTo-RelativePath -Path $SourcePath)
    })
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

if ($Clean) {
    Remove-LegacyReleaseAssetsDirectory
}
elseif (Test-Path -LiteralPath (Join-Path $repoRoot "artifacts\release-assets")) {
    Write-Warning "artifacts\release-assets is a legacy duplicate output directory. Run this script with -Clean to remove it."
}

$resolvedInstallerRoot = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($InstallerRoot)) { "artifacts\installers" } else { $InstallerRoot }) -BasePath $repoRoot
$resolvedEnigmaRoot = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($EnigmaRoot)) { "artifacts\portable-enigma" } else { $EnigmaRoot }) -BasePath $repoRoot
$assets = New-Object System.Collections.Generic.List[object]

foreach ($arch in @(Get-UniqueArchitectures)) {
    $installerPath = Join-Path $resolvedInstallerRoot "Real-ESRGAN-GUI-Setup-$arch.exe"
    if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
        Add-Asset -Assets $assets -SourcePath $installerPath -Kind "installer" -Architecture $arch
    }
    elseif ($RequireInstallers) {
        throw "Missing $arch installer: $installerPath"
    }

    $enigmaPath = Join-Path $resolvedEnigmaRoot "Real-ESRGAN-GUI-Portable-$arch.exe"
    if (Test-Path -LiteralPath $enigmaPath -PathType Leaf) {
        Add-Asset -Assets $assets -SourcePath $enigmaPath -Kind "enigma-portable" -Architecture $arch
    }
    elseif ($RequireEnigma) {
        throw "Missing $arch Enigma portable executable: $enigmaPath"
    }
}

if ($assets.Count -eq 0) {
    throw "No release assets were found."
}

Write-Host ""
Write-Host "========== Release Assets =========="
foreach ($asset in $assets) {
    Write-Host "$($asset.Architecture) $($asset.Kind): $($asset.Path)"
}

$assets | ForEach-Object { $_.Path }
