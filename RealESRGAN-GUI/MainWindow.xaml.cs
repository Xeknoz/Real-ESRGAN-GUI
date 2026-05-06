using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;

namespace RealESRGAN_GUI
{
    public partial class MainWindow : Window
    {
        private static readonly string[] SupportedExts =
            { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };

        private readonly string _exePath;
        private string _inputDir = string.Empty;
        private string _outputDir = string.Empty;
        private Process? _runningProcess;
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();

            _exePath = ResolveBackendExe();
            PopulateComboBoxes();
            InitializeDefaults();

            Loaded += (_, _) => LogLine($"工作引擎: {_exePath}");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableDarkTitleBar(this);
        }

        // Tell DWM to render the non-client area (title bar, border) in dark mode.
        // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 on Windows 10 build 19041+ and Windows 11.
        // 19 was the pre-release attribute used on builds 18985..19041; we try both.
        private static void EnableDarkTitleBar(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                int useDark = 1;
                if (DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
            }
            catch { /* DWM call is best-effort; ignore on unsupported OS */ }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // --- Initialization helpers --------------------------------------------------

        private static string ResolveBackendExe()
        {
            // The GUI exe lives in <root>\RealESRGAN-GUI-App\, the backend exe in <root>\.
            string here = AppContext.BaseDirectory;
            string parent = Path.GetFullPath(Path.Combine(here, ".."));
            string a = Path.Combine(parent, "realesrgan-ncnn-vulkan.exe");
            string b = Path.Combine(here,   "realesrgan-ncnn-vulkan.exe");
            return File.Exists(a) ? a : (File.Exists(b) ? b : a);
        }

        private void PopulateComboBoxes()
        {
            ModelCombo.ItemsSource = new[]
            {
                new ComboItem("realesrgan-x4plus",          "realesrgan-x4plus  (通用照片)"),
                new ComboItem("realesrgan-x4plus-anime",    "realesrgan-x4plus-anime  (动漫/插画)"),
                new ComboItem("realesr-animevideov3-x2",    "realesr-animevideov3-x2  (动漫视频 x2)"),
                new ComboItem("realesr-animevideov3-x3",    "realesr-animevideov3-x3  (动漫视频 x3)"),
                new ComboItem("realesr-animevideov3-x4",    "realesr-animevideov3-x4  (动漫视频 x4)"),
            };
            ModelCombo.DisplayMemberPath = nameof(ComboItem.Display);
            ModelCombo.SelectedIndex = 0;

            ScaleCombo.ItemsSource = BuildScaleItems(DefaultScaleFor("realesrgan-x4plus"));
            ScaleCombo.DisplayMemberPath = nameof(ComboItem.Display);
            ScaleCombo.SelectedIndex = 0;

            FormatCombo.ItemsSource = new[]
            {
                new ComboItem("png",  "PNG"),
                new ComboItem("jpg",  "JPG"),
                new ComboItem("webp", "WebP"),
            };
            FormatCombo.DisplayMemberPath = nameof(ComboItem.Display);
            FormatCombo.SelectedIndex = 0;

            ThreadsCombo.ItemsSource = new[]
            {
                new ComboItem("0", "自动"),
                new ComboItem("1", "1"),
                new ComboItem("2", "2"),
                new ComboItem("4", "4"),
                new ComboItem("8", "8"),
            };
            ThreadsCombo.DisplayMemberPath = nameof(ComboItem.Display);
            ThreadsCombo.SelectedIndex = 0;

            GpuCombo.ItemsSource = new[]
            {
                new ComboItem(string.Empty, "自动"),
                new ComboItem("0", "0"),
                new ComboItem("1", "1"),
                new ComboItem("2", "2"),
            };
            GpuCombo.DisplayMemberPath = nameof(ComboItem.Display);
            GpuCombo.SelectedIndex = 0;

            // Attach AFTER the initial selection so the handler doesn't run during population.
            ModelCombo.SelectionChanged += OnModelChanged;
        }

        // The CLI flag -s defaults are model-specific; reflect that in the UI label.
        private static int DefaultScaleFor(string model) => model switch
        {
            "realesr-animevideov3-x2" => 2,
            "realesr-animevideov3-x3" => 3,
            _                         => 4,
        };

        private static ComboItem[] BuildScaleItems(int defaultScale) => new[]
        {
            new ComboItem(string.Empty, $"默认 ({defaultScale}x)"),
            new ComboItem("2", "2x"),
            new ComboItem("3", "3x"),
            new ComboItem("4", "4x"),
        };

        private void OnModelChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ModelCombo.SelectedItem is not ComboItem mi) return;
            int prev = ScaleCombo.SelectedIndex;
            ScaleCombo.ItemsSource = BuildScaleItems(DefaultScaleFor(mi.Tag));
            ScaleCombo.SelectedIndex = prev >= 0 ? prev : 0;
        }

        private void InitializeDefaults()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            _inputDir  = Path.Combine(pictures, "Real-ESRGAN_Input");
            _outputDir = Path.Combine(pictures, "Real-ESRGAN_Output");
            InputPathBox.Text  = _inputDir;
            OutputPathBox.Text = _outputDir;
        }

        // --- Folder picking ----------------------------------------------------------

        private string? PickFolder(string initial)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "选择文件夹",
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
        }

        private void OnBrowseOutputClick(object sender, RoutedEventArgs e)
        {
            var picked = PickFolder(_outputDir);
            if (picked is null) return;
            _outputDir = picked;
            OutputPathBox.Text = picked;
        }

        private void OnOpenInputClick(object sender, RoutedEventArgs e)  => OpenInExplorer(_inputDir);
        private void OnOpenOutputClick(object sender, RoutedEventArgs e) => OpenInExplorer(_outputDir);

        private void OpenInExplorer(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogLine($"无法打开 {path}: {ex.Message}");
            }
        }

        // --- Process orchestration ---------------------------------------------------

        private async void OnStartClick(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_exePath))
            {
                MessageBox.Show(this,
                    $"找不到主程序：\n{_exePath}\n\n请确认 realesrgan-ncnn-vulkan.exe 与 GUI 安装目录相邻。",
                    "Real-ESRGAN GUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try { Directory.CreateDirectory(_inputDir);  } catch { /* surfaced below */ }
            try { Directory.CreateDirectory(_outputDir); } catch { /* surfaced below */ }

            if (!Directory.Exists(_inputDir))
            {
                MessageBox.Show(this, "无法创建/访问输入文件夹。", "Real-ESRGAN GUI",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!Directory.Exists(_outputDir))
            {
                MessageBox.Show(this, "无法创建/访问输出文件夹。", "Real-ESRGAN GUI",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var files = EnumerateInputs(_inputDir);
            if (files.Count == 0)
            {
                var ans = MessageBox.Show(this,
                    "输入文件夹中没有支持的图片 (png/jpg/jpeg/bmp/webp/tif)。\n是否复制示例 input.jpg 进去？",
                    "Real-ESRGAN GUI", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ans != MessageBoxResult.Yes) return;

                if (!TryCopySample(_inputDir, out string copyError))
                {
                    MessageBox.Show(this, copyError, "Real-ESRGAN GUI",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                files = EnumerateInputs(_inputDir);
                if (files.Count == 0) return;
            }

            string args = BuildArgs();
            LogLine($"命令: realesrgan-ncnn-vulkan.exe {args}");
            LogLine($"输入文件 {files.Count} 个，开始处理...");

            SetUIBusy(true);
            _cts = new CancellationTokenSource();

            try
            {
                int exitCode = await RunBackendAsync(args, _cts.Token);
                if (_cts.IsCancellationRequested)
                {
                    LogLine("已停止。");
                    StatusText.Text = "已停止";
                }
                else if (exitCode == 0)
                {
                    int produced = EnumerateInputs(_outputDir).Count;
                    LogLine($"完成。输出文件夹中有 {produced} 个图片。");
                    StatusText.Text = $"完成 — 输出 {produced} 个文件";
                }
                else
                {
                    LogLine($"进程退出码: {exitCode}");
                    StatusText.Text = $"失败 (代码 {exitCode})";
                }
            }
            catch (Exception ex)
            {
                LogLine($"异常: {ex.Message}");
                StatusText.Text = "发生错误";
            }
            finally
            {
                SetUIBusy(false);
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
            catch (Exception ex)
            {
                LogLine($"停止时出错: {ex.Message}");
            }
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
            proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog(e.Data); };
            proc.ErrorDataReceived  += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog("[stderr] " + e.Data); };
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
            string here = AppContext.BaseDirectory;
            string a = Path.GetFullPath(Path.Combine(here, "..", "input.jpg"));
            string b = Path.Combine(here, "input.jpg");
            string src = File.Exists(a) ? a : (File.Exists(b) ? b : string.Empty);
            if (string.IsNullOrEmpty(src))
            {
                error = "找不到示例图片 input.jpg。";
                return false;
            }
            try
            {
                File.Copy(src, Path.Combine(dir, "input.jpg"), overwrite: true);
                error = string.Empty;
                LogLine("已复制示例图片到输入文件夹。");
                return true;
            }
            catch (Exception ex)
            {
                error = $"复制示例图片失败: {ex.Message}";
                return false;
            }
        }

        // --- UI helpers --------------------------------------------------------------

        private void SetUIBusy(bool busy)
        {
            StartButton.IsEnabled = !busy;
            StopButton.IsEnabled  =  busy;
            ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (busy) StatusText.Text = "正在处理...";
        }

        private void AppendLog(string line)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => AppendLog(line)); return; }
            LogLine(line);
        }

        private void LogLine(string line)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            LogBox.AppendText($"[{ts}] {line}{Environment.NewLine}");
        }

        private void OnLogTextChanged(object sender, TextChangedEventArgs e) => LogBox.ScrollToEnd();

        // --- Helper type -------------------------------------------------------------

        private sealed record ComboItem(string Tag, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
