# Real-ESRGAN GUI

[English](README.md)

Real-ESRGAN GUI 是一个面向 Windows 的图片清晰化工具。它尽量把操作做得简单：选择输入文件夹、输出文件夹和图片类型，然后点击开始即可。

## 下载与使用

1. 打开最新的 GitHub Release。
2. 下载 `Real-ESRGAN-GUI-win-x64.zip`。
3. 将压缩包解压到任意文件夹。
4. 双击 `Launcher.exe` 启动软件。
5. 选择输入文件夹、输出文件夹和图片类型，然后开始处理。

说明：
- 请通过 `Launcher.exe` 启动软件，主程序会由它自动拉起。
- 如果输入文件夹为空，软件可以自动复制一张示例图片，方便你先试一下效果。
- 这是便携版软件，关闭后可直接移动整个文件夹。

## 功能特点

- 现代化的桌面界面
- 原生启动器，启动时会先显示闪屏，减少等待焦虑
- 内置简体中文和英文界面
- 支持批量处理，并分别显示总进度与当前文件进度
- 使用随包附带的 Real-ESRGAN NCNN/Vulkan 后端在本地处理

## 开发者说明

一键生成完整发布目录：

```powershell
.\scripts\build-all.ps1 -Clean
```

该脚本会自动编译需要更新的后端、编译启动器、发布 WPF 主程序，并组装好可直接分发的 `dist/` 目录。如果当前 `runtime/engine/realesrgan-ncnn-vulkan.exe` 已经与后端源码匹配，则会自动跳过后端编译；需要强制重编后端时使用 `.\scripts\build-all.ps1 -Clean -ForceBackend`。软件版本会从 `v1.0.1` 这类 release tag、显式 `-Version 1.0.1` 参数或根目录 `VERSION` 文件中解析。

仓库结构：

```text
src/
  Launcher/             原生 Win32 启动器
  RealESRGAN-GUI/       WPF 桌面程序
scripts/
  build-all.ps1         构建后端、启动器、GUI 与 dist
  build-backend.ps1     重建后端并复制到 runtime/engine
  backend-state.ps1     后端构建指纹辅助脚本
  build-dist.ps1        一键构建脚本
  version.ps1           构建与发布版本解析脚本
  Start_Real-ESRGAN.ps1 PowerShell 命令行入口
runtime/
  engine/               后端程序、运行时 DLL 与模型文件
  input.jpg             发布时附带的示例图片
third_party/
  ncnn_src/             后端源码子模块
VERSION                  开发构建的基础版本号
```

发布自动化：
- 可以手动运行 `.github/workflows/release.yml` 并填写可选版本号，也可以推送 `v1.0.1` 这类 tag 自动触发。
- tag 发布会直接使用 tag 作为软件显示版本；非 tag 构建会使用 `VERSION` 加提交信息，例如 `1.0.0-dev.58.g26d6948`。
- workflow 会生成 `dist/`、上传构建产物，并把 `Real-ESRGAN-GUI-win-x64.zip` 附加到对应的 GitHub Release。

## 许可证

当前仓库中的原创代码使用 MIT License。随仓库附带的第三方组件继续保留各自原始许可证与署名，详见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。
