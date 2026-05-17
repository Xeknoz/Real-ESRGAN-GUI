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

    $pattern = '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:\.(?<revision>0|[1-9]\d*))?$'
    if ($normalized -notmatch $pattern) {
        throw "Invalid version '$Value'. Use numeric versions like 1.2.3, 1.2.3.4, or v1.2.3."
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
        BaseVersion = "$major.$minor.$patch"
        InputVersion = if ($null -ne $revision) { "$major.$minor.$patch.$revision" } else { "$major.$minor.$patch" }
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
    $channel = if ($isReleaseVersion) { "release" } else { "dev" }
    $commitCount = Get-CommitCount -RepoRoot $RepoRoot
    $shortSha = Get-ShortCommitSha -RepoRoot $RepoRoot
    $revision = if ($isReleaseVersion) {
        if ($null -ne $versionParts.Revision) { $versionParts.Revision } else { 0 }
    }
    else {
        [Math]::Min($commitCount, 65535)
    }

    $numericVersion = "$($versionParts.Major).$($versionParts.Minor).$($versionParts.Patch).$revision"
    $versionNumber = if ($isReleaseVersion -and $null -eq $versionParts.Revision) {
        $versionParts.BaseVersion
    }
    else {
        $numericVersion
    }
    $displayVersion = if ($channel -eq "dev") { "$versionNumber dev" } else { $versionNumber }

    return [pscustomobject]@{
        Source = $source
        Channel = $channel
        VersionNumber = $versionNumber
        DisplayVersion = $displayVersion
        NumericVersion = $numericVersion
        BaseVersion = $versionParts.BaseVersion
        CommitCount = $commitCount
        ShortCommitSha = $shortSha
        PackageVersion = $versionNumber
        InformationalVersion = $versionNumber
        AssemblyVersion = $numericVersion
        FileVersion = $numericVersion
    }
}
