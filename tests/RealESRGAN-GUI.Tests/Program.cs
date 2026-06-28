using System;
using System.Collections.Generic;
using RealESRGAN_GUI.Services;

var tests = new (string Name, Action Body)[]
{
    ("release version comparison handles tag prefixes and dev suffixes", ReleaseVersionComparisonHandlesTagsAndSuffixes),
    ("preview debug update status distinguishes update, current, failed, and canceled results", PreviewDebugUpdateStatusDistinguishesResults),
    ("preview debug can force an update available result without live checking", PreviewDebugCanForceUpdateAvailableResult),
    ("preview debug can force a custom update version", PreviewDebugCanForceCustomUpdateVersion),
    ("update available version display replaces current version and keeps previous version", UpdateAvailableVersionDisplayReplacesCurrentVersion),
    ("later update available display replaces earlier detected version", LaterUpdateAvailableDisplayReplacesEarlierDetectedVersion),
};

int failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine(ex.Message);
    }
}

if (failures > 0)
{
    Environment.Exit(1);
}

static void ReleaseVersionComparisonHandlesTagsAndSuffixes()
{
    AssertTrue(UpdateCheckService.IsLatestReleaseNewer("1.0.2.120", "v1.0.3"));
    AssertFalse(UpdateCheckService.IsLatestReleaseNewer("1.0.2.120", "v1.0.2"));
    AssertFalse(UpdateCheckService.IsLatestReleaseNewer("1.0.10", "v1.0.2"));
    AssertFalse(UpdateCheckService.IsLatestReleaseNewer("1.0.2.120 dev", "v1.0.2.120"));
    AssertTrue(UpdateCheckService.IsLatestReleaseNewer("1.0.2.120 dev", "v1.0.3"));
}

static void PreviewDebugUpdateStatusDistinguishesResults()
{
    PreviewUpdateCheckStatus update = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Success(true, "v1.0.3", "https://example.test/release"));
    AssertEqual(PreviewUpdateCheckStatusKind.UpdateAvailable, update.Kind);
    AssertEqual("v1.0.3", update.LatestVersion);
    AssertEqual("https://example.test/release", update.ReleaseUrl);

    PreviewUpdateCheckStatus current = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Success(false, "v1.0.2", "https://example.test/current"));
    AssertEqual(PreviewUpdateCheckStatusKind.UpToDate, current.Kind);
    AssertEqual("v1.0.2", current.LatestVersion);

    PreviewUpdateCheckStatus failed = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Failed("network unavailable"));
    AssertEqual(PreviewUpdateCheckStatusKind.Failed, failed.Kind);
    AssertEqual("network unavailable", failed.ErrorMessage);

    PreviewUpdateCheckStatus canceled = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Canceled());
    AssertEqual(PreviewUpdateCheckStatusKind.Canceled, canceled.Kind);
}

static void PreviewDebugCanForceUpdateAvailableResult()
{
    PreviewUpdateCheckStatus forced = PreviewUpdateCheckStatus.CreateForcedUpdateAvailable();

    AssertEqual(PreviewUpdateCheckStatusKind.UpdateAvailable, forced.Kind);
    AssertEqual("v999.999.999-preview", forced.LatestVersion);
    AssertEqual(UpdateCheckService.ReleasesPageUrl, forced.ReleaseUrl);
}

static void PreviewDebugCanForceCustomUpdateVersion()
{
    PreviewUpdateCheckStatus forced = PreviewUpdateCheckStatus.CreateForcedUpdateAvailable("  v2.5.0-preview  ");

    AssertEqual(PreviewUpdateCheckStatusKind.UpdateAvailable, forced.Kind);
    AssertEqual("v2.5.0-preview", forced.LatestVersion);
    AssertEqual(UpdateCheckService.ReleasesPageUrl, forced.ReleaseUrl);
}

static void UpdateAvailableVersionDisplayReplacesCurrentVersion()
{
    PreviewUpdateCheckStatus update = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Success(true, "v1.0.3", UpdateCheckService.ReleasesPageUrl));

    UpdateVersionDisplayState updateDisplay = UpdateVersionDisplayState.FromStatus("1.0.2 dev", update);
    AssertEqual("v1.0.3", updateDisplay.PrimaryVersion);
    AssertEqual("1.0.2 dev", updateDisplay.PreviousVersion);
    AssertEqual(PreviousVersionPlacement.BelowPrimary, updateDisplay.PreviousVersionPlacement);
    AssertTrue(updateDisplay.HasPreviousVersion);

    PreviewUpdateCheckStatus current = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Success(false, "v1.0.2", UpdateCheckService.ReleasesPageUrl));

    UpdateVersionDisplayState currentDisplay = UpdateVersionDisplayState.FromStatus("1.0.2 dev", current);
    AssertEqual("1.0.2 dev", currentDisplay.PrimaryVersion);
    AssertEqual(null, currentDisplay.PreviousVersion);
    AssertEqual(PreviousVersionPlacement.None, currentDisplay.PreviousVersionPlacement);
    AssertFalse(currentDisplay.HasPreviousVersion);
}

static void LaterUpdateAvailableDisplayReplacesEarlierDetectedVersion()
{
    PreviewUpdateCheckStatus firstUpdate = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Success(true, "v1.0.3", UpdateCheckService.ReleasesPageUrl));
    PreviewUpdateCheckStatus laterUpdate = PreviewUpdateCheckStatus.FromResult(
        UpdateCheckResult.Success(true, "v1.0.4", UpdateCheckService.ReleasesPageUrl));

    UpdateVersionDisplayState firstDisplay = UpdateVersionDisplayState.FromStatus("1.0.2 dev", firstUpdate);
    UpdateVersionDisplayState laterDisplay = UpdateVersionDisplayState.FromStatus("1.0.2 dev", laterUpdate);

    AssertEqual("v1.0.3", firstDisplay.PrimaryVersion);
    AssertEqual("v1.0.4", laterDisplay.PrimaryVersion);
    AssertEqual("1.0.2 dev", laterDisplay.PreviousVersion);
    AssertEqual(PreviousVersionPlacement.BelowPrimary, laterDisplay.PreviousVersionPlacement);
}

static void AssertTrue(bool actual)
{
    if (!actual)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void AssertFalse(bool actual)
{
    if (actual)
    {
        throw new InvalidOperationException("Expected false.");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
