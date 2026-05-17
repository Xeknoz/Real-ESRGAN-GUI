using System;
using System.Collections.Generic;

namespace RealESRGAN_GUI.Services
{
    internal sealed record BackendCommandOptions(
        string InputPath,
        string OutputPath,
        string Model,
        string Scale,
        string Format,
        string Threads,
        string Gpu,
        bool EnhancedQuality);

    internal static class BackendCommandBuilder
    {
        public static string Build(BackendCommandOptions options)
        {
            static string Q(string value) => $"\"{value}\"";

            var parts = new List<string>
            {
                "-i", Q(options.InputPath),
                "-o", Q(options.OutputPath),
                "-n", options.Model,
                "-f", options.Format,
            };

            if (!string.IsNullOrEmpty(options.Scale))
            {
                parts.Add("-s");
                parts.Add(options.Scale);
            }

            if (!string.Equals(options.Threads, "0", StringComparison.Ordinal))
            {
                parts.Add("-t");
                parts.Add(options.Threads);
            }

            if (!string.IsNullOrEmpty(options.Gpu))
            {
                parts.Add("-g");
                parts.Add(options.Gpu);
            }

            if (options.EnhancedQuality)
            {
                parts.Add("-x");
            }

            return string.Join(" ", parts);
        }
    }
}
