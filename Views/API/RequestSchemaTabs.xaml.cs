using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MMG.Views.API
{
    public partial class RequestSchemaTabs : UserControl
    {
        public RequestSchemaTabs()
        {
            InitializeComponent();
        }

        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (sender is DataGrid dataGrid)
                {
                    dataGrid.SelectedItem = null;
                    dataGrid.CurrentItem = null;
                    e.Handled = true;
                }
            }
        }

        private void DataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dataGrid)
            {
                // 빈 영역을 클릭했는지 확인
                var hitTest = dataGrid.InputHitTest(e.GetPosition(dataGrid));
                if (hitTest == dataGrid || (hitTest is FrameworkElement element && element.DataContext == null))
                {
                    dataGrid.SelectedItem = null;
                    dataGrid.CurrentItem = null;
                }
            }
        }
    }
}
