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
            string version = GetVersionNumber();
            return IsDevChannel() ? $"{version} dev" : version;
        }

        private static string GetVersionNumber()
        {
            string? versionFromFile = ReadFirstNonBlankAppFileLine("VERSION.txt");
            if (!string.IsNullOrWhiteSpace(versionFromFile))
                return versionFromFile;

            string? informationalVersion = Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
                return informationalVersion.Split('+', 2)[0];

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        }

        private static bool IsDevChannel()
        {
            string? channel = ReadFirstNonBlankAppFileLine("CHANNEL.txt");
            return string.Equals(channel, "dev", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ReadFirstNonBlankAppFileLine(string fileName)
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, fileName);
            try
            {
                if (!File.Exists(filePath))
                    return null;

                return File
                    .ReadLines(filePath)
                    .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
                    ?.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
