using System;
using System.Diagnostics;
using System.Windows;

namespace RealESRGAN_GUI
{
    public partial class AboutWindow : Window
    {
        private readonly string _repositoryUrl;
        private readonly string _openRepositoryFailedMessage;

        public AboutWindow(
            string version,
            string repositoryUrl,
            string title,
            string description,
            string versionLabel,
            string repositoryLabel,
            string openRepositoryLabel,
            string closeLabel,
            string openRepositoryFailedMessage)
        {
            InitializeComponent();

            _repositoryUrl = repositoryUrl;
            _openRepositoryFailedMessage = openRepositoryFailedMessage;

            Title = title;
            DescriptionText.Text = description;
            VersionLabelText.Text = versionLabel;
            VersionValueText.Text = version;
            RepositoryLabelText.Text = repositoryLabel;
            RepositoryUrlText.Text = repositoryUrl;
            RepositoryButton.Content = openRepositoryLabel;
            CloseButton.Content = closeLabel;
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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            App.ApplyWindowTitleBarTheme(this);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
