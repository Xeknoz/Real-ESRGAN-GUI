using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using RealESRGAN_GUI.Services;

namespace RealESRGAN_GUI
{
    internal sealed record PreviewDebugWindowLabels(
        string Title,
        string UpdateBehaviorTitle,
        string CurrentVersionLabel,
        string SimulatedLatestVersionLabel,
        string LatestVersionLabel,
        string StatusLabel,
        string ReadyStatus,
        string UpdateAvailableStatus,
        string UpToDateStatus,
        string UnknownVersion,
        string SimulateUpdateAvailableLabel,
        string OpenReleasePageLabel,
        string OpenReleaseFailedMessage,
        string CloseLabel);

    public partial class PreviewDebugWindow : Window
    {
        private const string DefaultSimulatedLatestVersion = "v999.999.999-preview";
        private readonly string _currentVersion;
        private readonly PreviewDebugWindowLabels _labels;
        private readonly Action<UpdateCheckStatus> _applyDetectedUpdate;
        private string _latestReleaseUrl = UpdateCheckService.ReleasesPageUrl;

        internal PreviewDebugWindow(
            string currentVersion,
            PreviewDebugWindowLabels labels,
            Action<UpdateCheckStatus> applyDetectedUpdate)
        {
            InitializeComponent();

            _currentVersion = currentVersion;
            _labels = labels;
            _applyDetectedUpdate = applyDetectedUpdate;

            Title = labels.Title;
            TitleText.Text = labels.Title;
            UpdateCheckTitleText.Text = labels.UpdateBehaviorTitle;
            CurrentVersionLabelText.Text = labels.CurrentVersionLabel;
            CurrentVersionValueText.Text = _currentVersion;
            SimulatedLatestVersionLabelText.Text = labels.SimulatedLatestVersionLabel;
            SimulatedLatestVersionTextBox.Text = DefaultSimulatedLatestVersion;
            AutomationProperties.SetName(SimulatedLatestVersionTextBox, labels.SimulatedLatestVersionLabel);
            LatestVersionLabelText.Text = labels.LatestVersionLabel;
            LatestVersionValueText.Text = labels.UnknownVersion;
            StatusLabelText.Text = labels.StatusLabel;
            StatusValueText.Text = labels.ReadyStatus;
            SimulateUpdateAvailableButton.Content = labels.SimulateUpdateAvailableLabel;
            CloseButton.Content = labels.CloseLabel;
            LatestReleaseLinkRun.Text = labels.OpenReleasePageLabel;

            SourceInitialized += (_, _) => App.ApplyWindowTitleBarTheme(this);
        }

        private void OnSimulateUpdateAvailableClick(object sender, RoutedEventArgs e)
        {
            string simulatedLatestVersion = string.IsNullOrWhiteSpace(SimulatedLatestVersionTextBox.Text)
                ? DefaultSimulatedLatestVersion
                : SimulatedLatestVersionTextBox.Text.Trim();
            UpdateCheckStatus status = UpdateCheckStatus.FromLatestVersion(
                _currentVersion,
                simulatedLatestVersion,
                UpdateCheckService.ReleasesPageUrl);
            _applyDetectedUpdate(status);
            ApplyUpdateCheckStatus(status);
        }

        private void ApplyUpdateCheckStatus(UpdateCheckStatus status)
        {
            LatestVersionValueText.Text = string.IsNullOrWhiteSpace(status.LatestVersion)
                ? _labels.UnknownVersion
                : status.LatestVersion;

            if (string.IsNullOrWhiteSpace(status.ReleaseUrl))
            {
                LatestReleaseLinkText.Visibility = Visibility.Collapsed;
            }
            else
            {
                _latestReleaseUrl = status.ReleaseUrl;
                LatestReleaseLinkText.Visibility = Visibility.Visible;
            }

            StatusValueText.Text = status.Kind switch
            {
                UpdateCheckStatusKind.UpdateAvailable => _labels.UpdateAvailableStatus,
                UpdateCheckStatusKind.UpToDate => _labels.UpToDateStatus,
                _ => _labels.ReadyStatus,
            };
        }

        private void OnLatestReleaseClick(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(_latestReleaseUrl, _labels.OpenReleaseFailedMessage);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenExternalUrl(string url, string failedMessage)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch
            {
                MessageBox.Show(
                    this,
                    failedMessage,
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
