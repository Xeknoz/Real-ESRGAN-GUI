# Real-ESRGAN GUI

English | [简体中文](README.zh-CN.md)

Real-ESRGAN GUI is a Windows app for making images larger and sharper on your own PC. It uses the bundled Real-ESRGAN NCNN/Vulkan backend, but you do not need to type commands or install Python, PyTorch, CUDA, or the .NET Runtime.

The usual workflow is simple: choose an input folder, choose an output folder, pick the image type, then start. The app is meant for photos, portraits, anime images, illustrations, and animation frames.

Your images are processed locally. They are not uploaded to a cloud service.

## Download and install

These links always point to the latest release. If you are not sure which one to choose, download the first one.

| Your computer | Download |
| --- | --- |
| Windows 10/11, 64-bit | [Download installer for x64](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest/download/Real-ESRGAN-GUI-Setup-x64.exe) |
| Windows 10, 32-bit | [Download installer for x86](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest/download/Real-ESRGAN-GUI-Setup-x86.exe) |
| No-install copy on 64-bit Windows | [Download portable x64](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest/download/Real-ESRGAN-GUI-Portable-x64.exe) |
| No-install copy on 32-bit Windows | [Download portable x86](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest/download/Real-ESRGAN-GUI-Portable-x86.exe) |

You can also open the [latest release page](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest) to read the changelog and see all files.

Do not download "Source code (zip)" or "Source code (tar.gz)" if you only want to use the app. Those files are for developers and do not contain a ready-to-run GUI package.

## Use the installer

1. Run `Real-ESRGAN-GUI-Setup-x64.exe`, or the x86 installer if you are on 32-bit Windows 10.
2. Follow the installer prompts.
3. Open Real-ESRGAN GUI from the Start menu or the desktop shortcut.

The installer includes the GUI, launcher, backend executable, .NET runtime files, models, and license notices.

## Use the single-file portable version

1. Download the single-file portable executable from the release page, such as `Real-ESRGAN-GUI-Portable-x64.exe`.
2. Run the executable directly from a normal folder.
3. Keep using the file from that folder. The app uses virtualized internal files and removes extracted temporary files when it exits.

## Quick start

1. Put the images you want to process in one folder.
2. Open Real-ESRGAN GUI.
3. Click `Choose image folder` and select the folder with your images.
4. Click `Choose output folder` and select where the results should be saved.
5. Pick the image type.
6. Keep the default settings for your first run.
7. Click `Start upscaling`.

If the input folder is empty, add images before starting. The app does not ship a sample input image.

## Settings

| Setting | Practical choice |
| --- | --- |
| Image type | Use Photo / portrait for real photos, Anime / illustration for drawings, and the anime video options for animation frames. |
| Scale | Keep Model default unless you specifically need 2x, 3x, or 4x output. |
| Format | Use PNG when you care most about preservation, JPG for smaller files, and WebP for web use. |
| Enhanced quality | Try a normal run first. Enhanced quality may improve some images, but it is slower. |
| Advanced settings | Keep threads and GPU on Auto unless you are troubleshooting a device problem. |

Supported input files: `png`, `jpg`, `jpeg`, `bmp`, `webp`, `tif`, `tiff`.

Supported output formats: `png`, `jpg`, `webp`.

## Notes for users

- The supported release targets are Windows 10/11 x64 and Windows 10 x86.
- Use x64 on 64-bit Windows. The x86 build has a much lower memory limit and is mainly for 32-bit Windows 10.
- A Vulkan-capable GPU and a current graphics driver are recommended because the backend uses NCNN/Vulkan.
- Very large images may take a long time or run out of GPU memory.
- The normal release entry is `Launcher.exe`. It shows the startup splash and opens the main GUI.

## Relationship to Real-ESRGAN

This repository is a Windows GUI distribution around the Real-ESRGAN NCNN/Vulkan backend. Upstream Real-ESRGAN also covers command-line use, Python workflows, model research, training, and standalone NCNN releases.

Useful upstream projects:

- [Real-ESRGAN](https://github.com/xinntao/Real-ESRGAN)
- [Real-ESRGAN-ncnn-vulkan](https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan)

## Build from source

Basic build requirements:

- Windows 10/11 x64
- Git
- PowerShell 5.1 or newer
- .NET SDK 9
- Git submodules

Full release requirements:

- Visual Studio C++ Build Tools with x64 and x86 toolchains
- Windows SDK
- CMake 3.10 or newer
- Vulkan SDK. x86 builds need `Lib32\vulkan-1.lib`; `scripts/setup-vulkan-sdk.ps1 -RequireLib32` installs/selects an SDK with that component for CI.
- Inno Setup 6 if you want to build installers
- Enigma Virtual Box if you want to build single-file portable executables

Clone the repository and initialize the backend submodule:

```powershell
git clone --recursive https://github.com/Xeknoz/Real-ESRGAN-GUI.git
cd Real-ESRGAN-GUI
git submodule update --init --recursive
```

If you already cloned the repository without submodules, run only:

```powershell
git submodule update --init --recursive
```

Compile the WPF GUI project:

```powershell
dotnet build src/Real-ESRGAN-GUI/RealESRGAN-GUI.csproj
```

Build a portable x64 app folder:

```powershell
.\scripts\build-all.ps1 -Clean -Architecture x64
```

The output is written to:

```text
artifacts\portable\x64\
```

Build Enigma single-file portable executables:

```powershell
.\scripts\build-enigma.ps1 -Clean
```

By default this builds both release architectures. The script first builds or reuses rebuildable staging under `artifacts\intermediate\portable\<arch>\`, then packages each portable folder into:

```text
artifacts\portable-enigma\Real-ESRGAN-GUI-Portable-x64.exe
artifacts\portable-enigma\Real-ESRGAN-GUI-Portable-x86.exe
```

Pass `-Architecture x64` or `-Architecture x86` to build only one Enigma portable executable.

Build both release architectures and installers:

```powershell
.\scripts\build-release.ps1
```

Full release builds use `artifacts\intermediate\portable\<arch>\` as staging for installers and Enigma single-file portable executables. Use `-SkipInstaller` when you specifically want a ready-to-run portable folder under `artifacts\portable\<arch>\` for local testing.

Build only the portable folders, without installers:

```powershell
.\scripts\build-release.ps1 -SkipInstaller
```

Build release artifacts plus Enigma single-file portable executables:

```powershell
.\scripts\build-release.ps1 -BuildEnigma
```

Check the release upload assets after a release build:

```powershell
.\scripts\resolve-release-assets.ps1 -Clean -RequireInstallers -RequireEnigma
```

Release upload assets are the installer executables in `artifacts\installers\` and the Enigma single-file portable executables in `artifacts\portable-enigma\`. The check script prints those paths for upload and removes the legacy duplicate `artifacts\release-assets\` directory when `-Clean` is passed.

GitHub Actions publishes the same assets for numeric `v*` release tags such as `v1.0.1` or `v1.0.1.4`.

Build only one architecture:

```powershell
.\scripts\build-release.ps1 -Architecture x64
.\scripts\build-release.ps1 -Architecture x86
```

Useful focused commands:

```powershell
.\src\Launcher\build.ps1
.\scripts\build-backend.ps1 -Clean -Architecture x64
.\scripts\build-models.ps1
.\scripts\build-all.ps1 -Clean -ForceBackend
.\scripts\build-models.ps1 -Force
.\scripts\build-installer.ps1 -Clean -Architecture x64
.\scripts\build-enigma.ps1 -Clean
```

Generated files go under `artifacts\`:

```text
artifacts\
  backend\<arch>\engine\   Generated backend executable and runtime DLLs
  models\                  Generated NCNN model files shared by architectures
  portable\<arch>\         Ready-to-run portable app folder from build-all or -SkipInstaller
  portable-enigma\          Single-file portable executables built by Enigma Virtual Box
  intermediate\portable\<arch>\  Rebuildable staging for installers and Enigma
  intermediate\enigma-projects\  Rebuildable Enigma .evb intermediate projects
  installers\              Windows installers
```

The portable folder should contain `Launcher.exe`, `Real-ESRGAN GUI.exe`, `engine\realesrgan-ncnn-vulkan.exe`, model files under `engine\models\`, version markers, and license notices.

## License

The GUI, launcher, scripts, and repository-specific documentation are licensed under the MIT License. Bundled third-party components keep their own licenses and attributions; see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
