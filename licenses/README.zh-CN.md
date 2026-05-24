# 许可证文件

**语言选择：[English](README.md) | 简体中文**

## 为什么有这个目录

本目录存放运行时需要携带、但在仓库源码树里没有稳定独立文件可直接复制的许可证文本。

用户在 portable 目录或安装目录中看到的 `licenses/` 是由
`scripts/build-dist.ps1` 生成的。那个生成目录才是最终随软件交付给用户的完整
许可证目录。它会合并：

- 本目录直接维护的少量许可证文件；
- 从各上游源码树复制的许可证文件；
- 从本机 Microsoft 构建/运行时安装目录复制的运行时声明。

所以，源码里的这个目录不需要手工保存最终发布包里的每一个 notice 文件。它只负责放那些
在仓库或构建环境里没有更权威来源路径的许可证文本。

## 本目录维护的文件

| 最终文件 | 来源原因 |
| --- | --- |
| `Real-ESRGAN-LICENSE.txt` | 模型文件从官方 Real-ESRGAN NCNN release 压缩包中抽取，不来自仓库中签入的 Real-ESRGAN 源码树。 |
| `dirent-MIT-LICENSE.txt` | `third_party/ncnn_src/src/win32dirent.h` 会编译进 Windows 后端，源码里只有简短头部声明，没有独立许可证文件。 |
| `stb-LICENSE.txt` | `third_party/ncnn_src/src/stb_image.h` 和 `stb_image_write.h` 会编译进后端，许可证以内嵌双许可文本形式存在，而不是独立文件。 |

## 发布构建时复制的文件

下面这些文件会在发布构建时复制进最终运行时 `licenses/` 目录。它们应以各自的来源路径为准，
这样许可证更新会自然跟随对应组件，而不是在这里维护第二份副本。

| 最终文件 | 构建来源 |
| --- | --- |
| `Real-ESRGAN-ncnn-vulkan-Enhanced-LICENSE.txt` | `third_party/ncnn_src/LICENSE` |
| `ncnn-LICENSE.txt` | `third_party/ncnn_src/src/ncnn/LICENSE.txt` |
| `glslang-LICENSE.txt` | `third_party/ncnn_src/src/ncnn/glslang/LICENSE.txt` |
| `libwebp-COPYING.txt` | `third_party/ncnn_src/src/libwebp/COPYING` |
| `libwebp-PATENTS.txt` | `third_party/ncnn_src/src/libwebp/PATENTS` |
| `pybind11-LICENSE.txt` | `third_party/ncnn_src/src/ncnn/python/pybind11/LICENSE` |
| `Microsoft-dotnet-LICENSE.txt` | 自包含发布所解析到的 .NET 安装目录中的 `LICENSE.txt`。 |
| `Microsoft-dotnet-ThirdPartyNotices.txt` | 自包含发布所解析到的 .NET 安装目录中的 `ThirdPartyNotices.txt`。 |
| `Microsoft-Visual-Cpp-Redistributable-Redist.txt` | Visual Studio C++ Build Tools 的 `Licenses/1033/Redist.txt`，对应随包复制的 `engine/vcomp140.dll`。 |

## 仅安装器使用的 notice

只在安装器里展示的 notice 来源放在 `packaging/windows/`，因为它们由安装器显示，
不会复制进标准 portable-folder runtime。可选的 Enigma 单文件绿色版只在实际使用
Enigma 打包时加入 `licenses/Enigma-Virtual-Box-LICENSE.txt`。
