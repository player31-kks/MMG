using System.Windows;
using System.Windows.Input;

namespace MMG.Views.Common
{
    public partial class ConfirmDialog : Window
    {
        public bool Confirmed { get; private set; }

        public ConfirmDialog(string title, string message, string confirmButtonText = "삭제")
        {
            InitializeComponent();
            
            TitleText.Text = title;
            MessageText.Text = message;
            ConfirmButton.Content = confirmButtonText;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }

        // Static helper method
        public static bool Show(string title, string message, string confirmButtonText = "삭제", Window? owner = null)
        {
            var dialog = new ConfirmDialog(title, message, confirmButtonText);
            if (owner != null)
                dialog.Owner = owner;
            dialog.ShowDialog();
            return dialog.Confirmed;
        }
    }
}
