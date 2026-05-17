using System;
using System.Collections.Generic;
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
                T("LicenseSection"),
                T("LicenseMissing"),
                T("OpenRepository"),
                T("Close"),
                T("OpenRepositoryFailed"),
                ReadLicenseDocuments())
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

        private static IReadOnlyList<AboutWindow.LicenseDocument> ReadLicenseDocuments()
        {
            string root = ResolveLicenseRoot();
            var documents = new List<AboutWindow.LicenseDocument>();

            AddLicenseDocument(
                documents,
                "Real-ESRGAN GUI - MIT License",
                root,
                "LICENSE.txt",
                "LICENSE");
            AddLicenseDocument(
                documents,
                "Third-party notices",
                root,
                "THIRD_PARTY_NOTICES.md");
            AddLicenseDocument(
                documents,
                "Real-ESRGAN - BSD 3-Clause License",
                root,
                Path.Combine("licenses", "Real-ESRGAN-LICENSE.txt"));
            AddLicenseDocument(
                documents,
                "Real-ESRGAN ncnn Vulkan Enhanced - MIT License",
                root,
                Path.Combine("licenses", "Real-ESRGAN-ncnn-vulkan-Enhanced-LICENSE.txt"),
                Path.Combine("third_party", "ncnn_src", "LICENSE"));
            AddLicenseDocument(
                documents,
                "ncnn - License and third-party notices",
                root,
                Path.Combine("licenses", "ncnn-LICENSE.txt"),
                Path.Combine("third_party", "ncnn_src", "src", "ncnn", "LICENSE.txt"));
            AddLicenseDocument(
                documents,
                "glslang - License and notices",
                root,
                Path.Combine("licenses", "glslang-LICENSE.txt"),
                Path.Combine("third_party", "ncnn_src", "src", "ncnn", "glslang", "LICENSE.txt"));
            AddLicenseDocument(
                documents,
                "libwebp - BSD 3-Clause License",
                root,
                Path.Combine("licenses", "libwebp-COPYING.txt"),
                Path.Combine("third_party", "ncnn_src", "src", "libwebp", "COPYING"));
            AddLicenseDocument(
                documents,
                "pybind11 - BSD 3-Clause License",
                root,
                Path.Combine("licenses", "pybind11-LICENSE.txt"),
                Path.Combine("third_party", "ncnn_src", "src", "ncnn", "python", "pybind11", "LICENSE"));
            AddLicenseDocument(
                documents,
                "Microsoft .NET runtime - MIT License",
                root,
                Path.Combine("licenses", "Microsoft-dotnet-LICENSE.txt"));
            AddLicenseDocument(
                documents,
                "Microsoft .NET runtime - third-party notices",
                root,
                Path.Combine("licenses", "Microsoft-dotnet-ThirdPartyNotices.txt"));
            AddLicenseDocument(
                documents,
                "Microsoft Visual C++ Redistributable - Redist notice",
                root,
                Path.Combine("licenses", "Microsoft-Visual-Cpp-Redistributable-Redist.txt"));

            return documents;
        }

        private static string ResolveLicenseRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "LICENSE.txt")) ||
                    File.Exists(Path.Combine(current.FullName, "LICENSE")) ||
                    Directory.Exists(Path.Combine(current.FullName, "licenses")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return AppContext.BaseDirectory;
        }

        private static void AddLicenseDocument(
            ICollection<AboutWindow.LicenseDocument> documents,
            string title,
            string root,
            params string[] relativePaths)
        {
            foreach (string relativePath in relativePaths)
            {
                string path = Path.Combine(root, relativePath);
                if (!File.Exists(path))
                    continue;

                try
                {
                    string text = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        documents.Add(new AboutWindow.LicenseDocument(title, text));
                        return;
                    }
                }
                catch
                {
                    return;
                }
            }
        }
    }
}
