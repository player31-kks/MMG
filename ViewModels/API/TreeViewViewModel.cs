using System.Collections.ObjectModel;
using MMG.Models;
using MMG.Services;
using MMG.Views.Common;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MMG.ViewModels.API
{
    public partial class TreeViewViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<TreeViewItemModel> treeItems = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
        [NotifyCanExecuteChangedFor(nameof(LoadSelectedRequestCommand), nameof(DeleteSelectedCommand), nameof(RenameSelectedItemCommand))]
        private TreeViewItemModel? selectedTreeItem;

        [ObservableProperty]
        private ObservableCollection<Folder> folders = new();

        public bool HasSelectedItem => SelectedTreeItem != null;

        public TreeViewViewModel()
        {
            _databaseService = new DatabaseService();
            _ = BuildTreeView();
        }

        partial void OnSelectedTreeItemChanged(TreeViewItemModel? value)
        {
            if (value?.ItemType == TreeViewItemType.Request && value.Tag is SavedRequest request)
            {
                RequestSelected?.Invoke(this, request);
            }
        }

        public event EventHandler<SavedRequest>? RequestSelected;
        public event EventHandler? NewRequestCreated;

        #region Commands

        [RelayCommand(CanExecute = nameof(HasSelectedItem))]
        private void LoadSelectedRequest()
        {
            if (SelectedTreeItem?.ItemType == TreeViewItemType.Request && SelectedTreeItem.Tag is SavedRequest request)
            {
                RequestSelected?.Invoke(this, request);
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedItem))]
        private async Task DeleteSelected()
        {
            if (SelectedTreeItem == null) return;

            var result = ModernMessageDialog.ShowConfirm(
                $"'{SelectedTreeItem.Name}'을(를) 삭제하시겠습니까?",
                "삭제 확인");

            if (result != true) return;

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
                    ModernMessageDialog.ShowError("삭제에 실패했습니다.", "오류");
                }
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        [RelayCommand]
        private async Task NewFolder()
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
                ModernMessageDialog.ShowError($"폴더 생성 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        [RelayCommand]
        private async Task DeleteItem(TreeViewItemModel? item)
        {
            if (item == null) return;

            var result = ModernMessageDialog.ShowConfirm(
                $"'{item.Name}'을(를) 삭제하시겠습니까?",
                "삭제 확인");

            if (result != true) return;

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
                    ModernMessageDialog.ShowError("삭제에 실패했습니다.", "오류");
                }
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        [RelayCommand]
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
                        ModernMessageDialog.ShowError("복사에 실패했습니다.", "오류");
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"복사 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        private string GenerateCopyName(string originalName)
        {
            return originalName + " Copy";
        }

        [RelayCommand]
        private void RenameItem(TreeViewItemModel? item)
        {
            if (item != null)
                StartRenaming(item);
        }

        [RelayCommand]
        private async Task SaveRename(TreeViewItemModel? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name))
            {
                ModernMessageDialog.ShowWarning("이름을 입력해주세요.", "알림");
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
                ModernMessageDialog.ShowError($"이름 변경 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        [RelayCommand]
        private void CancelRename(TreeViewItemModel? item)
        {
            if (item == null) return;

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

        [RelayCommand(CanExecute = nameof(CanRenameSelectedItem))]
        private void RenameSelectedItem()
        {
            if (SelectedTreeItem != null && SelectedTreeItem.ItemType == TreeViewItemType.Request)
            {
                StartRenaming(SelectedTreeItem);
            }
        }

        private bool CanRenameSelectedItem() => HasSelectedItem && SelectedTreeItem?.ItemType == TreeViewItemType.Request;

        #endregion

        #region Public Methods

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
                ModernMessageDialog.ShowError($"이동 중 오류가 발생했습니다: {ex.Message}", "오류");
                return false;
            }
        }

        public async Task RefreshTreeView()
        {
            await BuildTreeView();
        }

        #endregion

        #region Private Methods

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
                ModernMessageDialog.ShowError($"트리 뷰 구성 중 오류가 발생했습니다: {ex.Message}", "오류");
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
                var requestItem = new TreeViewItemModel
                {
                    Name = request.Name,
                    ItemType = TreeViewItemType.Request,
                    Tag = request
                };
                treeItem.Children.Add(requestItem);
            }

            return treeItem;
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

        #endregion
    }
}