using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using RealESRGAN_GUI.Services;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private const string RepositoryUrl = "https://github.com/Xeknoz/Real-ESRGAN-GUI";
        private const string AboutNativeTitle = "About Real-ESRGAN GUI";
        private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]]+)\]\(([^)]+)\)");
        private static readonly Regex MarkdownInlineCodeRegex = new(@"`([^`]+)`");
        private string _updateCheckIntervalPreference = "daily";

        private void OnAboutClick(object sender, RoutedEventArgs e)
        {
            AboutButton.IsActive = true;

            var aboutWindow = new AboutWindow(
                GetAppVersion(),
                RepositoryUrl,
                AboutNativeTitle,
                T("AboutDescription"),
                T("VersionLabel"),
                T("LicenseSection"),
                T("LicenseMissing"),
                T("OpenRepository"),
#if PREVIEW_DEBUG
                T("PreviewDebug"),
                IsPreviewDebugEnabled(),
                BuildPreviewDebugWindowLabels(),
#endif
                T("CheckForUpdates"),
                T("CheckForUpdatesChecking"),
                T("LatestVersion"),
                T("NewVersion"),
                T("UpdateCheckFailedShort"),
                T("DownloadLatestVersion"),
                T("AutoCheckUpdates"),
                BuildUpdateCheckIntervalItems(),
                _updateCheckIntervalPreference,
                selectedInterval => _updateCheckIntervalPreference = selectedInterval,
                T("OpenRepositoryFailed"),
                T("OpenReleaseFailed"),
                ReadLicenseDocuments())
            {
                Owner = this,
            };

            try
            {
                WindowFirstPaintGate.PrepareForFirstPaint(aboutWindow);
                WindowFirstPaintGate.CloakUntilStablePaint(aboutWindow);
                aboutWindow.ShowDialog();
            }
            finally
            {
                AboutButton.IsActive = false;
            }
        }

        private static string GetAppVersion()
        {
            string version = GetVersionNumber();
            return IsDevChannel() ? $"{version} dev" : version;
        }

        private ComboItem[] BuildUpdateCheckIntervalItems() => new[]
        {
            new ComboItem("daily", T("UpdateCheckIntervalDaily")),
            new ComboItem("weekly", T("UpdateCheckIntervalWeekly")),
            new ComboItem("monthly", T("UpdateCheckIntervalMonthly")),
            new ComboItem("startup", T("UpdateCheckIntervalStartup")),
            new ComboItem("never", T("UpdateCheckIntervalNever")),
        };

#if PREVIEW_DEBUG
        private PreviewDebugWindowLabels BuildPreviewDebugWindowLabels() => new(
            T("PreviewDebugTitle"),
            T("PreviewDebugUpdateCheck"),
            T("PreviewDebugCurrentVersion"),
            T("PreviewDebugSimulatedLatestVersion"),
            T("PreviewDebugLatestVersion"),
            T("PreviewDebugStatus"),
            T("PreviewDebugNotChecked"),
            T("PreviewDebugUpdateAvailable"),
            T("PreviewDebugUnknownVersion"),
            T("PreviewDebugCheckUpdates"),
            T("PreviewDebugOpenReleasePage"),
            T("OpenReleaseFailed"),
            T("Close"));
#endif

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

#if PREVIEW_DEBUG
        private static bool IsPreviewDebugEnabled()
        {
            string? channel = ReadFirstNonBlankAppFileLine("CHANNEL.txt");
            return string.Equals(channel, "dev", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel, "preview", StringComparison.OrdinalIgnoreCase);
        }
#endif

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
                "dirent - MIT License",
                root,
                Path.Combine("licenses", "dirent-MIT-LICENSE.txt"));
            AddLicenseDocument(
                documents,
                "stb image headers - MIT / Public Domain License",
                root,
                Path.Combine("licenses", "stb-LICENSE.txt"));
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
                "libwebp - WebM Project patent grant",
                root,
                Path.Combine("licenses", "libwebp-PATENTS.txt"),
                Path.Combine("third_party", "ncnn_src", "src", "libwebp", "PATENTS"));
            AddLicenseDocument(
                documents,
                "pybind11 - BSD 3-Clause License",
                root,
                Path.Combine("licenses", "pybind11-LICENSE.txt"),
                Path.Combine("third_party", "ncnn_src", "src", "ncnn", "python", "pybind11", "LICENSE"));
            AddLicenseDocument(
                documents,
                "Microsoft .NET runtime - license terms",
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
            AddLicenseDocument(
                documents,
                "Enigma Virtual Box - License Agreement",
                root,
                Path.Combine("licenses", "Enigma-Virtual-Box-LICENSE.txt"),
                Path.Combine("packaging", "windows", "EnigmaVirtualBox.LICENSE.txt"));

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
                    string text = PrepareLicenseDocumentText(relativePath, File.ReadAllText(path));
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

        internal static string PrepareLicenseDocumentText(string relativePath, string text)
        {
            return relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? ConvertMarkdownNoticeToPlainText(text)
                : text;
        }

        private static string ConvertMarkdownNoticeToPlainText(string markdown)
        {
            string[] lines = markdown
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

            var result = new List<string>(lines.Length);
            bool inCodeFence = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    inCodeFence = !inCodeFence;
                    continue;
                }

                result.Add(inCodeFence ? line : ConvertMarkdownLineToPlainText(line));
            }

            return string.Join(Environment.NewLine, result).Trim();
        }

        private static string ConvertMarkdownLineToPlainText(string line)
        {
            string trimmed = line.TrimStart();

            if (TryConvertMarkdownHeading(trimmed, out string heading))
                return heading;

            if (trimmed.StartsWith("> ", StringComparison.Ordinal))
                line = trimmed[2..];

            line = MarkdownLinkRegex.Replace(line, FormatMarkdownLink);
            line = MarkdownInlineCodeRegex.Replace(line, "$1");
            line = line.Replace("**", string.Empty, StringComparison.Ordinal);
            line = line.Replace("__", string.Empty, StringComparison.Ordinal);

            return line;
        }

        private static bool TryConvertMarkdownHeading(string line, out string heading)
        {
            int depth = 0;
            while (depth < line.Length && depth < 6 && line[depth] == '#')
            {
                depth++;
            }

            if (depth == 0 || depth >= line.Length || line[depth] != ' ')
            {
                heading = string.Empty;
                return false;
            }

            heading = line[(depth + 1)..].Trim();
            return heading.Length > 0;
        }

        private static string FormatMarkdownLink(Match match)
        {
            string label = match.Groups[1].Value.Trim().Trim('`');
            string target = match.Groups[2].Value.Trim();

            if (string.IsNullOrEmpty(label))
                return target;

            if (string.Equals(label, target, StringComparison.OrdinalIgnoreCase))
                return target;

            if (target.StartsWith("#", StringComparison.Ordinal))
                return label;

            return $"{label} ({target})";
        }
    }
}
