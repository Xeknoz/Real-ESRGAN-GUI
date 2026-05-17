using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RealESRGAN_GUI.Services
{
    internal sealed record BatchProgressSnapshot(
        int TotalFiles,
        int CompletedFiles,
        double Percent,
        int CurrentFileIndex,
        double CurrentFilePercent);

    internal static class BackendProgressParser
    {
        private static readonly Regex BatchProgressRegex = new(
            @"^@batch\s+total=(\d+)\s+completed=(\d+)\s+percent=(\d+(?:\.\d+)?)\s+current=(-?\d+)\s+current_percent=(\d+(?:\.\d+)?)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryParse(string line, out BatchProgressSnapshot? snapshot)
        {
            snapshot = null;
            Match match = BatchProgressRegex.Match(line);
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int totalFiles) ||
                !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int completedFiles) ||
                !double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent) ||
                !int.TryParse(match.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int currentFileIndex) ||
                !double.TryParse(match.Groups[5].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double currentFilePercent))
            {
                return false;
            }

            snapshot = new BatchProgressSnapshot(totalFiles, completedFiles, percent, currentFileIndex, currentFilePercent);
            return true;
        }
    }
}
