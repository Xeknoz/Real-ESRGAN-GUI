using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RealESRGAN_GUI.Services
{
    internal enum SampleCopyFailureKind
    {
        None,
        MissingSample,
        CopyFailed,
    }

    internal sealed record RunPreflightResult(bool InputExists, bool OutputExists, bool InputReadable, bool HasInputImages);

    internal sealed record SampleCopyResult(bool Success, SampleCopyFailureKind FailureKind, string ErrorMessage)
    {
        public static SampleCopyResult Ok { get; } = new(true, SampleCopyFailureKind.None, string.Empty);
    }

    internal static class FolderStateService
    {
        private static readonly HashSet<string> SupportedExts = new(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };

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

        public static int CountSupportedFiles(string dir)
        {
            try
            {
                return Directory.EnumerateFiles(dir).Count(IsSupportedFile);
            }
            catch
            {
                return -1;
            }
        }

        public static SampleCopyResult CopySample(string appDir, string targetDir)
        {
            string src = Path.Combine(appDir, "input.jpg");
            if (!File.Exists(src))
                return new SampleCopyResult(false, SampleCopyFailureKind.MissingSample, string.Empty);

            try
            {
                File.Copy(src, Path.Combine(targetDir, "input.jpg"), overwrite: true);
                return SampleCopyResult.Ok;
            }
            catch (Exception ex)
            {
                return new SampleCopyResult(false, SampleCopyFailureKind.CopyFailed, ex.Message);
            }
        }

        private static bool HasSupportedInputs(string dir)
        {
            if (!Directory.Exists(dir)) return false;
            return Directory.EnumerateFiles(dir).Any(IsSupportedFile);
        }

        private static bool IsSupportedFile(string path)
        {
            return SupportedExts.Contains(Path.GetExtension(path));
        }
    }
}
