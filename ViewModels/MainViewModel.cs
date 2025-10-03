using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Linq;
using MMG.Models;
using MMG.Services;
using MMG.Views;
using System.Windows;

namespace MMG.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly UdpClientService _udpClientService;
        private readonly DatabaseService _databaseService;
        private UdpRequest _currentRequest;
        private ResponseSchema _responseSchema;
        private UdpResponse? _lastResponse;
        private bool _isSending;
        private string _responseText = "";
        private ObservableCollection<SavedRequest> _savedRequests = new();
        private SavedRequest? _selectedSavedRequest;
        private SavedRequest? _currentLoadedRequest; // 현재 로드된 요청 추적
        private ObservableCollection<TreeViewItemModel> _treeItems = new();
        private TreeViewItemModel? _selectedTreeItem;
        private ObservableCollection<Folder> _folders = new();
        private bool _hasSelectedItem;

        public MainViewModel()
        {
            _udpClientService = new UdpClientService();
            _databaseService = new DatabaseService();
            _currentRequest = new UdpRequest();
            _responseSchema = new ResponseSchema();

            // Initialize with some default fields
            var defaultHeader = new DataField { Name = "Header1", Type = DataType.Byte, Value = "0" };
            var defaultPayload = new DataField { Name = "Field1", Type = DataType.Byte, Value = "0" };

            defaultHeader.PropertyChanged += OnDataFieldPropertyChanged;
            defaultPayload.PropertyChanged += OnDataFieldPropertyChanged;

            _currentRequest.Headers.Add(defaultHeader);
            _currentRequest.Payload.Add(defaultPayload);

            var defaultResponseHeader = new DataField { Name = "ResponseHeader1", Type = DataType.Byte };
            var defaultResponsePayload = new DataField { Name = "ResponseField1", Type = DataType.Byte };

            defaultResponseHeader.PropertyChanged += OnResponseDataFieldPropertyChanged;
            defaultResponsePayload.PropertyChanged += OnResponseDataFieldPropertyChanged;

            _responseSchema.Headers.Add(defaultResponseHeader);
            _responseSchema.Payload.Add(defaultResponsePayload);

            SendCommand = new RelayCommand(async () => await SendRequest(), () => !IsSending);
            SaveCommand = new RelayCommand(async () => await SaveRequest(), () => !IsSending);
            RefreshCommand = new RelayCommand(async () => await LoadSavedRequests());
            LoadSelectedCommand = new RelayCommand(() => LoadSelectedRequest(), () => SelectedSavedRequest != null);
            LoadSelectedRequestCommand = new RelayCommand(() => LoadSelectedTreeRequest(), () => HasSelectedItem);
            DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedItem(), () => HasSelectedItem);
            DeleteItemCommand = new RelayCommand<TreeViewItemModel>(async (item) => await DeleteSpecificItem(item));
            RenameItemCommand = new RelayCommand<TreeViewItemModel>((item) => StartRenaming(item));
            SaveRenameCommand = new RelayCommand<TreeViewItemModel>(async (item) => await SaveRename(item));
            CancelRenameCommand = new RelayCommand<TreeViewItemModel>((item) => CancelRename(item));
            CopyItemCommand = new RelayCommand<TreeViewItemModel>(async (item) => await CopyItem(item));
            NewRequestCommand = new RelayCommand(() => CreateNewRequest());
            NewFolderCommand = new RelayCommand(async () => await CreateNewFolder());
            AddHeaderCommand = new RelayCommand(AddHeader);
            RemoveHeaderCommand = new RelayCommand<DataField>(RemoveHeader);
            AddPayloadFieldCommand = new RelayCommand(AddPayloadField);
            RemovePayloadFieldCommand = new RelayCommand<DataField>(RemovePayloadField);
            AddResponseHeaderCommand = new RelayCommand(AddResponseHeader);
            RemoveResponseHeaderCommand = new RelayCommand<DataField>(RemoveResponseHeader);
            AddResponsePayloadFieldCommand = new RelayCommand(AddResponsePayloadField);
            RemoveResponsePayloadFieldCommand = new RelayCommand<DataField>(RemoveResponsePayloadField);

            // Keep old commands for backward compatibility
            AddResponseFieldCommand = new RelayCommand(AddResponsePayloadField);
            RemoveResponseFieldCommand = new RelayCommand<DataField>(RemoveResponsePayloadField);

            // Load saved requests on startup
            _ = LoadSavedRequests();
        }

        public UdpRequest CurrentRequest
        {
            get => _currentRequest;
            set
            {
                _currentRequest = value;
                OnPropertyChanged(nameof(CurrentRequest));
            }
        }

        public ResponseSchema ResponseSchema
        {
            get => _responseSchema;
            set
            {
                _responseSchema = value;
                OnPropertyChanged(nameof(ResponseSchema));
            }
        }

        public UdpResponse? LastResponse
        {
            get => _lastResponse;
            set
            {
                _lastResponse = value;
                OnPropertyChanged(nameof(LastResponse));
                UpdateResponseText();
            }
        }

        public bool IsSending
        {
            get => _isSending;
            set
            {
                _isSending = value;
                OnPropertyChanged(nameof(IsSending));
                ((RelayCommand)SendCommand).RaiseCanExecuteChanged();
            }
        }

        public string ResponseText
        {
            get => _responseText;
            set
            {
                _responseText = value;
                OnPropertyChanged(nameof(ResponseText));
            }
        }

        public ObservableCollection<SavedRequest> SavedRequests
        {
            get => _savedRequests;
            set
            {
                _savedRequests = value;
                OnPropertyChanged(nameof(SavedRequests));
            }
        }

        public SavedRequest? SelectedSavedRequest
        {
            get => _selectedSavedRequest;
            set
            {
                _selectedSavedRequest = value;
                OnPropertyChanged(nameof(SelectedSavedRequest));
                ((RelayCommand)LoadSelectedCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteSelectedCommand).RaiseCanExecuteChanged();

                // 선택된 요청이 있으면 자동으로 로드
                if (_selectedSavedRequest != null)
                {
                    LoadSelectedRequest();
                }
            }
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

                // Update HasSelectedItem and commands
                HasSelectedItem = _selectedTreeItem != null;

                // Auto-load request if it's a request type
                if (_selectedTreeItem?.ItemType == TreeViewItemType.Request && _selectedTreeItem.Tag is SavedRequest request)
                {
                    SelectedSavedRequest = request;
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
            }
        }

        public int HeaderBytes
        {
            get => CalculateBytes(CurrentRequest.Headers);
        }

        public int PayloadBytes
        {
            get => CalculateBytes(CurrentRequest.Payload);
        }

        public string HeaderBytesText => $"Total: {HeaderBytes} bytes";
        public string PayloadBytesText => $"Total: {PayloadBytes} bytes";

        public int ResponseHeaderBytes
        {
            get => CalculateBytes(ResponseSchema.Headers);
        }

        public int ResponsePayloadBytes
        {
            get => CalculateBytes(ResponseSchema.Payload);
        }

        public string ResponseHeaderBytesText => $"Total: {ResponseHeaderBytes} bytes";
        public string ResponsePayloadBytesText => $"Total: {ResponsePayloadBytes} bytes";

        public string CurrentLoadedRequestName => _currentLoadedRequest?.Name ?? "새 요청";

        public ICommand SendCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand LoadSelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand NewRequestCommand { get; }
        public ICommand AddHeaderCommand { get; }
        public ICommand RemoveHeaderCommand { get; }
        public ICommand AddPayloadFieldCommand { get; }
        public ICommand RemovePayloadFieldCommand { get; }
        public ICommand AddResponseHeaderCommand { get; }
        public ICommand RemoveResponseHeaderCommand { get; }
        public ICommand AddResponsePayloadFieldCommand { get; }
        public ICommand RemoveResponsePayloadFieldCommand { get; }
        public ICommand AddResponseFieldCommand { get; }
        public ICommand RemoveResponseFieldCommand { get; }
        public ICommand LoadSelectedRequestCommand { get; }
        public ICommand NewFolderCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand RenameItemCommand { get; }
        public ICommand SaveRenameCommand { get; }
        public ICommand CancelRenameCommand { get; }
        public ICommand CopyItemCommand { get; }

        private async Task SendRequest()
        {
            IsSending = true;
            try
            {
                LastResponse = await _udpClientService.SendRequestAsync(CurrentRequest, ResponseSchema);
            }
            finally
            {
                IsSending = false;
            }
        }

        private async Task SaveRequest()
        {
            IsSending = true;
            try
            {
                if (_currentLoadedRequest != null)
                {
                    // 기존 요청 업데이트
                    _currentLoadedRequest.IpAddress = CurrentRequest.IpAddress;
                    _currentLoadedRequest.Port = CurrentRequest.Port;
                    _currentLoadedRequest.RequestSchemaJson = _databaseService.SerializeDataFields(CurrentRequest.Headers) +
                                           "|" + _databaseService.SerializeDataFields(CurrentRequest.Payload);
                    _currentLoadedRequest.ResponseSchemaJson = _databaseService.SerializeDataFields(ResponseSchema.Headers) +
                                            "|" + _databaseService.SerializeDataFields(ResponseSchema.Payload);

                    await _databaseService.SaveRequestAsync(_currentLoadedRequest);
                    MessageBox.Show($"요청 '{_currentLoadedRequest.Name}'이 업데이트되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 저장된 요청 목록 새로고침
                    await LoadSavedRequests();
                }
                else
                {
                    // 새로운 요청 저장
                    var folders = await _databaseService.GetAllFoldersAsync();
                    var dialog = new SaveRequestDialog(folders);
                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.RequestName))
                    {
                        var savedRequest = new SavedRequest
                        {
                            Name = dialog.RequestName,
                            IpAddress = CurrentRequest.IpAddress,
                            Port = CurrentRequest.Port,
                            FolderId = dialog.SelectedFolderId,
                            RequestSchemaJson = _databaseService.SerializeDataFields(CurrentRequest.Headers) +
                                               "|" + _databaseService.SerializeDataFields(CurrentRequest.Payload),
                            ResponseSchemaJson = _databaseService.SerializeDataFields(ResponseSchema.Headers) +
                                                "|" + _databaseService.SerializeDataFields(ResponseSchema.Payload)
                        };

                        await _databaseService.SaveRequestAsync(savedRequest);
                        _currentLoadedRequest = savedRequest; // 저장 후 현재 로드된 요청으로 설정
                        OnPropertyChanged(nameof(CurrentLoadedRequestName));
                        MessageBox.Show($"요청 '{dialog.RequestName}'이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                        // 저장된 요청 목록 새로고침
                        await LoadSavedRequests();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSending = false;
            }
        }

        private async Task LoadSavedRequests()
        {
            try
            {
                SavedRequests = await _databaseService.GetAllRequestsAsync();
                await BuildTreeView(); // TreeView도 함께 업데이트
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장된 요청을 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSelectedRequest()
        {
            if (SelectedSavedRequest == null)
                return;

            try
            {
                // IP와 Port 설정
                CurrentRequest.IpAddress = SelectedSavedRequest.IpAddress;
                CurrentRequest.Port = SelectedSavedRequest.Port;

                // Request Schema 복원
                var requestSchemaParts = SelectedSavedRequest.RequestSchemaJson.Split('|');
                if (requestSchemaParts.Length >= 2)
                {
                    // Headers 복원
                    var headersJson = requestSchemaParts[0];
                    var payloadJson = requestSchemaParts[1];

                    CurrentRequest.Headers.Clear();
                    var headers = _databaseService.DeserializeDataFields(headersJson);
                    foreach (var header in headers)
                    {
                        header.PropertyChanged += OnDataFieldPropertyChanged;
                        CurrentRequest.Headers.Add(header);
                    }

                    CurrentRequest.Payload.Clear();
                    var payload = _databaseService.DeserializeDataFields(payloadJson);
                    foreach (var field in payload)
                    {
                        field.PropertyChanged += OnDataFieldPropertyChanged;
                        CurrentRequest.Payload.Add(field);
                    }
                }

                // Response Schema 복원
                if (!string.IsNullOrEmpty(SelectedSavedRequest.ResponseSchemaJson))
                {
                    var responseSchemaParts = SelectedSavedRequest.ResponseSchemaJson.Split('|');
                    if (responseSchemaParts.Length >= 2)
                    {
                        var responseHeadersJson = responseSchemaParts[0];
                        var responsePayloadJson = responseSchemaParts[1];

                        ResponseSchema.Headers.Clear();
                        var responseHeaders = _databaseService.DeserializeDataFields(responseHeadersJson);
                        foreach (var header in responseHeaders)
                        {
                            header.PropertyChanged += OnResponseDataFieldPropertyChanged;
                            ResponseSchema.Headers.Add(header);
                        }

                        ResponseSchema.Payload.Clear();
                        var responsePayload = _databaseService.DeserializeDataFields(responsePayloadJson);
                        foreach (var field in responsePayload)
                        {
                            field.PropertyChanged += OnResponseDataFieldPropertyChanged;
                            ResponseSchema.Payload.Add(field);
                        }
                    }
                }

                // 현재 로드된 요청으로 설정
                _currentLoadedRequest = SelectedSavedRequest;
                OnPropertyChanged(nameof(CurrentLoadedRequestName));

                NotifyBytesChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"요청 로드 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateNewRequest()
        {
            try
            {
                // 새로운 요청 생성
                _currentLoadedRequest = null; // 현재 로드된 요청 초기화
                OnPropertyChanged(nameof(CurrentLoadedRequestName));
                SelectedSavedRequest = null; // 선택된 요청 초기화

                // IP와 Port 초기화
                CurrentRequest.IpAddress = "127.0.0.1";
                CurrentRequest.Port = 8080;

                // Headers 초기화
                CurrentRequest.Headers.Clear();
                var defaultHeader = new DataField { Name = "Header1", Type = DataType.Byte, Value = "0" };
                defaultHeader.PropertyChanged += OnDataFieldPropertyChanged;
                CurrentRequest.Headers.Add(defaultHeader);

                // Payload 초기화
                CurrentRequest.Payload.Clear();
                var defaultPayload = new DataField { Name = "Field1", Type = DataType.Byte, Value = "0" };
                defaultPayload.PropertyChanged += OnDataFieldPropertyChanged;
                CurrentRequest.Payload.Add(defaultPayload);

                // Response Schema 초기화
                ResponseSchema.Headers.Clear();
                var defaultResponseHeader = new DataField { Name = "ResponseHeader1", Type = DataType.Byte };
                defaultResponseHeader.PropertyChanged += OnResponseDataFieldPropertyChanged;
                ResponseSchema.Headers.Add(defaultResponseHeader);

                ResponseSchema.Payload.Clear();
                var defaultResponsePayload = new DataField { Name = "ResponseField1", Type = DataType.Byte };
                defaultResponsePayload.PropertyChanged += OnResponseDataFieldPropertyChanged;
                ResponseSchema.Payload.Add(defaultResponsePayload);

                // Response 초기화
                LastResponse = null;

                NotifyBytesChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"새 요청 생성 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteSelectedRequest()
        {
            if (SelectedSavedRequest == null)
                return;

            var result = MessageBox.Show($"'{SelectedSavedRequest.Name}' 요청을 삭제하시겠습니까?",
                                       "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _databaseService.DeleteRequestAsync(SelectedSavedRequest.Id);
                    await LoadSavedRequests();
                    SelectedSavedRequest = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddHeader()
        {
            var newHeader = new DataField { Name = $"Header{CurrentRequest.Headers.Count + 1}", Type = DataType.Byte, Value = "0" };
            newHeader.PropertyChanged += OnDataFieldPropertyChanged;
            CurrentRequest.Headers.Add(newHeader);
            NotifyBytesChanged();
        }

        private void RemoveHeader(DataField? header)
        {
            if (header != null)
            {
                header.PropertyChanged -= OnDataFieldPropertyChanged;
                CurrentRequest.Headers.Remove(header);
                NotifyBytesChanged();
            }
        }

        private void AddPayloadField()
        {
            var newField = new DataField { Name = $"Field{CurrentRequest.Payload.Count + 1}", Type = DataType.Int, Value = "0" };
            newField.PropertyChanged += OnDataFieldPropertyChanged;
            CurrentRequest.Payload.Add(newField);
            NotifyBytesChanged();
        }

        private void RemovePayloadField(DataField? field)
        {
            if (field != null)
            {
                field.PropertyChanged -= OnDataFieldPropertyChanged;
                CurrentRequest.Payload.Remove(field);
                NotifyBytesChanged();
            }
        }

        private void AddResponseField()
        {
            ResponseSchema.Payload.Add(new DataField { Name = $"Response{ResponseSchema.Payload.Count + 1}", Type = DataType.Int });
        }

        private void RemoveResponseField(DataField? field)
        {
            if (field != null)
                ResponseSchema.Payload.Remove(field);
        }

        private void AddResponseHeader()
        {
            var newHeader = new DataField { Name = $"ResponseHeader{ResponseSchema.Headers.Count + 1}", Type = DataType.Byte };
            newHeader.PropertyChanged += OnResponseDataFieldPropertyChanged;
            ResponseSchema.Headers.Add(newHeader);
            OnPropertyChanged(nameof(ResponseHeaderBytesText));
        }

        private void RemoveResponseHeader(DataField? header)
        {
            if (header != null)
            {
                ResponseSchema.Headers.Remove(header);
                OnPropertyChanged(nameof(ResponseHeaderBytesText));
            }
        }

        private void AddResponsePayloadField()
        {
            var newField = new DataField { Name = $"ResponseField{ResponseSchema.Payload.Count + 1}", Type = DataType.Int };
            newField.PropertyChanged += OnResponseDataFieldPropertyChanged;
            ResponseSchema.Payload.Add(newField);
            OnPropertyChanged(nameof(ResponsePayloadBytesText));
        }

        private void RemoveResponsePayloadField(DataField? field)
        {
            if (field != null)
            {
                ResponseSchema.Payload.Remove(field);
                OnPropertyChanged(nameof(ResponsePayloadBytesText));
            }
        }

        private void UpdateResponseText()
        {
            if (LastResponse == null)
            {
                ResponseText = "";
                return;
            }

            var text = $"Status: {LastResponse.Status}\n";
            text += $"Received At: {LastResponse.ReceivedAt:yyyy-MM-dd HH:mm:ss}\n";
            text += $"Raw Data Length: {LastResponse.RawData.Length} bytes\n";
            text += $"Raw Data (Hex): {Convert.ToHexString(LastResponse.RawData)}\n\n";

            if (LastResponse.ParsedData.Any())
            {
                text += "Parsed Data:\n";
                foreach (var kvp in LastResponse.ParsedData)
                {
                    text += $"  {kvp.Key}: {kvp.Value}\n";
                }
            }

            ResponseText = text;
        }

        private int CalculateBytes(ObservableCollection<DataField> fields)
        {
            int totalBytes = 0;
            foreach (var field in fields)
            {
                totalBytes += field.Type switch
                {
                    DataType.Byte => 1,
                    DataType.UInt16 => 2,
                    DataType.Int => 4,
                    DataType.UInt => 4,
                    DataType.Float => 4,
                    DataType.Padding => field.PaddingSize,
                    _ => 0
                };
            }
            return totalBytes;
        }

        private void NotifyBytesChanged()
        {
            OnPropertyChanged(nameof(HeaderBytes));
            OnPropertyChanged(nameof(PayloadBytes));
            OnPropertyChanged(nameof(HeaderBytesText));
            OnPropertyChanged(nameof(PayloadBytesText));
        }

        private void OnDataFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataField.Type) ||
                e.PropertyName == nameof(DataField.Value) ||
                e.PropertyName == nameof(DataField.PaddingSize))
            {
                NotifyBytesChanged();
            }
        }

        private void OnResponseDataFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataField.Type) ||
                e.PropertyName == nameof(DataField.Value) ||
                e.PropertyName == nameof(DataField.PaddingSize))
            {
                OnPropertyChanged(nameof(ResponseHeaderBytes));
                OnPropertyChanged(nameof(ResponsePayloadBytes));
                OnPropertyChanged(nameof(ResponseHeaderBytesText));
                OnPropertyChanged(nameof(ResponsePayloadBytesText));
            }
        }

        // Tree View Methods
        private void LoadSelectedTreeRequest()
        {
            if (SelectedTreeItem?.ItemType == TreeViewItemType.Request && SelectedTreeItem.Tag is SavedRequest request)
            {
                SelectedSavedRequest = request;
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
                    // 삭제하려는 Request가 현재 로드된 Request인지 확인
                    bool isCurrentlyLoaded = _currentLoadedRequest?.Id == request.Id;

                    success = await _databaseService.DeleteRequestAsync(request.Id);

                    // 삭제 성공 시 현재 로드된 Request였다면 새 Request로 초기화
                    if (success && isCurrentlyLoaded)
                    {
                        CreateNewRequest(); // 새로운 Request로 RequestPanel 초기화
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
                    // 복사된 요청의 이름 생성 (Copy 추가)
                    string copyName = GenerateCopyName(originalRequest.Name);

                    // 새로운 SavedRequest 객체 생성
                    var copiedRequest = new SavedRequest
                    {
                        Name = copyName,
                        IpAddress = originalRequest.IpAddress,
                        Port = originalRequest.Port,
                        RequestSchemaJson = originalRequest.RequestSchemaJson,
                        ResponseSchemaJson = originalRequest.ResponseSchemaJson,
                        FolderId = originalRequest.FolderId
                    };

                    // 데이터베이스에 저장 (SaveRequestAsync는 int를 반환)
                    int newId = await _databaseService.SaveRequestAsync(copiedRequest);

                    if (newId > 0)
                    {
                        // TreeView 새로고침
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
            // "Copy"를 뒤에 추가하는 로직
            return originalName + " Copy";
        }

        private void StartRenaming(TreeViewItemModel item)
        {
            // 다른 편집 중인 항목들 종료
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
                await LoadSavedRequests(); // 트리 새로고침
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이름 변경 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelRename(TreeViewItemModel item)
        {
            item.IsEditing = false;

            // 원래 이름으로 복원
            if (item.Tag is SavedRequest request)
            {
                item.Name = request.Name;
            }
            else if (item.Tag is Folder folder)
            {
                item.Name = folder.Name;
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
