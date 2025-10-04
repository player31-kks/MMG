using System.Windows;
using MMG.Services;

namespace MMG.Views.Common
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            DataContext = SettingsService.Instance;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.SaveSettings();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}