using System.Windows;

namespace MMG.Views.Common
{
    public partial class CreateScenarioDialog : Window
    {
        public string ScenarioName { get; private set; } = string.Empty;
        public bool UseBindPort { get; private set; } = false;
        public int BindPort { get; private set; } = 0;

        public CreateScenarioDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            ScenarioNameTextBox.Focus();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ScenarioNameTextBox.Text))
            {
                ModernMessageDialog.ShowWarning("시나리오 이름을 입력해주세요.", "입력 오류");
                ScenarioNameTextBox.Focus();
                return;
            }

            ScenarioName = ScenarioNameTextBox.Text.Trim();
            UseBindPort = UseBindPortCheckBox.IsChecked == true;

            if (UseBindPort && int.TryParse(BindPortTextBox.Text, out int port))
            {
                BindPort = port;
            }

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