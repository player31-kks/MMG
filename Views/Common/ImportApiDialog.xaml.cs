using System.Windows;
using System.Windows.Controls;
using MMG.Models.Import;

namespace MMG.Views.Common
{
    public partial class ImportApiDialog : Window
    {
        public ApiImportFormat SelectedFormat { get; private set; } = ApiImportFormat.Json;

        public ImportApiDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (FormatComboBox.SelectedItem is not ComboBoxItem selectedItem)
            {
                ModernMessageDialog.ShowWarning("Import 형식을 선택해주세요.", "입력 오류");
                return;
            }

            SelectedFormat = selectedItem.Tag?.ToString() == "Idl"
                ? ApiImportFormat.Idl
                : ApiImportFormat.Json;

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