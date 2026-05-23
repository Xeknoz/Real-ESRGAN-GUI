---
name: windows-wpf-trace-analysis
description: Analyze Windows WPF performance traces captured with WPR/WPA/xperf. Use when investigating WPF startup flashes, dialog white frames, splash-to-main handoff, DWM/GPU composition issues, UI input delay, hard faults, launch performance, or when an agent needs to compare ETL traces before changing Windows desktop UI or performance code.
---

# Windows WPF Trace Analysis

## Overview

Use this skill to turn Windows ETL traces into actionable evidence before changing WPF UI, startup, dialog, or DWM-related code. Prefer measured conclusions over visual guesswork, and explicitly separate what the trace proves from what still needs instrumentation or documentation lookup.

## Required Tools

- Windows Performance Toolkit from the Windows SDK, especially `wpr.exe`, `wpaexporter.exe`, `wpa.exe`, and `xperf.exe`.
- The ETL file captured from the affected app flow.
- Optional: app-specific ETW/EventSource marks for lifecycle events such as click, constructor start/end, `SourceInitialized`, `ContentRendered`, first render frames, and close.

## Safe Export Rule

`wpaexporter.exe` may write WPA cache/configuration files under `%LOCALAPPDATA%` on first run. In sandboxed or permission-limited agent sessions, do not let that write target the real user profile. Run the bundled export script so `LOCALAPPDATA`, `TEMP`, and `TMP` are redirected to a trace-analysis output folder for the exporter process only:

```powershell
.\skills\windows-wpf-trace-analysis\scripts\export-wpf-trace.ps1 `
  -TracePath .\artifacts\traces\baseline-splash-main-about.etl `
  -OutputDirectory .\artifacts\trace-analysis\baseline
```

The script exports targeted WPA profiles one at a time to avoid one slow profile blocking the entire run, and it also emits xperf text summaries for quick inspection.

The wrapper covers the common analysis options: `-Mode`, `-OutputFormat`, `-Delimiter`, `-RangeStart`/`-RangeEnd`, `-Marks`, `-RegionsXml`, `-Region`, `-Symbols`, `-Tti`, plus `-WpaExporterArgument` and `-XperfArgument` escape hatches for version-specific flags. For the full WPT command surface and source links, read `references/wpt-command-reference.md`.

## Workflow

1. Confirm trace quality first.
   - Check trace duration, lost buffers, lost events, and whether screenshot data exists.
   - If events were lost, treat timing conclusions as suspect.
   - If screenshots are absent, do not claim the ETL directly shows a white frame or visual color.

2. Export focused evidence.
   - Use `scripts/export-wpf-trace.ps1` unless the user explicitly asks for manual WPA usage.
   - Export each ETL into a separate output directory, for example `trace-analysis/new` and `trace-analysis/old`.
   - Keep raw ETLs unchanged.

3. Compare app-specific timing before system-wide noise.
   - Process lifetime and focus windows.
   - UI delay rows for the target process/thread.
   - GPU utilization for the target process and `dwm.exe`.
   - Hard faults and page faults attributed to the target process.
   - Disk I/O only inside the app lifetime; ignore post-rundown noise unless it overlaps the target flow.

4. Decide what the trace can prove.
   - A UI delay spike supports a UI-thread or dispatcher hypothesis.
   - DWM/GPU activity without UI delay supports a composition or first-surface hypothesis.
   - Hard faults on app DLLs or assets support cold-start paging or file-loading hypotheses.
   - No screenshots and no app lifecycle marks means the trace cannot identify the exact visual frame. Add instrumentation instead of guessing.

5. Only propose fixes after evidence is strong enough.
   - Avoid stacking arbitrary waits, opacity tricks, or extra frames when the trace does not show they address the failing boundary.
   - Prefer lifecycle marks and another targeted trace when multiple plausible timing races remain.
   - If external behavior is poorly documented, check primary sources such as Microsoft WPF/DWM documentation before choosing a design.

## Reading The Output

Start with these files when present:

- `xperf_tracestats.txt`: trace health and lost events.
- `xperf_process.txt`: process lifetimes and PID boundaries.
- `xperf_uidelay.txt`: UI delays by process/thread.
- `xperf_hardfault.txt` and `xperf_pagefault.txt`: paging evidence.
- `xperf_screenshots.txt`: whether visual screenshots were captured.
- `wpa-xaml\*_UI_Delays_*.csv`: WPF/XAML responsiveness summary.
- `wpa-xaml\*_Dwm_Frame_*.csv`: DWM frame-rate evidence, if exported.
- `wpa-xaml\*_GPU_Utilization_*.csv`: target process and DWM GPU activity.
- `wpa-applaunch\*` and `wpa-dotnet\*`: launch and .NET runtime context.

For more interpretation guidance, read `references/interpretation-guide.md`.

For command-line syntax details verified against Microsoft documentation and the local Windows Performance Toolkit help output, read `references/wpt-command-reference.md`.

## Report Template

When reporting findings, include:

- ETL paths and output directories.
- Tool profiles/actions exported.
- Trace health: duration, lost events, screenshots present or absent.
- Target process PID/lifetime.
- Evidence that supports or rules out each hypothesis.
- Confidence level and the next action: fix, instrument, recapture, or research primary docs.
