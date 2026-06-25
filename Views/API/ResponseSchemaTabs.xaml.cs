using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MMG.Views.API
{
    public partial class ResponseSchemaTabs : UserControl
    {
        public ResponseSchemaTabs()
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

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            if (FindVisualParent<DataGridColumnHeader>(source) != null ||
                FindVisualParent<Button>(source) != null ||
                FindVisualParent<ComboBox>(source) != null ||
                FindVisualParent<ScrollBar>(source) != null)
            {
                return;
            }

            var cell = FindVisualParent<DataGridCell>(source);
            if (cell == null || cell.IsReadOnly || cell.IsEditing || cell.Column is not DataGridTextColumn)
            {
                return;
            }

            var row = FindVisualParent<DataGridRow>(cell);
            if (row == null)
            {
                return;
            }

            dataGrid.SelectedItem = row.Item;
            dataGrid.CurrentCell = new DataGridCellInfo(row.Item, cell.Column);
            dataGrid.Focus();
            cell.Focus();
            dataGrid.BeginEdit(e);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var textBox = FindVisualChild<TextBox>(cell);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }), DispatcherPriority.Input);

            e.Handled = true;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T target)
                {
                    return target;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T target)
                {
                    return target;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }
    }
}
