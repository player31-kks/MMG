using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
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
            _responseSchema.Headers.Add(new DataField { Name = "ResponseHeader1", Type = DataType.Byte });
            _responseSchema.Payload.Add(new DataField { Name = "ResponseField1", Type = DataType.Byte });

            SendCommand = new RelayCommand(async () => await SendRequest(), () => !IsSending);
            SaveCommand = new RelayCommand(async () => await SaveRequest(), () => !IsSending);
            RefreshCommand = new RelayCommand(async () => await LoadSavedRequests());
            LoadSelectedCommand = new RelayCommand(() => LoadSelectedRequest(), () => SelectedSavedRequest != null);
            DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedRequest(), () => SelectedSavedRequest != null);
            NewRequestCommand = new RelayCommand(() => CreateNewRequest());
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
                    var dialog = new SaveRequestDialog();
                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.RequestName))
                    {
                        var savedRequest = new SavedRequest
                        {
                            Name = dialog.RequestName,
                            IpAddress = CurrentRequest.IpAddress,
                            Port = CurrentRequest.Port,
                            RequestSchemaJson = _databaseService.SerializeDataFields(CurrentRequest.Headers) +
                                               "|" + _databaseService.SerializeDataFields(CurrentRequest.Payload),
                            ResponseSchemaJson = _databaseService.SerializeDataFields(ResponseSchema.Headers) +
                                                "|" + _databaseService.SerializeDataFields(ResponseSchema.Payload)
                        };

                        await _databaseService.SaveRequestAsync(savedRequest);
                        _currentLoadedRequest = savedRequest; // 저장 후 현재 로드된 요청으로 설정
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
                            ResponseSchema.Headers.Add(header);
                        }

                        ResponseSchema.Payload.Clear();
                        var responsePayload = _databaseService.DeserializeDataFields(responsePayloadJson);
                        foreach (var field in responsePayload)
                        {
                            ResponseSchema.Payload.Add(field);
                        }
                    }
                }

                // 현재 로드된 요청으로 설정
                _currentLoadedRequest = SelectedSavedRequest;

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
                ResponseSchema.Headers.Add(new DataField { Name = "ResponseHeader1", Type = DataType.Byte });

                ResponseSchema.Payload.Clear();
                ResponseSchema.Payload.Add(new DataField { Name = "ResponseField1", Type = DataType.Byte });

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
            ResponseSchema.Headers.Add(new DataField { Name = $"ResponseHeader{ResponseSchema.Headers.Count + 1}", Type = DataType.Byte });
        }

        private void RemoveResponseHeader(DataField? header)
        {
            if (header != null)
                ResponseSchema.Headers.Remove(header);
        }

        private void AddResponsePayloadField()
        {
            ResponseSchema.Payload.Add(new DataField { Name = $"ResponseField{ResponseSchema.Payload.Count + 1}", Type = DataType.Int });
        }

        private void RemoveResponsePayloadField(DataField? field)
        {
            if (field != null)
                ResponseSchema.Payload.Remove(field);
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
