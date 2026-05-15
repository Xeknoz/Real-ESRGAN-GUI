using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RealESRGAN_GUI
{
    public partial class MainWindow : Window
    {
        private static readonly string[] SupportedExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };

        private readonly string _exePath;
        private readonly string _appDir;
        private string _inputDir = string.Empty;
        private string _outputDir = string.Empty;
        private Process? _runningProcess;
        private CancellationTokenSource? _cts;
        private FileSystemWatcher? _inputWatcher;
        private FileSystemWatcher? _outputWatcher;
        private readonly DispatcherTimer _folderSummaryTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        private bool _windowHeightFrozen;

        private string _languagePreference = "auto";
        private string _currentLanguage = "zh";
        private string _themePreference = "system";
        private bool _updatingSelections;
        private bool _busy;
        private string _statusKey = "StatusReady";
        private object[] _statusArgs = Array.Empty<object>();
        private string? _progressTextKey = "ProgressZero";
        private double? _progressPercent = 0;

        private int _totalFiles;
        private int _completedFiles;
        private double _currentFilePercent;
        private int _currentOutputFile = -1;
        private readonly Dictionary<int, double> _fileProgress = new();
        private DateTime _runStartedUtc;
        private HashSet<string> _expectedRunOutputs = new(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _logBuilder = new();

        public MainWindow()
        {
            InitializeComponent();
            ConfigureWindowSizing();

            // Portable folder layout: realesrgan-ncnn-vulkan.exe, vcomp140*.dll,
            // models\, input.jpg all sit next to this GUI exe.
            _appDir  = AppContext.BaseDirectory;
            _exePath = Path.Combine(_appDir, "realesrgan-ncnn-vulkan.exe");

            _currentLanguage = ResolveLanguage();
            PopulatePreferenceCombos();
            ApplyThemePreference();
            PopulateComboBoxes();
            InitializeDefaults();
            ApplyLanguage(rebuildCombos: false);
            _folderSummaryTimer.Tick += OnFolderSummaryTimerTick;
            ConfigureFolderWatchers();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            LocationChanged += (_, _) => ConfigureWindowSizing();
            StateChanged += (_, _) => ConfigureWindowSizing();
            ContentRendered += (_, _) => FreezeAdaptiveHeight();
            Activated += (_, _) =>
            {
                ConfigureFolderWatchers();
                RefreshFolderSummaries();
            };
            Closed += (_, _) =>
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                DisposeWatcher(ref _inputWatcher);
                DisposeWatcher(ref _outputWatcher);
                _folderSummaryTimer.Stop();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ConfigureWindowSizing();
            ApplyThemePreference();
        }

        // Tell DWM to render the non-client area (title bar, border) in the active app theme.
        private static void SetTitleBarTheme(Window window, bool dark)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int useDark = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref useDark, sizeof(int));

                SetTitleBarColor(hwnd, DwmwaCaptionColor, "RailBrush");
                SetTitleBarColor(hwnd, DwmwaTextColor, "HeaderForegroundBrush");
                SetTitleBarColor(hwnd, DwmwaBorderColor, "RailBrush");
            }
            catch { /* DWM call is best-effort; ignore on unsupported OS */ }
        }

        private static void SetTitleBarColor(IntPtr hwnd, int attribute, string resourceKey)
        {
            if (Application.Current.Resources[resourceKey] is not SolidColorBrush brush) return;

            int colorRef = brush.Color.R |
                           brush.Color.G << 8 |
                           brush.Color.B << 16;
            DwmSetWindowAttribute(hwnd, attribute, ref colorRef, sizeof(int));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DwmwaUseImmersiveDarkModeLegacy = 19;
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaBorderColor = 34;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;
        private const uint MonitorDefaultToNearest = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect MonitorArea;
            public NativeRect WorkArea;
            public uint Flags;
        }

        // --- Initialization helpers --------------------------------------------------

        private void ConfigureWindowSizing()
        {
            var workArea = GetCurrentWorkArea();
            double maxWidth = Math.Max(1, workArea.Width);
            double maxHeight = Math.Max(1, workArea.Height);

            bool maximized = WindowState == WindowState.Maximized;
            MaxWidth = maximized ? double.PositiveInfinity : maxWidth;
            MaxHeight = maximized ? double.PositiveInfinity : maxHeight;
            MinWidth = Math.Min(860, maxWidth);
            MinHeight = Math.Min(520, maxHeight);

            if (WindowState == WindowState.Normal)
            {
                if (Width > maxWidth) Width = maxWidth;
                if (_windowHeightFrozen && Height > maxHeight) Height = maxHeight;
            }

            MainScrollViewer.MaxHeight = Math.Max(80, maxHeight - 156);
        }

        private void FreezeAdaptiveHeight()
        {
            if (_windowHeightFrozen) return;

            UpdateLayout();
            Height = Math.Min(ActualHeight, MaxHeight);
            SizeToContent = SizeToContent.Manual;
            _windowHeightFrozen = true;
        }

        private Rect GetCurrentWorkArea()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    var source = PresentationSource.FromVisual(this);
                    if (source?.CompositionTarget is not null)
                    {
                        Matrix transform = source.CompositionTarget.TransformFromDevice;
                        Point topLeft = transform.Transform(new Point(info.WorkArea.Left, info.WorkArea.Top));
                        Point bottomRight = transform.Transform(new Point(info.WorkArea.Right, info.WorkArea.Bottom));
                        return new Rect(topLeft, bottomRight);
                    }

                    return new Rect(info.WorkArea.Left, info.WorkArea.Top, info.WorkArea.Width, info.WorkArea.Height);
                }
            }

            return SystemParameters.WorkArea;
        }

        private void PopulatePreferenceCombos()
        {
            string theme = _themePreference;
            string language = _languagePreference;

            _updatingSelections = true;
            SetComboItems(ThemeCombo, new[]
            {
                new ComboItem("system", T("ThemeSystem")),
                new ComboItem("light",  T("ThemeLight")),
                new ComboItem("dark",   T("ThemeDark")),
            }, theme);

            SetComboItems(LanguageCombo, new[]
            {
                new ComboItem("auto", T("LanguageAuto")),
                new ComboItem("zh",   T("LanguageZh")),
                new ComboItem("en",   T("LanguageEn")),
            }, language);
            _updatingSelections = false;
        }

        private void PopulateComboBoxes()
        {
            string model = SelectedTag(ModelCombo) ?? "realesrgan-x4plus";
            string scale = SelectedTag(ScaleCombo) ?? string.Empty;
            string format = SelectedTag(FormatCombo) ?? "png";
            string threads = SelectedTag(ThreadsCombo) ?? "0";
            string gpu = SelectedTag(GpuCombo) ?? string.Empty;

            ModelCombo.SelectionChanged -= OnModelChanged;
            _updatingSelections = true;

            SetComboItems(ModelCombo, new[]
            {
                new ComboItem("realesrgan-x4plus",       T("ModelPhoto")),
                new ComboItem("realesrgan-x4plus-anime", T("ModelAnime")),
                new ComboItem("realesr-animevideov3-x2", T("ModelVideo2")),
                new ComboItem("realesr-animevideov3-x3", T("ModelVideo3")),
                new ComboItem("realesr-animevideov3-x4", T("ModelVideo4")),
            }, model);

            SetComboItems(ScaleCombo, BuildScaleItems(DefaultScaleFor(model)), scale);
            SetComboItems(FormatCombo, new[]
            {
                new ComboItem("png",  T("FormatPng")),
                new ComboItem("jpg",  T("FormatJpg")),
                new ComboItem("webp", T("FormatWebp")),
            }, format);

            SetComboItems(ThreadsCombo, new[]
            {
                new ComboItem("0", T("AutoRecommended")),
                new ComboItem("1", "1"),
                new ComboItem("2", "2"),
                new ComboItem("4", "4"),
                new ComboItem("8", "8"),
            }, threads);

            SetComboItems(GpuCombo, new[]
            {
                new ComboItem(string.Empty, T("AutoRecommended")),
                new ComboItem("0", "0"),
                new ComboItem("1", "1"),
                new ComboItem("2", "2"),
            }, gpu);

            _updatingSelections = false;
            ModelCombo.SelectionChanged += OnModelChanged;
        }

        private static void SetComboItems(ComboBox combo, ComboItem[] items, string selectedTag)
        {
            combo.ItemsSource = items;
            combo.DisplayMemberPath = nameof(ComboItem.Display);
            combo.SelectedItem = items.FirstOrDefault(item => item.Tag == selectedTag) ?? items[0];
        }

        private static string? SelectedTag(ComboBox combo)
            => combo.SelectedItem is ComboItem item ? item.Tag : null;

        // The CLI flag -s defaults are model-specific; reflect that in the UI label.
        private static int DefaultScaleFor(string model) => model switch
        {
            "realesr-animevideov3-x2" => 2,
            "realesr-animevideov3-x3" => 3,
            _                         => 4,
        };

        private ComboItem[] BuildScaleItems(int defaultScale)
        {
            var items = new List<ComboItem>
            {
                new(string.Empty, string.Format(CultureInfo.CurrentCulture, T("ScaleAuto"), defaultScale)),
            };

            foreach (int scale in new[] { 2, 3, 4 })
            {
                if (scale == defaultScale) continue;
                items.Add(new ComboItem(scale.ToString(CultureInfo.InvariantCulture), $"{scale}x"));
            }

            return items.ToArray();
        }

        private void OnModelChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections || ModelCombo.SelectedItem is not ComboItem mi) return;
            string scale = SelectedTag(ScaleCombo) ?? string.Empty;
            _updatingSelections = true;
            SetComboItems(ScaleCombo, BuildScaleItems(DefaultScaleFor(mi.Tag)), scale);
            _updatingSelections = false;
        }

        private void InitializeDefaults()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            _inputDir  = Path.Combine(pictures, "Real-ESRGAN_Input");
            _outputDir = Path.Combine(pictures, "Real-ESRGAN_Output");
            InputPathBox.Text  = _inputDir;
            OutputPathBox.Text = _outputDir;
            RefreshFolderSummaries();
            SetStatus("StatusReady");
            SetProgressText("ProgressZero");
        }

        // --- Preferences -------------------------------------------------------------

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections || ThemeCombo.SelectedItem is not ComboItem item) return;
            _themePreference = item.Tag;
            ApplyThemePreference();
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections || LanguageCombo.SelectedItem is not ComboItem item) return;
            _languagePreference = item.Tag;
            _currentLanguage = ResolveLanguage();
            ApplyLanguage();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;

            Dispatcher.Invoke(() =>
            {
                if (_themePreference == "system")
                    ApplyThemePreference();

                if (_languagePreference == "auto")
                {
                    string resolved = ResolveLanguage();
                    if (resolved != _currentLanguage)
                    {
                        _currentLanguage = resolved;
                        ApplyLanguage();
                    }
                }
            });
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(ConfigureWindowSizing);
        }

        private void ApplyThemePreference()
        {
            bool dark = _themePreference switch
            {
                "dark" => true,
                "light" => false,
                _ => App.IsSystemDarkTheme(),
            };

            App.ApplyTheme(dark);
            SetTitleBarTheme(this, dark);
        }

        private string ResolveLanguage()
        {
            if (_languagePreference is "zh" or "en")
                return _languagePreference;

            string name = CultureInfo.CurrentUICulture.Name;
            return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
        }

        private void ApplyLanguage(bool rebuildCombos = true)
        {
            if (rebuildCombos)
            {
                PopulatePreferenceCombos();
                PopulateComboBoxes();
            }

            HeaderSubtitleText.Text = T("HeaderSubtitle");
            ThemeLabelText.Text = T("ThemeLabel");
            LanguageLabelText.Text = T("LanguageLabel");
            ReadySectionTitleText.Text = T("ReadySection");
            InputTitleText.Text = T("InputTitle");
            OpenInputButton.Content = T("OpenFolder");
            BrowseInputButton.Content = T("BrowseInput");
            OutputTitleText.Text = T("OutputTitle");
            OpenOutputButton.Content = T("OpenFolder");
            BrowseOutputButton.Content = T("BrowseOutput");
            StartTitleText.Text = T("StartTitle");
            StartButton.Content = T("StartButton");
            StopButton.Content = T("StopButton");
            SettingsSectionTitleText.Text = T("SettingsSection");
            ModelLabelText.Text = T("ModelLabel");
            ScaleLabelText.Text = T("ScaleLabel");
            FormatLabelText.Text = T("FormatLabel");
            UpdateAdvancedToggleText();
            UpdateLogToggleText();
            LogHeaderText.Text = T("LogHeader");
            ThreadsLabelText.Text = T("ThreadsLabel");
            GpuLabelText.Text = T("GpuLabel");
            TtaCheck.Content = T("Tta");
            HintText.Text = T("Hint");
            RenderStatusText();
            RenderProgressText();
            RefreshFolderSummaries();
        }

        // --- Folder picking ----------------------------------------------------------

        private string? PickFolder(string initial)
        {
            var dlg = new OpenFolderDialog
            {
                Title = T("PickFolderTitle"),
                Multiselect = false,
                InitialDirectory = Directory.Exists(initial) ? initial : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            };
            return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
        }

        private void OnBrowseInputClick(object sender, RoutedEventArgs e)
        {
            var picked = PickFolder(_inputDir);
            if (picked is null) return;
            _inputDir = picked;
            InputPathBox.Text = picked;
            ConfigureFolderWatchers();
            RefreshFolderSummaries();
        }

        private void OnBrowseOutputClick(object sender, RoutedEventArgs e)
        {
            var picked = PickFolder(_outputDir);
            if (picked is null) return;
            _outputDir = picked;
            OutputPathBox.Text = picked;
            ConfigureFolderWatchers();
            RefreshFolderSummaries();
        }

        private void OnOpenInputClick(object sender, RoutedEventArgs e)  => OpenInExplorer(_inputDir);
        private void OnOpenOutputClick(object sender, RoutedEventArgs e) => OpenInExplorer(_outputDir);

        private void OnAdvancedToggleClick(object sender, RoutedEventArgs e)
        {
            AdvancedPanel.Visibility = AdvancedToggle.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateAdvancedToggleText();
        }

        private void UpdateAdvancedToggleText()
        {
            string arrow = AdvancedToggle.IsChecked == true ? "▲" : "▼";
            AdvancedToggle.Content = $"{T("Advanced")} {arrow}";
        }

        private void OnLogToggleClick(object sender, RoutedEventArgs e)
        {
            LogPanel.Visibility = LogToggle.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateLogToggleText();
        }

        private void UpdateLogToggleText()
        {
            string arrow = LogToggle.IsChecked == true ? "▲" : "▼";
            LogToggle.Content = $"{T("Log")} {arrow}";
        }

        private void AppendLog(string line)
        {
            if (_logBuilder.Length > 50000)
                _logBuilder.Remove(0, _logBuilder.Length - 40000);
            _logBuilder.AppendLine(line);
            LogText.Text = _logBuilder.ToString();
            LogScrollViewer.ScrollToEnd();
        }

        private void OpenInExplorer(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }

        // --- Process orchestration ---------------------------------------------------

        private async void OnStartClick(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_exePath))
            {
                MessageBox.Show(this,
                    string.Format(CultureInfo.CurrentCulture, T("MissingExe"), _exePath),
                    "Real-ESRGAN GUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try { Directory.CreateDirectory(_inputDir);  } catch { /* surfaced below */ }
            try { Directory.CreateDirectory(_outputDir); } catch { /* surfaced below */ }
            ConfigureFolderWatchers();
            RefreshFolderSummaries();

            if (!Directory.Exists(_inputDir))
            {
                MessageBox.Show(this, T("InputAccessError"), "Real-ESRGAN GUI",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!Directory.Exists(_outputDir))
            {
                MessageBox.Show(this, T("OutputAccessError"), "Real-ESRGAN GUI",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var files = EnumerateInputs(_inputDir);
            if (files.Count == 0)
            {
                var ans = MessageBox.Show(this,
                    T("NoImagesAsk"),
                    "Real-ESRGAN GUI", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ans != MessageBoxResult.Yes) return;

                if (!TryCopySample(_inputDir, out string copyError))
                {
                    MessageBox.Show(this, copyError, "Real-ESRGAN GUI",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                files = EnumerateInputs(_inputDir);
                RefreshFolderSummaries();
                if (files.Count == 0) return;
            }

            _totalFiles = files.Count;
            _completedFiles = 0;
            _currentFilePercent = 0;
            _runStartedUtc = DateTime.UtcNow;
            string outputFormat = ((ComboItem)FormatCombo.SelectedItem).Tag;
            _expectedRunOutputs = files
                .Select(file => Path.Combine(_outputDir, $"{Path.GetFileNameWithoutExtension(file)}.{outputFormat}"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            string args = BuildArgs();

            SetUIBusy(true);
            _cts = new CancellationTokenSource();

            try
            {
                int exitCode = await RunBackendAsync(args, _cts.Token);
                if (_cts.IsCancellationRequested)
                {
                    SetStatus("StatusStopped");
                    SetProgressText("ProgressStopped");
                }
                else if (exitCode == 0)
                {
                    _completedFiles = _totalFiles;
                    _currentFilePercent = 100;
                    SetStatus("StatusDone", _completedFiles);
                    UpdateProgressBars();
                    SetProgressPercent(100);
                }
                else
                {
                    SetStatus("StatusFailed", exitCode);
                    SetProgressText("ProgressIncomplete");
                }
            }
            catch (Exception ex)
            {
                SetStatus("StatusError", ex.Message);
                SetProgressText("ProgressError");
            }
            finally
            {
                SetUIBusy(false);
                RefreshFolderSummaries();
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _cts?.Cancel();
                if (_runningProcess is { HasExited: false })
                {
                    _runningProcess.Kill(entireProcessTree: true);
                }
            }
            catch { /* ignore */ }
        }

        private string BuildArgs()
        {
            string Q(string s) => $"\"{s}\"";
            string model = ((ComboItem)ModelCombo.SelectedItem).Tag;
            string scale = ((ComboItem)ScaleCombo.SelectedItem).Tag;
            string format = ((ComboItem)FormatCombo.SelectedItem).Tag;
            string threads = ((ComboItem)ThreadsCombo.SelectedItem).Tag;
            string gpu = ((ComboItem)GpuCombo.SelectedItem).Tag;
            bool tta = TtaCheck.IsChecked == true;

            var parts = new List<string>
            {
                "-i", Q(_inputDir),
                "-o", Q(_outputDir),
                "-n", model,
                "-f", format,
            };
            if (!string.IsNullOrEmpty(scale))   { parts.Add("-s"); parts.Add(scale); }
            if (threads != "0")                 { parts.Add("-t"); parts.Add(threads); }
            if (!string.IsNullOrEmpty(gpu))     { parts.Add("-g"); parts.Add(gpu); }
            if (tta)                            parts.Add("-x");
            parts.Add("-v");
            return string.Join(" ", parts);
        }

        private async Task<int> RunBackendAsync(string args, CancellationToken token)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(_exePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Dispatcher.Invoke(() =>
                {
                    ParseProgress(e.Data);
                    AppendLog(e.Data);
                });
            };
            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Dispatcher.Invoke(() => AppendLog(e.Data));
            };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            _runningProcess = proc;

            try
            {
                await proc.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                try { if (!proc.HasExited) proc.Kill(true); } catch { /* ignore */ }
            }
            finally
            {
                _runningProcess = null;
            }
            return proc.ExitCode;
        }

        private static List<string> EnumerateInputs(string dir)
        {
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.EnumerateFiles(dir)
                .Where(f => SupportedExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
        }

        private bool TryCopySample(string dir, out string error)
        {
            string src = Path.Combine(_appDir, "input.jpg");
            if (!File.Exists(src))
            {
                error = T("MissingSample");
                return false;
            }
            try
            {
                File.Copy(src, Path.Combine(dir, "input.jpg"), overwrite: true);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = string.Format(CultureInfo.CurrentCulture, T("CopySampleFailed"), ex.Message);
                return false;
            }
        }

        // --- UI helpers --------------------------------------------------------------

        private void RefreshFolderSummaries()
        {
            InputSummaryText.Text = DescribeInputFolder(_inputDir);
            OutputSummaryText.Text = DescribeOutputFolder(_outputDir);
        }

        private void ConfigureFolderWatchers()
        {
            ReplaceWatcher(ref _inputWatcher, _inputDir);
            ReplaceWatcher(ref _outputWatcher, _outputDir);
        }

        private void ReplaceWatcher(ref FileSystemWatcher? watcher, string path)
        {
            string? currentPath = watcher?.Path;
            bool shouldWatch = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

            if (shouldWatch &&
                watcher is not null &&
                string.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            DisposeWatcher(ref watcher);
            if (!shouldWatch) return;

            try
            {
                watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName |
                                   NotifyFilters.CreationTime |
                                   NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                };
                watcher.Created += OnFolderContentsChanged;
                watcher.Deleted += OnFolderContentsChanged;
                watcher.Changed += OnFolderContentsChanged;
                watcher.Renamed += OnFolderContentsChanged;
                watcher.Error += OnFolderWatcherError;
            }
            catch
            {
                DisposeWatcher(ref watcher);
            }
        }

        private void DisposeWatcher(ref FileSystemWatcher? watcher)
        {
            if (watcher is null) return;
            watcher.Created -= OnFolderContentsChanged;
            watcher.Deleted -= OnFolderContentsChanged;
            watcher.Changed -= OnFolderContentsChanged;
            watcher.Renamed -= OnFolderContentsChanged;
            watcher.Error -= OnFolderWatcherError;
            watcher.Dispose();
            watcher = null;
        }

        private void OnFolderContentsChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.BeginInvoke(ScheduleFolderSummaryRefresh);
        }

        private void OnFolderWatcherError(object sender, ErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ConfigureFolderWatchers();
                ScheduleFolderSummaryRefresh();
            });
        }

        private void ScheduleFolderSummaryRefresh()
        {
            _folderSummaryTimer.Stop();
            _folderSummaryTimer.Start();
        }

        private void OnFolderSummaryTimerTick(object? sender, EventArgs e)
        {
            _folderSummaryTimer.Stop();
            RefreshFolderSummaries();
            RefreshRunProgressFromOutputs();
        }

        private void RefreshRunProgressFromOutputs()
        {
            if (!_busy || _totalFiles <= 0 || _expectedRunOutputs.Count == 0) return;

            int completed = _expectedRunOutputs.Count(path =>
            {
                if (!File.Exists(path)) return false;

                try
                {
                    return File.GetLastWriteTimeUtc(path) >= _runStartedUtc.AddSeconds(-2);
                }
                catch
                {
                    return false;
                }
            });

            if (completed <= _completedFiles) return;

            int remaining = Math.Max(0, _totalFiles - Math.Max(completed, _completedFiles));
            SetStatus("StatusProcessingFiles", Math.Max(completed, _completedFiles), remaining);
            UpdateProgressBars();
            SetProgressPercent(GetDisplayPercent());
        }

        private string DescribeInputFolder(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return T("InputSummaryNone");

            if (!Directory.Exists(dir))
                return T("FolderCreateOnStart");

            int count = CountSupportedFiles(dir);
            if (count < 0)
                return T("FolderUnreadable");

            return count == 0
                ? T("InputNoImages")
                : string.Format(CultureInfo.CurrentCulture, T("InputCount"), count);
        }

        private string DescribeOutputFolder(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return T("OutputSummaryNone");

            if (!Directory.Exists(dir))
                return T("FolderCreateOnStart");

            int count = CountSupportedFiles(dir);
            if (count < 0)
                return T("FolderUnreadable");

            return count == 0
                ? T("OutputNoFiles")
                : string.Format(CultureInfo.CurrentCulture, T("OutputCount"), count);
        }

        private static int CountSupportedFiles(string dir)
        {
            try
            {
                return EnumerateInputs(dir).Count;
            }
            catch
            {
                return -1;
            }
        }

        private void SetUIBusy(bool busy)
        {
            _busy = busy;
            StartButton.IsEnabled = !busy;
            StopButton.IsEnabled  =  busy;
            BrowseInputButton.IsEnabled = !busy;
            BrowseOutputButton.IsEnabled = !busy;
            OpenInputButton.IsEnabled = !busy;
            OpenOutputButton.IsEnabled = !busy;
            ModelCombo.IsEnabled = !busy;
            ScaleCombo.IsEnabled = !busy;
            FormatCombo.IsEnabled = !busy;
            ThreadsCombo.IsEnabled = !busy;
            GpuCombo.IsEnabled = !busy;
            TtaCheck.IsEnabled = !busy;
            ProgressTrack.Visibility = Visibility.Visible;
            LogToggle.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (!busy)
            {
                LogPanel.Visibility = Visibility.Collapsed;
                LogToggle.IsChecked = false;
                UpdateLogToggleText();
            }
            if (busy)
            {
                _logBuilder.Clear();
                LogText.Text = "";
                _currentOutputFile = -1;
                _fileProgress.Clear();
                SetStatus("StatusProcessingFiles", 0, _totalFiles);
                CompletedProgressBar.IsIndeterminate = false;
                UpdateProgressBars();
                SetProgressPercent(0);
            }
        }

        private bool ParseProgress(string line)
        {
            // Match "[N] processing input/xxx.jpg -> output/xxx.jpg ..." lines
            var fileMatch = Regex.Match(line, @"^\[(\d+)\]\s+processing\s");
            if (fileMatch.Success)
            {
                _currentOutputFile = int.Parse(fileMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                _completedFiles = _currentOutputFile; // files before the current one are done
                _fileProgress[_currentOutputFile] = 0;
                _currentFilePercent = 0;
                UpdateProgressBars();
                SetProgressPercent(GetDisplayPercent());
                return true;
            }

            // Match percentage lines
            if (line.EndsWith("%", StringComparison.Ordinal) &&
                double.TryParse(line.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                double newPct = Math.Clamp(pct, 0, 100);
                if (_currentOutputFile >= 0)
                {
                    // Per-file monotonic guard
                    _fileProgress.TryGetValue(_currentOutputFile, out double oldPct);
                    if (newPct >= oldPct)
                    {
                        _fileProgress[_currentOutputFile] = newPct;
                        _currentFilePercent = newPct;
                    }
                }
                else
                {
                    // Fallback: no file marker seen yet
                    if (newPct >= _currentFilePercent || _currentFilePercent >= 99)
                        _currentFilePercent = newPct;
                }
                UpdateProgressBars();
                SetProgressPercent(GetDisplayPercent());
                return true;
            }
            return false;
        }

        private void UpdateProgressBars()
        {
            CompletedProgressBar.Value = GetDisplayPercent();
        }

        private double GetOverallProgressPercent()
        {
            if (_totalFiles <= 1)
                return Math.Clamp(_currentFilePercent, 0, 100);

            double completedUnits = _completedFiles + _currentFilePercent / 100d;
            return 100d * Math.Clamp(completedUnits, 0, _totalFiles) / _totalFiles;
        }

        private double GetDisplayPercent()
        {
            if (_totalFiles <= 0) return 0;
            return Math.Clamp((_completedFiles * 100d + _currentFilePercent) / _totalFiles, 0, 100);
        }

        private void SetStatus(string key, params object[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            RenderStatusText();
        }

        private void RenderStatusText()
        {
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, T(_statusKey), _statusArgs);
        }

        private void SetProgressText(string key)
        {
            _progressTextKey = key;
            _progressPercent = null;
            RenderProgressText();
        }

        private void SetProgressPercent(double percent)
        {
            _progressTextKey = null;
            _progressPercent = percent;
            RenderProgressText();
        }

        private void RenderProgressText()
        {
            if (_progressTextKey is not null)
            {
                ProgressPercentText.Text = T(_progressTextKey);
                return;
            }

            if (_progressPercent.HasValue)
            {
                string text = string.Format(CultureInfo.InvariantCulture, "{0:0}%", _progressPercent.Value);
                if (_totalFiles > 1)
                {
                    text += $" ({_completedFiles}/{_totalFiles})";
                }
                ProgressPercentText.Text = text;
            }
            else
            {
                ProgressPercentText.Text = T("ProgressZero");
            }
        }

        private string T(string key)
        {
            bool en = _currentLanguage == "en";
            return en ? EnglishText(key) : ChineseText(key);
        }

        private static string ChineseText(string key) => key switch
        {
            "HeaderSubtitle" => "图片清晰化工作台",
            "ThemeLabel" => "主题",
            "ThemeSystem" => "跟随系统",
            "ThemeLight" => "浅色",
            "ThemeDark" => "深色",
            "LanguageLabel" => "语言",
            "LanguageAuto" => "自动识别",
            "LanguageZh" => "简体中文",
            "LanguageEn" => "English",
            "ReadySection" => "准备处理",
            "InputTitle" => "图片来源",
            "OutputTitle" => "保存位置",
            "OpenFolder" => "打开文件夹",
            "BrowseInput" => "选择图片文件夹",
            "BrowseOutput" => "选择保存文件夹",
            "StartTitle" => "开始处理",
            "StartButton" => "开始清晰化",
            "StopButton" => "停止",
            "SettingsSection" => "处理方式",
            "ModelLabel" => "图片类型",
            "ScaleLabel" => "放大倍数",
            "FormatLabel" => "保存格式",
            "Advanced" => "高级设置",
            "Log" => "日志",
            "LogHeader" => "输出日志",
            "ThreadsLabel" => "线程数",
            "GpuLabel" => "GPU 设备",
            "Tta" => "质量增强（较慢）",
            "Hint" => "请先根据图片内容选择类型，模型不匹配可能影响效果；其余设置保持默认即可。",
            "ModelPhoto" => "照片 / 人像",
            "ModelAnime" => "动漫 / 插画",
            "ModelVideo2" => "动漫视频（2x）",
            "ModelVideo3" => "动漫视频（3x）",
            "ModelVideo4" => "动漫视频（4x）",
            "ScaleAuto" => "模型默认（{0}x）",
            "FormatPng" => "PNG（清晰，推荐）",
            "FormatJpg" => "JPG（文件更小）",
            "FormatWebp" => "WebP（网页友好）",
            "AutoRecommended" => "自动（推荐）",
            "PickFolderTitle" => "选择文件夹",
            "MissingExe" => "找不到主程序：\n{0}\n\n请确认 realesrgan-ncnn-vulkan.exe 与 GUI 安装目录相邻。",
            "InputAccessError" => "无法创建/访问输入文件夹。",
            "OutputAccessError" => "无法创建/访问输出文件夹。",
            "NoImagesAsk" => "输入文件夹中没有支持的图片 (png/jpg/jpeg/bmp/webp/tif)。\n是否复制示例 input.jpg 进去？",
            "MissingSample" => "找不到示例图片 input.jpg。",
            "CopySampleFailed" => "复制示例图片失败: {0}",
            "InputSummaryNone" => "尚未选择文件夹",
            "OutputSummaryNone" => "尚未选择保存位置",
            "FolderCreateOnStart" => "开始时会自动创建文件夹",
            "FolderUnreadable" => "无法读取文件夹内容",
            "InputNoImages" => "未发现支持的图片",
            "OutputNoFiles" => "暂无输出结果",
            "InputCount" => "发现 {0} 张可处理图片",
            "OutputCount" => "已有 {0} 个结果文件",
            "StatusReady" => "就绪",
            "StatusProcessing" => "正在处理...",
            "StatusProcessingFiles" => "未完成 {1} 个，已完成 {0} 个",
            "StatusStopped" => "已停止",
            "StatusDone" => "完成，输出 {0} 个文件",
            "StatusFailed" => "失败 (代码 {0})",
            "StatusError" => "错误: {0}",
            "ProgressZero" => "0%",
            "ProgressPreparing" => "准备中",
            "ProgressStopped" => "已停止",
            "ProgressIncomplete" => "未完成",
            "ProgressError" => "出错",
            _ => key,
        };

        private static string EnglishText(string key) => key switch
        {
            "HeaderSubtitle" => "Image upscaling workspace",
            "ThemeLabel" => "Theme",
            "ThemeSystem" => "System",
            "ThemeLight" => "Light",
            "ThemeDark" => "Dark",
            "LanguageLabel" => "Language",
            "LanguageAuto" => "Auto",
            "LanguageZh" => "简体中文",
            "LanguageEn" => "English",
            "ReadySection" => "Prepare",
            "InputTitle" => "Image source",
            "OutputTitle" => "Save to",
            "OpenFolder" => "Open folder",
            "BrowseInput" => "Choose image folder",
            "BrowseOutput" => "Choose output folder",
            "StartTitle" => "Run",
            "StartButton" => "Start upscaling",
            "StopButton" => "Stop",
            "SettingsSection" => "Processing",
            "ModelLabel" => "Image type",
            "ScaleLabel" => "Scale",
            "FormatLabel" => "Format",
            "Advanced" => "Advanced settings",
            "Log" => "Log",
            "LogHeader" => "Output log",
            "ThreadsLabel" => "Threads",
            "GpuLabel" => "GPU device",
            "Tta" => "Enhanced quality (slower)",
            "Hint" => "Choose the image type first. A mismatched model can reduce quality; the other defaults are usually fine.",
            "ModelPhoto" => "Photo / portrait",
            "ModelAnime" => "Anime / illustration",
            "ModelVideo2" => "Anime video (2x)",
            "ModelVideo3" => "Anime video (3x)",
            "ModelVideo4" => "Anime video (4x)",
            "ScaleAuto" => "Model default ({0}x)",
            "FormatPng" => "PNG (clear, recommended)",
            "FormatJpg" => "JPG (smaller files)",
            "FormatWebp" => "WebP (web friendly)",
            "AutoRecommended" => "Auto (recommended)",
            "PickFolderTitle" => "Choose a folder",
            "MissingExe" => "Main program not found:\n{0}\n\nPlease make sure realesrgan-ncnn-vulkan.exe is next to the GUI.",
            "InputAccessError" => "Cannot create or access the input folder.",
            "OutputAccessError" => "Cannot create or access the output folder.",
            "NoImagesAsk" => "No supported images were found in the input folder (png/jpg/jpeg/bmp/webp/tif).\nCopy the sample input.jpg into it?",
            "MissingSample" => "Sample image input.jpg was not found.",
            "CopySampleFailed" => "Failed to copy the sample image: {0}",
            "InputSummaryNone" => "No folder selected",
            "OutputSummaryNone" => "No output folder selected",
            "FolderCreateOnStart" => "The folder will be created when processing starts",
            "FolderUnreadable" => "Cannot read this folder",
            "InputNoImages" => "No supported images found",
            "OutputNoFiles" => "No output files yet",
            "InputCount" => "{0} supported images found",
            "OutputCount" => "{0} result files already here",
            "StatusReady" => "Ready",
            "StatusProcessing" => "Processing...",
            "StatusProcessingFiles" => "Remaining {1}, completed {0}",
            "StatusStopped" => "Stopped",
            "StatusDone" => "Done, exported {0} files",
            "StatusFailed" => "Failed (code {0})",
            "StatusError" => "Error: {0}",
            "ProgressZero" => "0%",
            "ProgressPreparing" => "Preparing",
            "ProgressStopped" => "Stopped",
            "ProgressIncomplete" => "Incomplete",
            "ProgressError" => "Error",
            _ => key,
        };

        // --- Helper type -------------------------------------------------------------

        private sealed record ComboItem(string Tag, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
