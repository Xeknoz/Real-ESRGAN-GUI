function Get-BackendRuntimeExePath {
    param(
        [string]$RepoRoot,
        [ValidateSet("x64", "x86")]
        [string]$Architecture = "x64"
    )

    return (Join-Path (Get-BackendRuntimeDir -RepoRoot $RepoRoot -Architecture $Architecture) "realesrgan-ncnn-vulkan.exe")
}

function Get-BackendRuntimeDir {
    param(
        [string]$RepoRoot,
        [ValidateSet("x64", "x86")]
        [string]$Architecture = "x64"
    )

    return (Join-Path (Join-Path (Join-Path $RepoRoot "artifacts\backend") $Architecture) "engine")
}

function Get-BackendFingerprintPath {
    param(
        [string]$RepoRoot,
        [ValidateSet("x64", "x86")]
        [string]$Architecture = "x64"
    )

    return (Join-Path (Get-BackendRuntimeDir -RepoRoot $RepoRoot -Architecture $Architecture) "realesrgan-ncnn-vulkan.buildfingerprint.json")
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
        [string]$Configuration = "Release",
        [ValidateSet("x64", "x86")]
        [string]$Architecture = "x64"
    )

    $backendRoot = Join-Path $RepoRoot "third_party\ncnn_src"
    $srcRoot = Join-Path $backendRoot "src"

    if (-not (Test-Path -LiteralPath $srcRoot)) {
        throw "Backend source directory was not found: $srcRoot"
    }

    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add("schema=1")
    $parts.Add("configuration=$Configuration")
    $parts.Add("architecture=$Architecture")

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
            if ($normalized.StartsWith("src/build/", [StringComparison]::OrdinalIgnoreCase) -or
                $normalized -match '^src/build-[^/]+/') {
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
        Where-Object {
            $relativePath = $_.FullName.Substring($srcRoot.Length).TrimStart('\', '/').Replace('\', '/')
            -not ($relativePath.StartsWith("build/", [StringComparison]::OrdinalIgnoreCase) -or
                $relativePath -match '^build-[^/]+/')
        } |
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
        [string]$Configuration = "Release",
        [ValidateSet("x64", "x86")]
        [string]$Architecture = "x64"
    )

    $exePath = Get-BackendRuntimeExePath -RepoRoot $RepoRoot -Architecture $Architecture
    $exeHash = if (Test-Path -LiteralPath $exePath -PathType Leaf) {
        Get-Sha256ForFile $exePath
    } else {
        $null
    }

    return [ordered]@{
        schema = 1
        configuration = $Configuration
        architecture = $Architecture
        sourceFingerprint = Get-BackendSourceFingerprint -RepoRoot $RepoRoot -Configuration $Configuration -Architecture $Architecture
        executableSha256 = $exeHash
    }
}

function Get-BackendBuildStatus {
    param(
        [string]$RepoRoot,
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Release",
        [ValidateSet("x64", "x86")]
        [string]$Architecture = "x64"
    )

    $exePath = Get-BackendRuntimeExePath -RepoRoot $RepoRoot -Architecture $Architecture
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "$Architecture runtime backend executable is missing" }
    }

    $fingerprintPath = Get-BackendFingerprintPath -RepoRoot $RepoRoot -Architecture $Architecture
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

    if ($record.architecture -ne $Architecture) {
        return [pscustomobject]@{ IsCurrent = $false; Reason = "backend architecture changed" }
    }

    $state = Get-BackendBuildState -RepoRoot $RepoRoot -Configuration $Configuration -Architecture $Architecture
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
        [string]$Configuration = "Release",
        [ValidateSet("x64", "x86")]
        [string]$Architecture = "x64"
    )

    $fingerprintPath = Get-BackendFingerprintPath -RepoRoot $RepoRoot -Architecture $Architecture
    $fingerprintDir = Split-Path -Parent $fingerprintPath
    New-Item -ItemType Directory -Force -Path $fingerprintDir | Out-Null

    $state = Get-BackendBuildState -RepoRoot $RepoRoot -Configuration $Configuration -Architecture $Architecture
    $json = ($state | ConvertTo-Json -Depth 4) + [Environment]::NewLine
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($fingerprintPath, $json, $utf8NoBom)

    return $state
}
