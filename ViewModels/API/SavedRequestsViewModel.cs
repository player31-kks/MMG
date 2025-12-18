using System.Collections.ObjectModel;
using System.Windows.Input;
using MMG.Models;
using MMG.Services;
using MMG.Views.Common;
using MMG.ViewModels.Base;
using MMG.ViewModels.Spec;
using System.Windows;

namespace MMG.ViewModels.API
{
    public class SavedRequestsViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<SavedRequest> _savedRequests = new();
        private SavedRequest? _selectedSavedRequest;
        private SavedRequest? _currentLoadedRequest;

        public SavedRequestsViewModel()
        {
            _databaseService = new DatabaseService();

            SaveCommand = new RelayCommand(async () => await SaveRequest());
            RefreshCommand = new RelayCommand(async () => await LoadSavedRequests());
            LoadSelectedCommand = new RelayCommand(() => LoadSelectedRequest(), () => SelectedSavedRequest != null);
            DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedRequest(), () => SelectedSavedRequest != null);
            NewRequestCommand = new RelayCommand(() => CreateNewRequest());

            _ = LoadSavedRequests();
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

        public SavedRequest? CurrentLoadedRequest
        {
            get => _currentLoadedRequest;
            set
            {
                _currentLoadedRequest = value;
                OnPropertyChanged(nameof(CurrentLoadedRequest));
                OnPropertyChanged(nameof(CurrentLoadedRequestName));
                OnPropertyChanged(nameof(CanSave));
                ((RelayCommand)SaveCommand).CanExecute(null);
            }
        }

        public string CurrentLoadedRequestName => _currentLoadedRequest?.Name ?? "새 요청";
        public bool CanSave => true; // 항상 저장 가능하도록 설정

        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand LoadSelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand NewRequestCommand { get; }

        public event EventHandler<SavedRequest>? RequestLoaded;
        public event EventHandler? NewRequestCreated;
        public event EventHandler<SaveRequestEventArgs>? SaveRequested;

        public async Task SaveRequest()
        {
            try
            {
                var args = new SaveRequestEventArgs();
                SaveRequested?.Invoke(this, args);

                if (args.CurrentRequest == null || args.ResponseSchema == null)
                {
                    ModernMessageDialog.ShowError("저장할 요청 데이터가 없습니다.", "오류");
                    return;
                }

                if (_currentLoadedRequest != null)
                {
                    // 기존 요청 업데이트
                    _currentLoadedRequest.IpAddress = args.CurrentRequest.IpAddress;
                    _currentLoadedRequest.Port = args.CurrentRequest.Port;
                    _currentLoadedRequest.IsBigEndian = args.CurrentRequest.IsBigEndian;
                    _currentLoadedRequest.RequestSchemaJson = _databaseService.SerializeDataFields(args.CurrentRequest.Headers) +
                                           "|" + _databaseService.SerializeDataFields(args.CurrentRequest.Payload);
                    _currentLoadedRequest.ResponseSchemaJson = _databaseService.SerializeDataFields(args.ResponseSchema.Headers) +
                                            "|" + _databaseService.SerializeDataFields(args.ResponseSchema.Payload);

                    await _databaseService.SaveRequestAsync(_currentLoadedRequest);
                    ModernMessageDialog.ShowSuccess($"요청 '{_currentLoadedRequest.Name}'이 업데이트되었습니다.", "저장 완료");
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
                            IpAddress = args.CurrentRequest.IpAddress,
                            Port = args.CurrentRequest.Port,
                            IsBigEndian = args.CurrentRequest.IsBigEndian,
                            FolderId = dialog.SelectedFolderId,
                            RequestSchemaJson = _databaseService.SerializeDataFields(args.CurrentRequest.Headers) +
                                               "|" + _databaseService.SerializeDataFields(args.CurrentRequest.Payload),
                            ResponseSchemaJson = _databaseService.SerializeDataFields(args.ResponseSchema.Headers) +
                                                "|" + _databaseService.SerializeDataFields(args.ResponseSchema.Payload)
                        };

                        await _databaseService.SaveRequestAsync(savedRequest);
                        CurrentLoadedRequest = savedRequest;
                        ModernMessageDialog.ShowSuccess($"요청 '{dialog.RequestName}'이 저장되었습니다.", "저장 완료");
                    }
                }

                await LoadSavedRequests();
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"저장 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        public async Task LoadSavedRequests()
        {
            try
            {
                SavedRequests = await _databaseService.GetAllRequestsAsync();
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"저장된 요청을 불러오는 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        private void LoadSelectedRequest()
        {
            if (SelectedSavedRequest == null)
                return;

            try
            {
                CurrentLoadedRequest = SelectedSavedRequest;
                RequestLoaded?.Invoke(this, SelectedSavedRequest);
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"요청 로드 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        public void CreateNewRequest()
        {
            try
            {
                CurrentLoadedRequest = null;
                SelectedSavedRequest = null;
                NewRequestCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"새 요청 생성 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        /// <summary>
        /// Spec에서 새 요청을 생성하고 저장
        /// </summary>
        public async Task SaveFromSpec(CreateApiRequestEventArgs args)
        {
            try
            {
                var folders = await _databaseService.GetAllFoldersAsync();
                var dialog = new SaveRequestDialog(folders, args.MessageId); // 메시지 ID를 기본 이름으로 설정

                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.RequestName))
                {
                    var savedRequest = new SavedRequest
                    {
                        Name = dialog.RequestName,
                        IpAddress = args.IpAddress,
                        Port = args.Port,
                        FolderId = dialog.SelectedFolderId,
                        RequestSchemaJson = _databaseService.SerializeDataFields(args.Headers) +
                                           "|" + _databaseService.SerializeDataFields(args.Payload),
                        ResponseSchemaJson = _databaseService.SerializeDataFields(args.ResponseHeaders) +
                                            "|" + _databaseService.SerializeDataFields(args.ResponsePayload)
                    };

                    await _databaseService.SaveRequestAsync(savedRequest);
                    CurrentLoadedRequest = savedRequest;

                    await LoadSavedRequests();

                    // 저장된 요청 로드
                    RequestLoaded?.Invoke(this, savedRequest);

                    ModernMessageDialog.ShowSuccess($"요청 '{dialog.RequestName}'이 저장되었습니다.", "저장 완료");
                }
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"Spec에서 저장 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        private async Task DeleteSelectedRequest()
        {
            if (SelectedSavedRequest == null)
                return;

            var result = ModernMessageDialog.ShowConfirm($"'{SelectedSavedRequest.Name}' 요청을 삭제하시겠습니까?",
                                       "삭제 확인");

            if (result == true)
            {
                try
                {
                    bool isCurrentlyLoaded = _currentLoadedRequest?.Id == SelectedSavedRequest.Id;

                    await _databaseService.DeleteRequestAsync(SelectedSavedRequest.Id);

                    if (isCurrentlyLoaded)
                    {
                        CreateNewRequest();
                    }

                    await LoadSavedRequests();
                    SelectedSavedRequest = null;
                }
                catch (Exception ex)
                {
                    ModernMessageDialog.ShowError($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }
    }

    public class SaveRequestEventArgs : EventArgs
    {
        public UdpRequest? CurrentRequest { get; set; }
        public ResponseSchema? ResponseSchema { get; set; }
    }
}