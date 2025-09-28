using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using MMG.Models;
using MMG.ViewModels;

namespace MMG.Views
{
    /// <summary>
    /// Interaction logic for SavedRequestsPanel.xaml
    /// </summary>
    public partial class SavedRequestsPanel : UserControl
    {
        public SavedRequestsPanel()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItemModel selectedItem && DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedTreeItem = selectedItem;
            }
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is TreeViewItemModel treeItem)
            {
                if (treeItem.ItemType == TreeViewItemType.Request && DataContext is MainViewModel viewModel)
                {
                    viewModel.LoadSelectedRequestCommand?.Execute(null);
                }
                e.Handled = true;
            }
        }

        private void TreeViewItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.IsSelected = true;
                e.Handled = true;
            }
        }
    }
}