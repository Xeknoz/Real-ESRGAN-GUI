using System;
using System.Globalization;
using System.Reflection;
using System.Windows;

namespace RealESRGAN_GUI
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();

            bool zh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            StatusText.Text = zh ? "正在启动..." : "Starting...";

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = version != null
                ? $"v{version.Major}.{version.Minor}"
                : "v1.0";
        }
    }
}
