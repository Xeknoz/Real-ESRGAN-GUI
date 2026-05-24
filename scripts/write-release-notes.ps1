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

function Get-ReleaseHighlights {
    param([string]$Version)

    if ($Version -eq "1.0.1") {
        return [pscustomobject]@{
            English = @(
                "- Rebuilds the x64 and x86 Windows installers and single-file portable executables for version $Version.",
                "- Packages the GUI startup fixes so the launcher splash waits for a stable first render before handing off to the main window.",
                "- Packages the About and license-notice fixes so bundled third-party notices open as readable plain text.",
                "- Keeps the bundled Real-ESRGAN NCNN/Vulkan backend, model files, .NET runtime files, and license notices in every downloadable app package."
            )
            Chinese = @(
                "- 重新生成 $Version 的 x64 / x86 Windows 安装包和单文件绿色版。",
                "- 随包包含 GUI 启动修复：启动器 splash 会等待主窗口稳定完成首次渲染后再交接。",
                "- 随包包含 About 与许可证说明修复：内置第三方 notice 会以可读的纯文本方式打开。",
                "- 每个可下载应用包都继续内置 Real-ESRGAN NCNN/Vulkan 后端、模型文件、.NET runtime 文件和许可证说明。"
            )
        }
    }

    return [pscustomobject]@{
        English = @(
            "- Rebuilds the x64 and x86 Windows installers and single-file portable executables for version $Version.",
            "- Includes the current packaged GUI, launcher, Real-ESRGAN NCNN/Vulkan backend, model files, .NET runtime files, and license notices."
        )
        Chinese = @(
            "- 重新生成 $Version 的 x64 / x86 Windows 安装包和单文件绿色版。",
            "- 随包包含当前 GUI、启动器、Real-ESRGAN NCNN/Vulkan 后端、模型文件、.NET runtime 文件和许可证说明。"
        )
    }
}

$highlights = Get-ReleaseHighlights -Version $version
$englishHighlights = $highlights.English -join "`r`n"
$chineseHighlights = $highlights.Chinese -join "`r`n"

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

### Release highlights / 更新重点

$englishHighlights

---

$chineseHighlights

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
