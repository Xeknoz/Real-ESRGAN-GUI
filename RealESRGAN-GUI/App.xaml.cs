using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RealESRGAN_GUI
{
    public partial class App : Application
    {
        private static Mutex? _mutex;

        protected override async void OnStartup(StartupEventArgs e)
        {
            ApplyTheme(IsSystemDarkTheme());

            const string mutexName = @"Global\RealESRGAN_GUI_SingleInstance";
            _mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                bool zh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                string message = zh ? "Real-ESRGAN GUI 已经在运行中。" : "Real-ESRGAN GUI is already running.";
                string caption = zh ? "提示" : "Notice";
                MessageBox.Show(message, caption,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            var splash = new SplashWindow();
            MainWindow = splash;
            splash.Show();

            await Dispatcher.Yield(DispatcherPriority.Background);

            var minimumDisplayTime = Task.Delay(650);
            var mainWindow = new MainWindow();
            await minimumDisplayTime;

            MainWindow = mainWindow;
            mainWindow.Show();
            splash.Close();
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

            if (dark)
            {
                SetBrush(resources, "BgBrush", "#FF111412");
                SetBrush(resources, "SurfaceBrush", "#FF1B1F1C");
                SetBrush(resources, "PanelBrush", "#FF232820");
                SetBrush(resources, "ControlBrush", "#FF242B25");
                SetBrush(resources, "ControlHoverBrush", "#FF30382F");
                SetBrush(resources, "BorderBrush", "#FF3D463D");
                SetBrush(resources, "ForegroundBrush", "#FFF4F0E8");
                SetBrush(resources, "MutedForegroundBrush", "#FFC0B8AB");
                SetBrush(resources, "SubtleForegroundBrush", "#FF8E968E");
                SetBrush(resources, "AccentBrush", "#FF66C2B2");
                SetBrush(resources, "AccentHoverBrush", "#FF7AD5C4");
                SetBrush(resources, "AccentSoftBrush", "#FF203D38");
                SetBrush(resources, "StopBrush", "#FFFF8A66");
                SetBrush(resources, "StopHoverBrush", "#FFFF9D80");
                SetBrush(resources, "RailBrush", "#FF17211E");
                SetBrush(resources, "HeaderDividerBrush", "#FF31453E");
                SetBrush(resources, "HeaderForegroundBrush", "#FFFFFFFF");
                SetBrush(resources, "HeaderSubtleBrush", "#FFBBD5CD");
                SetBrush(resources, "StatusPillBrush", "#553A4A43");
                SetBrush(resources, "StatusPillForegroundBrush", "#FFEAF5EF");
                SetBrush(resources, "ProgressTrackBrush", "#FF2B332D");
                SetBrush(resources, "ScrollThumbBrush", "#FF5E675D");
                SetBrush(resources, "ScrollThumbHoverBrush", "#FF788376");
                SetBrush(resources, "DangerBackgroundBrush", "#FF2F211D");
                SetBrush(resources, "DangerBorderBrush", "#FF664237");
                SetBrush(resources, "DangerHoverBrush", "#FF3B2822");
                SetBrush(resources, "SubtleHoverBrush", "#18FFFFFF");
            }
            else
            {
                SetBrush(resources, "BgBrush", "#FFF7F4EE");
                SetBrush(resources, "SurfaceBrush", "#FFFFFFFF");
                SetBrush(resources, "PanelBrush", "#FFF0EBE2");
                SetBrush(resources, "ControlBrush", "#FFFFFFFF");
                SetBrush(resources, "ControlHoverBrush", "#FFF8F4EC");
                SetBrush(resources, "BorderBrush", "#FFD9D1C4");
                SetBrush(resources, "ForegroundBrush", "#FF22201C");
                SetBrush(resources, "MutedForegroundBrush", "#FF6D675E");
                SetBrush(resources, "SubtleForegroundBrush", "#FF948D82");
                SetBrush(resources, "AccentBrush", "#FF286C67");
                SetBrush(resources, "AccentHoverBrush", "#FF1F5B57");
                SetBrush(resources, "AccentSoftBrush", "#FFE0F0EC");
                SetBrush(resources, "StopBrush", "#FFC65F42");
                SetBrush(resources, "StopHoverBrush", "#FFA94F37");
                SetBrush(resources, "RailBrush", "#FF242821");
                SetBrush(resources, "HeaderDividerBrush", "#14334B42");
                SetBrush(resources, "HeaderForegroundBrush", "#FFFFFFFF");
                SetBrush(resources, "HeaderSubtleBrush", "#FFD7E3DA");
                SetBrush(resources, "StatusPillBrush", "#25334B42");
                SetBrush(resources, "StatusPillForegroundBrush", "#FFEAF5EF");
                SetBrush(resources, "ProgressTrackBrush", "#FFE8E0D5");
                SetBrush(resources, "ScrollThumbBrush", "#FFC7BBAA");
                SetBrush(resources, "ScrollThumbHoverBrush", "#FFA99C8A");
                SetBrush(resources, "DangerBackgroundBrush", "#FFFFF5F1");
                SetBrush(resources, "DangerBorderBrush", "#FFF0C4B4");
                SetBrush(resources, "DangerHoverBrush", "#FFFFECE6");
                SetBrush(resources, "SubtleHoverBrush", "#10000000");
            }

            SetBrush(resources, SystemColors.WindowBrushKey, dark ? "#FF1B1F1C" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.ControlBrushKey, dark ? "#FF242B25" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.ControlTextBrushKey, dark ? "#FFF4F0E8" : "#FF22201C");
            SetBrush(resources, SystemColors.WindowTextBrushKey, dark ? "#FFF4F0E8" : "#FF22201C");
            SetBrush(resources, SystemColors.HighlightBrushKey, dark ? "#FF66C2B2" : "#FF286C67");
            SetBrush(resources, SystemColors.HighlightTextBrushKey, "#FFFFFFFF");
            SetBrush(resources, SystemColors.GrayTextBrushKey, dark ? "#FF8E968E" : "#FF948D82");
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

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
