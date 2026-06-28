using System;
using RealESRGAN_GUI.Services;

var tests = new (string Name, Action Body)[]
{
    ("release version comparison handles tag prefixes and dev suffixes", ReleaseVersionComparisonHandlesTagsAndSuffixes),
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
