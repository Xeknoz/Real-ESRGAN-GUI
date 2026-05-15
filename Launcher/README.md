# Launcher

`Launcher.exe` is the small native Win32 front-end shown before the WPF runtime is ready.
It also owns the shipped application icon (`app.ico`); the WPF executable is not a user-facing entry point.

## Build

```powershell
.\Launcher\build.ps1
```

The script locates Visual Studio C++ build tools with `vswhere.exe` and writes build output to:

- `Launcher\bin\Launcher.exe`
- `Launcher\obj\Launcher.obj`
- `Launcher\obj\Launcher.res`

## Publish integration

After publishing the WPF app, copy `Launcher\bin\Launcher.exe` into `dist\Launcher.exe`.
When packaging with Enigma Virtual Box, use `Launcher.exe` as the entry point.
