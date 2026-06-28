using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
#if PREVIEW_DEBUG
using System.Windows.Data;
#endif
using System.Windows.Media;
using System.Windows.Media.Animation;
#if PREVIEW_DEBUG
using System.Windows.Shapes;
#endif
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
#if PREVIEW_DEBUG
        private readonly PreviewDebugWindowLabels _previewDebugLabels;
#endif
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
#if PREVIEW_DEBUG
            string previewDebugLabel,
            bool isPreviewDebugEnabled,
            PreviewDebugWindowLabels previewDebugLabels,
#endif
            string checkUpdatesLabel,
            string checkUpdatesCheckingLabel,
            string latestVersionLabel,
            string newVersionLabel,
            string updateCheckFailedLabel,
            string downloadLatestVersionLabel,
            bool isAutoCheckUpdatesAvailable,
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
#if PREVIEW_DEBUG
            _previewDebugLabels = previewDebugLabels;
#endif
            _currentVersion = version;
            _updateCheckIntervalChanged = updateCheckIntervalChanged;

            Title = title;
            DescriptionText.Text = description;
            VersionLabelText.Text = _versionLabel;
            SetVersionDisplay(UpdateVersionDisplayState.Current(_currentVersion));
            LicenseSectionTitleText.Text = licenseSectionTitle;
            RepositoryButton.Content = openRepositoryLabel;
#if PREVIEW_DEBUG
            if (isPreviewDebugEnabled)
                FooterActionsPanel.Children.Insert(0, CreatePreviewDebugButton(previewDebugLabel));
#endif
            SetCheckUpdatesButtonLabel(_checkUpdatesLabel);
            if (isAutoCheckUpdatesAvailable)
            {
                AutoCheckUpdatesLabelText.Text = autoCheckUpdatesLabel;
                AutomationProperties.SetName(AutoCheckUpdatesCombo, autoCheckUpdatesLabel);
                PopulateUpdateCheckIntervalCombo(updateCheckIntervalItems, selectedUpdateCheckInterval);
            }
            else
            {
                AutoCheckUpdatesPanel.Visibility = Visibility.Collapsed;
            }

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

#if PREVIEW_DEBUG
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
#endif

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

                ApplyUpdateCheckStatus(UpdateCheckStatus.FromResult(result));
            }
            finally
            {
                _isCheckingForUpdates = false;
                SetUpdateCheckingAnimation(isChecking: false);
            }
        }

        private void ApplyUpdateCheckStatus(UpdateCheckStatus status)
        {
            LatestReleaseLinkText.Visibility = Visibility.Collapsed;

            if (status.Kind == UpdateCheckStatusKind.Failed)
            {
                VersionLabelText.Text = _versionLabel;
                SetVersionDisplay(UpdateVersionDisplayState.Current(_currentVersion));
                SetCheckUpdatesButtonLabel(_updateCheckFailedLabel);
                return;
            }

            if (status.Kind == UpdateCheckStatusKind.Canceled)
            {
                VersionLabelText.Text = _versionLabel;
                SetVersionDisplay(UpdateVersionDisplayState.Current(_currentVersion));
                SetCheckUpdatesButtonLabel(_checkUpdatesLabel);
                return;
            }

            if (status.Kind == UpdateCheckStatusKind.UpdateAvailable)
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

#if PREVIEW_DEBUG
        private Button CreatePreviewDebugButton(string label)
        {
            var button = new Button
            {
                Width = 30,
                Height = 30,
                MinWidth = 30,
                MinHeight = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Style = (Style)FindResource("AboutIconButton"),
                VerticalAlignment = VerticalAlignment.Center,
                Content = CreatePreviewDebugIcon(),
            };

            ToolTipService.SetShowsToolTipOnKeyboardFocus(button, true);
            AutomationProperties.SetName(button, label);
            button.ToolTip = label;
            button.Click += OnPreviewDebugClick;
            return button;
        }

        private static Viewbox CreatePreviewDebugIcon()
        {
            var canvas = new Canvas
            {
                Width = 24,
                Height = 24,
            };
            canvas.Children.Add(CreatePreviewDebugIconPath("M4 17L10 11L4 5"));
            canvas.Children.Add(CreatePreviewDebugIconPath("M12 19H20"));

            return new Viewbox
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                Child = canvas,
            };
        }

        private static Path CreatePreviewDebugIconPath(string data)
        {
            var path = new Path
            {
                Data = Geometry.Parse(data),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
            };
            path.SetBinding(
                Shape.StrokeProperty,
                new Binding("Foreground")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1),
                });
            return path;
        }
#endif

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
