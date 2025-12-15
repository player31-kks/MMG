using System.Collections.ObjectModel;
using System.Windows.Input;
using MMG.Models;
using MMG.Services;
using MMG.Views.Common;
using MMG.ViewModels.Base;
using System.Windows;

namespace MMG.ViewModels.API
{
    public class TreeViewViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<TreeViewItemModel> _treeItems = new();
        private TreeViewItemModel? _selectedTreeItem;
        private ObservableCollection<Folder> _folders = new();
        private bool _hasSelectedItem;

        public TreeViewViewModel()
        {
            _databaseService = new DatabaseService();

            LoadSelectedRequestCommand = new RelayCommand(() => LoadSelectedTreeRequest(), () => HasSelectedItem);
            DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedItem(), () => HasSelectedItem);
            NewFolderCommand = new RelayCommand(async () => await CreateNewFolder());
            DeleteItemCommand = new RelayCommand<TreeViewItemModel>(async (item) => await DeleteSpecificItem(item));
            RenameItemCommand = new RelayCommand<TreeViewItemModel>((item) => { if (item != null) StartRenaming(item); });
            SaveRenameCommand = new RelayCommand<TreeViewItemModel>(async (item) => { if (item != null) await SaveRename(item); });
            CancelRenameCommand = new RelayCommand<TreeViewItemModel>((item) => { if (item != null) CancelRename(item); });
            CopyItemCommand = new RelayCommand<TreeViewItemModel>(async (item) => await CopyItem(item));
            RenameSelectedItemCommand = new RelayCommand(() => RenameSelectedItem(), () => HasSelectedItem && SelectedTreeItem?.ItemType == TreeViewItemType.Request);

            _ = BuildTreeView();
        }

        public ObservableCollection<TreeViewItemModel> TreeItems
        {
            get => _treeItems;
            set
            {
                _treeItems = value;
                OnPropertyChanged(nameof(TreeItems));
            }
        }

        public TreeViewItemModel? SelectedTreeItem
        {
            get => _selectedTreeItem;
            set
            {
                _selectedTreeItem = value;
                OnPropertyChanged(nameof(SelectedTreeItem));
                HasSelectedItem = _selectedTreeItem != null;

                if (_selectedTreeItem?.ItemType == TreeViewItemType.Request && _selectedTreeItem.Tag is SavedRequest request)
                {
                    RequestSelected?.Invoke(this, request);
                }
            }
        }

        public ObservableCollection<Folder> Folders
        {
            get => _folders;
            set
            {
                _folders = value;
                OnPropertyChanged(nameof(Folders));
            }
        }

        public bool HasSelectedItem
        {
            get => _hasSelectedItem;
            set
            {
                _hasSelectedItem = value;
                OnPropertyChanged(nameof(HasSelectedItem));
                ((RelayCommand)LoadSelectedRequestCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteSelectedCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RenameSelectedItemCommand).CanExecute(null);
            }
        }

        public ICommand LoadSelectedRequestCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand NewFolderCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand RenameItemCommand { get; }
        public ICommand SaveRenameCommand { get; }
        public ICommand CancelRenameCommand { get; }
        public ICommand CopyItemCommand { get; }
        public ICommand RenameSelectedItemCommand { get; }

        public event EventHandler<SavedRequest>? RequestSelected;
        public event EventHandler? NewRequestCreated;

        /// <summary>
        /// Request를 다른 폴더로 이동
        /// </summary>
        public async Task<bool> MoveRequestToFolder(SavedRequest request, int? targetFolderId)
        {
            try
            {
                var result = await _databaseService.MoveRequestToFolderAsync(request.Id, targetFolderId);
                if (result)
                {
                    request.FolderId = targetFolderId;
                    await BuildTreeView();
                }
                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이동 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task RefreshTreeView()
        {
            await BuildTreeView();
        }

        private void LoadSelectedTreeRequest()
        {
            if (SelectedTreeItem?.ItemType == TreeViewItemType.Request && SelectedTreeItem.Tag is SavedRequest request)
            {
                RequestSelected?.Invoke(this, request);
            }
        }

        private async Task DeleteSelectedItem()
        {
            if (SelectedTreeItem == null) return;

            var result = MessageBox.Show(
                $"'{SelectedTreeItem.Name}'을(를) 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                bool success = false;

                if (SelectedTreeItem.ItemType == TreeViewItemType.Folder && SelectedTreeItem.Tag is Folder folder)
                {
                    success = await _databaseService.DeleteFolderAsync(folder.Id);
                }
                else if (SelectedTreeItem.ItemType == TreeViewItemType.Request && SelectedTreeItem.Tag is SavedRequest request)
                {
                    success = await _databaseService.DeleteRequestAsync(request.Id);

                    if (success)
                    {
                        NewRequestCreated?.Invoke(this, EventArgs.Empty);
                    }
                }

                if (success)
                {
                    await BuildTreeView();
                }
                else
                {
                    MessageBox.Show("삭제에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CreateNewFolder()
        {
            try
            {
                var folders = await _databaseService.GetAllFoldersAsync();
                var dialog = new CreateFolderDialog(folders);

                if (dialog.ShowDialog() == true)
                {
                    var newFolder = new Folder
                    {
                        Name = dialog.FolderName,
                        ParentId = dialog.ParentFolderId
                    };

                    await _databaseService.SaveFolderAsync(newFolder);
                    await BuildTreeView();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"폴더 생성 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task BuildTreeView()
        {
            try
            {
                var folders = await _databaseService.GetAllFoldersAsync();
                var allRequests = await _databaseService.GetAllRequestsAsync();

                Folders = folders;

                var treeItems = new ObservableCollection<TreeViewItemModel>();

                // Build root folders first
                var rootFolders = folders.Where(f => f.ParentId == null).OrderBy(f => f.Name);

                foreach (var folder in rootFolders)
                {
                    var treeItem = CreateFolderTreeItem(folder, folders, allRequests);
                    treeItems.Add(treeItem);
                }

                // Add root-level requests (requests without folder)
                var rootRequests = allRequests.Where(r => r.FolderId == null).OrderBy(r => r.Name);
                foreach (var request in rootRequests)
                {
                    var treeItem = new TreeViewItemModel
                    {
                        Name = request.Name,
                        ItemType = TreeViewItemType.Request,
                        Tag = request
                    };
                    treeItems.Add(treeItem);
                }

                TreeItems = treeItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"트리 뷰 구성 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TreeViewItemModel CreateFolderTreeItem(Folder folder, ObservableCollection<Folder> allFolders, ObservableCollection<SavedRequest> allRequests)
        {
            var treeItem = new TreeViewItemModel
            {
                Name = folder.Name,
                ItemType = TreeViewItemType.Folder,
                Tag = folder,
                IsExpanded = folder.IsExpanded
            };

            // Add subfolders
            var subFolders = allFolders.Where(f => f.ParentId == folder.Id).OrderBy(f => f.Name);
            foreach (var subFolder in subFolders)
            {
                var subTreeItem = CreateFolderTreeItem(subFolder, allFolders, allRequests);
                treeItem.Children.Add(subTreeItem);
            }

            // Add requests in this folder
            var folderRequests = allRequests.Where(r => r.FolderId == folder.Id).OrderBy(r => r.Name);
            foreach (var request in folderRequests)
            {
                var requestTreeItem = new TreeViewItemModel
                {
                    Name = request.Name,
                    ItemType = TreeViewItemType.Request,
                    Tag = request
                };
                treeItem.Children.Add(requestTreeItem);
            }

            return treeItem;
        }

        private async Task DeleteSpecificItem(TreeViewItemModel? item)
        {
            if (item == null) return;

            var result = MessageBox.Show(
                $"'{item.Name}'을(를) 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                bool success = false;

                if (item.ItemType == TreeViewItemType.Folder && item.Tag is Folder folder)
                {
                    success = await _databaseService.DeleteFolderAsync(folder.Id);
                }
                else if (item.ItemType == TreeViewItemType.Request && item.Tag is SavedRequest request)
                {
                    success = await _databaseService.DeleteRequestAsync(request.Id);

                    if (success)
                    {
                        NewRequestCreated?.Invoke(this, EventArgs.Empty);
                    }
                }

                if (success)
                {
                    await BuildTreeView();
                }
                else
                {
                    MessageBox.Show("삭제에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CopyItem(TreeViewItemModel? item)
        {
            if (item == null) return;

            try
            {
                if (item.ItemType == TreeViewItemType.Request && item.Tag is SavedRequest originalRequest)
                {
                    string copyName = GenerateCopyName(originalRequest.Name);

                    var copiedRequest = new SavedRequest
                    {
                        Name = copyName,
                        IpAddress = originalRequest.IpAddress,
                        Port = originalRequest.Port,
                        RequestSchemaJson = originalRequest.RequestSchemaJson,
                        ResponseSchemaJson = originalRequest.ResponseSchemaJson,
                        FolderId = originalRequest.FolderId
                    };

                    int newId = await _databaseService.SaveRequestAsync(copiedRequest);

                    if (newId > 0)
                    {
                        await BuildTreeView();
                    }
                    else
                    {
                        MessageBox.Show("복사에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"복사 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateCopyName(string originalName)
        {
            return originalName + " Copy";
        }

        private void StartRenaming(TreeViewItemModel item)
        {
            foreach (var treeItem in GetAllTreeItems())
            {
                if (treeItem.IsEditing)
                {
                    treeItem.IsEditing = false;
                }
            }

            item.IsEditing = true;
        }

        private async Task SaveRename(TreeViewItemModel item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                MessageBox.Show("이름을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (item.ItemType == TreeViewItemType.Request && item.Tag is SavedRequest request)
                {
                    request.Name = item.Name;
                    await _databaseService.SaveRequestAsync(request);
                }
                else if (item.ItemType == TreeViewItemType.Folder && item.Tag is Folder folder)
                {
                    folder.Name = item.Name;
                    await _databaseService.SaveFolderAsync(folder);
                }

                item.IsEditing = false;
                await BuildTreeView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이름 변경 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelRename(TreeViewItemModel item)
        {
            item.IsEditing = false;

            if (item.Tag is SavedRequest request)
            {
                item.Name = request.Name;
            }
            else if (item.Tag is Folder folder)
            {
                item.Name = folder.Name;
            }
        }

        private void RenameSelectedItem()
        {
            if (SelectedTreeItem != null && SelectedTreeItem.ItemType == TreeViewItemType.Request)
            {
                StartRenaming(SelectedTreeItem);
            }
        }

        private IEnumerable<TreeViewItemModel> GetAllTreeItems()
        {
            var allItems = new List<TreeViewItemModel>();
            foreach (var item in TreeItems)
            {
                allItems.Add(item);
                allItems.AddRange(GetChildrenRecursively(item));
            }
            return allItems;
        }

        private IEnumerable<TreeViewItemModel> GetChildrenRecursively(TreeViewItemModel parent)
        {
            var children = new List<TreeViewItemModel>();
            foreach (var child in parent.Children)
            {
                children.Add(child);
                children.AddRange(GetChildrenRecursively(child));
            }
            return children;
        }
    }
}