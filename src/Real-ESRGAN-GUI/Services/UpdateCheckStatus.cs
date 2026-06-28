namespace RealESRGAN_GUI.Services
{
    public enum UpdateCheckStatusKind
    {
        UpdateAvailable,
        UpToDate,
        Failed,
        Canceled,
    }

    public sealed record UpdateCheckStatus(
        UpdateCheckStatusKind Kind,
        string? LatestVersion,
        string? ReleaseUrl,
        string? ErrorMessage)
    {
        public static UpdateCheckStatus CreateUpdateAvailable(string latestVersion, string releaseUrl)
        {
            return new(
                UpdateCheckStatusKind.UpdateAvailable,
                latestVersion.Trim(),
                releaseUrl,
                null);
        }

        public static UpdateCheckStatus FromLatestVersion(
            string currentVersion,
            string latestVersion,
            string releaseUrl)
        {
            string normalizedLatestVersion = latestVersion.Trim();
            bool updateAvailable = UpdateCheckService.IsLatestReleaseNewer(
                currentVersion,
                normalizedLatestVersion);

            return new(
                updateAvailable ? UpdateCheckStatusKind.UpdateAvailable : UpdateCheckStatusKind.UpToDate,
                normalizedLatestVersion,
                updateAvailable ? releaseUrl : null,
                null);
        }

        public static UpdateCheckStatus FromResult(UpdateCheckResult result)
        {
            if (result.IsCancelled)
                return new(UpdateCheckStatusKind.Canceled, null, null, null);

            if (!result.Succeeded)
            {
                return new(
                    UpdateCheckStatusKind.Failed,
                    result.LatestVersion,
                    result.ReleaseUrl,
                    result.ErrorMessage);
            }

            return new(
                result.UpdateAvailable
                    ? UpdateCheckStatusKind.UpdateAvailable
                    : UpdateCheckStatusKind.UpToDate,
                result.LatestVersion,
                result.ReleaseUrl,
                null);
        }
    }
}
