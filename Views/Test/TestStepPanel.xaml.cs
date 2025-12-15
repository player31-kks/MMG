using System.Windows;
using System.Windows.Controls;
using MMG.ViewModels;

namespace MMG.Views.Test
{
    public partial class TestStepPanel : UserControl
    {
        private TestLogWindow? _logWindow;

        public TestStepPanel()
        {
            InitializeComponent();
        }

        private void OpenLogWindow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TestsViewModel viewModel)
            {
                // 이미 창이 열려있으면 포커스
                if (_logWindow != null && _logWindow.IsLoaded)
                {
                    _logWindow.Activate();
                    return;
                }

                // 새 로그 창 열기
                _logWindow = new TestLogWindow(viewModel.LogItems);
                _logWindow.Owner = Window.GetWindow(this);
                _logWindow.Show();
            }
        }
    }
}