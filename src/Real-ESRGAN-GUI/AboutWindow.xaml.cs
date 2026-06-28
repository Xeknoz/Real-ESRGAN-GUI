using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RealESRGAN_GUI.Services;

namespace RealESRGAN_GUI
{
    public partial class AboutWindow : Window
    {
        private const double AutoCheckUpdatesComboChromeWidth = 54;
        private readonly string _repositoryUrl;
        private readonly string _openRepositoryFailedMessage;
        private readonly string _openReleaseFailedMessage;
        private readonly string _licenseMissingMessage;
        private readonly string _versionLabel;
        private readonly string _latestVersionLabel;
        private readonly string _newVersionLabel;
        private readonly string _checkUpdatesLabel;
        private readonly string _checkUpdatesCheckingLabel;
        private readonly string _updateCheckFailedLabel;
        private readonly string _downloadLatestVersionLabel;
        private readonly PreviewDebugWindowLabels _previewDebugLabels;
        private readonly string _currentVersion;
        private readonly Action<string> _updateCheckIntervalChanged;
        private string _latestReleaseUrl = UpdateCheckService.ReleasesPageUrl;
        private bool _isCheckingForUpdates;
        private bool _isUpdatingUpdateCheckInterval;

        internal AboutWindow(
            string version,
            string repositoryUrl,
            string title,
            string description,
            string versionLabel,
            string licenseSectionTitle,
            string licenseMissingMessage,
            string openRepositoryLabel,
            string previewDebugLabel,
            bool isPreviewDebugEnabled,
            PreviewDebugWindowLabels previewDebugLabels,
            string checkUpdatesLabel,
            string checkUpdatesCheckingLabel,
            string latestVersionLabel,
            string newVersionLabel,
            string updateCheckFailedLabel,
            string downloadLatestVersionLabel,
            string autoCheckUpdatesLabel,
            IReadOnlyList<ComboItem> updateCheckIntervalItems,
            string selectedUpdateCheckInterval,
            Action<string> updateCheckIntervalChanged,
            string openRepositoryFailedMessage,
            string openReleaseFailedMessage,
            IReadOnlyList<LicenseDocument> licenseDocuments)
        {
            InitializeComponent();

            _repositoryUrl = repositoryUrl;
            _openRepositoryFailedMessage = openRepositoryFailedMessage;
            _openReleaseFailedMessage = openReleaseFailedMessage;
            _licenseMissingMessage = licenseMissingMessage;
            _versionLabel = versionLabel;
            _latestVersionLabel = latestVersionLabel;
            _newVersionLabel = newVersionLabel;
            _checkUpdatesLabel = checkUpdatesLabel;
            _checkUpdatesCheckingLabel = checkUpdatesCheckingLabel;
            _updateCheckFailedLabel = updateCheckFailedLabel;
            _downloadLatestVersionLabel = downloadLatestVersionLabel;
            _previewDebugLabels = previewDebugLabels;
            _currentVersion = version;
            _updateCheckIntervalChanged = updateCheckIntervalChanged;

            Title = title;
            DescriptionText.Text = description;
            VersionLabelText.Text = _versionLabel;
            SetVersionDisplay(UpdateVersionDisplayState.Current(_currentVersion));
            LicenseSectionTitleText.Text = licenseSectionTitle;
            RepositoryButton.Content = openRepositoryLabel;
            SetPreviewDebugButtonLabel(previewDebugLabel);
            PreviewDebugButton.Visibility = isPreviewDebugEnabled ? Visibility.Visible : Visibility.Collapsed;
            SetCheckUpdatesButtonLabel(_checkUpdatesLabel);
            AutoCheckUpdatesLabelText.Text = autoCheckUpdatesLabel;
            AutomationProperties.SetName(AutoCheckUpdatesCombo, autoCheckUpdatesLabel);
            PopulateUpdateCheckIntervalCombo(updateCheckIntervalItems, selectedUpdateCheckInterval);

            foreach (var document in licenseDocuments)
                LicenseCombo.Items.Add(document);

            if (LicenseCombo.Items.Count > 0)
            {
                LicenseCombo.SelectedIndex = 0;
            }
            else
            {
                LicenseCombo.IsEnabled = false;
                LicenseTextBox.Text = _licenseMissingMessage;
            }
        }

        public sealed class LicenseDocument
        {
            public LicenseDocument(string title, string text)
            {
                Title = title;
                Text = text;
            }

            public string Title { get; }
            public string Text { get; }

            public override string ToString() => Title;
        }

        private void PopulateUpdateCheckIntervalCombo(
            IReadOnlyList<ComboItem> items,
            string selectedTag)
        {
            _isUpdatingUpdateCheckInterval = true;
            AutoCheckUpdatesCombo.ItemsSource = items;
            AutoCheckUpdatesCombo.DisplayMemberPath = nameof(ComboItem.Display);
            AutoCheckUpdatesCombo.SelectedItem = items.FirstOrDefault(item => item.Tag == selectedTag) ??
                items.FirstOrDefault();
            AutoCheckUpdatesCombo.Width = CalculateUpdateCheckIntervalComboWidth(items);
            _isUpdatingUpdateCheckInterval = false;
        }

        private double CalculateUpdateCheckIntervalComboWidth(IReadOnlyList<ComboItem> items)
        {
            double textWidth = items
                .Select(item => MeasureComboItemTextWidth(item.Display))
                .DefaultIfEmpty(0)
                .Max();

            return Math.Ceiling(textWidth + AutoCheckUpdatesComboChromeWidth);
        }

        private double MeasureComboItemTextWidth(string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontFamily = AutoCheckUpdatesCombo.FontFamily,
                FontSize = AutoCheckUpdatesCombo.FontSize,
                FontStretch = AutoCheckUpdatesCombo.FontStretch,
                FontStyle = AutoCheckUpdatesCombo.FontStyle,
                FontWeight = AutoCheckUpdatesCombo.FontWeight,
            };

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return textBlock.DesiredSize.Width;
        }

        private void OnRepositoryClick(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(_repositoryUrl, _openRepositoryFailedMessage);
        }

        private void OnPreviewDebugClick(object sender, RoutedEventArgs e)
        {
            var debugWindow = new PreviewDebugWindow(
                _currentVersion,
                _previewDebugLabels,
                ApplyUpdateCheckStatus)
            {
                Owner = this,
            };

            debugWindow.ShowDialog();
        }

        private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_isCheckingForUpdates)
                return;

            _isCheckingForUpdates = true;
            SetCheckUpdatesButtonLabel(_checkUpdatesCheckingLabel);
            SetUpdateCheckingAnimation(isChecking: true);
            LatestReleaseLinkText.Visibility = Visibility.Collapsed;

            try
            {
                UpdateCheckResult result = await UpdateCheckService
                    .CheckLatestReleaseAsync(_currentVersion, CancellationToken.None)
                    .ConfigureAwait(true);

                ApplyUpdateCheckStatus(PreviewUpdateCheckStatus.FromResult(result));
            }
            finally
            {
                _isCheckingForUpdates = false;
                SetUpdateCheckingAnimation(isChecking: false);
            }
        }

        private void ApplyUpdateCheckStatus(PreviewUpdateCheckStatus status)
        {
            LatestReleaseLinkText.Visibility = Visibility.Collapsed;

            if (status.Kind == PreviewUpdateCheckStatusKind.Failed)
            {
                VersionLabelText.Text = _versionLabel;
                SetVersionDisplay(UpdateVersionDisplayState.Current(_currentVersion));
                SetCheckUpdatesButtonLabel(_updateCheckFailedLabel);
                return;
            }

            if (status.Kind == PreviewUpdateCheckStatusKind.Canceled)
            {
                VersionLabelText.Text = _versionLabel;
                SetVersionDisplay(UpdateVersionDisplayState.Current(_currentVersion));
                SetCheckUpdatesButtonLabel(_checkUpdatesLabel);
                return;
            }

            if (status.Kind == PreviewUpdateCheckStatusKind.UpdateAvailable)
            {
                VersionLabelText.Text = _newVersionLabel;
                SetVersionDisplay(UpdateVersionDisplayState.FromStatus(_currentVersion, status));
                _latestReleaseUrl = status.ReleaseUrl ?? UpdateCheckService.ReleasesPageUrl;
                LatestReleaseLinkRun.Text = _downloadLatestVersionLabel;
                LatestReleaseLinkText.Visibility = Visibility.Visible;
                SetCheckUpdatesButtonLabel(_newVersionLabel);
                return;
            }

            VersionLabelText.Text = _latestVersionLabel;
            SetVersionDisplay(UpdateVersionDisplayState.Current(_currentVersion));
            SetCheckUpdatesButtonLabel(_latestVersionLabel);
        }

        private void OnLatestReleaseClick(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(_latestReleaseUrl, _openReleaseFailedMessage);
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

        private void SetCheckUpdatesButtonLabel(string label)
        {
            AutomationProperties.SetName(CheckUpdatesButton, label);
            CheckUpdatesButton.ToolTip = label;
        }

        private void SetPreviewDebugButtonLabel(string label)
        {
            AutomationProperties.SetName(PreviewDebugButton, label);
            PreviewDebugButton.ToolTip = label;
        }

        private void SetVersionDisplay(UpdateVersionDisplayState state)
        {
            VersionValueText.Text = state.PrimaryVersion;

            if (!state.HasPreviousVersion)
            {
                PreviousVersionValueText.Text = string.Empty;
                PreviousVersionValueText.Visibility = Visibility.Collapsed;
                return;
            }

            PreviousVersionValueText.Text = state.PreviousVersion!;
            PreviousVersionValueText.Visibility = state.PreviousVersionPlacement == PreviousVersionPlacement.BelowPrimary
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SetUpdateCheckingAnimation(bool isChecking)
        {
            if (!isChecking)
            {
                CheckUpdatesIconRotate.BeginAnimation(RotateTransform.AngleProperty, null);
                CheckUpdatesIconRotate.Angle = 0;
                return;
            }

            var animation = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            CheckUpdatesIconRotate.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void OnAutoCheckUpdatesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUpdateCheckInterval ||
                AutoCheckUpdatesCombo.SelectedItem is not ComboItem item)
            {
                return;
            }

            _updateCheckIntervalChanged(item.Tag);
        }

        private void OnLicenseSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LicenseTextBox.Text = LicenseCombo.SelectedItem is LicenseDocument document
                ? document.Text
                : _licenseMissingMessage;
            LicenseTextBox.ScrollToHome();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            App.ApplyWindowTitleBarTheme(this);
        }
    }
}
