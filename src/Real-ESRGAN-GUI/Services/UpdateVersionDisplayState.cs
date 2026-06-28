namespace RealESRGAN_GUI.Services
{
    public enum PreviousVersionPlacement
    {
        None,
        BelowPrimary,
    }

    public sealed record UpdateVersionDisplayState(
        string PrimaryVersion,
        string? PreviousVersion,
        PreviousVersionPlacement PreviousVersionPlacement)
    {
        public bool HasPreviousVersion => !string.IsNullOrWhiteSpace(PreviousVersion);

        public static UpdateVersionDisplayState Current(string currentVersion)
        {
            return new(currentVersion, null, PreviousVersionPlacement.None);
        }

        public static UpdateVersionDisplayState FromStatus(
            string currentVersion,
            UpdateCheckStatus status)
        {
            if (status.Kind == UpdateCheckStatusKind.UpdateAvailable &&
                !string.IsNullOrWhiteSpace(status.LatestVersion))
            {
                return new(status.LatestVersion, currentVersion, PreviousVersionPlacement.BelowPrimary);
            }

            return Current(currentVersion);
        }
    }
}
