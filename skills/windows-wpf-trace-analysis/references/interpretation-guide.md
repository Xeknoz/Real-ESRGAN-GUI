# Interpretation Guide

Use this reference after exporting a Windows WPF ETL trace. It is a checklist for turning trace tables into engineering decisions.

## Trace Health

- `xperf_tracestats.txt` should show no lost buffers and no lost events. If it does not, use the trace only for coarse timing.
- `xperf_screenshots.txt` often contains no frames unless screenshots were captured. If it is empty, the trace cannot directly prove the color of a flash.
- `xperf_marks.txt` can show WPR rundown markers. Do not treat CPU or disk activity after rundown as app behavior.

## Process Boundary

Use `xperf_process.txt` first. Record the target process name, PID, start time, and end time. Treat events outside that lifetime as system noise unless they clearly block launch or shutdown.

For launcher-to-app handoff, record both the launcher PID/lifetime and the GUI PID/lifetime. If the user reports a splash-to-main problem, the boundary between those two lifetimes matters more than the total trace duration.

## UI Delay

Use `xperf_uidelay.txt` and WPA UI delay CSV files.

- Short input delays around clicks can be normal and do not prove a visual flash.
- Long UI delays aligned with the symptom support dispatcher starvation, synchronous file I/O, expensive layout, blocking waits, or slow control construction.
- No meaningful UI delay during the symptom pushes the investigation toward DWM, GPU, first surface creation, or visual tree composition.

## DWM And GPU

Use WPA DWM frame and GPU utilization tables.

- Compare the target process and `dwm.exe`.
- Normal-looking GPU activity with no UI delay does not identify a fix by itself; it only narrows the class of problem.
- If the trace has no screenshots or app lifecycle marks, DWM data cannot tell which exact frame was white.

## Paging And Disk I/O

Use hard fault, page fault, and disk I/O summaries inside the target process lifetime.

- Hard faults on app assemblies, WPF DLLs, graphics drivers, or assets may explain first-use only symptoms.
- Disk I/O after the app closes or after WPR rundown is usually unrelated to the user-visible app flow.
- If content is read before `Show` or `ShowDialog`, file I/O cannot directly explain a white frame after the window is shown unless it delays first layout or render.

## Decision Rules

- If the trace directly supports a single cause, implement the narrowest fix and validate with a new trace or repeated manual runs.
- If the trace rules out obvious causes but cannot distinguish between multiple render races, add temporary lifecycle ETW/EventSource marks and capture again.
- If a proposed fix depends on WPF/DWM semantics, check primary Microsoft documentation before landing the design.
- Avoid accumulating arbitrary waits. Extra dispatcher frames, opacity changes, and DWM flushes are only useful when the trace or a minimal prototype shows that they move the failing boundary.
