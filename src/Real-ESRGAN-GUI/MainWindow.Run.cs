using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using RealESRGAN_GUI.Services;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private void OnAdvancedToggleClick(object sender, RoutedEventArgs e)
        {
            AdvancedPanel.Visibility = AdvancedToggle.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateAdvancedToggleText();
        }

        private void UpdateAdvancedToggleText()
        {
            AdvancedToggleLabelText.Text = T("Advanced");
            AdvancedToggleIndicator.RenderTransformOrigin = new Point(0.5, 0.5);
            AdvancedToggleIndicator.RenderTransform = new RotateTransform(
                AdvancedToggle.IsChecked == true ? 180 : 0);
        }

        private void OnLogToggleClick(object sender, RoutedEventArgs e)
        {
            LogPanel.Visibility = LogToggle.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateLogToggleText();
        }

        private void UpdateLogToggleText()
        {
            LogToggleLabelText.Text = T("Log");
            LogToggleIndicator.RenderTransformOrigin = new Point(0.5, 0.5);
            LogToggleIndicator.RenderTransform = new RotateTransform(
                LogToggle.IsChecked == true ? 180 : 0);
        }

        private void AppendLog(string line)
        {
            if (_logBuilder.Length > 50000)
                _logBuilder.Remove(0, _logBuilder.Length - 40000);
            _logBuilder.AppendLine(line);
            LogText.Text = _logBuilder.ToString();
            LogScrollViewer.ScrollToEnd();
        }

        private async void OnStartClick(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            if (!File.Exists(_exePath))
            {
                MessageBox.Show(this,
                    string.Format(CultureInfo.CurrentCulture, T("MissingExe"), _exePath),
                    "Real-ESRGAN GUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _totalFiles = 0;
            _completedFiles = 0;
            _currentFileIndex = -1;
            _currentFilePercent = 0;
            _batchPercent = 0;

            SetUIBusy(true);
            _cts = new();
            // Paint the busy state before slower folder checks or backend launch work can begin.
            await Dispatcher.Yield(DispatcherPriority.Background);

            bool stopped = false;
            int failed = 0;
            bool backendStarted = false;
            bool finalStatusSet = false;

            try
            {
                SetStatus("StatusStarting");
                CompletedProgressBar.IsIndeterminate = false;

                RunPreflightResult preflight = await FolderStateService.PrepareRunFoldersAsync(_inputDir, _outputDir, _cts.Token);
                ConfigureFolderWatchers();

                if (!preflight.InputExists || !preflight.InputReadable)
                {
                    MessageBox.Show(this, T("InputAccessError"), "Real-ESRGAN GUI",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!preflight.OutputExists)
                {
                    MessageBox.Show(this, T("OutputAccessError"), "Real-ESRGAN GUI",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!preflight.HasInputImages)
                {
                    MessageBox.Show(this, T("NoImagesFound"), "Real-ESRGAN GUI",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                backendStarted = true;
                int exitCode = await _backendRunner.RunAsync(
                    BuildArgs(_inputDir, _outputDir),
                    line => Dispatcher.InvokeAsync(() => AppendLog(line)),
                    progress => Dispatcher.InvokeAsync(() => ApplyBackendProgress(progress)),
                    _cts.Token);

                if (_cts.IsCancellationRequested)
                {
                    stopped = true;
                }
                else if (exitCode != 0)
                {
                    failed = 1;
                }

                UpdateProgressBars();
                SetProgressPercent(_batchPercent);

                if (stopped)
                {
                    FinalizeStoppedRun();
                    finalStatusSet = true;
                }
                else if (failed > 0)
                {
                    SetStatus("StatusPartial", _completedFiles, _totalFiles);
                    SetProgressText("ProgressIncomplete");
                    finalStatusSet = true;
                }
                else if (_completedFiles < _totalFiles)
                {
                    SetStatus("StatusPartial", _completedFiles, _totalFiles);
                    SetProgressText("ProgressIncomplete");
                    finalStatusSet = true;
                }
                else
                {
                    SetStatus("StatusDone", _completedFiles);
                    UpdateProgressBars();
                    SetProgressPercent(100);
                    finalStatusSet = true;
                }
            }
            catch (OperationCanceledException)
            {
                FinalizeStoppedRun();
                finalStatusSet = true;
            }
            catch (Exception ex)
            {
                SetStatus("StatusError", ex.Message);
                SetProgressText("ProgressError");
                finalStatusSet = true;
            }
            finally
            {
                SetUIBusy(false);
                if (!backendStarted && !finalStatusSet)
                {
                    SetStatus("StatusReady");
                    SetProgressPercent(0);
                }
                ScheduleFolderSummaryRefresh();
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _cts?.Cancel();
                _backendRunner.Stop();
            }
            catch { /* ignore */ }
        }

        private string BuildArgs(string input, string output)
        {
            return BackendCommandBuilder.Build(new BackendCommandOptions(
                input,
                output,
                ((ComboItem)ModelCombo.SelectedItem).Tag,
                ((ComboItem)ScaleCombo.SelectedItem).Tag,
                ((ComboItem)FormatCombo.SelectedItem).Tag,
                ((ComboItem)ThreadsCombo.SelectedItem).Tag,
                ((ComboItem)GpuCombo.SelectedItem).Tag,
                TtaCheck.IsChecked == true));
        }

        private void SetUIBusy(bool busy)
        {
            _busy = busy;
            StartButton.IsEnabled = !busy;
            StopButton.IsEnabled  =  busy;
            BrowseInputButton.IsEnabled = !busy;
            BrowseOutputButton.IsEnabled = !busy;
            OpenInputButton.IsEnabled = !busy;
            OpenOutputButton.IsEnabled = !busy;
            ModelCombo.IsEnabled = !busy;
            ScaleCombo.IsEnabled = !busy;
            FormatCombo.IsEnabled = !busy;
            ThreadsCombo.IsEnabled = !busy;
            GpuCombo.IsEnabled = !busy;
            TtaCheck.IsEnabled = !busy;
            ProgressTrack.Visibility = Visibility.Visible;
            if (busy)
            {
                LogToggle.Visibility = Visibility.Visible;
                _logBuilder.Clear();
                LogText.Text = "";
                SetStatus("StatusStarting");
                CompletedProgressBar.IsIndeterminate = false;
                CurrentFileProgressBar.IsIndeterminate = false;
                UpdateProgressBars();
                SetProgressPercent(0);
            }
        }

        private void UpdateProgressBars()
        {
            bool multipleFiles = _totalFiles > 1;

            CurrentFileProgressBar.Visibility = multipleFiles ? Visibility.Visible : Visibility.Collapsed;
            CurrentFileProgressBar.Value = Math.Clamp(_currentFilePercent, 0, 100);
            CompletedProgressBar.Value = Math.Clamp(_batchPercent, 0, 100);
        }

        private void FinalizeStoppedRun()
        {
            _currentFileIndex = -1;
            _currentFilePercent = 0;
            UpdateProgressBars();
            SetStatus("StatusStoppedFinal", _completedFiles, _totalFiles);
            SetProgressText("ProgressStopped");
        }

        private void ApplyBackendProgress(int totalFiles, int completedFiles, double percent, int currentFileIndex, double currentFilePercent)
        {
            _totalFiles = Math.Max(0, totalFiles);
            _completedFiles = Math.Clamp(completedFiles, 0, _totalFiles);
            _batchPercent = Math.Clamp(percent, 0, 100);
            _currentFileIndex = currentFileIndex;
            _currentFilePercent = Math.Clamp(currentFilePercent, 0, 100);
            UpdateProgressBars();
            SetStatus("StatusProcessingFiles", _completedFiles, _totalFiles - _completedFiles);
            SetProgressPercent(_batchPercent);
        }

        private void ApplyBackendProgress(BatchProgressSnapshot snapshot)
        {
            ApplyBackendProgress(
                snapshot.TotalFiles,
                snapshot.CompletedFiles,
                snapshot.Percent,
                snapshot.CurrentFileIndex,
                snapshot.CurrentFilePercent);
        }

        private void SetStatus(string key, params object[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            RenderStatusText();
        }

        private void RenderStatusText()
        {
            StatusText.Text = string.Format(CultureInfo.CurrentCulture, T(_statusKey), _statusArgs);
        }

        private void SetProgressText(string key)
        {
            _progressTextKey = key;
            _progressPercent = null;
            RenderProgressText();
        }

        private void SetProgressPercent(double percent)
        {
            _progressTextKey = null;
            _progressPercent = percent;
            RenderProgressText();
        }

        private void RenderProgressText()
        {
            if (_progressTextKey is not null)
            {
                ProgressPercentText.Text = T(_progressTextKey);
                return;
            }

            if (_totalFiles > 1 && _currentFileIndex >= 0)
            {
                ProgressPercentText.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    T("CurrentFileProgress"),
                    _currentFileIndex + 1,
                    _totalFiles,
                    _currentFilePercent);
                return;
            }

            if (_progressPercent.HasValue)
            {
                string text = string.Format(CultureInfo.InvariantCulture, "{0:0}%", _progressPercent.Value);
                ProgressPercentText.Text = text;
            }
            else
            {
                ProgressPercentText.Text = T("ProgressZero");
            }
        }
    }
}
