using System.Windows;

namespace MMG.Views.Common
{
    public partial class CreateScenarioDialog : Window
    {
        public string ScenarioName { get; private set; } = string.Empty;

        public CreateScenarioDialog()
        {
            InitializeComponent();
            ScenarioNameTextBox.Focus();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ScenarioNameTextBox.Text))
            {
                MessageBox.Show("시나리오 이름을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                ScenarioNameTextBox.Focus();
                return;
            }

            ScenarioName = ScenarioNameTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}