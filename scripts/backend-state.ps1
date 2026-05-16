function Get-BackendRuntimeExePath {
    param([string]$RepoRoot)

    return (Join-Path $RepoRoot "runtime\engine\realesrgan-ncnn-vulkan.exe")
}

function Get-BackendFingerprintPath {
    param([string]$RepoRoot)

    return (Join-Path $RepoRoot "runtime\engine\realesrgan-ncnn-vulkan.buildfingerprint.json")
}

function Get-Sha256ForText {
    param([string]$Text)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") }) -join "")
    }
    finally {
        $sha.Dispose()
    }
}

function Get-Sha256ForFile {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-BackendGit {
    param(
        [string]$BackendRoot,
        [string[]]$Arguments
    )

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return $null
    }

    $output = & git -C $BackendRoot @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return @($output)
}

function Get-BackendSourceFingerprint {
    param(
        [string]$RepoRoot,
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Release"
    )

    $backendRoot = Join-Path $RepoRoot "third_party\ncnn_src"
    $srcRoot = Join-Path $backendRoot "src"
    $buildRoot = Join-Path $srcRoot "build"

    if (-not (Test-Path -LiteralPath $srcRoot)) {
        throw "Backend source directory was not found: $srcRoot"
    }

    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add("schema=1")
    $parts.Add("configuration=$Configuration")

    $srcTree = Invoke-BackendGit -BackendRoot $backendRoot -Arguments @("rev-parse", "HEAD:src")
    if ($srcTree) {
        $parts.Add("mode=git")
        $parts.Add("tree=$($srcTree[0])")

        $diff = Invoke-BackendGit -BackendRoot $backendRoot -Arguments @("diff", "--binary", "--", "src")
        $cachedDiff = Invoke-BackendGit -BackendRoot $backendRoot -Arguments @("diff", "--cached", "--binary", "--", "src")
        $untracked = Invoke-BackendGit -BackendRoot $backendRoot -Arguments @("ls-files", "--others", "--exclude-standard", "--", "src")

        $parts.Add("diff=$(Get-Sha256ForText (($diff -join "`n")))")
        $parts.Add("cachedDiff=$(Get-Sha256ForText (($cachedDiff -join "`n")))")

        foreach ($relativePath in @($untracked | Sort-Object)) {
            $normalized = $relativePath.Replace('\', '/')
            if ($normalized.StartsWith("src/build/", [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $fullPath = Join-Path $backendRoot $relativePath
            if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
                $parts.Add("untracked=${normalized}:$(Get-Sha256ForFile $fullPath)")
            }
        }

        return Get-Sha256ForText ($parts -join "`n")
    }

    $parts.Add("mode=filesystem")
    $files = Get-ChildItem -LiteralPath $srcRoot -Recurse -File |
        Where-Object { -not $_.FullName.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object FullName

    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($srcRoot.Length).TrimStart('\', '/').Replace('\', '/')
        $parts.Add("$relativePath=$(Get-Sha256ForFile $file.FullName)")
    }

    return Get-Sha256ForText ($parts -join "`n")
}

function Get-BackendBuildState {
    param(
        [string]$RepoRoot,
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Release"
    )

    $exePath = Get-BackendRuntimeExePath -RepoRoot $RepoRoot
    $exeHash = if (Test-Path -LiteralPath $exePath -PathType Leaf) {
        Get-Sha256ForFile $exePath
    } else {
        $null
    }

    return [ordered]@{
        schema = 1
        configuration = $Configuration
        sourceFingerprint = Get-BackendSourceFingerprint -RepoRoot $RepoRoot -Configuration $Configuration
        executableSha256 = $exeHash
    }
}

function Get-BackendBuildStatus {
    param(
        [string]$RepoRoot,
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Release"
    )

    $exePath = Get-BackendRuntimeExePath -RepoRoot $RepoRoot
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "runtime backend executable is missing" }
    }

    $fingerprintPath = Get-BackendFingerprintPath -RepoRoot $RepoRoot
    if (-not (Test-Path -LiteralPath $fingerprintPath -PathType Leaf)) {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "backend build fingerprint is missing" }
    }

    try {
        $record = Get-Content -LiteralPath $fingerprintPath -Raw | ConvertFrom-Json
    }
    catch {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "backend build fingerprint is invalid" }
    }

    if ($record.configuration -ne $Configuration) {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "backend configuration changed" }
    }

    $state = Get-BackendBuildState -RepoRoot $RepoRoot -Configuration $Configuration
    if ($record.sourceFingerprint -ne $state.sourceFingerprint) {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "backend source changed" }
    }

    if ($record.executableSha256 -ne $state.executableSha256) {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "runtime backend executable changed" }
    }

    return [pscustomobject]@{ IsCurrent = $true; Reason = "backend source unchanged" }
}

function Write-BackendBuildFingerprint {
    param(
        [string]$RepoRoot,
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Release"
    )

    $fingerprintPath = Get-BackendFingerprintPath -RepoRoot $RepoRoot
    $fingerprintDir = Split-Path -Parent $fingerprintPath
    New-Item -ItemType Directory -Force -Path $fingerprintDir | Out-Null

    $state = Get-BackendBuildState -RepoRoot $RepoRoot -Configuration $Configuration
    $json = ($state | ConvertTo-Json -Depth 4) + [Environment]::NewLine
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($fingerprintPath, $json, $utf8NoBom)

    return $state
}
