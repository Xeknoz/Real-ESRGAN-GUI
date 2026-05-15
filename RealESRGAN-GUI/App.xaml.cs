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
                SetBrush(resources, "BgBrush", "#FF0F1412");
                SetBrush(resources, "SurfaceBrush", "#FF1A1F1C");
                SetBrush(resources, "PanelBrush", "#FF252A26");
                SetBrush(resources, "ControlBrush", "#FF2A302B");
                SetBrush(resources, "ControlHoverBrush", "#FF3A453D");
                SetBrush(resources, "BorderBrush", "#FF4A524A");
                SetBrush(resources, "ForegroundBrush", "#FFE0E3E0");
                SetBrush(resources, "MutedForegroundBrush", "#FFA0A3A0");
                SetBrush(resources, "SubtleForegroundBrush", "#FF707370");
                SetBrush(resources, "AccentBrush", "#FF4DB6AC");
                SetBrush(resources, "AccentHoverBrush", "#FF80CBC4");
                SetBrush(resources, "AccentSoftBrush", "#FF004D40");
                SetBrush(resources, "StopBrush", "#FFEF5350");
                SetBrush(resources, "StopHoverBrush", "#FFE57373");
                SetBrush(resources, "RailBrush", "#FF3A453D");
                SetBrush(resources, "HeaderDividerBrush", "#FF3D463D");
                SetBrush(resources, "HeaderForegroundBrush", "#FFFFFFFF");
                SetBrush(resources, "HeaderSubtleBrush", "#FFBBD5CD");
                SetBrush(resources, "StatusPillBrush", "#FF1B3A2A");
                SetBrush(resources, "StatusPillForegroundBrush", "#FFA5D6A7");
                SetBrush(resources, "ProgressTrackBrush", "#FF3A453D");
                SetBrush(resources, "ScrollThumbBrush", "#FF707370");
                SetBrush(resources, "ScrollThumbHoverBrush", "#FF9E9E9E");
                SetBrush(resources, "DangerBackgroundBrush", "#FF3B0806");
                SetBrush(resources, "DangerBorderBrush", "#FF930000");
                SetBrush(resources, "DangerHoverBrush", "#FF601410");
                SetBrush(resources, "SubtleHoverBrush", "#18FFFFFF");
                SetBrush(resources, "ComboBoxSelectedBgBrush", "#FF4DB6AC");
                SetBrush(resources, "ComboBoxSelectedFgBrush", "#FF003833");
                SetBrush(resources, "OnAccentBrush", "#FF003833");
            }
            else
            {
                SetBrush(resources, "BgBrush", "#FFF5F5F5");
                SetBrush(resources, "SurfaceBrush", "#FFFFFFFF");
                SetBrush(resources, "PanelBrush", "#FFF0F0F0");
                SetBrush(resources, "ControlBrush", "#FFFFFFFF");
                SetBrush(resources, "ControlHoverBrush", "#FFE0F2F1");
                SetBrush(resources, "BorderBrush", "#FFD6D6D6");
                SetBrush(resources, "ForegroundBrush", "#FF1A1C1A");
                SetBrush(resources, "MutedForegroundBrush", "#FF5F6368");
                SetBrush(resources, "SubtleForegroundBrush", "#FF80868B");
                SetBrush(resources, "AccentBrush", "#FF00695C");
                SetBrush(resources, "AccentHoverBrush", "#FF004D40");
                SetBrush(resources, "AccentSoftBrush", "#FFB2DFDB");
                SetBrush(resources, "StopBrush", "#FFC62828");
                SetBrush(resources, "StopHoverBrush", "#FFB71C1C");
                SetBrush(resources, "RailBrush", "#FFE0E0E0");
                SetBrush(resources, "HeaderDividerBrush", "#FFD6D6D6");
                SetBrush(resources, "HeaderForegroundBrush", "#FF1A1C1A");
                SetBrush(resources, "HeaderSubtleBrush", "#FF5F6368");
                SetBrush(resources, "StatusPillBrush", "#FFE8F5E9");
                SetBrush(resources, "StatusPillForegroundBrush", "#FF1B5E20");
                SetBrush(resources, "ProgressTrackBrush", "#FFE0E0E0");
                SetBrush(resources, "ScrollThumbBrush", "#FF9E9E9E");
                SetBrush(resources, "ScrollThumbHoverBrush", "#FF757575");
                SetBrush(resources, "DangerBackgroundBrush", "#FFFFEBEE");
                SetBrush(resources, "DangerBorderBrush", "#FFEF9A9A");
                SetBrush(resources, "DangerHoverBrush", "#FFFFCDD2");
                SetBrush(resources, "SubtleHoverBrush", "#1E000000");
                SetBrush(resources, "ComboBoxSelectedBgBrush", "#FF00695C");
                SetBrush(resources, "ComboBoxSelectedFgBrush", "#FFFFFFFF");
                SetBrush(resources, "OnAccentBrush", "#FFFFFFFF");
            }

            SetBrush(resources, SystemColors.WindowBrushKey, dark ? "#FF1A1F1C" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.ControlBrushKey, dark ? "#FF2A302B" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.ControlTextBrushKey, dark ? "#FFE0E3E0" : "#FF1A1C1A");
            SetBrush(resources, SystemColors.WindowTextBrushKey, dark ? "#FFE0E3E0" : "#FF1A1C1A");
            SetBrush(resources, SystemColors.HighlightBrushKey, dark ? "#FF4DB6AC" : "#FF00695C");
            SetBrush(resources, SystemColors.HighlightTextBrushKey, dark ? "#FF003833" : "#FFFFFFFF");
            SetBrush(resources, SystemColors.GrayTextBrushKey, dark ? "#FF707370" : "#FF80868B");
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
