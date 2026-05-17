using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace RealESRGAN_GUI
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaBorderColor = 34;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;

        public static bool CurrentThemeIsDark { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            ApplyTheme(IsSystemDarkTheme());

            bool launchedByLauncher = Array.Exists(e.Args,
                arg => string.Equals(arg, "--from-launcher", StringComparison.OrdinalIgnoreCase));
            if (!launchedByLauncher)
            {
                StartLauncherOrNotify();
                Current.Shutdown();
                return;
            }

            const string mutexName = @"Global\RealESRGAN_GUI_SingleInstance";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                bool zh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                string message = zh ? "Real-ESRGAN GUI 已经在运行中。" : "Real-ESRGAN GUI is already running.";
                string caption = zh ? "提示" : "Notice";
                ShowThemedNotice(caption, message, zh);
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            ActivateMainWindow(mainWindow);
        }

        private static void ActivateMainWindow(Window window)
        {
            window.Activate();
            window.Focus();
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                window.Activate();
                window.Focus();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static void StartLauncherOrNotify()
        {
            string appDir = AppContext.BaseDirectory;
            string launcherPath = Path.Combine(appDir, "Launcher.exe");

            try
            {
                if (File.Exists(launcherPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = launcherPath,
                        WorkingDirectory = appDir,
                        UseShellExecute = true,
                    });
                    return;
                }
            }
            catch
            {
                // Fall through to a short actionable notice below.
            }

            bool zh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            string message = zh
                ? "无法找到 Launcher.exe。"
                : "Launcher.exe could not be found.";
            string caption = zh ? "启动失败" : "Launch Failed";
            ShowThemedNotice(caption, message, zh);
        }

        private static void ShowThemedNotice(string caption, string message, bool zh)
        {
            var notice = new NoticeWindow(caption, message, zh ? "确定" : "OK");
            notice.ShowDialog();
        }

        public static bool IsSystemDarkTheme()
        {
            try
            {
                object? value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    1);
                return value is int lightTheme && lightTheme == 0;
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyTheme(bool dark)
        {
            var resources = Current.Resources;
            CurrentThemeIsDark = dark;

            if (dark)
            {
                SetBrush(resources, "BgBrush", "#FF0E151A");
                SetBrush(resources, "SurfaceBrush", "#FF141D23");
                SetBrush(resources, "PanelBrush", "#FF1A252C");
                SetBrush(resources, "ControlBrush", "#FF202D35");
                SetBrush(resources, "ControlHoverBrush", "#FF293842");
                SetBrush(resources, "BorderBrush", "#FF33444F");
                SetBrush(resources, "ForegroundBrush", "#FFF4F7FA");
                SetBrush(resources, "MutedForegroundBrush", "#FFB5C0C9");
                SetBrush(resources, "SubtleForegroundBrush", "#FF87949F");
                SetBrush(resources, "AccentBrush", "#FF2DD4BF");
                SetBrush(resources, "AccentHoverBrush", "#FF5EEAD4");
                SetBrush(resources, "AccentSoftBrush", "#FF123A39");
                SetBrush(resources, "StopBrush", "#FFFB7185");
                SetBrush(resources, "StopHoverBrush", "#FFFDA4AF");
                SetBrush(resources, "RailBrush", "#FF10232B");
                SetBrush(resources, "HeaderDividerBrush", "#FF28454F");
                SetBrush(resources, "HeaderForegroundBrush", "#FFFFFFFF");
                SetBrush(resources, "HeaderSubtleBrush", "#FFC1D5DE");
                SetBrush(resources, "ProgressTrackBrush", "#FF25333A");
                SetBrush(resources, "ProgressCurrentBrush", "#FF5EEAD4");
                SetBrush(resources, "ProgressCompleteBrush", "#FF168F83");
                SetBrush(resources, "ScrollThumbBrush", "#FF566873");
                SetBrush(resources, "ScrollThumbHoverBrush", "#FF718590");
                SetBrush(resources, "DangerBackgroundBrush", "#FF311D24");
                SetBrush(resources, "DangerBorderBrush", "#FF68404A");
                SetBrush(resources, "DangerHoverBrush", "#FF3D252E");
                SetBrush(resources, "SubtleHoverBrush", "#1AFFFFFF");
                SetBrush(resources, "ContextMenuBorderBrush", "#FF263A43");
                SetBrush(resources, "ContextMenuDividerBrush", "#FF213038");
                SetBrush(resources, "ContextMenuHoverBrush", "#1F2DD4BF");
                SetBrush(resources, "ComboBoxSelectedBgBrush", "#FF2DD4BF");
                SetBrush(resources, "ComboBoxSelectedFgBrush", "#FF06312D");
                SetBrush(resources, "OnAccentBrush", "#FF06312D");
            }
            else
            {
                SetBrush(resources, "BgBrush", "#FFF5F7F9");
                SetBrush(resources, "SurfaceBrush", "#FFFFFFFF");
                SetBrush(resources, "PanelBrush", "#FFEEF3F6");
                SetBrush(resources, "ControlBrush", "#FFFFFFFF");
                SetBrush(resources, "ControlHoverBrush", "#FFF4F7F9");
                SetBrush(resources, "BorderBrush", "#FFD7E0E6");
                SetBrush(resources, "ForegroundBrush", "#FF18222B");
                SetBrush(resources, "MutedForegroundBrush", "#FF55636E");
                SetBrush(resources, "SubtleForegroundBrush", "#FF7B8791");
                SetBrush(resources, "AccentBrush", "#FF0F766E");
                SetBrush(resources, "AccentHoverBrush", "#FF0B625C");
                SetBrush(resources, "AccentSoftBrush", "#FFDDF5F0");
                SetBrush(resources, "StopBrush", "#FFD9485F");
                SetBrush(resources, "StopHoverBrush", "#FFBF3550");
                SetBrush(resources, "RailBrush", "#FF18313A");
                SetBrush(resources, "HeaderDividerBrush", "#FF284A56");
                SetBrush(resources, "HeaderForegroundBrush", "#FFFFFFFF");
                SetBrush(resources, "HeaderSubtleBrush", "#FFC9D8E0");
                SetBrush(resources, "ProgressTrackBrush", "#FFDCE6EB");
                SetBrush(resources, "ProgressCurrentBrush", "#FF82CBC2");
                SetBrush(resources, "ProgressCompleteBrush", "#FF0F766E");
                SetBrush(resources, "ScrollThumbBrush", "#FFB4C2CC");
                SetBrush(resources, "ScrollThumbHoverBrush", "#FF91A3AF");
                SetBrush(resources, "DangerBackgroundBrush", "#FFFFF2F4");
                SetBrush(resources, "DangerBorderBrush", "#FFF3C4CC");
                SetBrush(resources, "DangerHoverBrush", "#FFFFE7EC");
                SetBrush(resources, "SubtleHoverBrush", "#12000000");
                SetBrush(resources, "ContextMenuBorderBrush", "#FFE2EAF0");
                SetBrush(resources, "ContextMenuDividerBrush", "#FFECF2F5");
                SetBrush(resources, "ContextMenuHoverBrush", "#140F766E");
                SetBrush(resources, "ComboBoxSelectedBgBrush", "#FF0F766E");
                SetBrush(resources, "ComboBoxSelectedFgBrush", "#FFFFFFFF");
                SetBrush(resources, "OnAccentBrush", "#FFFFFFFF");
            }

            SetBrush(resources, SystemColors.WindowBrushKey, dark ? "#FF141D23" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.ControlBrushKey, dark ? "#FF202D35" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.ControlTextBrushKey, dark ? "#FFF4F7FA" : "#FF18222B");
            SetBrush(resources, SystemColors.WindowTextBrushKey, dark ? "#FFF4F7FA" : "#FF18222B");
            SetBrush(resources, SystemColors.HighlightBrushKey, dark ? "#FF2DD4BF" : "#FF0F766E");
            SetBrush(resources, SystemColors.HighlightTextBrushKey, dark ? "#FF06312D" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.GrayTextBrushKey, dark ? "#FF87949F" : "#FF7B8791");
        }

        public static void ApplyWindowTitleBarTheme(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int useDark = CurrentThemeIsDark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref useDark, sizeof(int));

                SetTitleBarColor(hwnd, DwmwaCaptionColor, "RailBrush");
                SetTitleBarColor(hwnd, DwmwaTextColor, "HeaderForegroundBrush");
                SetTitleBarColor(hwnd, DwmwaBorderColor, "RailBrush");
            }
            catch
            {
                // DWM calls are best-effort and unsupported on older systems.
            }
        }

        private static void SetBrush(ResourceDictionary resources, object key, string color)
        {
            var parsed = (Color)ColorConverter.ConvertFromString(color);

            if (resources[key] is SolidColorBrush { IsFrozen: false } brush)
            {
                brush.Color = parsed;
                return;
            }

            resources[key] = new SolidColorBrush(parsed);
        }

        private static void SetTitleBarColor(IntPtr hwnd, int attribute, string resourceKey)
        {
            if (Current.Resources[resourceKey] is not SolidColorBrush brush) return;

            int colorRef = brush.Color.R |
                           brush.Color.G << 8 |
                           brush.Color.B << 16;
            DwmSetWindowAttribute(hwnd, attribute, ref colorRef, sizeof(int));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
