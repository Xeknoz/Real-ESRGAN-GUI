# Real-ESRGAN GUI

[简体中文](README.zh-CN.md)

Real-ESRGAN GUI is a Windows app for making images larger and sharper on your own PC. It uses the bundled Real-ESRGAN NCNN/Vulkan backend, but you do not need to type commands or install Python, PyTorch, CUDA, or the .NET Runtime.

The usual workflow is simple: choose an input folder, choose an output folder, pick the image type, then start. The app is meant for photos, portraits, anime images, illustrations, and animation frames.

Your images are processed locally. They are not uploaded to a cloud service.

## Download and install

Open the [latest release](https://github.com/Xeknoz/Real-ESRGAN-GUI/releases/latest), then download one of the app files from Assets.

For most users:

- Use `Real-ESRGAN-GUI-Setup-x64.exe` on 64-bit Windows 10 or Windows 11.
- Use `Real-ESRGAN-GUI-Setup-x86.exe` only on 32-bit Windows 10.
- If you want a no-install copy, download the portable archive or single-file portable executable if the release includes one, for example `Real-ESRGAN-GUI-win-x64.zip` or `Real-ESRGAN-GUI-Portable-x64.exe`.

Do not download "Source code (zip)" or "Source code (tar.gz)" if you only want to use the app. Those files are for developers and do not contain a ready-to-run GUI package.

The installers are currently unsigned. If Windows SmartScreen appears, continue only when you downloaded the file from this repository's Releases page.

## Use the installer

1. Run `Real-ESRGAN-GUI-Setup-x64.exe`, or the x86 installer if you are on 32-bit Windows 10.
2. Follow the installer prompts.
3. Open Real-ESRGAN GUI from the Start menu or the desktop shortcut.

The installer includes the GUI, launcher, backend executable, .NET runtime files, models, and license notices.

## Use the portable version

1. Download the portable archive from the release page, such as `Real-ESRGAN-GUI-win-x64.zip`.
2. Extract the whole archive to a normal folder, for example `C:\Apps\Real-ESRGAN GUI\`.
3. Open the extracted folder and run `Launcher.exe`.
4. Keep the files together. The `engine\` folder and model files must stay next to the app files.

Do not run the app from inside the zip file. To remove the portable version, close the app and delete the extracted folder.

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

This repository is a Windows GUI distribution around the Real-ESRGAN NCNN/Vulkan backend. Upstream Real-ESRGAN also covers command-line use, Python workflows, model research, training, and standalone NCNN releases. This project keeps the end-user flow narrower: choose folders and settings, then run locally.

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
- Vulkan SDK, including `Lib32\vulkan-1.lib` when building x86
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

By default this builds both release architectures. The script first builds or reuses `artifacts\portable\<arch>\`, then packages each portable folder into:

```text
artifacts\portable-enigma\Real-ESRGAN-GUI-Portable-x64.exe
artifacts\portable-enigma\Real-ESRGAN-GUI-Portable-x86.exe
```

Pass `-Architecture x64` or `-Architecture x86` to build only one Enigma portable executable.

Build both release architectures and installers:

```powershell
.\scripts\build-release.ps1
```

Build only the portable folders, without installers:

```powershell
.\scripts\build-release.ps1 -SkipInstaller
```

Build release artifacts plus Enigma single-file portable executables:

```powershell
.\scripts\build-release.ps1 -BuildEnigma
```

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
  portable\<arch>\         Ready-to-run portable app folder
  portable-enigma\          Single-file portable executables built by Enigma Virtual Box
  intermediate\enigma-projects\  Rebuildable Enigma .evb intermediate projects
  installers\              Unsigned Windows installers
```

The portable folder should contain `Launcher.exe`, `Real-ESRGAN GUI.exe`, `engine\realesrgan-ncnn-vulkan.exe`, model files under `engine\models\`, version markers, and license notices.

## License

The GUI, launcher, scripts, and repository-specific documentation are licensed under the MIT License. Bundled third-party components keep their own licenses and attributions; see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
