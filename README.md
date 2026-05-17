# Real-ESRGAN GUI

[简体中文](README.zh-CN.md)

Real-ESRGAN GUI is a Windows desktop app for making images sharper and larger with Real-ESRGAN. It wraps the bundled Real-ESRGAN NCNN/Vulkan backend in a desktop interface, so normal use does not require Python, PyTorch, CUDA, .NET Runtime, or command-line commands.

Use it when you want to upscale photos, portraits, anime images, or illustration/video frames locally on your PC.

## Download

1. Open the [latest release](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest).
2. Download `Real-ESRGAN-GUI-Setup-x64.exe` for 64-bit Windows, or `Real-ESRGAN-GUI-Setup-x86.exe` for 32-bit Windows 10.
3. Run the installer.
4. Open **Real-ESRGAN GUI** from the Start menu or desktop shortcut.

Do not download the repository source code zip if you only want to use the app. The installer contains the GUI, backend executable, .NET runtime files, and models.

## Quick Start

1. Put your images in one folder.
2. Open **Real-ESRGAN GUI**.
3. Click **Choose image folder** and select the folder with your images.
4. Click **Choose output folder** and select where results should be saved.
5. Pick an image type.
6. Leave the other settings at their defaults for the first run.
7. Click **Start upscaling**.

If the input folder is empty, add images to it before starting. The app no longer ships a sample input image.

## Choosing Settings

| Setting | What to choose |
| --- | --- |
| Image type | Use **Photo / portrait** for real-world photos, **Anime / illustration** for drawings, and the anime video options for animation frames. |
| Scale | Leave **Model default** unless you know you need 2x, 3x, or 4x output. |
| Format | Use **PNG** for best preservation, **JPG** for smaller files, or **WebP** for web use. |
| Enhanced quality | This can improve some outputs, but it is slower. Try it only after a normal run. |
| Advanced settings | Leave threads and GPU on **Auto** unless you are troubleshooting a specific device. |

Supported input files: `png`, `jpg`, `jpeg`, `bmp`, `webp`, `tif`, `tiff`.

Supported output formats: `png`, `jpg`, `webp`.

## Notes

- This release supports Windows 10/11 x64 and Windows 10 x86.
- Use the x64 installer on 64-bit Windows. Use the x86 installer only on 32-bit Windows 10.
- The x86 build can process smaller images, but it has a much lower memory ceiling than x64.
- You do not need to install .NET separately; the app is published as a self-contained Windows build.
- The app processes images locally. It does not upload images to a cloud service.
- The installed shortcut starts the app through `Launcher.exe`, which handles the startup splash and opens the main GUI.
- A Vulkan-capable GPU and a working graphics driver are recommended because the bundled backend uses NCNN/Vulkan.
- Very large images can take time and may use significant GPU memory.
- Installers are currently unsigned. If Windows SmartScreen appears, only continue when the file came from this repository's Releases page.

## Relationship to Real-ESRGAN

This project is a GUI distribution around the Real-ESRGAN NCNN/Vulkan backend. The upstream Real-ESRGAN project documents command-line and Python workflows, model research, training, and portable NCNN executables. This repository focuses on a simpler Windows GUI workflow: choose folders and settings, then run.

Useful upstream projects:

- [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN)
- [Real-ESRGAN-ncnn-vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan)

## For Developers

<details>
<summary>Build and repository notes</summary>

Build a complete distributable folder with:

```powershell
git submodule update --init --recursive
.\scripts\build-all.ps1 -Clean
```

This script builds the backend when needed, prepares the shared NCNN models, builds the native launcher, publishes the WPF app, and assembles a ready-to-ship folder under `artifacts/portable/<arch>/`.
The backend is skipped automatically when the generated `artifacts/backend/<arch>/engine/realesrgan-ncnn-vulkan.exe` already matches the backend source. Use `.\scripts\build-all.ps1 -Clean -ForceBackend` when you intentionally need a full backend rebuild.
Model preparation extracts only the required `*.bin` and `*.param` files from the official Real-ESRGAN NCNN release archive into `artifacts/models/`; it does not fully extract the archive and does not reuse bundled backend binaries, DLLs, videos, or sample inputs. Use `.\scripts\build-models.ps1 -Force` if you need to refresh the local model cache.

Build a local installer with:

```powershell
.\scripts\build-installer.ps1 -Clean
.\scripts\build-installer.ps1 -Clean -Architecture x86
```

Repository layout:

```text
src/
  Launcher/             Native Win32 splash launcher
  Real-ESRGAN-GUI/      WPF desktop application
scripts/
  build-all.ps1         Build backend, launcher, GUI, and portable output
  build-backend.ps1     Rebuild backend and copy it into artifacts/backend/<arch>/engine
  build-models.ps1      Prepare architecture-independent NCNN models
  backend-state.ps1     Backend build fingerprint helpers
  build-dist.ps1        Publish the GUI into artifacts/portable/<arch>
  build-installer.ps1   Build a local Windows installer
  version.ps1           Resolve app versions for builds and releases
  Start_Real-ESRGAN.ps1 PowerShell CLI wrapper
artifacts/
  backend/<arch>/engine Generated backend executable and runtime DLLs
  models/                Generated architecture-independent NCNN models
  portable/<arch>/      Ready-to-ship app folder
third_party/
  ncnn_src/             Backend source submodule
VERSION                 Base numeric version for development builds
```

</details>

## License

The original GUI, launcher, scripts, and repository-specific documentation are licensed under the MIT License. Bundled third-party components keep their own licenses and attributions; see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
