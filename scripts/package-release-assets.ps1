[CmdletBinding()]
param(
    [ValidateSet("x64", "x86")]
    [string[]]$Architecture = @("x64", "x86"),

    [string]$PortableRoot,

    [string]$InstallerRoot,

    [string]$EnigmaRoot,

    [string]$OutputDir,

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

function Assert-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Missing ${Description}: $Path"
    }
}

function Assert-RequiredDirectory {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "Missing ${Description}: $Path"
    }
}

function Remove-OutputDirectory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    if (-not (Test-PathUnderRoot -Path $Path -Root $repoRoot)) {
        throw "Refusing to clean output directory outside repository: $Path"
    }

    Remove-Item -LiteralPath $Path -Recurse -Force
}

function Copy-Asset {
    param(
        [System.Collections.Generic.List[object]]$Assets,
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$Kind,
        [string]$Architecture
    )

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    $Assets.Add([pscustomobject]@{
        Architecture = $Architecture
        Kind = $Kind
        Path = $DestinationPath
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

$resolvedPortableRoot = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($PortableRoot)) { "artifacts\portable" } else { $PortableRoot }) -BasePath $repoRoot
$resolvedInstallerRoot = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($InstallerRoot)) { "artifacts\installers" } else { $InstallerRoot }) -BasePath $repoRoot
$resolvedEnigmaRoot = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($EnigmaRoot)) { "artifacts\portable-enigma" } else { $EnigmaRoot }) -BasePath $repoRoot
$resolvedOutputDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputDir)) { "artifacts\release-assets" } else { $OutputDir }) -BasePath $repoRoot

if ($Clean) {
    Remove-OutputDirectory -Path $resolvedOutputDir
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null
$assets = New-Object System.Collections.Generic.List[object]

foreach ($arch in @(Get-UniqueArchitectures)) {
    $portableDir = Join-Path $resolvedPortableRoot $arch
    $architectureMarkerPath = Join-Path $portableDir "ARCHITECTURE.txt"
    Assert-RequiredDirectory -Path $portableDir -Description "$arch portable output"
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

    $portableArchivePath = Join-Path $resolvedOutputDir "Real-ESRGAN-GUI-win-$arch.zip"
    if (Test-Path -LiteralPath $portableArchivePath -PathType Leaf) {
        Remove-Item -LiteralPath $portableArchivePath -Force
    }

    Write-Host "Creating portable archive: $portableArchivePath"
    Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $portableArchivePath -Force
    $assets.Add([pscustomobject]@{
        Architecture = $arch
        Kind = "portable-archive"
        Path = $portableArchivePath
    })

    $installerPath = Join-Path $resolvedInstallerRoot "Real-ESRGAN-GUI-Setup-$arch.exe"
    if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
        Copy-Asset -Assets $assets -SourcePath $installerPath -DestinationPath (Join-Path $resolvedOutputDir (Split-Path -Leaf $installerPath)) -Kind "installer" -Architecture $arch
    }
    elseif ($RequireInstallers) {
        throw "Missing $arch installer: $installerPath"
    }

    $enigmaPath = Join-Path $resolvedEnigmaRoot "Real-ESRGAN-GUI-Portable-$arch.exe"
    if (Test-Path -LiteralPath $enigmaPath -PathType Leaf) {
        Copy-Asset -Assets $assets -SourcePath $enigmaPath -DestinationPath (Join-Path $resolvedOutputDir (Split-Path -Leaf $enigmaPath)) -Kind "enigma-portable" -Architecture $arch
    }
    elseif ($RequireEnigma) {
        throw "Missing $arch Enigma portable executable: $enigmaPath"
    }
}

Write-Host ""
Write-Host "========== Release Assets Complete =========="
foreach ($asset in $assets) {
    Write-Host "$($asset.Architecture) $($asset.Kind): $($asset.Path)"
}
