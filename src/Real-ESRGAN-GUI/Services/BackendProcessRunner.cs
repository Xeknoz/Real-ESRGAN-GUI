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
        private readonly object _processGate = new();
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

            try
            {
                await StartProcessAsync(process, token).ConfigureAwait(false);
                await process.WaitForExitAsync(token).ConfigureAwait(false);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                Stop();
                try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { /* ignore */ }
                throw;
            }
            finally
            {
                try { process.CancelErrorRead(); } catch { /* ignore */ }
                try { process.CancelOutputRead(); } catch { /* ignore */ }
                ClearCurrentProcess(process);
                process.Dispose();
            }
        }

        private async Task StartProcessAsync(Process process, CancellationToken token)
        {
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                if (!process.Start())
                    throw new InvalidOperationException("Failed to start backend process.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                SetCurrentProcess(process);

                if (token.IsCancellationRequested)
                {
                    Stop();
                    token.ThrowIfCancellationRequested();
                }
            }, token).ConfigureAwait(false);
        }

        private void SetCurrentProcess(Process process)
        {
            lock (_processGate)
            {
                _process = process;
            }
        }

        private void ClearCurrentProcess(Process process)
        {
            lock (_processGate)
            {
                if (ReferenceEquals(_process, process))
                    _process = null;
            }
        }

        public void Stop()
        {
            Process? process;
            lock (_processGate)
            {
                process = _process;
            }

            try
            {
                if (process is { HasExited: false })
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Stop is best-effort; the UI reports the cancellation state separately.
            }
        }
    }
}
