[CmdletBinding()]
param(
    [switch]$Clean,

    [switch]$Force,

    [string]$SourceArchive,

    [string]$DownloadUrl = "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$artifactRoot = Join-Path $repoRoot "artifacts"
$modelArtifactDir = Join-Path $artifactRoot "models"
$cacheDir = Join-Path $repoRoot ".cache\models"
$legacyArtifactCacheDir = Join-Path $artifactRoot "cache"
$legacyArtifactObjDir = Join-Path $artifactRoot "obj"
$manifestPath = Join-Path $cacheDir "realesrgan-models.manifest.json"
$legacyModelManifestPath = Join-Path $modelArtifactDir "realesrgan-models.manifest.json"

$requiredModelFiles = @(
    "realesr-animevideov3-x2.bin",
    "realesr-animevideov3-x2.param",
    "realesr-animevideov3-x3.bin",
    "realesr-animevideov3-x3.param",
    "realesr-animevideov3-x4.bin",
    "realesr-animevideov3-x4.param",
    "realesrgan-x4plus-anime.bin",
    "realesrgan-x4plus-anime.param",
    "realesrgan-x4plus.bin",
    "realesrgan-x4plus.param"
)

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

function Get-Sha256ForFile {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-ModelsComplete {
    foreach ($fileName in $requiredModelFiles) {
        $path = Join-Path $modelArtifactDir $fileName
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            return $false
        }
    }

    return $true
}

function Resolve-ArchiveFileName {
    param([string]$Url)

    $uri = [System.Uri]$Url
    $name = [System.IO.Path]::GetFileName($uri.AbsolutePath)
    if ([string]::IsNullOrWhiteSpace($name)) {
        throw "Unable to determine archive file name from URL: $Url"
    }

    return $name
}

function Remove-DirectoryIfExistsUnderRoot {
    param(
        [string]$Path,
        [string]$Root,
        [string]$Reason
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    if (-not (Test-PathUnderRoot -Path $Path -Root $Root)) {
        throw "Refusing to clean directory outside repository: $Path"
    }

    Write-Host $Reason
    Remove-Item -LiteralPath $Path -Recurse -Force
}

function Remove-FileIfExistsUnderRoot {
    param(
        [string]$Path,
        [string]$Root,
        [string]$Reason
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    if (-not (Test-PathUnderRoot -Path $Path -Root $Root)) {
        throw "Refusing to clean file outside repository: $Path"
    }

    Write-Host $Reason
    Remove-Item -LiteralPath $Path -Force
}

function Copy-ModelFilesFromArchive {
    param([string]$ArchivePath)

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        foreach ($fileName in $requiredModelFiles) {
            $matches = @($archive.Entries | Where-Object { $_.Name -eq $fileName })
            if ($matches.Count -eq 0) {
                throw "Required model file was not found in archive: $fileName"
            }

            if ($matches.Count -gt 1) {
                throw "Required model file appears more than once in archive: $fileName"
            }

            $destination = Join-Path $modelArtifactDir $fileName
            $sourceStream = $matches[0].Open()
            try {
                $destinationStream = [System.IO.File]::Open($destination, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
                try {
                    $sourceStream.CopyTo($destinationStream)
                }
                finally {
                    $destinationStream.Dispose()
                }
            }
            finally {
                $sourceStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

if ($Clean -and (Test-Path -LiteralPath $modelArtifactDir)) {
    Remove-DirectoryIfExistsUnderRoot -Path $modelArtifactDir -Root $repoRoot -Reason "Cleaning model artifact directory..."
}

Remove-FileIfExistsUnderRoot -Path $legacyModelManifestPath -Root $repoRoot -Reason "Cleaning legacy model manifest from artifacts..."

if (-not $Force -and (Test-ModelsComplete)) {
    Remove-DirectoryIfExistsUnderRoot -Path $legacyArtifactObjDir -Root $repoRoot -Reason "Cleaning legacy model extraction directory..."
    Remove-DirectoryIfExistsUnderRoot -Path $legacyArtifactCacheDir -Root $repoRoot -Reason "Cleaning legacy model cache directory..."
    Write-Host "Model artifacts unchanged; skipping model preparation."
    return
}

$archivePath = Resolve-FullPath -Path $SourceArchive -BasePath $repoRoot
if ([string]::IsNullOrWhiteSpace($archivePath)) {
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $archiveFileName = Resolve-ArchiveFileName -Url $DownloadUrl
    $archivePath = Join-Path $cacheDir $archiveFileName
    $legacyArchivePath = Join-Path (Join-Path $legacyArtifactCacheDir "models") $archiveFileName

    if ((-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) -and
        (Test-Path -LiteralPath $legacyArchivePath -PathType Leaf)) {
        Write-Host "Migrating cached model archive out of artifacts: $legacyArchivePath"
        Copy-Item -LiteralPath $legacyArchivePath -Destination $archivePath -Force
    }

    if ($Force -or -not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        Write-Host "Downloading official Real-ESRGAN NCNN model package..."
        Write-Host $DownloadUrl
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $archivePath
    }
    else {
        Write-Host "Using cached model archive: $archivePath"
    }
}
elseif (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw "Model source archive was not found: $archivePath"
}

Remove-DirectoryIfExistsUnderRoot -Path $legacyArtifactObjDir -Root $repoRoot -Reason "Cleaning legacy model extraction directory..."
Remove-DirectoryIfExistsUnderRoot -Path $legacyArtifactCacheDir -Root $repoRoot -Reason "Cleaning legacy model cache directory..."
New-Item -ItemType Directory -Force -Path $modelArtifactDir | Out-Null

Write-Host "Extracting model files from archive..."
Copy-ModelFilesFromArchive -ArchivePath $archivePath

$files = @()
foreach ($fileName in $requiredModelFiles) {
    $path = Join-Path $modelArtifactDir $fileName
    $item = Get-Item -LiteralPath $path
    if ($item.Length -le 0) {
        throw "Generated model file is empty: $path"
    }

    $files += [ordered]@{
        name = $fileName
        size = $item.Length
        sha256 = Get-Sha256ForFile $path
    }
}

$manifest = [ordered]@{
    schema = 1
    source = "official-real-esrgan-ncnn-release"
    sourceArchive = $archivePath
    sourceUrl = $(if ([string]::IsNullOrWhiteSpace($SourceArchive)) { $DownloadUrl } else { $null })
    sourceArchiveSha256 = Get-Sha256ForFile $archivePath
    generatedAtUtc = [System.DateTime]::UtcNow.ToString("o")
    files = $files
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
$json = ($manifest | ConvertTo-Json -Depth 4) + [Environment]::NewLine
[System.IO.File]::WriteAllText($manifestPath, $json, $utf8NoBom)

Write-Host "Model artifacts ready: $modelArtifactDir"
