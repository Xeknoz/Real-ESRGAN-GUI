# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo actually is

Despite the legacy [README_windows.md](README_windows.md) describing this as a pure "binary distribution", the repo now contains both GUI source and backend source. Five relevant areas coexist:

1. **[`engine/`](engine/)** — runtime payload copied into releases: `realesrgan-ncnn-vulkan.exe`, the `vcomp140*.dll` files, and `engine/models/`.
2. **[`ncnn_src/`](ncnn_src/)** — backend source submodule (`Real-ESRGAN-ncnn-vulkan-Enhanced`), including the modified NCNN/Vulkan CLI implementation used for GUI-facing progress work.
3. **[`Start_Real-ESRGAN.ps1`](Start_Real-ESRGAN.ps1)** — PowerShell wrapper (interactive menu / scripted use).
4. **[`Launcher/`](Launcher/)** — native Win32 splash launcher that starts the GUI before the WPF runtime is ready.
5. **[`RealESRGAN-GUI/`](RealESRGAN-GUI/)** — WPF GUI (net9.0-windows, x64, self-contained). This is where most UI work happens.

Both `Start_Real-ESRGAN.ps1` and the WPF GUI are **frontends to the same CLI**. If you change the backend contract (model list, default scales, supported formats, or progress output), update the frontends and this document together — they are kept in sync by hand.

## Build / Run / Publish

```powershell
# Rebuild backend after editing ncnn_src/
cmake --build ncnn_src/src/build --config Release
Copy-Item ncnn_src/src/build/Release/realesrgan-ncnn-vulkan.exe engine/realesrgan-ncnn-vulkan.exe -Force

# Build (debug)
dotnet build RealESRGAN-GUI/RealESRGAN-GUI.csproj

# Run from source when debugging the GUI entry check
dotnet run --project RealESRGAN-GUI/RealESRGAN-GUI.csproj -- --from-launcher

# Publish portable folder (this is what ships) → dist/
dotnet publish RealESRGAN-GUI/RealESRGAN-GUI.csproj -c Release -r win-x64 --self-contained true -o dist
```

**Before publishing:** a running `Real-ESRGAN GUI.exe` holds file locks on `dist/`, which makes `dotnet publish` fail with MSB3026/MSB3027. Kill any running instance first (`taskkill /F /IM "Real-ESRGAN GUI.exe"`) before re-publishing.

If `cmake` is not on `PATH`, use the Visual Studio-bundled `cmake.exe` against the existing `ncnn_src/src/build` tree.

The `.sln` file is **gitignored** (Visual Studio auto-recreates it). Don't commit it.

There is **no automated test suite**. Manual validation: launch `dist/Launcher.exe`, accept the "copy sample input.jpg" prompt, click 开始处理, verify a file appears in the output folder.

## Architecture: the big picture

### Launcher (Win32 native)

Because the app is shipped via Enigma Virtual Box as a single executable, the extraction phase happens before the CLR/WPF runtime even loads — the WPF `SplashWindow` cannot cover it. A native Win32 launcher ([`Launcher/Launcher.c`](Launcher/Launcher.c)) sits in front:

1. Acquire a named mutex (`Global\RealESRGAN_Launcher`) → if already held, activate the existing main window and exit.
2. Detect system dark/light theme + locale (zh vs en).
3. Create and present a 400×130 layered GDI splash window (themed, rounded corners via DWM).
4. After the first visible splash frame is committed, `CreateProcess` the WPF executable (`Real-ESRGAN GUI.exe --from-launcher`) so the user gets feedback before the slower managed startup path begins.
5. Poll `EnumWindows` + `GetWindowThreadProcessId` until the main window appears.
6. Fade out and exit. The WPF process continues independently.

**Build:**
```powershell
.\Launcher\build.ps1
```

Requires MSVC x64 build tools (Visual Studio or the standalone Build Tools with the C++ workload); the script discovers them via `vswhere.exe` and invokes `cl.exe` / `rc.exe` through `vcvars64.bat`.

**Enigma packing flow:** `dotnet publish -o dist` → copy `Launcher\bin\Launcher.exe` into `dist/` → set Enigma entry point to `Launcher.exe` (not `Real-ESRGAN GUI.exe`). `Launcher.exe` is a small x64 PE with zero external dependencies.

### Boot flow

[App.OnStartup](RealESRGAN-GUI/App.xaml.cs) accepts only launches marked with `--from-launcher`; a direct double-click on `Real-ESRGAN GUI.exe` immediately starts sibling `Launcher.exe` and exits. Valid launches apply the OS theme, acquire the named single-instance mutex (`Global\RealESRGAN_GUI_SingleInstance`), and show `MainWindow` directly. The native Launcher is the sole splash and PE application-icon owner; the WPF app links the same asset only as its runtime window/taskbar icon.

### Process-orchestration model

The GUI does **not** call NCNN directly. [MainWindow.xaml.cs](RealESRGAN-GUI/MainWindow.xaml.cs) builds one directory-mode CLI invocation in `BuildArgs(string input, string output)` and spawns `engine/realesrgan-ncnn-vulkan.exe` once per run via `RunBackendAsync`. stdout/stderr are redirected line-by-line into the in-window log panel, and `CancellationTokenSource` is wired to a kill-tree on Stop. Cross-thread UI/log writes go through the Dispatcher. The contract between GUI and backend is the CLI flags (`-i/-o/-n/-s/-f/-t/-g/-x`) plus stderr batch-progress records.

The backend owns the task queue and aggregate progress. It emits machine-readable stderr lines in the form `@batch total=<n> completed=<n> percent=<n.nn> current=<id> current_percent=<n.nn>`, where `current` is the first backend-ordered unfinished task. The GUI only renders that state. Do **not** reintroduce front-end task aggregation, output-file polling, or per-task progress reconstruction in `MainWindow.xaml.cs`. The log panel is shown during a run and uses a rolling buffer that trims old text once it grows past its cap. If you change the stderr protocol, update the GUI parser and this document in the same change.

Implication: most GUI features are still questions of *which CLI flag or output signal to expose*. Keep the backend boundary textual and explicit; do not introduce NCNN bindings or P/Invoke into model code.

### Live folder state & progress

Input and output folders are watched via `FileSystemWatcher` (`ReplaceWatcher` wires them up; recreated on folder change or window activation). Bursts of file events are coalesced through `_folderSummaryTimer` (a `DispatcherTimer`) so the UI refreshes once per quiet period instead of per event. Those watchers are for folder summaries only, not execution progress.

Backend progress IDs are the `Task.id` values assigned in `ncnn_src/src/main.cpp::load` and passed into `RealESRGAN::process`. They are stable for one run because they are derived from the backend's sorted input-file list index. Tile progress from `ncnn_src/src/realesrgan.cpp` is capped below `100%`; `BatchProgress::complete_task()` advances a task to `100%` only after `save()` has persisted the encoded output. Keep that distinction intact so neither the backend nor GUI confuses "GPU processing finished" with "file fully saved".

### Portable-folder layout (enforced by the csproj)

[RealESRGAN-GUI.csproj](RealESRGAN-GUI/RealESRGAN-GUI.csproj) declares `<Content Include="..\engine\realesrgan-ncnn-vulkan.exe">`, the two `engine\vcomp140*.dll`s, `input.jpg`, and `..\engine\models\*.bin`/`*.param` with `CopyToOutputDirectory="PreserveNewest"`. Published builds therefore preserve an `engine/` subdirectory beside the GUI exe, and `_exePath` points to `engine/realesrgan-ncnn-vulkan.exe`. If you rebuild the backend or add model/DLL assets, replace the runtime file in `engine/` and mirror the `<Content Include>` pattern so the files reach `dist/`.

### Theming (runtime-switchable)

[App.xaml](RealESRGAN-GUI/App.xaml) declares the light palette as defaults and ships the entire styling layer (no MahApps/MaterialDesign), including a from-scratch `ScrollBar` template with a vertical-vs-horizontal orientation trigger. [App.xaml.cs](RealESRGAN-GUI/App.xaml.cs)'s `ApplyTheme(bool dark)` then swaps **every** brush key at runtime — both the custom `*Brush` resources and the WPF `SystemColors.*BrushKey` overrides — so the whole UI flips without reloading XAML.

MainWindow's "Theme" dropdown (System / Light / Dark) drives `ApplyThemePreference`, which also re-runs `SetTitleBarTheme` to paint the non-client title bar via `DwmSetWindowAttribute` P/Invoke (tries `DWMWA_USE_IMMERSIVE_DARK_MODE=20` first, falls back to `19` for pre-19041 builds; failure is silent). `SystemEvents.UserPreferenceChanged` keeps the "System" choice in sync if the OS theme flips mid-session.

When adding a styled control: reuse existing brush keys. If you must introduce a new color, add it to **both** branches of `App.ApplyTheme` (dark + light) — otherwise theme switching will leave the new surface stuck on one side.

### Internationalization

UI strings are not in `.resx` files. [MainWindow.xaml.cs](RealESRGAN-GUI/MainWindow.xaml.cs) carries two dictionaries — `ChineseText` and `EnglishText` — keyed by short identifiers (`"HeaderSubtitle"`, `"StartButton"`, …). `T(key)` returns the entry for `_currentLanguage`; `ApplyLanguage()` is the single place that assigns every visible string on the window and is re-run on language-dropdown change. Status and progress strings are stored as `(key, args)` pairs (`_statusKey`/`_statusArgs`, `_progressTextKey`) and re-formatted by `RenderStatusText` / `RenderProgressText` on language change.

When adding a visible string: add a key to **both** dictionaries, fetch it via `T("...")`, and assign it inside `ApplyLanguage`. XAML may seed an initial value, but the runtime-correct value comes from `ApplyLanguage`.

### ComboItem pattern

Every dropdown uses the private `record ComboItem(string Tag, string Display)`: `Tag` is the literal CLI flag value (e.g. `"realesr-animevideov3-x2"`, `"jpg"`, `""` for "use default"), `Display` is the localized label. `BuildArgs()` reads `Tag`; never read `Display` in logic.

The scale dropdown is rebuilt on model change in `OnModelChanged` because default scale is model-specific (animevideov3-x2 → 2, -x3 → 3, everything else → 4). Mirror this if adding a new model.

## Conventions

- **File encoding:** UTF-8 without BOM. The codebase explicitly relies on this for stdout decoding (`StandardOutputEncoding = UTF8`).
- **UI language:** all user-facing strings are Simplified Chinese. Keep new UI text in Chinese; log/diagnostic prefixes (e.g. `[stderr]`) stay English.
- **Target:** `net9.0-windows`, x64 only. Don't add AnyCPU configurations.
- **Runtime assets live under `engine/`.** Binary files remain gitignored (`*.bin`, `*.exe`, `*.dll` in [.gitignore](.gitignore)); `engine/models/` is the shipped model location referenced by the csproj.
- **Adding a model:** update *both* `Start_Real-ESRGAN.ps1` (`$modelOptions` hashtable + `[ValidateSet]` attribute) *and* [MainWindow.xaml.cs](RealESRGAN-GUI/MainWindow.xaml.cs) (`PopulateComboBoxes` model list + `DefaultScaleFor`).
- **Backend source now exists in `ncnn_src/`.** Keep local GUI-facing backend changes focused in that submodule, and copy the rebuilt executable into `engine/` when you want the GUI/published app to use it.
- **Publish to `dist/` after every change.** `dotnet build` alone is insufficient — the GUI ships as the published portable folder. End every edit session with `dotnet publish RealESRGAN-GUI/RealESRGAN-GUI.csproj -c Release -r win-x64 --self-contained true -o dist` so the shipped artifact matches the source.
- **Window sizing:** `MainWindow` adapts to the current monitor's work area in `ConfigureWindowSizing` (`MonitorFromWindow` / `GetMonitorInfo` P/Invoke), and `FreezeAdaptiveHeight` locks the height after `ContentRendered`. Don't hard-code `Width` / `Height` in XAML — go through that path so multi-monitor / scaled-DPI setups don't clip.

## Serena onboarding

Per the global instructions, activate Serena for this project path before non-trivial work. The existing Serena memories (`project_overview`, etc.) predate the WPF GUI and still describe the repo as a binary distribution only — prefer this file and the actual source over those memories when they conflict.

## Sibling doc

[`AGENTS.md`](AGENTS.md) is a Codex-targeted near-duplicate of this file (gitignored but present on disk). When you edit one, mirror the change to the other so the two agents stay in sync.
