using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using RealESRGAN_GUI.Services;

namespace RealESRGAN_GUI
{
    public partial class MainWindow : Window
    {
        private readonly string _exePath;
        private readonly string _appDir;
        private readonly BackendProcessRunner _backendRunner;
        private string _inputDir = string.Empty;
        private string _outputDir = string.Empty;
        private CancellationTokenSource? _cts;
        private FileSystemWatcher? _inputWatcher;
        private FileSystemWatcher? _outputWatcher;
        private readonly DispatcherTimer _folderSummaryTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

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
        private int _currentFileIndex = -1;
        private double _currentFilePercent;
        private double _batchPercent;
        private readonly StringBuilder _logBuilder = new();

        public MainWindow()
        {
            InitializeComponent();
            ConfigureHeaderActions();
            ConfigureWindowSizing();

            // Portable folder layout: engine/realesrgan-ncnn-vulkan.exe, engine/vcomp140*.dll,
            // engine/models/
            _appDir = AppContext.BaseDirectory;
            _exePath = Path.Combine(_appDir, "engine", "realesrgan-ncnn-vulkan.exe");
            _backendRunner = new BackendProcessRunner(_exePath);

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
            // Do not clamp position while the user drags; the work-area bottom edge would trap the window at the taskbar.
            LocationChanged += (_, _) => ConfigureWindowSizing(keepInsideWorkArea: false);
            SizeChanged += (_, _) => ApplyResponsiveLayout();
            StateChanged += (_, _) => ConfigureWindowSizing();
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
            ConfigureWindowChromeForVerticalResize();
            ConfigureWindowSizing();
            ApplyThemePreference();
        }

    }
}
