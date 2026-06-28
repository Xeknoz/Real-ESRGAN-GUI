using System;
using System.IO;
using System.Linq;

namespace RealESRGAN_GUI.Services
{
    public static class PackageKindService
    {
        public const string PackageKindFileName = "PACKAGE_KIND.txt";
        public const string InstalledPackageKind = "installed";
        public const string PortablePackageKind = "portable";

        public static bool IsAutoCheckUpdatesAvailable(string? packageKind)
        {
            return string.Equals(
                packageKind?.Trim(),
                InstalledPackageKind,
                StringComparison.OrdinalIgnoreCase);
        }

        public static string? ReadPackageKind(string appBaseDirectory)
        {
            string filePath = Path.Combine(appBaseDirectory, PackageKindFileName);
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
