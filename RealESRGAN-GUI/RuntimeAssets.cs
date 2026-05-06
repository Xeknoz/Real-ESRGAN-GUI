using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RealESRGAN_GUI
{
    /// <summary>
    /// Extracts the embedded Real-ESRGAN backend (exe, OpenMP DLLs, models, sample image)
    /// from the assembly to a stable per-user runtime directory on first launch.
    /// </summary>
    internal static class RuntimeAssets
    {
        private const string ResourcePrefix = "res/";
        private const string MarkerFileName = ".extracted";

        public static string EnsureExtracted()
        {
            var asm = typeof(RuntimeAssets).Assembly;
            string version = asm.GetName().Version?.ToString() ?? "0.0.0.0";

            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Real-ESRGAN GUI",
                $"runtime-{version}");

            string fingerprint = ComputeFingerprint(asm, version);
            string markerFile = Path.Combine(root, MarkerFileName);

            if (File.Exists(markerFile))
            {
                try
                {
                    if (string.Equals(File.ReadAllText(markerFile).Trim(), fingerprint, StringComparison.Ordinal))
                        return root;
                }
                catch { /* fall through to re-extract */ }
            }

            Extract(asm, root);
            File.WriteAllText(markerFile, fingerprint);
            return root;
        }

        private static void Extract(Assembly asm, string root)
        {
            Directory.CreateDirectory(root);
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
            {
                string relative = name.Substring(ResourcePrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                string outPath = Path.Combine(root, relative);
                string? outDir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

                using var src = asm.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded resource missing: {name}");
                using var dst = File.Create(outPath);
                src.CopyTo(dst);
            }
        }

        // Cheap content-aware cache key: (version, resource count, total bytes).
        // Skips re-extraction across launches as long as the embedded payload is unchanged.
        private static string ComputeFingerprint(Assembly asm, string version)
        {
            long totalSize = 0;
            int count = 0;
            foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
            {
                using var s = asm.GetManifestResourceStream(name);
                if (s != null) { totalSize += s.Length; count++; }
            }
            return $"version={version} count={count} size={totalSize}";
        }
    }
}
