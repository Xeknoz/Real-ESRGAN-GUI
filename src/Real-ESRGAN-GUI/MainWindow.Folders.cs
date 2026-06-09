using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using RealESRGAN_GUI.Services;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private string? PickFolder(string initial)
        {
            var dlg = new OpenFolderDialog
            {
                Title = T("PickFolderTitle"),
                Multiselect = false,
                InitialDirectory = Directory.Exists(initial) ? initial : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            };
            return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
        }

        private void OnBrowseInputClick(object sender, RoutedEventArgs e)
        {
            var picked = PickFolder(_inputDir);
            if (picked is null) return;
            _inputDir = picked;
            InputPathBox.Text = picked;
            ConfigureFolderWatchers();
            RefreshFolderSummaries();
        }

        private void OnBrowseOutputClick(object sender, RoutedEventArgs e)
        {
            var picked = PickFolder(_outputDir);
            if (picked is null) return;
            _outputDir = picked;
            OutputPathBox.Text = picked;
            ConfigureFolderWatchers();
            RefreshFolderSummaries();
        }

        private void OnOpenInputClick(object sender, RoutedEventArgs e)  => OpenInExplorer(_inputDir);
        private void OnOpenOutputClick(object sender, RoutedEventArgs e) => OpenInExplorer(_outputDir);

        private void OpenInExplorer(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }

        private void RefreshFolderSummaries()
        {
            InputSummaryText.Text = DescribeInputFolder(_inputDir);
            OutputSummaryText.Text = DescribeOutputFolder(_outputDir);
        }

        private void ConfigureFolderWatchers()
        {
            ReplaceWatcher(ref _inputWatcher, _inputDir);
            ReplaceWatcher(ref _outputWatcher, _outputDir);
        }

        private void ReplaceWatcher(ref FileSystemWatcher? watcher, string path)
        {
            string? currentPath = watcher?.Path;
            bool shouldWatch = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

            if (shouldWatch &&
                watcher is not null &&
                string.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            DisposeWatcher(ref watcher);
            if (!shouldWatch) return;

            try
            {
                watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName |
                                   NotifyFilters.CreationTime |
                                   NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                };
                watcher.Created += OnFolderContentsChanged;
                watcher.Deleted += OnFolderContentsChanged;
                watcher.Changed += OnFolderContentsChanged;
                watcher.Renamed += OnFolderContentsChanged;
                watcher.Error += OnFolderWatcherError;
            }
            catch
            {
                DisposeWatcher(ref watcher);
            }
        }

        private void DisposeWatcher(ref FileSystemWatcher? watcher)
        {
            if (watcher is null) return;
            watcher.Created -= OnFolderContentsChanged;
            watcher.Deleted -= OnFolderContentsChanged;
            watcher.Changed -= OnFolderContentsChanged;
            watcher.Renamed -= OnFolderContentsChanged;
            watcher.Error -= OnFolderWatcherError;
            watcher.Dispose();
            watcher = null;
        }

        private void OnFolderContentsChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.BeginInvoke(ScheduleFolderSummaryRefresh);
        }

        private void OnFolderWatcherError(object sender, ErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ConfigureFolderWatchers();
                ScheduleFolderSummaryRefresh();
            });
        }

        private void ScheduleFolderSummaryRefresh()
        {
            _folderSummaryTimer.Stop();
            _folderSummaryTimer.Start();
        }

        private void OnFolderSummaryTimerTick(object? sender, EventArgs e)
        {
            _folderSummaryTimer.Stop();
            RefreshFolderSummaries();
        }

        private string DescribeInputFolder(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return T("InputSummaryNone");

            if (!Directory.Exists(dir))
                return T("FolderCreateOnStart");

            int count = FolderStateService.CountInputFiles(dir);
            if (count < 0)
                return T("FolderUnreadable");

            return count == 0
                ? T(IsAnimeVideoModelSelected() ? "InputNoFrames" : "InputNoImages")
                : string.Format(CultureInfo.CurrentCulture, T(IsAnimeVideoModelSelected() ? "InputFrameCount" : "InputCount"), count);
        }

        private string DescribeOutputFolder(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
                return T("OutputSummaryNone");

            if (!Directory.Exists(dir))
                return T("FolderCreateOnStart");

            int count = FolderStateService.CountOutputFiles(dir, SelectedOutputFormat());
            if (count < 0)
                return T("FolderUnreadable");

            return count == 0
                ? T("OutputNoFiles")
                : string.Format(CultureInfo.CurrentCulture, T(IsAnimeVideoModelSelected() ? "OutputFrameCount" : "OutputCount"), count);
        }
    }
}
