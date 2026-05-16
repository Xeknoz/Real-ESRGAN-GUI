using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private const string RepositoryUrl = "https://github.com/Xeknoz/realesrgan-gui";

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow(
                GetAppVersion(),
                RepositoryUrl,
                T("AboutTitle"),
                T("AboutDescription"),
                T("VersionLabel"),
                T("OpenRepository"),
                T("Close"),
                T("OpenRepositoryFailed"))
            {
                Owner = this,
            };

            aboutWindow.ShowDialog();
        }

        private static string GetAppVersion()
        {
            string versionFilePath = Path.Combine(AppContext.BaseDirectory, "VERSION.txt");
            try
            {
                if (File.Exists(versionFilePath))
                {
                    string? versionFromFile = File
                        .ReadLines(versionFilePath)
                        .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
                        ?.Trim();

                    if (!string.IsNullOrWhiteSpace(versionFromFile))
                        return versionFromFile;
                }
            }
            catch
            {
                // Fall back to assembly metadata if the portable version file cannot be read.
            }

            string? informationalVersion = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
                return informationalVersion.Split('+', 2)[0];

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        }
    }
}
