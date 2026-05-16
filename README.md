# Real-ESRGAN GUI

[简体中文](README.zh-CN.md)

Real-ESRGAN GUI is a Windows desktop app for making images clearer with Real-ESRGAN. It is designed for ordinary users: choose the input folder, output folder, image type, and click the start button.

## Download and use

1. Open the latest GitHub Release.
2. Download `Real-ESRGAN-GUI-win-x64.zip`.
3. Extract the zip file to any folder.
4. Double-click `Launcher.exe` to start the app.
5. Choose the input folder, output folder, and image type, then start processing.

Notes:
- Use `Launcher.exe` to open the app. The main executable is started by the launcher.
- If the input folder is empty, the app can copy a sample image for a quick test.
- The app is portable. You can move the extracted folder elsewhere after closing it.

## What is included

- A modern WPF desktop interface
- A native splash launcher for faster startup feedback
- Built-in Simplified Chinese and English UI
- Batch processing with total progress and current-file progress
- Local processing through the bundled Real-ESRGAN NCNN/Vulkan backend

## For developers

Build a complete distributable folder with:

```powershell
.\scripts\build-all.ps1 -Clean
```

This script builds the backend when needed, builds the native launcher, publishes the WPF app, and assembles a ready-to-ship `dist/` folder.
The backend is skipped automatically when the current `runtime/engine/realesrgan-ncnn-vulkan.exe` already matches the backend source. Use `.\scripts\build-all.ps1 -Clean -ForceBackend` when you intentionally need a full backend rebuild.
The app version is resolved from a release tag such as `v1.0.1`, an explicit `-Version 1.0.1`, or the root `VERSION` file for development builds.

Repository layout:

```text
src/
  Launcher/             Native Win32 splash launcher
  RealESRGAN-GUI/       WPF desktop application
scripts/
  build-all.ps1         Build backend, launcher, GUI, and dist
  build-backend.ps1     Rebuild backend and copy it into runtime/engine
  backend-state.ps1     Backend build fingerprint helpers
  build-dist.ps1        One-click build script
  version.ps1           Resolve app versions for builds and releases
  Start_Real-ESRGAN.ps1 PowerShell CLI wrapper
runtime/
  engine/               Backend executable, runtime DLLs, and models
  input.jpg             Sample image copied into published builds
third_party/
  ncnn_src/             Backend source submodule
VERSION                  Base development version
```

Release automation:
- Run `.github/workflows/release.yml` manually with an optional version input, or push a tag such as `v1.0.1`.
- Tag releases use the tag as the displayed app version. Non-tag builds use `VERSION` plus commit metadata, for example `1.0.0-dev.58.g26d6948`.
- The workflow builds `dist/`, uploads a workflow artifact, and attaches `Real-ESRGAN-GUI-win-x64.zip` to the matching GitHub Release.

## License

The original code in this repository is licensed under the MIT License. Bundled third-party components keep their own licenses and attributions; see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
