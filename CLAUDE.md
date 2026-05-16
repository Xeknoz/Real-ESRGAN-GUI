# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo actually is

Despite the legacy [README_windows.md](README_windows.md) describing this as a pure "binary distribution", the repo now contains **first-party source** around the upstream `realesrgan-ncnn-vulkan.exe` backend. Four components coexist:

1. **`realesrgan-ncnn-vulkan.exe`** — opaque NCNN/Vulkan inference binary from upstream (xinntao/Real-ESRGAN, via nihui/realsr-ncnn-vulkan). Treat as an external CLI; do not try to rebuild it.
2. **[`Start_Real-ESRGAN.ps1`](Start_Real-ESRGAN.ps1)** — PowerShell wrapper (interactive menu / scripted use).
3. **[`Launcher/`](Launcher/)** — native Win32 splash launcher that starts the GUI before the WPF runtime is ready.
4. **[`RealESRGAN-GUI/`](RealESRGAN-GUI/)** — WPF GUI (net9.0-windows, x64, self-contained). This is where most code work happens.

Both `Start_Real-ESRGAN.ps1` and the WPF GUI are **frontends to the same CLI**. If you change the backend contract (model list, default scales, supported formats), update *both* — they are kept in sync by hand.

## Build / Run / Publish

```powershell
# Build (debug)
dotnet build RealESRGAN-GUI/RealESRGAN-GUI.csproj

# Run from source when debugging the GUI entry check
dotnet run --project RealESRGAN-GUI/RealESRGAN-GUI.csproj -- --from-launcher

# Publish portable folder (this is what ships) → dist/
dotnet publish RealESRGAN-GUI/RealESRGAN-GUI.csproj -c Release -r win-x64 --self-contained true -o dist
```

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

**Enigma packing flow:** `dotnet publish -o dist` → copy `Launcher\bin\Launcher.exe` into `dist/` → set Enigma entry point to `Launcher.exe` (not `Real-ESRGAN GUI.exe`). `Launcher.exe` is a small x64 PE with zero external dependencies.

### Boot flow

[App.OnStartup](RealESRGAN-GUI/App.xaml.cs) accepts only launches marked with `--from-launcher`; a direct double-click on `Real-ESRGAN GUI.exe` immediately starts sibling `Launcher.exe` and exits. Valid launches apply the OS theme, acquire the named single-instance mutex (`Global\RealESRGAN_GUI_SingleInstance`), and show `MainWindow` directly. The native Launcher is the sole splash and PE application-icon owner; the WPF app links the same asset only as its runtime window/taskbar icon.

### Process-orchestration model

The GUI does **not** call NCNN directly. [MainWindow.xaml.cs](RealESRGAN-GUI/MainWindow.xaml.cs) builds a per-file CLI flag string in `BuildArgs(string input, string output)` and spawns `realesrgan-ncnn-vulkan.exe` for **each input file** in a for-loop (`RunBackendAsync`), with stdout/stderr redirected line-by-line into the in-window log box. The `CancellationTokenSource` is wired to a kill-tree on Stop. After the process exits, `CancelErrorRead`/`CancelOutputRead` are called in `finally` to prevent async read callbacks from firing post-exit. Cross-thread log writes go through `Dispatcher.Invoke`. The contract between GUI and backend is purely the CLI flags (`-i/-o/-n/-s/-f/-t/-g/-x`), with `-i`/`-o` now pointing to individual files, not directories.

The log panel auto-expands during a run (LogToggle visibility tied to `_busy`) and uses a rolling buffer that trims to the last ~40 KB once it exceeds 50 KB.

Implication: nearly every "feature" is a question of *what CLI flag to add and how to expose it in XAML*. Don't introduce NCNN bindings or P/Invoke into model code.

### Live folder state & progress

Input and output folders are watched via `FileSystemWatcher` (`ReplaceWatcher` wires them up; recreated on folder change or window activation). Bursts of file events are coalesced through `_folderSummaryTimer` (a `DispatcherTimer`) so the UI refreshes once per quiet period instead of per event.

Progress is purely file-count based: `GetDisplayPercent()` computes `100 * _completedFiles / _totalFiles`, and `SetProgressPercent` renders it with a `(N/M)` suffix for multi-file batches. Since the backend is called per-file in a for-loop and `_completedFiles` is incremented directly after each successful exit, there is no stderr percentage parsing and no fallback file-system progress scan. The `_expectedRunOutputs` field and `RefreshRunProgressFromOutputs` are leftover dead code — `_expectedRunOutputs` is never populated, so `RefreshRunProgressFromOutputs` always returns immediately at the `Count == 0` guard. If you re-enable directory-mode or add a side-channel output path, clean up or repopulate that path.

### Portable-folder layout (enforced by the csproj)

[RealESRGAN-GUI.csproj](RealESRGAN-GUI/RealESRGAN-GUI.csproj) declares `<Content Include="..\realesrgan-ncnn-vulkan.exe">`, the two `vcomp140*.dll`s, `input.jpg`, and `..\models\*.bin`/`*.param` with `CopyToOutputDirectory="PreserveNewest"`. This is why `_appDir = AppContext.BaseDirectory` "just works" — the backend, OpenMP runtime, sample image, and models are all colocated next to the published GUI exe. If you add new model files or DLL dependencies, mirror the `<Content Include>` pattern; otherwise they won't reach `dist/`.

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
- **Models folder is gitignored** (`models/`, `*.bin`, `*.exe`, `*.dll` in [.gitignore](.gitignore)) — the binary assets live outside source control but are referenced by the csproj. Don't try to commit them.
- **Adding a model:** update *both* `Start_Real-ESRGAN.ps1` (`$modelOptions` hashtable + `[ValidateSet]` attribute) *and* [MainWindow.xaml.cs](RealESRGAN-GUI/MainWindow.xaml.cs) (`PopulateComboBoxes` model list + `DefaultScaleFor`).
- **Don't** add CMake/Cargo/npm/pip configs or try to rebuild the NCNN backend — it comes from upstream as a binary.
- **Publish to `dist/` after every change.** `dotnet build` alone is insufficient — the GUI ships as the published portable folder. End every edit session with `dotnet publish RealESRGAN-GUI/RealESRGAN-GUI.csproj -c Release -r win-x64 --self-contained true -o dist` so the shipped artifact matches the source.
- **Window sizing:** `MainWindow` adapts to the current monitor's work area in `ConfigureWindowSizing` (`MonitorFromWindow` / `GetMonitorInfo` P/Invoke), and `FreezeAdaptiveHeight` locks the height after `ContentRendered`. Don't hard-code `Width` / `Height` in XAML — go through that path so multi-monitor / scaled-DPI setups don't clip.

## Serena onboarding

Per the global instructions, activate Serena for this project path before non-trivial work. The existing Serena memories (`project_overview`, etc.) predate the WPF GUI and still describe the repo as a binary distribution only — prefer this file and the actual source over those memories when they conflict.
