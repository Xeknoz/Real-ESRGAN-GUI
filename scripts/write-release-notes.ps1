[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [string]$OutputPath
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

$version = $Tag.TrimStart("v")
$resolvedOutputPath = Resolve-FullPath -Path $(if ([string]::IsNullOrWhiteSpace($OutputPath)) { "artifacts\release-notes\$Tag.md" } else { $OutputPath }) -BasePath $repoRoot
$outputDir = Split-Path -Parent $resolvedOutputPath
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$releaseBaseUrl = "https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/download/$Tag"
$setupX64 = "$releaseBaseUrl/Real-ESRGAN-GUI-Setup-x64.exe"
$setupX86 = "$releaseBaseUrl/Real-ESRGAN-GUI-Setup-x86.exe"
$portableX64 = "$releaseBaseUrl/Real-ESRGAN-GUI-Portable-x64.exe"
$portableX86 = "$releaseBaseUrl/Real-ESRGAN-GUI-Portable-x86.exe"

function Get-GitOutput {
    param(
        [string[]]$Arguments
    )

    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        return @()
    }

    $output = & git -C $repoRoot @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return @()
    }

    return @($output)
}

function ConvertTo-VersionObject {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $normalized = $Value.Trim()
    if ($normalized.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(1)
    }

    if ($normalized -notmatch '^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:\.(?<revision>0|[1-9]\d*))?$') {
        return $null
    }

    $revision = if ($Matches["revision"]) { [int]$Matches["revision"] } else { 0 }
    return [version]::new([int]$Matches["major"], [int]$Matches["minor"], [int]$Matches["patch"], $revision)
}

function Get-PreviousVersionTag {
    param([string]$CurrentTag)

    $currentVersion = ConvertTo-VersionObject -Value $CurrentTag
    if (-not $currentVersion) {
        return $null
    }

    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($candidateTag in @(Get-GitOutput -Arguments @("tag", "--list", "v[0-9]*"))) {
        if ([string]::IsNullOrWhiteSpace($candidateTag) -or $candidateTag -eq $CurrentTag) {
            continue
        }

        $candidateVersion = ConvertTo-VersionObject -Value $candidateTag
        if ($candidateVersion -and $candidateVersion -lt $currentVersion) {
            $candidates.Add([pscustomobject]@{
                Tag = $candidateTag
                Version = $candidateVersion
            })
        }
    }

    $previous = $candidates | Sort-Object -Property Version -Descending | Select-Object -First 1
    if ($previous) {
        return $previous.Tag
    }

    return $null
}

function Format-ChangeList {
    param([string]$PreviousTag)

    if ([string]::IsNullOrWhiteSpace($PreviousTag)) {
        return @(
            "- First release of Real-ESRGAN GUI $version.",
            "- Includes Windows installers for x64 and x86.",
            "- Includes single-file portable executables for x64 and x86.",
            "- Bundles the GUI, launcher, Real-ESRGAN NCNN/Vulkan backend, models, .NET runtime files, and license notices.",
            "- Supports folder-based batch upscaling, photo/anime/video model choices, PNG/JPG/WebP output, GPU and thread settings, and enhanced-quality mode."
        )
    }

    $subjects = @(Get-GitOutput -Arguments @("log", "--first-parent", "--max-count=12", "--pretty=format:%s", "$PreviousTag..HEAD")) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if ($subjects.Count -eq 0) {
        return @("- Maintenance update built from tag $Tag.")
    }

    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($subject in $subjects) {
        $lines.Add("- $subject")
    }

    if ($subjects.Count -eq 12) {
        $lines.Add("- Additional internal build, documentation, and release maintenance updates.")
    }

    return @($lines)
}

$previousTag = Get-PreviousVersionTag -CurrentTag $Tag
$changeHeading = if ($previousTag) {
    "Changes since $previousTag / 自 $previousTag 以来的变化"
}
else {
    "Initial release / 首次发布"
}
$changeList = (Format-ChangeList -PreviousTag $previousTag) -join "`r`n"

$content = @"
## Download / 下载

Most people should download the x64 installer. It includes the GUI, launcher, backend, models, and .NET runtime.

大多数 Windows 10/11 用户下载 x64 安装包即可。安装包已经包含 GUI、启动器、后端、模型和 .NET runtime。

| Your computer / 你的电脑 | Download / 下载 |
| --- | --- |
| Windows 10/11, 64-bit / 64 位 Windows 10/11 | [Download installer for x64 / 下载 x64 安装包]($setupX64) |
| Windows 10, 32-bit / 32 位 Windows 10 | [Download installer for x86 / 下载 x86 安装包]($setupX86) |
| No-install x64 / 64 位免安装单文件版 | [Download portable x64 / 下载 x64 单文件绿色版]($portableX64) |
| No-install x86 / 32 位免安装单文件版 | [Download portable x86 / 下载 x86 单文件绿色版]($portableX86) |

Do not download "Source code (zip)" or "Source code (tar.gz)" if you only want to use the app.

如果只是使用软件，不要下载 "Source code (zip)" 或 "Source code (tar.gz)"。

## What's new / 更新内容

### $changeHeading

$changeList

### Included packages / 包含内容

- Includes Windows installers for x64 and x86.
- Includes single-file portable executables for x64 and x86.
- Bundles the GUI, launcher, Real-ESRGAN NCNN/Vulkan backend, models, .NET runtime files, and license notices.
- The installer uses current-user installation by default and can be switched to all-users installation when needed.

---

- 提供 x64 和 x86 Windows 安装包。
- 提供 x64 和 x86 单文件绿色版。
- 随包包含 GUI、启动器、Real-ESRGAN NCNN/Vulkan 后端、模型、.NET runtime 文件和许可证说明。
- 安装包默认仅为当前用户安装，需要时可以切换为所有用户安装。

## Notes / 注意事项

- For most Windows 10/11 PCs, choose the x64 installer above.
- A dedicated GPU and a recent graphics driver are recommended for faster processing.
- Very large images may take longer and need more memory.

---

- 大多数 Windows 10/11 电脑请选择上方的 x64 安装包。
- 建议使用独立显卡和较新的显卡驱动，处理速度会更稳定。
- 特别大的图片可能需要更久时间和更多内存。
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedOutputPath, $content, $utf8NoBom)
Write-Host "Release notes written: $resolvedOutputPath"
$resolvedOutputPath
