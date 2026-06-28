namespace RealESRGAN_GUI.Services
{
    public enum PreviewUpdateCheckStatusKind
    {
        UpdateAvailable,
        UpToDate,
        Failed,
        Canceled,
    }

    public sealed record PreviewUpdateCheckStatus(
        PreviewUpdateCheckStatusKind Kind,
        string? LatestVersion,
        string? ReleaseUrl,
        string? ErrorMessage)
    {
        public const string DefaultForcedPreviewLatestVersion = "v999.999.999-preview";

        public static PreviewUpdateCheckStatus CreateForcedUpdateAvailable(string? latestVersion = null)
        {
            string forcedLatestVersion = string.IsNullOrWhiteSpace(latestVersion)
                ? DefaultForcedPreviewLatestVersion
                : latestVersion.Trim();

            return new(
                PreviewUpdateCheckStatusKind.UpdateAvailable,
                forcedLatestVersion,
                UpdateCheckService.ReleasesPageUrl,
                null);
        }

        public static PreviewUpdateCheckStatus FromResult(UpdateCheckResult result)
        {
            if (result.IsCancelled)
                return new(PreviewUpdateCheckStatusKind.Canceled, null, null, null);

            if (!result.Succeeded)
            {
                return new(
                    PreviewUpdateCheckStatusKind.Failed,
                    result.LatestVersion,
                    result.ReleaseUrl,
                    result.ErrorMessage);
            }

            return new(
                result.UpdateAvailable
                    ? PreviewUpdateCheckStatusKind.UpdateAvailable
                    : PreviewUpdateCheckStatusKind.UpToDate,
                result.LatestVersion,
                result.ReleaseUrl,
                null);
        }
    }
}
