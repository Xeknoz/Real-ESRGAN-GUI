using System;
using System.Globalization;
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
        }
    }
}
