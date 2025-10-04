using System.Windows;
using System.Collections.ObjectModel;
using MMG.Models;

namespace MMG.Views.Common
{
    public partial class SaveRequestDialog : Window
    {
        public string RequestName { get; private set; } = "";
        public int? SelectedFolderId { get; private set; }

        public SaveRequestDialog(ObservableCollection<Folder> folders)
        {
            InitializeComponent();

            // 사용 가능한 폴더들을 ComboBox에 추가
            foreach (var folder in folders)
            {
                FolderComboBox.Items.Add(folder);
            }

            Loaded += (s, e) => RequestNameTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RequestNameTextBox.Text))
            {
                MessageBox.Show("요청 이름을 입력해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RequestName = RequestNameTextBox.Text.Trim();

            if (FolderComboBox.SelectedItem is Folder selectedFolder)
            {
                SelectedFolderId = selectedFolder.Id;
            }
            else
            {
                SelectedFolderId = null; // 루트 폴더
            }

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