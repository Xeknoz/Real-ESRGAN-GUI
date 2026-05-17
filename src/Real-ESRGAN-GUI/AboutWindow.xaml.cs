using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RealESRGAN_GUI
{
    public partial class AboutWindow : Window
    {
        private readonly string _repositoryUrl;
        private readonly string _openRepositoryFailedMessage;
        private readonly string _licenseMissingMessage;

        public AboutWindow(
            string version,
            string repositoryUrl,
            string title,
            string description,
            string versionLabel,
            string licenseSectionTitle,
            string licenseMissingMessage,
            string openRepositoryLabel,
            string closeLabel,
            string openRepositoryFailedMessage,
            IReadOnlyList<LicenseDocument> licenseDocuments)
        {
            InitializeComponent();

            _repositoryUrl = repositoryUrl;
            _openRepositoryFailedMessage = openRepositoryFailedMessage;
            _licenseMissingMessage = licenseMissingMessage;

            Title = title;
            DescriptionText.Text = description;
            VersionLabelText.Text = versionLabel;
            VersionValueText.Text = version;
            LicenseSectionTitleText.Text = licenseSectionTitle;
            RepositoryButton.Content = openRepositoryLabel;
            CloseButton.Content = closeLabel;

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

        private void OnRepositoryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _repositoryUrl,
                    UseShellExecute = true,
                });
            }
            catch
            {
                MessageBox.Show(
                    this,
                    _openRepositoryFailedMessage,
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
