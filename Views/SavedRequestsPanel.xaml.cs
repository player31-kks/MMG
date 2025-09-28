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
            // 우클릭 시 시각적 선택을 하지 않도록 수정
            // 컨텍스트 메뉴는 여전히 정상 작동함
            e.Handled = true;
        }

        private void TreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is TreeViewItemModel treeItem)
            {
                // 아이템 타입에 따라 다른 컨텍스트 메뉴 설정
                if (treeItem.ItemType == TreeViewItemType.Folder)
                {
                    var contextMenu = FindResource("FolderContextMenu") as ContextMenu;
                    if (contextMenu != null)
                    {
                        contextMenu.DataContext = this.DataContext; // MainViewModel을 DataContext로 설정
                        item.ContextMenu = contextMenu;
                    }
                }
                else if (treeItem.ItemType == TreeViewItemType.Request)
                {
                    var contextMenu = FindResource("RequestContextMenu") as ContextMenu;
                    if (contextMenu != null)
                    {
                        contextMenu.DataContext = this.DataContext; // MainViewModel을 DataContext로 설정
                        item.ContextMenu = contextMenu;
                    }
                }
            }
        }
    }
}