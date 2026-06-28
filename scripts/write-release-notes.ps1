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
$sha256Sums = "$releaseBaseUrl/SHA256SUMS.txt"
$releaseManifest = "$releaseBaseUrl/release-manifest.json"

function Get-ReleaseHighlights {
    param([string]$Version)

    if ($Version -eq "1.0.3") {
        return [pscustomobject]@{
            English = @(
                "- Added update checking in the About window.",
                "- Improved the version display when a newer version is available.",
                "- Improved Anime Video model scale and frame-format handling."
            )
            Chinese = @(
                "- 在 About 窗口中添加检查更新功能。",
                "- 改进发现新版本时的版本号展示。",
                "- 改进 Anime Video 模型的倍率选择和帧格式处理。"
            )
        }
    }

    if ($Version -eq "1.0.2") {
        return [pscustomobject]@{
            English = @(
                "- Fixed a bug when processing image scaling.",
                "- Added a hint to the image scaling dropdown.",
                "- Updated and expanded license information."
            )
            Chinese = @(
                "- 修复处理图像缩放时的 Bug",
                "- 添加图像缩放下拉菜单的提示",
                "- 变更并完善许可证说明"
            )
        }
    }

    if ($Version -eq "1.0.1") {
        return [pscustomobject]@{
            English = @(
                "- The app is more stable when opening.",
                "- The About window is clearer."
            )
            Chinese = @(
                "- 改进软件启动稳定性。",
                "- 关于窗口说明更加清晰。"
            )
        }
    }

    return [pscustomobject]@{
        English = @(
            "- This release includes the latest app improvements.",
            "- For most people, the x64 installer is the right download."
        )
        Chinese = @(
            "- 本版本包含最新的软件改进。",
            "- 大多数用户建议下载 x64 安装包。"
        )
    }
}

$highlights = Get-ReleaseHighlights -Version $version
$englishHighlights = $highlights.English -join "`r`n"
$chineseHighlights = $highlights.Chinese -join "`r`n"

$content = @"
## Download / 下载

Most people should download the x64 installer. It includes the GUI, launcher, backend, models, and .NET runtime.

大多数 Windows 10/11 用户建议下载 x64 安装包。安装包已经包含 GUI、启动器、后端、模型和 .NET runtime。

| Your computer / 你的电脑 | Download / 下载 |
| --- | --- |
| Windows 10/11, 64-bit / 64 位 Windows 10/11 | [Download installer for x64 / 下载 x64 安装包]($setupX64) |
| Windows 10, 32-bit / 32 位 Windows 10 | [Download installer for x86 / 下载 x86 安装包]($setupX86) |
| No-install x64 / 64 位免安装单文件版 | [Download portable x64 / 下载 x64 单文件绿色版]($portableX64) |
| No-install x86 / 32 位免安装单文件版 | [Download portable x86 / 下载 x86 单文件绿色版]($portableX86) |

Do not download "Source code (zip)" or "Source code (tar.gz)" if you only want to use the app.

仅使用软件时，请不要下载 "Source code (zip)" 或 "Source code (tar.gz)"。

## Check the download / 验证下载文件

This release is unsigned, so Windows may show "Unknown publisher" or a SmartScreen warning. That is expected for this version. Do not turn off Windows security; check the downloaded file first.

当前发布二进制未做代码签名。Windows 可能显示 "Unknown publisher" 或 SmartScreen 提示。这是本版本的预期情况；请不要关闭 Windows 安全功能，并先验证下载文件。

Quick check / 验证步骤：

1. Download the installer or portable file from this Release. / 从本 Release 下载安装包或绿色版。
2. Download [SHA256SUMS.txt]($sha256Sums) from the same Release. / 从同一个 Release 下载 [SHA256SUMS.txt]($sha256Sums)。
3. Open PowerShell in the folder where the downloaded file is saved, then run: / 在文件所在目录打开 PowerShell，运行：

~~~powershell
Get-FileHash .\Real-ESRGAN-GUI-Setup-x64.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
~~~

4. Compare the SHA256 value with the line for the same file name in SHA256SUMS.txt. If the values differ, delete the file and download it again. / 将输出的 SHA256 与 SHA256SUMS.txt 中同名文件的记录进行比对。如果两者不一致，请删除文件并重新下载。

This only checks that your local file matches the file published in the Release. It does not require a GitHub account.

这一步只是确认你电脑上的文件和 Release 里发布的文件一致，不需要 GitHub 账号。

For extra detail, [release-manifest.json]($releaseManifest) lists the tag, commit, workflow run, file sizes, SHA256 hashes, and submodule revisions. If you already use GitHub CLI, you can also check the release provenance:

如需查看更完整的发布记录，可以下载 [release-manifest.json]($releaseManifest)。它会列出 tag、commit、workflow run、文件大小、SHA256 和子模块版本。已经安装 GitHub CLI 的用户，也可以验证来源证明：

~~~powershell
gh attestation verify .\Real-ESRGAN-GUI-Setup-x64.exe -R Xeknoz/Real-ESRGAN-GUI
~~~

## What's new / 更新内容

$englishHighlights

---

$chineseHighlights

## Notes / 注意事项

- For most Windows 10/11 PCs, choose the x64 installer above.
- A dedicated GPU and a recent graphics driver are recommended for faster processing.
- Very large images may take longer and need more memory.

---

- 大多数 Windows 10/11 电脑请选择上方的 x64 安装包。
- 建议使用独立显卡和较新的显卡驱动，有助于提升处理速度和稳定性。
- 特别大的图片可能需要更久时间和更多内存。
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($resolvedOutputPath, $content, $utf8NoBom)
Write-Host "Release notes written: $resolvedOutputPath"
$resolvedOutputPath
