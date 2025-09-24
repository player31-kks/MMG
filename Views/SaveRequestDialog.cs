using System.Windows;

namespace MMG.Views
{
    public partial class SaveRequestDialog : Window
    {
        public string RequestName { get; private set; } = "";

        public SaveRequestDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => RequestNameTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            RequestName = RequestNameTextBox.Text;
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