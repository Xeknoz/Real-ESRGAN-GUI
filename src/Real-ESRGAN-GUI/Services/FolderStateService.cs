using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RealESRGAN_GUI.Services
{
    internal sealed record RunPreflightResult(bool InputExists, bool OutputExists, bool InputReadable, bool HasInputImages);

    internal static class FolderStateService
    {
        private static readonly HashSet<string> SupportedInputExts = new(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };

        private static readonly HashSet<string> PngOutputExts = new(StringComparer.OrdinalIgnoreCase)
            { ".png" };

        private static readonly HashSet<string> JpgOutputExts = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg" };

        private static readonly HashSet<string> WebpOutputExts = new(StringComparer.OrdinalIgnoreCase)
            { ".webp" };

        public static Task<RunPreflightResult> PrepareRunFoldersAsync(string inputDir, string outputDir, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                try { Directory.CreateDirectory(inputDir); } catch { /* surfaced below */ }
                try { Directory.CreateDirectory(outputDir); } catch { /* surfaced below */ }

                bool inputExists = Directory.Exists(inputDir);
                bool outputExists = Directory.Exists(outputDir);
                bool inputReadable = true;
                bool hasInputImages = false;

                if (inputExists)
                {
                    try
                    {
                        hasInputImages = HasSupportedInputs(inputDir);
                    }
                    catch
                    {
                        inputReadable = false;
                    }
                }

                token.ThrowIfCancellationRequested();
                return new RunPreflightResult(inputExists, outputExists, inputReadable, hasInputImages);
            }, token);
        }

        public static Task<bool> HasSupportedInputsAsync(string dir, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                bool result = HasSupportedInputs(dir);
                token.ThrowIfCancellationRequested();
                return result;
            }, token);
        }

        public static int CountInputFiles(string dir)
        {
            return CountFiles(dir, SupportedInputExts);
        }

        public static int CountOutputFiles(string dir, string format)
        {
            return CountFiles(dir, OutputExtensionsFor(format));
        }

        private static bool HasSupportedInputs(string dir)
        {
            if (!Directory.Exists(dir)) return false;
            return Directory.EnumerateFiles(dir).Any(IsSupportedInputFile);
        }

        private static bool IsSupportedInputFile(string path)
        {
            return SupportedInputExts.Contains(Path.GetExtension(path));
        }

        private static int CountFiles(string dir, HashSet<string> extensions)
        {
            try
            {
                return Directory.EnumerateFiles(dir)
                    .Count(path => extensions.Contains(Path.GetExtension(path)));
            }
            catch
            {
                return -1;
            }
        }

        private static HashSet<string> OutputExtensionsFor(string format)
        {
            if (string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase))
            {
                return JpgOutputExts;
            }

            if (string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase))
            {
                return WebpOutputExts;
            }

            return PngOutputExts;
        }
    }
}
