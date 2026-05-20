# Real-ESRGAN GUI

[English](README.md)

Real-ESRGAN GUI 是一个 Windows 图片放大和清晰化工具。它使用随包附带的 Real-ESRGAN NCNN/Vulkan 后端，但你不需要输入命令，也不需要安装 Python、PyTorch、CUDA 或 .NET Runtime。

日常使用很直接：选择图片文件夹，选择输出文件夹，选图片类型，然后开始处理。它适合处理照片、人像、动漫图片、插画和动画帧。

图片会在你的电脑本机处理，不会上传到云端服务。

## 下载和安装

打开 [最新 Release](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest)，在 Assets 里下载应用文件。

大多数用户可以这样选：

- 64 位 Windows 10 或 Windows 11 下载 `Real-ESRGAN-GUI-Setup-x64.exe`。
- 32 位 Windows 10 才下载 `Real-ESRGAN-GUI-Setup-x86.exe`。
- 如果想免安装使用，下载 Release 里提供的便携压缩包，例如 `Real-ESRGAN-GUI-win-x64.zip`。

如果只是使用软件，不要下载 "Source code (zip)" 或 "Source code (tar.gz)"。那是给开发者看的源码包，不是可直接运行的 GUI 软件包。

当前安装程序还没有代码签名。如果 Windows SmartScreen 弹窗，请只在确认文件来自本仓库 Releases 页面后继续。

## 安装版怎么用

1. 运行 `Real-ESRGAN-GUI-Setup-x64.exe`；如果你是 32 位 Windows 10，则运行 x86 安装包。
2. 按安装向导完成安装。
3. 从开始菜单或桌面快捷方式打开 Real-ESRGAN GUI。

安装包已经包含 GUI、启动器、后端程序、.NET 运行时文件、模型和许可证说明。

## 免安装版怎么用

1. 从 Release 页面下载便携压缩包，例如 `Real-ESRGAN-GUI-win-x64.zip`。
2. 把整个压缩包解压到普通文件夹，例如 `C:\Apps\Real-ESRGAN GUI\`。
3. 打开解压后的文件夹，运行 `Launcher.exe`。
4. 不要拆散目录里的文件。`engine\` 文件夹和模型文件必须和程序文件放在一起。

不要直接在压缩包里运行程序。想删除免安装版时，先关闭软件，再删除解压出来的文件夹即可。

## 快速开始

1. 把要处理的图片放进同一个文件夹。
2. 打开 Real-ESRGAN GUI。
3. 点击 `选择图片文件夹`，选择你的图片目录。
4. 点击 `选择保存文件夹`，选择结果保存位置。
5. 选择图片类型。
6. 第一次运行时，其他设置保持默认即可。
7. 点击 `开始清晰化`。

如果输入文件夹为空，请先放入图片再开始。软件不附带示例输入图。

## 设置怎么选

| 设置 | 实用建议 |
| --- | --- |
| 图片类型 | 真实照片选 `照片 / 人像`，绘画类图片选 `动漫 / 插画`，动画帧选对应的 `动漫视频` 选项。 |
| 放大倍数 | 不确定时保持 `模型默认`。只有需要固定 2x、3x 或 4x 输出时再手动选择。 |
| 保存格式 | 更在意保留质量选 `PNG`，想减小文件选 `JPG`，网页使用可选 `WebP`。 |
| 质量增强 | 建议先普通运行一次。`质量增强` 可能改善部分图片，但速度更慢。 |
| 高级设置 | 线程数和 GPU 保持 `自动` 即可，只有排查设备问题时再手动修改。 |

支持的输入文件：`png`、`jpg`、`jpeg`、`bmp`、`webp`、`tif`、`tiff`。

支持的输出格式：`png`、`jpg`、`webp`。

## 用户须知

- 当前发布目标是 Windows 10/11 x64 和 Windows 10 x86。
- 64 位 Windows 请使用 x64 版本。x86 版本内存上限低，主要给 32 位 Windows 10 使用。
- 后端使用 NCNN/Vulkan，建议使用支持 Vulkan 的显卡和较新的显卡驱动。
- 特别大的图片可能处理很久，也可能因为显存不足而失败。
- 正常发布入口是 `Launcher.exe`。它负责显示启动闪屏并打开主界面。

## 与 Real-ESRGAN 的关系

本仓库是围绕 Real-ESRGAN NCNN/Vulkan 后端制作的 Windows GUI 发行版。上游 Real-ESRGAN 项目还包含命令行用法、Python 工作流、模型研究、训练和独立 NCNN 发布包。本项目把普通用户流程收窄为：选择文件夹和设置，然后在本机运行。

相关上游项目：

- [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN)
- [Real-ESRGAN-ncnn-vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan)

## 从源码构建

基础构建环境：

- Windows 10/11 x64
- Git
- PowerShell 5.1 或更新版本
- .NET SDK 9
- Git submodules

完整发布构建还需要：

- Visual Studio C++ Build Tools，包含 x64 和 x86 工具链
- Windows SDK
- CMake 3.10 或更新版本
- Vulkan SDK；构建 x86 时需要 SDK 里有 `Lib32\vulkan-1.lib`
- Inno Setup 6；只有生成安装包时才需要

克隆仓库并初始化后端子模块：

```powershell
git clone --recursive https://github.com/Xeknoz/Real-ESRGAN-GUI.git
cd Real-ESRGAN-GUI
git submodule update --init --recursive
```

如果你已经克隆过仓库，但还没有初始化子模块，只需要运行：

```powershell
git submodule update --init --recursive
```

编译 WPF GUI 项目：

```powershell
dotnet build src/Real-ESRGAN-GUI/RealESRGAN-GUI.csproj
```

构建 x64 便携版目录：

```powershell
.\scripts\build-all.ps1 -Clean -Architecture x64
```

输出位置：

```text
artifacts\portable\x64\
```

构建两个发布架构和安装包：

```powershell
.\scripts\build-release.ps1
```

只构建便携版目录，不生成安装包：

```powershell
.\scripts\build-release.ps1 -SkipInstaller
```

只构建单一架构：

```powershell
.\scripts\build-release.ps1 -Architecture x64
.\scripts\build-release.ps1 -Architecture x86
```

常用的单项命令：

```powershell
.\src\Launcher\build.ps1
.\scripts\build-backend.ps1 -Clean -Architecture x64
.\scripts\build-models.ps1
.\scripts\build-all.ps1 -Clean -ForceBackend
.\scripts\build-models.ps1 -Force
.\scripts\build-installer.ps1 -Clean -Architecture x64
```

生成产物都在 `artifacts\` 下：

```text
artifacts\
  backend\<arch>\engine\   生成的后端程序和运行时 DLL
  models\                  生成的 NCNN 模型文件，各架构共用
  portable\<arch>\         可直接运行的便携版目录
  installers\              未签名 Windows 安装包
```

便携版目录里应包含 `Launcher.exe`、`Real-ESRGAN GUI.exe`、`engine\realesrgan-ncnn-vulkan.exe`、`engine\models\` 下的模型文件、版本标记和许可证说明。

## 许可证

GUI、启动器、脚本和本仓库专属文档使用 MIT License。随包附带的第三方组件保留各自原始许可证和署名，详见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。
