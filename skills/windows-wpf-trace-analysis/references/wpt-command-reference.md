# Windows Performance Toolkit Command Reference

Use this reference when you need to verify or extend trace collection/export commands. Prefer the local tool's `-?` or `-help` output for the exact installed WPT version, then cross-check Microsoft documentation.

## WPA Exporter

Microsoft documents WPA Exporter as an automation tool for exporting tables from one ETL trace and one WPA profile to CSV. The documented syntax is:

```text
wpaexporter.exe [-i] traceFile.etl -profile profile.wpaProfile
  [-delimiter <char>] [-prefix <prefix>] [-outputfolder <folder>]
  [-range <start> <end>] [-marks <M1> <M2>]
  [-regionsxml <manifest> ...] [-region <region_name>]
  [-symbols] [-tti] [-h | /?]
```

Important documented behavior:

- If no time range is specified, the whole trace is exported.
- `-range` accepts numeric timestamps; units can be `s`, `ms`, or `us`, otherwise nanoseconds are assumed.
- `-marks` exports the range between two named markers.
- `-regionsxml` supplies Regions of Interest manifests. Without `-region`, it exports the whole ROI table.
- `-region` narrows export to a named region and requires at least one `-regionsxml` manifest.
- `-symbols` enables symbol loading.
- `-tti` permits processing traces with time inversions.
- Comparative Analysis Views cannot currently be exported by WPA Exporter.

Local WPT 11.7 help also exposes newer/generalized options:

- `-i path [arg]...` for one or more files.
- `-mode Single|Separate`.
- `-exporterconfig configpath.json`; when present, other options are ignored.
- `-outputformat CSV|XML`.
- `-listplugins`, `-addsearchdir`, and `-nodefault`.
- File options such as `-processor`, `-processors`, `-slot`, `-range`, and `-sysconfig`.
- Environment variables `WPA_ADDITIONAL_SEARCH_DIRECTORIES` and `WPA_NO_DEFAULT_SEARCH_DIRECTORY`.

The bundled `scripts/export-wpf-trace.ps1` intentionally uses the profile-based path because it is stable for automated WPF analysis. Use `-WpaExporterArgument` for a small version-specific addition. If the workflow needs `-exporterconfig`, create a separate script path because that option ignores the normal profile/output flags.

## Xperf Post-Processing

Microsoft documents xperf actions with this pattern:

```text
xperf -i input.etl -o output.txt -a <action_name> [action_parameters]
```

Useful actions for WPF launch/dialog investigations include:

- `tracestats`: trace health and lost event statistics.
- `process`: process, thread, and image lifetime data.
- `uidelay`: simple UI delay information.
- `focuschange`: thread focus changes.
- `hardfault`: hard fault statistics by process and file.
- `pagefault`: page fault information.
- `diskio`: disk I/O statistics.
- `cpudisk`: combined CPU/disk activity.
- `screenshots`: screenshots recorded in the trace, if present.
- `marks`: WPR/xperf marks.
- `eventmetadata`: event metadata in the trace.
- `wprrundown`: WPR rundown mark information, if supported by the local WPT version.

Local `xperf -help processing` also supports multiple actions in one invocation with `-ao` and `-ae`, but the bundled script runs one action per file so partial failures are easier to inspect.

## WPR Capture

Microsoft documents WPR as a profile-driven recorder. The useful command forms are:

```text
wpr -profiles
wpr -help start
wpr -start <profile> [-start <profile> ...] [-filemode] [-recordtempto <temp folder path>]
wpr -status [profiles] [collectors [-details]]
wpr -marker <text> [-flush]
wpr -stop <output.etl> "<description>"
wpr -cancel
```

Profile names use:

```text
[<filename.wprp>!]<profile name>[.{light|verbose}]
```

Notes:

- WPR can start up to 64 profiles on one command line.
- If neither `.light` nor `.verbose` is specified, verbose is used unless the profile only defines light.
- `-filemode` records to files instead of memory and is suitable for longer GUI reproduction traces.
- `wpr -status` verifies whether WPR is recording and whether dropped events are present.
- `wpr -marker` can add coarse timeline anchors without changing app code.

## Sources

- Microsoft Learn: WPA Exporter, `https://learn.microsoft.com/en-us/windows-hardware/test/wpt/exporter`
- Microsoft Learn: WPR Command-Line Options, `https://learn.microsoft.com/en-us/windows-hardware/test/wpt/wpr-command-line-options`
- Microsoft Learn: Xperf Actions, `https://learn.microsoft.com/windows-hardware/test/wpt/xperf-actions`
