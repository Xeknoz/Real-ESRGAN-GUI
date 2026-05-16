function Get-GitOutput {
    param(
        [string]$RepoRoot,
        [string[]]$Arguments
    )

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return $null
    }

    $output = & git -C $RepoRoot @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return @($output)
}

function ConvertTo-AppVersionParts {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Version value is empty."
    }

    $normalized = $Value.Trim()
    if ($normalized.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(1)
    }

    $pattern = '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:\.(?<revision>\d+))?(?<suffix>-[0-9A-Za-z][0-9A-Za-z.-]*)?$'
    if ($normalized -notmatch $pattern) {
        throw "Invalid version '$Value'. Use SemVer like 1.2.3 or v1.2.3."
    }

    $suffix = $Matches["suffix"]
    if ($suffix) {
        foreach ($part in $suffix.Substring(1).Split('.')) {
            if ([string]::IsNullOrWhiteSpace($part)) {
                throw "Invalid version '$Value'. Pre-release identifiers cannot be empty."
            }
        }
    }

    $revision = $null
    if ($Matches["revision"]) {
        $revision = [int]$Matches["revision"]
        if ($revision -gt 65535) {
            throw "Invalid version '$Value'. Numeric version parts must be <= 65535."
        }
    }

    $major = [int]$Matches["major"]
    $minor = [int]$Matches["minor"]
    $patch = [int]$Matches["patch"]
    foreach ($part in @($major, $minor, $patch)) {
        if ($part -gt 65535) {
            throw "Invalid version '$Value'. Numeric version parts must be <= 65535."
        }
    }

    return [pscustomobject]@{
        SemVer = "$major.$minor.$patch$suffix"
        Major = $major
        Minor = $minor
        Patch = $patch
        Revision = $revision
    }
}

function Get-RepositoryVersion {
    param([string]$RepoRoot)

    $versionFile = Join-Path $RepoRoot "VERSION"
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
        throw "VERSION file was not found at repository root."
    }

    return (Get-Content -LiteralPath $versionFile -TotalCount 1).Trim()
}

function Get-TagVersion {
    param([string]$RepoRoot)

    $tag = $null
    if ($env:GITHUB_REF_TYPE -eq "tag" -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_REF_NAME)) {
        $tag = $env:GITHUB_REF_NAME
    }
    elseif ($env:GITHUB_REF -match '^refs/tags/(.+)$') {
        $tag = $Matches[1]
    }
    else {
        $exactTag = @(Get-GitOutput -RepoRoot $RepoRoot -Arguments @("describe", "--tags", "--exact-match", "HEAD"))
        if ($exactTag) {
            $tag = ([string]$exactTag[0]).Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($tag)) {
        return $null
    }

    return $tag
}

function Get-CommitCount {
    param([string]$RepoRoot)

    $count = @(Get-GitOutput -RepoRoot $RepoRoot -Arguments @("rev-list", "--count", "HEAD"))
    if ($count -and $count[0] -match '^\d+$') {
        return [int]$count[0]
    }

    return 0
}

function Get-ShortCommitSha {
    param([string]$RepoRoot)

    $sha = @(Get-GitOutput -RepoRoot $RepoRoot -Arguments @("rev-parse", "--short=8", "HEAD"))
    if ($sha -and -not [string]::IsNullOrWhiteSpace($sha[0])) {
        return ([string]$sha[0]).Trim()
    }

    return "nogit"
}

function Join-DevVersion {
    param(
        [string]$BaseSemVer,
        [int]$CommitCount,
        [string]$ShortSha
    )

    if ($BaseSemVer.Contains("-")) {
        return "$BaseSemVer.dev.$CommitCount.g$ShortSha"
    }

    return "$BaseSemVer-dev.$CommitCount.g$ShortSha"
}

function Resolve-AppVersion {
    param(
        [string]$RepoRoot,
        [string]$VersionOverride
    )

    $source = "VERSION"
    $rawVersion = $null

    if (-not [string]::IsNullOrWhiteSpace($VersionOverride)) {
        $rawVersion = $VersionOverride
        $source = "override"
    }
    else {
        $tagVersion = Get-TagVersion -RepoRoot $RepoRoot
        if ($tagVersion) {
            $rawVersion = $tagVersion
            $source = "tag"
        }
        else {
            $rawVersion = Get-RepositoryVersion -RepoRoot $RepoRoot
        }
    }

    $versionParts = ConvertTo-AppVersionParts -Value $rawVersion
    $isReleaseVersion = $source -eq "override" -or $source -eq "tag"
    $commitCount = Get-CommitCount -RepoRoot $RepoRoot
    $shortSha = Get-ShortCommitSha -RepoRoot $RepoRoot
    $revision = if ($isReleaseVersion) {
        if ($null -ne $versionParts.Revision) { $versionParts.Revision } else { 0 }
    }
    else {
        [Math]::Min($commitCount, 65535)
    }

    $numericVersion = "$($versionParts.Major).$($versionParts.Minor).$($versionParts.Patch).$revision"
    $informationalVersion = if ($isReleaseVersion) {
        $versionParts.SemVer
    }
    else {
        Join-DevVersion -BaseSemVer $versionParts.SemVer -CommitCount $commitCount -ShortSha $shortSha
    }

    return [pscustomobject]@{
        Source = $source
        PackageVersion = $informationalVersion
        InformationalVersion = $informationalVersion
        AssemblyVersion = $numericVersion
        FileVersion = $numericVersion
    }
}
