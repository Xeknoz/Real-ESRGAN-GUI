using System.Windows;

namespace RealESRGAN_GUI
{
    public partial class NoticeWindow : Window
    {
        public NoticeWindow(string title, string message, string okText)
        {
            InitializeComponent();

            Title = title;
            TitleText.Text = title;
            MessageText.Text = message;
            OkButton.Content = okText;

            SourceInitialized += (_, _) => App.ApplyWindowTitleBarTheme(this);
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
