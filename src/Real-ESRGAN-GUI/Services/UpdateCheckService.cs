using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RealESRGAN_GUI.Services
{
    public static class UpdateCheckService
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/Xeknoz/Real-ESRGAN-GUI/releases/latest";
        public const string ReleasesPageUrl = "https://github.com/Xeknoz/Real-ESRGAN-GUI/releases";

        private static readonly Regex VersionTextRegex = new(
            @"\d+(?:\.\d+){0,3}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly HttpClient HttpClient = CreateHttpClient();

        public static async Task<UpdateCheckResult> CheckLatestReleaseAsync(
            string currentVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                request.Headers.UserAgent.ParseAdd("Real-ESRGAN-GUI");

                using HttpResponseMessage response = await HttpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string error = string.Format(
                        CultureInfo.InvariantCulture,
                        "GitHub returned HTTP {0}.",
                        (int)response.StatusCode);
                    return UpdateCheckResult.Failed(error);
                }

                await using var stream = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                using JsonDocument document = await JsonDocument
                    .ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (!TryReadString(document.RootElement, "tag_name", out string latestVersion))
                    return UpdateCheckResult.Failed("The latest release did not include a version tag.");

                string releaseUrl = TryReadString(document.RootElement, "html_url", out string htmlUrl)
                    ? htmlUrl
                    : ReleasesPageUrl;

                return UpdateCheckResult.Success(
                    IsLatestReleaseNewer(currentVersion, latestVersion),
                    latestVersion,
                    releaseUrl);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return UpdateCheckResult.Canceled();
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                return UpdateCheckResult.Failed(ex.Message);
            }
        }

        public static bool IsLatestReleaseNewer(string currentVersionText, string latestVersionText)
        {
            return TryParseVersionText(currentVersionText, out Version currentVersion) &&
                TryParseVersionText(latestVersionText, out Version latestVersion) &&
                latestVersion.CompareTo(currentVersion) > 0;
        }

        private static bool TryParseVersionText(string? versionText, out Version version)
        {
            version = new Version(0, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(versionText))
                return false;

            Match match = VersionTextRegex.Match(versionText);
            if (!match.Success)
                return false;

            string[] parts = match.Value.Split('.');
            int major = ParseVersionPart(parts, 0);
            int minor = ParseVersionPart(parts, 1);
            int build = ParseVersionPart(parts, 2);
            int revision = ParseVersionPart(parts, 3);
            version = new Version(major, minor, build, revision);
            return true;
        }

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12),
            };
        }

        private static int ParseVersionPart(string[] parts, int index)
        {
            return index < parts.Length &&
                int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                    ? value
                    : 0;
        }

        private static bool TryReadString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out JsonElement property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
    }

    public sealed record UpdateCheckResult(
        bool Succeeded,
        bool IsCancelled,
        bool UpdateAvailable,
        string? LatestVersion,
        string? ReleaseUrl,
        string? ErrorMessage)
    {
        public static UpdateCheckResult Success(bool updateAvailable, string latestVersion, string releaseUrl)
            => new(true, false, updateAvailable, latestVersion, releaseUrl, null);

        public static UpdateCheckResult Failed(string errorMessage)
            => new(false, false, false, null, null, errorMessage);

        public static UpdateCheckResult Canceled()
            => new(false, true, false, null, null, null);
    }
}
