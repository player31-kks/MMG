using System.Windows;
using System.Collections.ObjectModel;
using MMG.Models;

namespace MMG.Views
{
    public partial class CreateFolderDialog : Window
    {
        public string FolderName { get; private set; } = "";
        public int? ParentFolderId { get; private set; }

        public CreateFolderDialog(ObservableCollection<Folder> availableFolders)
        {
            InitializeComponent();
            FolderNameTextBox.Focus();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderNameTextBox.Text))
            {
                MessageBox.Show("폴더 이름을 입력해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FolderName = FolderNameTextBox.Text.Trim();
            ParentFolderId = null; // 일단 루트 폴더로만

            base.DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            base.DialogResult = false;
            Close();
        }
    }
}