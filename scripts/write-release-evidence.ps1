[CmdletBinding()]
param(
    [ValidateSet("x64", "x86")]
    [string[]]$Architecture = @("x64", "x86"),

    [string]$InstallerRoot,

    [string]$EnigmaRoot,

    [string]$OutputDir,

    [string]$Tag,

    [string]$Repository,

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
        throw "Path is outside repository: $fullPath"
    }

    $rootPath = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + "\"
    $rootUri = New-Object System.Uri($rootPath)
    $pathUri = New-Object System.Uri($fullPath)
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace("/", "\")
}

function ConvertTo-PortablePath {
    param([string]$Path)

    return $Path.Replace("\", "/")
}

function Get-GitOutput {
    param(
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        if ($AllowFailure) {
            return [pscustomobject]@{
                Succeeded = $false
                Output = ($output -join "`n")
            }
        }

        throw "git $($Arguments -join ' ') failed: $($output -join "`n")"
    }

    return [pscustomobject]@{
        Succeeded = $true
        Output = ($output -join "`n")
    }
}

function Get-SubmoduleStatus {
    $result = Get-GitOutput -Arguments @("submodule", "status", "--recursive") -AllowFailure
    if ($result.Succeeded) {
        return $result.Output
    }

    $bashPath = Join-Path $env:ProgramFiles "Git\bin\bash.exe"
    if (Test-Path -LiteralPath $bashPath -PathType Leaf) {
        $bashOutput = & $bashPath -lc "git submodule status --recursive" 2>&1
        if ($LASTEXITCODE -eq 0) {
            return ($bashOutput -join "`n")
        }
    }

    throw "Unable to read recursive submodule status: $($result.Output)"
}

function Get-GitModulesMap {
    $map = @{}
    $gitmodules = Join-Path $repoRoot ".gitmodules"
    if (-not (Test-Path -LiteralPath $gitmodules -PathType Leaf)) {
        return $map
    }

    $config = Get-GitOutput -Arguments @("config", "--file", ".gitmodules", "--get-regexp", "^submodule\..*\.(path|url)$") -AllowFailure
    if (-not $config.Succeeded -or [string]::IsNullOrWhiteSpace($config.Output)) {
        return $map
    }

    $entries = @{}
    foreach ($line in $config.Output -split "`n") {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $parts = $trimmed -split "\s+", 2
        if ($parts.Count -ne 2) {
            continue
        }

        if ($parts[0] -match "^submodule\.(?<name>.+)\.(?<field>path|url)$") {
            $name = $Matches.name
            $field = $Matches.field
            if (-not $entries.ContainsKey($name)) {
                $entries[$name] = @{}
            }

            $entries[$name][$field] = $parts[1]
        }
    }

    foreach ($name in $entries.Keys) {
        if ($entries[$name].ContainsKey("path")) {
            $path = $entries[$name]["path"]
            $map[$path] = [pscustomobject]@{
                Name = $name
                Path = $path
                Url = $(if ($entries[$name].ContainsKey("url")) { $entries[$name]["url"] } else { $null })
            }
        }
    }

    return $map
}

function Convert-SubmoduleStatusLine {
    param(
        [string]$Line,
        [hashtable]$GitModules
    )

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return $null
    }

    $prefix = $Line.Substring(0, 1)
    $rest = $Line.Substring(1).Trim()
    $parts = $rest -split "\s+", 3
    if ($parts.Count -lt 2) {
        throw "Unexpected submodule status line: $Line"
    }

    $path = $parts[1]
    $describe = if ($parts.Count -ge 3) { $parts[2].Trim().TrimStart("(").TrimEnd(")") } else { $null }
    $state = switch ($prefix) {
        " " { "clean" }
        "-" { "not-initialized" }
        "+" { "different-commit" }
        "U" { "merge-conflict" }
        default { "unknown" }
    }

    $module = if ($GitModules.ContainsKey($path)) { $GitModules[$path] } else { $null }
    return [pscustomobject]@{
        path = $path
        commit = $parts[0]
        state = $state
        describe = $describe
        url = $(if ($module) { $module.Url } else { $null })
    }
}

function Assert-CleanRepositorySet {
    param([object[]]$Submodules)

    $badSubmodules = @($Submodules | Where-Object { $_.state -ne "clean" })
    if ($badSubmodules.Count -gt 0) {
        $summary = ($badSubmodules | ForEach-Object { "$($_.path): $($_.state)" }) -join ", "
        throw "Submodule status is not release-ready: $summary"
    }
}

function Get-ReleaseAssets {
    $resolver = Join-Path $scriptRoot "resolve-release-assets.ps1"
    $arguments = @{
        Architecture = $Architecture
    }

    if (-not [string]::IsNullOrWhiteSpace($InstallerRoot)) {
        $arguments.InstallerRoot = $InstallerRoot
    }
    if (-not [string]::IsNullOrWhiteSpace($EnigmaRoot)) {
        $arguments.EnigmaRoot = $EnigmaRoot
    }
    if ($Clean) {
        $arguments.Clean = $true
    }
    if ($RequireInstallers) {
        $arguments.RequireInstallers = $true
    }
    if ($RequireEnigma) {
        $arguments.RequireEnigma = $true
    }

    $paths = @(& $resolver @arguments)
    if ($paths.Count -eq 0) {
        throw "No release assets were resolved."
    }

    return $paths
}

$resolvedOutputDir = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputDir)) { "artifacts\release-evidence" } else { $OutputDir }) -BasePath $repoRoot
if (-not (Test-PathUnderRoot -Path $resolvedOutputDir -Root $repoRoot)) {
    throw "Output directory is outside repository: $resolvedOutputDir"
}

if ($Clean -and (Test-Path -LiteralPath $resolvedOutputDir)) {
    Remove-Item -LiteralPath $resolvedOutputDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

$assetPaths = @(Get-ReleaseAssets)
$assetRecords = New-Object System.Collections.Generic.List[object]
foreach ($relativePath in $assetPaths) {
    $fullPath = Resolve-FullPath -Path $relativePath -BasePath $repoRoot
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Resolved release asset does not exist: $relativePath"
    }

    $file = Get-Item -LiteralPath $fullPath
    $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $fileName = Split-Path -Leaf $relativePath
    $kind = if ($fileName -like "*Setup*") { "installer" } elseif ($fileName -like "*Portable*") { "enigma-portable" } else { "unknown" }
    $arch = if ($fileName -like "*x64*") { "x64" } elseif ($fileName -like "*x86*") { "x86" } else { $null }

    $assetRecords.Add([pscustomobject]@{
        fileName = $fileName
        path = (ConvertTo-PortablePath -Path $relativePath)
        kind = $kind
        architecture = $arch
        sizeBytes = $file.Length
        sha256 = $hash
    })
}

$assetRecords = @($assetRecords | Sort-Object fileName)
$checksumPath = Join-Path $resolvedOutputDir "SHA256SUMS.txt"
$checksumLines = @($assetRecords | ForEach-Object { "$($_.sha256)  $($_.fileName)" })
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($checksumPath, (($checksumLines -join "`n") + "`n"), $utf8NoBom)

$submoduleLines = @((Get-SubmoduleStatus) -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$gitModules = Get-GitModulesMap
$submodules = @($submoduleLines | ForEach-Object { Convert-SubmoduleStatusLine -Line $_ -GitModules $gitModules })
Assert-CleanRepositorySet -Submodules $submodules

$head = (Get-GitOutput -Arguments @("rev-parse", "HEAD")).Output.Trim()
$branch = (Get-GitOutput -Arguments @("rev-parse", "--abbrev-ref", "HEAD")).Output.Trim()
$remoteResult = Get-GitOutput -Arguments @("config", "--get", "remote.origin.url") -AllowFailure
$remoteUrl = if ($remoteResult.Succeeded) { $remoteResult.Output.Trim() } else { $null }
$statusResult = Get-GitOutput -Arguments @("status", "--short") -AllowFailure
$workingTreeClean = $statusResult.Succeeded -and [string]::IsNullOrWhiteSpace($statusResult.Output)

$resolvedTag = if (-not [string]::IsNullOrWhiteSpace($Tag)) {
    $Tag
}
elseif (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
    $env:GITHUB_REF_NAME
}
else {
    "local"
}

$resolvedRepository = if (-not [string]::IsNullOrWhiteSpace($Repository)) {
    $Repository
}
elseif (-not [string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY)) {
    $env:GITHUB_REPOSITORY
}
else {
    "Xeknoz/Real-ESRGAN-GUI"
}

$runUrl = if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SERVER_URL) -and
    -not [string]::IsNullOrWhiteSpace($env:GITHUB_REPOSITORY) -and
    -not [string]::IsNullOrWhiteSpace($env:GITHUB_RUN_ID)) {
    "$($env:GITHUB_SERVER_URL)/$($env:GITHUB_REPOSITORY)/actions/runs/$($env:GITHUB_RUN_ID)"
}
else {
    $null
}

$checksumRelativePath = ConvertTo-RelativePath -Path $checksumPath
$checksumHash = (Get-FileHash -LiteralPath $checksumPath -Algorithm SHA256).Hash.ToLowerInvariant()
$manifestPath = Join-Path $resolvedOutputDir "release-manifest.json"
$manifestRelativePath = ConvertTo-RelativePath -Path $manifestPath

$manifest = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    tag = $resolvedTag
    version = $resolvedTag.TrimStart("v")
    repository = [ordered]@{
        slug = $resolvedRepository
        url = "https://github.com/$resolvedRepository"
        remote = $remoteUrl
    }
    git = [ordered]@{
        commit = $head
        ref = $env:GITHUB_REF
        refName = $(if ($env:GITHUB_REF_NAME) { $env:GITHUB_REF_NAME } else { $branch })
        branch = $branch
        workingTreeClean = $workingTreeClean
    }
    workflow = [ordered]@{
        name = $env:GITHUB_WORKFLOW
        runId = $env:GITHUB_RUN_ID
        runNumber = $env:GITHUB_RUN_NUMBER
        runAttempt = $env:GITHUB_RUN_ATTEMPT
        actor = $env:GITHUB_ACTOR
        url = $runUrl
    }
    releasePolicy = [ordered]@{
        codeSigned = $false
        expectedWindowsPublisher = "Unknown publisher"
        expectedUserEntry = "Launcher.exe"
        officialDownloadPage = "https://github.com/$resolvedRepository/releases/latest"
        note = "Release binaries are currently unsigned. Users should download from the official GitHub Release page and verify SHA256 hashes."
    }
    assets = $assetRecords
    evidence = [ordered]@{
        sha256Sums = [ordered]@{
            fileName = "SHA256SUMS.txt"
            path = (ConvertTo-PortablePath -Path $checksumRelativePath)
            sha256 = $checksumHash
        }
        manifest = [ordered]@{
            fileName = "release-manifest.json"
            path = (ConvertTo-PortablePath -Path $manifestRelativePath)
        }
    }
    repositorySet = [ordered]@{
        root = [ordered]@{
            path = "."
            commit = $head
            remote = $remoteUrl
            workingTreeClean = $workingTreeClean
        }
        submodules = $submodules
    }
}

$json = $manifest | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($manifestPath, ($json + "`n"), $utf8NoBom)

Write-Host ""
Write-Host "========== Release Evidence =========="
Write-Host "SHA256 sums: $(ConvertTo-RelativePath -Path $checksumPath)"
Write-Host "Manifest: $(ConvertTo-RelativePath -Path $manifestPath)"
Write-Host ""

[pscustomobject]@{
    Sha256SumsPath = (ConvertTo-RelativePath -Path $checksumPath)
    ManifestPath = (ConvertTo-RelativePath -Path $manifestPath)
}
