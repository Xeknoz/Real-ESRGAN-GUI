using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RealESRGAN_GUI.Services
{
    internal sealed class BackendProcessRunner
    {
        private readonly string _executablePath;
        private Process? _process;

        public BackendProcessRunner(string executablePath)
        {
            _executablePath = executablePath;
        }

        public async Task<int> RunAsync(
            string arguments,
            Action<string> onLogLine,
            Action<BatchProgressSnapshot> onProgress,
            CancellationToken token)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(_executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                if (BackendProgressParser.TryParse(e.Data, out BatchProgressSnapshot? snapshot) && snapshot is not null)
                    onProgress(snapshot);

                onLogLine(e.Data);
            };
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    onLogLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;

            try
            {
                await process.WaitForExitAsync(token);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                Stop();
                try { await process.WaitForExitAsync(CancellationToken.None); } catch { /* ignore */ }
                throw;
            }
            finally
            {
                try { process.CancelErrorRead(); } catch { /* ignore */ }
                try { process.CancelOutputRead(); } catch { /* ignore */ }
                _process = null;
                process.Dispose();
            }
        }

        public void Stop()
        {
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Stop is best-effort; the UI reports the cancellation state separately.
            }
        }
    }
}
