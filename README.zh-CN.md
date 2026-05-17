# Real-ESRGAN GUI

[English](README.md)

Real-ESRGAN GUI 是一个 Windows 桌面图片清晰化工具。它把随包附带的 Real-ESRGAN NCNN/Vulkan 后端包装成桌面界面，日常使用不需要安装 Python、PyTorch、CUDA、.NET Runtime，也不需要输入命令行。

它适合在本机处理照片、人像、动漫图片、插画或动画帧。

## 下载

1. 打开 [最新 Release](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest)。
2. 64 位 Windows 下载 `Real-ESRGAN-GUI-Setup-x64.exe`；32 位 Windows 10 下载 `Real-ESRGAN-GUI-Setup-x86.exe`。
3. 运行安装程序。
4. 从开始菜单或桌面快捷方式打开 **Real-ESRGAN GUI**。

如果只是使用软件，不要下载仓库源码压缩包。安装程序已经包含 GUI、后端程序、.NET 运行时文件和模型。

## 快速开始

1. 把要处理的图片放进同一个文件夹。
2. 打开 **Real-ESRGAN GUI**。
3. 点击 **选择图片文件夹**，选择你的图片目录。
4. 点击 **选择保存文件夹**，选择输出结果保存位置。
5. 选择图片类型。
6. 第一次运行时，其他设置保持默认即可。
7. 点击 **开始清晰化**。

如果输入文件夹为空，请先放入要处理的图片再开始；软件不再附带示例输入图。

## 怎么选设置

| 设置 | 建议 |
| --- | --- |
| 图片类型 | 真实照片选 **照片 / 人像**，绘画类图片选 **动漫 / 插画**，动画帧选对应的动漫视频选项。 |
| 放大倍数 | 不确定时保持 **模型默认**。需要固定尺寸时再选择 2x、3x 或 4x。 |
| 保存格式 | 想尽量保留质量选 **PNG**，想减小文件选 **JPG**，网页使用可选 **WebP**。 |
| 质量增强 | 可能改善部分结果，但速度更慢。建议先普通运行，再决定是否开启。 |
| 高级设置 | 线程数和 GPU 默认自动即可，只有排查设备问题时再手动修改。 |

支持的输入文件：`png`、`jpg`、`jpeg`、`bmp`、`webp`、`tif`、`tiff`。

支持的输出格式：`png`、`jpg`、`webp`。

## 使用说明

- 当前版本支持 Windows 10/11 x64 和 Windows 10 x86。
- 64 位 Windows 使用 x64 安装包；x86 安装包只面向 32 位 Windows 10。
- x86 版本可以处理较小图片，但可用内存上限明显低于 x64。
- 用户不需要单独安装 .NET；本应用按 Windows self-contained 方式发布。
- 图片在本机处理，不会上传到云端服务。
- 安装后的快捷方式会通过 `Launcher.exe` 启动软件；启动器负责显示闪屏并拉起主界面。
- 后端使用 NCNN/Vulkan，建议使用支持 Vulkan 的显卡和正常工作的显卡驱动。
- 特别大的图片可能处理较慢，也可能占用较多显存。
- 当前安装程序未做代码签名。如果 Windows SmartScreen 弹窗，请只在确认文件来自本仓库 Releases 页面后继续。

## 与 Real-ESRGAN 的关系

本项目是围绕 Real-ESRGAN NCNN/Vulkan 后端制作的 GUI 发行版。上游 Real-ESRGAN 项目主要提供命令行、Python、模型研究、训练和便携版 NCNN 程序说明；本仓库聚焦更简单的 Windows 图形界面流程：选择文件夹和设置，然后运行。

相关上游项目：

- [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN)
- [Real-ESRGAN-ncnn-vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan)

## 开发者说明

<details>
<summary>构建和仓库说明</summary>

一键生成发布产物：

```powershell
git submodule update --init --recursive
.\scripts\build-release.ps1
```

在 Windows 上也可以双击 `scripts/build-release.cmd`。发布脚本会生成 `artifacts/portable/<arch>/` 下的 x64 和 x86 便携版目录，然后在 `artifacts/installers/` 下生成未签名安装包，并在后端构建成功后清理后端 CMake 构建目录。
只构建单一架构时使用 `.\scripts\build-release.ps1 -Architecture x64`；只需要便携版目录时使用 `.\scripts\build-release.ps1 -SkipInstaller`。

`build-all.ps1` 每次构建一个便携版目录。该脚本会自动编译需要更新的后端、准备共享 NCNN 模型、编译启动器、发布 WPF 主程序，并组装好 `artifacts/portable/<arch>/` 下可直接分发的目录。如果当前生成的 `artifacts/backend/<arch>/engine/realesrgan-ncnn-vulkan.exe` 已经与后端源码匹配，则会自动跳过后端编译；需要强制重编后端时使用 `.\scripts\build-all.ps1 -Clean -ForceBackend`。
模型准备步骤只会从官方 Real-ESRGAN NCNN release 压缩包中抽取必需的 `*.bin` 和 `*.param` 到 `artifacts/models/`；不会全量解压压缩包，也不会复用压缩包里的后端程序、DLL、视频或示例输入图。需要刷新本地模型缓存时使用 `.\scripts\build-models.ps1 -Force`。

仓库结构：

```text
src/
  Launcher/             原生 Win32 启动器
  Real-ESRGAN-GUI/      WPF 桌面程序
scripts/
  build-all.ps1         构建后端、启动器、GUI 与 portable 输出
  build-backend.ps1     重建后端并复制到 artifacts/backend/<arch>/engine
  build-models.ps1      准备架构无关 NCNN 模型
  backend-state.ps1     后端构建指纹辅助脚本
  build-dist.ps1        发布 GUI 到 artifacts/portable/<arch>
  build-installer.ps1   本地生成 Windows 安装程序
  build-release.ps1     构建 x64/x86 便携版目录和安装包
  build-release.cmd     build-release.ps1 的双击入口
  version.ps1           构建与发布版本解析脚本
  Start_Real-ESRGAN.ps1 PowerShell 命令行入口
artifacts/
  backend/<arch>/engine 生成的后端程序与运行时 DLL
  installers/           未签名 Windows 安装包
  models/                生成的架构无关 NCNN 模型
  portable/<arch>/      可分发应用目录
third_party/
  ncnn_src/             后端源码子模块
VERSION                 开发构建的数字基础版本号
```

</details>

## 许可证

当前仓库中的 GUI、启动器、脚本和仓库专属文档使用 MIT License。随仓库附带的第三方组件继续保留各自原始许可证与署名，详见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。
