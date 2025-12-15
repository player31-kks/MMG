using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MMG.Models;
using MMG.ViewModels;

namespace MMG.Views.API
{
    /// <summary>
    /// Interaction logic for SavedRequestsPanel.xaml
    /// </summary>
    public partial class SavedRequestsPanel : UserControl
    {
        private DispatcherTimer _showTimer;
        private DispatcherTimer _hideTimer;
        private bool _isMouseOverButton = false;
        private bool _isMouseOverPopup = false;

        // 드래그 앤 드롭 관련
        private Point _dragStartPoint;
        private TreeViewItem? _draggedItem;
        private bool _isDragging = false;

        public SavedRequestsPanel()
        {
            InitializeComponent();

            // 0.3초 후에 Popup을 보여주는 타이머
            _showTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _showTimer.Tick += ShowTimer_Tick;

            // 0.3초 후에 Popup을 숨기는 타이머
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _hideTimer.Tick += HideTimer_Tick;
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

        private void AddButton_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverButton = true;
            _hideTimer.Stop(); // 숨기기 타이머 중지

            if (!AddPopup.IsOpen)
            {
                _showTimer.Start(); // 0.3초 후에 보여주기
            }
        }

        private void AddButton_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOverButton = false;
            _showTimer.Stop(); // 보여주기 타이머 중지

            // 마우스가 Popup으로 이동하지 않았다면 숨기기 타이머 시작
            if (!_isMouseOverPopup)
            {
                _hideTimer.Start(); // 0.3초 후에 숨기기
            }
        }

        private void AddPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = true;
            _hideTimer.Stop(); // 숨기기 타이머 중지
        }

        private void AddPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOverPopup = false;
            _hideTimer.Start(); // 0.3초 후에 숨기기
        }

        private void ShowTimer_Tick(object? sender, EventArgs e)
        {
            _showTimer.Stop();
            if (_isMouseOverButton || _isMouseOverPopup)
            {
                AddPopup.IsOpen = true;
            }
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            _hideTimer.Stop();
            if (!_isMouseOverButton && !_isMouseOverPopup)
            {
                AddPopup.IsOpen = false;
            }
        }

        private void PopupButton_Click(object sender, RoutedEventArgs e)
        {
            AddPopup.IsOpen = false;
            _showTimer.Stop();
            _hideTimer.Stop();
            _isMouseOverButton = false;
            _isMouseOverPopup = false;
        }

        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is TreeViewItemModel item)
            {
                if (e.Key == Key.Enter)
                {
                    // Enter 키를 누르면 저장
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.SaveRenameCommand.Execute(item);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    // Escape 키를 누르면 취소
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.CancelRenameCommand.Execute(item);
                    }
                    e.Handled = true;
                }
            }
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                if (e.Key == Key.F2)
                {
                    // F2 키를 누르면 선택된 항목의 이름 변경 시작
                    viewModel.RenameSelectedItemCommand?.Execute(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    // ESC 키를 누르면 현재 편집 중인 항목 취소
                    var editingItem = FindEditingItem(viewModel.TreeItems);
                    if (editingItem != null)
                    {
                        viewModel.CancelRenameCommand?.Execute(editingItem);
                        e.Handled = true;
                    }
                }
            }
        }

        private TreeViewItemModel? FindEditingItem(System.Collections.ObjectModel.ObservableCollection<TreeViewItemModel> items)
        {
            foreach (var item in items)
            {
                if (item.IsEditing)
                    return item;

                if (item.Children != null)
                {
                    var editingChild = FindEditingItem(item.Children);
                    if (editingChild != null)
                        return editingChild;
                }
            }
            return null;
        }

        #region Drag and Drop

        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void TreeViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point position = e.GetPosition(null);

                if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is TreeViewItem treeViewItem &&
                        treeViewItem.DataContext is TreeViewItemModel treeItem &&
                        treeItem.ItemType == TreeViewItemType.Request)
                    {
                        _isDragging = true;
                        _draggedItem = treeViewItem;

                        var data = new DataObject("TreeViewItemModel", treeItem);
                        DragDrop.DoDragDrop(treeViewItem, data, DragDropEffects.Move);

                        _isDragging = false;
                        _draggedItem = null;
                    }
                }
            }
        }

        private void TreeViewItem_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TreeViewItemModel"))
            {
                var targetTreeViewItem = sender as TreeViewItem;
                var targetItem = targetTreeViewItem?.DataContext as TreeViewItemModel;
                var sourceItem = e.Data.GetData("TreeViewItemModel") as TreeViewItemModel;

                // 자기 자신이거나 폴더가 아닌 경우 드롭 불가
                if (sourceItem == null || targetItem == null ||
                    sourceItem == targetItem ||
                    (targetItem.ItemType != TreeViewItemType.Folder && targetItem.ItemType != TreeViewItemType.Request))
                {
                    e.Effects = DragDropEffects.None;
                }
                else
                {
                    e.Effects = DragDropEffects.Move;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TreeViewItemModel"))
            {
                var targetTreeViewItem = sender as TreeViewItem;
                var targetItem = targetTreeViewItem?.DataContext as TreeViewItemModel;
                var sourceItem = e.Data.GetData("TreeViewItemModel") as TreeViewItemModel;

                if (sourceItem?.Tag is SavedRequest request && targetItem != null && DataContext is MainViewModel viewModel)
                {
                    int? targetFolderId = null;

                    if (targetItem.ItemType == TreeViewItemType.Folder && targetItem.Tag is Folder folder)
                    {
                        targetFolderId = folder.Id;
                    }
                    else if (targetItem.ItemType == TreeViewItemType.Request && targetItem.Tag is SavedRequest targetRequest)
                    {
                        // 요청 위에 드롭하면 같은 폴더로 이동
                        targetFolderId = targetRequest.FolderId;
                    }

                    // 같은 폴더면 이동하지 않음
                    if (request.FolderId != targetFolderId)
                    {
                        await viewModel.TreeViewViewModel.MoveRequestToFolder(request, targetFolderId);
                    }
                }
            }
            e.Handled = true;
        }

        private async void TreeView_Drop(object sender, DragEventArgs e)
        {
            // TreeView 루트에 드롭하면 루트로 이동 (FolderId = null)
            if (e.Data.GetDataPresent("TreeViewItemModel"))
            {
                var sourceItem = e.Data.GetData("TreeViewItemModel") as TreeViewItemModel;

                if (sourceItem?.Tag is SavedRequest request && DataContext is MainViewModel viewModel)
                {
                    if (request.FolderId != null)
                    {
                        await viewModel.TreeViewViewModel.MoveRequestToFolder(request, null);
                    }
                }
            }
            e.Handled = true;
        }

        #endregion
    }
}