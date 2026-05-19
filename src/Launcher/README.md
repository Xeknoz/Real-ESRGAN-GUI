# Launcher

`Launcher.exe` is the small native Win32 front-end shown before the WPF runtime is ready.
It also owns the shipped application icon (`app.ico`); the WPF executable is not a user-facing entry point.

The launcher owns duplicate-start feedback. If another launcher is already starting the app or the WPF single-instance mutex already exists, it activates the existing GUI when possible, asks the running WPF window for its current UI language, shows one themed native "already running" notice, and exits without starting a second WPF process.

## Build

```powershell
.\src\Launcher\build.ps1
.\src\Launcher\build.ps1 -Architecture x86
```

The script locates Visual Studio C++ build tools with `vswhere.exe` and writes build output to:

- `src\Launcher\bin\Launcher.exe`
- `src\Launcher\obj\Launcher.obj`
- `src\Launcher\obj\Launcher.res`

When run from the repository, it resolves the same app version metadata as the WPF build and embeds it into the native PE version resource. Release builds normally call it through `scripts\build-dist.ps1`, which passes the already-resolved app version and target architecture.

The splash screen uses the resolved display version. Development builds append the `dev` channel marker in the splash text while keeping the PE version metadata numeric.

## Publish integration

`scripts\build-dist.ps1` and `scripts\build-all.ps1` copy `src\Launcher\bin\Launcher.exe` into `artifacts\portable\<arch>\Launcher.exe` automatically.
When packaging with Enigma Virtual Box, use `Launcher.exe` as the entry point.
