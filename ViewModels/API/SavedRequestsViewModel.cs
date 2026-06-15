using System.Collections.ObjectModel;
using MMG.Models;
using MMG.Services;
using MMG.Views.Common;
using MMG.ViewModels.Spec;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MMG.ViewModels.API
{
    public partial class SavedRequestsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private bool _isHandlingSelectionChange;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadSelectedCommand), nameof(DeleteSelectedCommand))]
        private SavedRequest? selectedSavedRequest;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentLoadedRequestName), nameof(CanSave))]
        private SavedRequest? currentLoadedRequest;

        [ObservableProperty]
        private ObservableCollection<SavedRequest> savedRequests = new();

        public SavedRequestsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _ = LoadSavedRequests();
        }

        partial void OnSelectedSavedRequestChanged(SavedRequest? value)
        {
            // 선택된 요청이 있으면 자동으로 로드
            if (value != null)
            {
                _ = HandleSelectedRequestChangedAsync(value);
            }
        }

        public string CurrentLoadedRequestName => CurrentLoadedRequest?.Name ?? "새 요청";
        public bool CanSave => true; // 항상 저장 가능하도록 설정

        public event EventHandler<SavedRequest>? RequestLoaded;
        public event EventHandler? NewRequestCreated;
        public event EventHandler<SaveRequestEventArgs>? SaveRequested;

        #region Commands

        [RelayCommand]
        public async Task SaveRequest()
        {
            try
            {
                var savedRequest = await SaveCurrentLoadedRequestAsync(showSuccessMessage: true, allowCreateDialog: true);
                if (savedRequest != null && CurrentLoadedRequest == null)
                {
                    CurrentLoadedRequest = savedRequest;
                }

                await LoadSavedRequests();
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"저장 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        [RelayCommand]
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

        [RelayCommand(CanExecute = nameof(CanLoadSelected))]
        private void LoadSelected()
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

        private bool CanLoadSelected() => SelectedSavedRequest != null;

        [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
        private async Task DeleteSelected()
        {
            if (SelectedSavedRequest == null)
                return;

            var result = MessageBox.Show(
                $"'{SelectedSavedRequest.Name}' 요청을 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _databaseService.DeleteRequestAsync(SelectedSavedRequest.Id);
                    await LoadSavedRequests();

                    if (CurrentLoadedRequest?.Id == SelectedSavedRequest.Id)
                    {
                        CurrentLoadedRequest = null;
                        NewRequestCreated?.Invoke(this, EventArgs.Empty);
                    }
                    SelectedSavedRequest = null;
                }
                catch (Exception ex)
                {
                    ModernMessageDialog.ShowError($"요청 삭제 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }

        private bool CanDeleteSelected() => SelectedSavedRequest != null;

        [RelayCommand]
        public void NewRequest()
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

        #endregion

        #region Public Methods

        public async Task<bool> AutoSaveCurrentRequestAsync()
        {
            var savedRequest = await SaveCurrentLoadedRequestAsync(showSuccessMessage: false, allowCreateDialog: false);
            return savedRequest != null;
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
                        WaitForResponse = true,
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

        #endregion

        #region Private Methods

        private async Task HandleSelectedRequestChangedAsync(SavedRequest selectedRequest)
        {
            if (_isHandlingSelectionChange)
            {
                return;
            }

            if (CurrentLoadedRequest?.Id == selectedRequest.Id)
            {
                LoadSelected();
                return;
            }

            try
            {
                _isHandlingSelectionChange = true;

                if (CurrentLoadedRequest != null)
                {
                    await SaveCurrentLoadedRequestAsync(showSuccessMessage: false, allowCreateDialog: false);
                }

                LoadSelected();
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"요청 전환 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
            finally
            {
                _isHandlingSelectionChange = false;
            }
        }

        private async Task<SavedRequest?> SaveCurrentLoadedRequestAsync(bool showSuccessMessage, bool allowCreateDialog)
        {
            var args = new SaveRequestEventArgs();
            SaveRequested?.Invoke(this, args);

            if (args.CurrentRequest == null || args.ResponseSchema == null)
            {
                ModernMessageDialog.ShowError("저장할 요청 데이터가 없습니다.", "오류");
                return null;
            }

            if (CurrentLoadedRequest != null)
            {
                ApplyRequestData(CurrentLoadedRequest, args.CurrentRequest, args.ResponseSchema);
                await _databaseService.SaveRequestAsync(CurrentLoadedRequest);

                if (showSuccessMessage)
                {
                    ModernMessageDialog.ShowSuccess($"요청 '{CurrentLoadedRequest.Name}'이 업데이트되었습니다.", "저장 완료");
                }

                return CurrentLoadedRequest;
            }

            if (!allowCreateDialog)
            {
                return null;
            }

            var folders = await _databaseService.GetAllFoldersAsync();
            var dialog = new SaveRequestDialog(folders);
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.RequestName))
            {
                return null;
            }

            var savedRequest = new SavedRequest
            {
                Name = dialog.RequestName,
                FolderId = dialog.SelectedFolderId
            };

            ApplyRequestData(savedRequest, args.CurrentRequest, args.ResponseSchema);
            await _databaseService.SaveRequestAsync(savedRequest);

            if (showSuccessMessage)
            {
                ModernMessageDialog.ShowSuccess($"요청 '{dialog.RequestName}'이 저장되었습니다.", "저장 완료");
            }

            return savedRequest;
        }

        private void ApplyRequestData(SavedRequest targetRequest, UdpRequest currentRequest, ResponseSchema responseSchema)
        {
            targetRequest.IpAddress = currentRequest.IpAddress;
            targetRequest.Port = currentRequest.Port;
            targetRequest.IsBigEndian = currentRequest.IsBigEndian;
            targetRequest.WaitForResponse = currentRequest.WaitForResponse;
            targetRequest.UseCustomLocalPort = currentRequest.UseCustomLocalPort;
            targetRequest.CustomLocalPort = currentRequest.CustomLocalPort;
            targetRequest.RequestSchemaJson = _databaseService.SerializeDataFields(currentRequest.Headers) +
                                      "|" + _databaseService.SerializeDataFields(currentRequest.Payload);
            targetRequest.ResponseSchemaJson = _databaseService.SerializeDataFields(responseSchema.Headers) +
                                       "|" + _databaseService.SerializeDataFields(responseSchema.Payload);
        }

        #endregion
    }

    public class SaveRequestEventArgs : EventArgs
    {
        public UdpRequest? CurrentRequest { get; set; }
        public ResponseSchema? ResponseSchema { get; set; }
    }
}