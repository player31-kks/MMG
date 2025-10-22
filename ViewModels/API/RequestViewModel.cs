using System.ComponentModel;
using System.Windows.Input;
using MMG.Models;
using MMG.Services;
using MMG.ViewModels.Base;
using System.Collections.ObjectModel;

namespace MMG.ViewModels.API
{
    public class RequestViewModel : ViewModelBase
    {
        private readonly UdpClientService _udpClientService;
        private UdpRequest _currentRequest;
        private bool _isSending;

        public RequestViewModel()
        {
            _udpClientService = new UdpClientService();
            _currentRequest = new UdpRequest
            {
                IpAddress = "127.0.0.1",
                Port = 8080
            };

            SendCommand = new RelayCommand(async () => await SendRequest(), () => !IsSending);
            AddHeaderCommand = new RelayCommand<object>(AddHeader);
            RemoveHeaderCommand = new RelayCommand<DataField>(RemoveHeader);
            AddPayloadFieldCommand = new RelayCommand<object>(AddPayloadField);
            RemovePayloadFieldCommand = new RelayCommand<DataField>(RemovePayloadField);
            ClearAllHeadersCommand = new RelayCommand(ClearAllHeaders);
            ClearAllPayloadFieldsCommand = new RelayCommand(ClearAllPayloadFields);

            InitializeDefaultFields();
        }

        public UdpRequest CurrentRequest
        {
            get => _currentRequest;
            set
            {
                _currentRequest = value;
                OnPropertyChanged(nameof(CurrentRequest));
                NotifyBytesChanged();
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

        public int HeaderBytes => CalculateBytes(CurrentRequest.Headers);
        public int PayloadBytes => CalculateBytes(CurrentRequest.Payload);
        public string HeaderBytesText => $"Total: {HeaderBytes} bytes";
        public string PayloadBytesText => $"Total: {PayloadBytes} bytes";

        public ICommand SendCommand { get; }
        public ICommand AddHeaderCommand { get; }
        public ICommand RemoveHeaderCommand { get; }
        public ICommand AddPayloadFieldCommand { get; }
        public ICommand RemovePayloadFieldCommand { get; }
        public ICommand ClearAllHeadersCommand { get; }
        public ICommand ClearAllPayloadFieldsCommand { get; }

        public event EventHandler<UdpResponse>? ResponseReceived;
        public event EventHandler<ResponseSchemaRequestEventArgs>? ResponseSchemaRequested;

        private void InitializeDefaultFields()
        {
            var defaultHeader = new DataField { Name = "Header1", Type = DataType.Byte, Value = "0" };
            defaultHeader.PropertyChanged += OnDataFieldPropertyChanged;
            CurrentRequest.Headers.Add(defaultHeader);

            var defaultPayload = new DataField { Name = "Field1", Type = DataType.Byte, Value = "0" };
            defaultPayload.PropertyChanged += OnDataFieldPropertyChanged;
            CurrentRequest.Payload.Add(defaultPayload);
        }

        private async Task SendRequest()
        {
            IsSending = true;
            try
            {
                // ResponseSchema를 외부에서 요청
                var args = new ResponseSchemaRequestEventArgs();
                ResponseSchemaRequested?.Invoke(this, args);

                var responseSchema = args.ResponseSchema ?? new ResponseSchema();
                var response = await _udpClientService.SendRequestAsync(CurrentRequest, responseSchema);
                ResponseReceived?.Invoke(this, response);
            }
            finally
            {
                IsSending = false;
            }
        }

        public void LoadRequest(SavedRequest savedRequest)
        {
            CurrentRequest.IpAddress = savedRequest.IpAddress;
            CurrentRequest.Port = savedRequest.Port;

            var requestSchemaParts = savedRequest.RequestSchemaJson.Split('|');
            if (requestSchemaParts.Length >= 2)
            {
                var databaseService = new DatabaseService();

                // Headers 복원
                CurrentRequest.Headers.Clear();
                var headers = databaseService.DeserializeDataFields(requestSchemaParts[0]);
                foreach (var header in headers)
                {
                    header.PropertyChanged += OnDataFieldPropertyChanged;
                    CurrentRequest.Headers.Add(header);
                }

                // Payload 복원
                CurrentRequest.Payload.Clear();
                var payload = databaseService.DeserializeDataFields(requestSchemaParts[1]);
                foreach (var field in payload)
                {
                    field.PropertyChanged += OnDataFieldPropertyChanged;
                    CurrentRequest.Payload.Add(field);
                }
            }

            NotifyBytesChanged();
        }

        public void CreateNewRequest()
        {
            CurrentRequest.IpAddress = "127.0.0.1";
            CurrentRequest.Port = 8080;

            // Clear existing fields
            foreach (var header in CurrentRequest.Headers)
            {
                header.PropertyChanged -= OnDataFieldPropertyChanged;
            }
            foreach (var field in CurrentRequest.Payload)
            {
                field.PropertyChanged -= OnDataFieldPropertyChanged;
            }

            CurrentRequest.Headers.Clear();
            CurrentRequest.Payload.Clear();

            // Add default fields
            InitializeDefaultFields();
            NotifyBytesChanged();
        }

        private void AddHeader(object? selectedIndexObj = null)
        {
            var newHeader = new DataField { Name = $"Header{CurrentRequest.Headers.Count + 1}", Type = DataType.Byte, Value = "0" };
            newHeader.PropertyChanged += OnDataFieldPropertyChanged;

            if (selectedIndexObj is int selectedIndex && selectedIndex >= 0 && selectedIndex < CurrentRequest.Headers.Count)
            {
                CurrentRequest.Headers.Insert(selectedIndex + 1, newHeader);
            }
            else
            {
                CurrentRequest.Headers.Add(newHeader);
            }

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

        private void AddPayloadField(object? selectedIndexObj = null)
        {
            var newField = new DataField { Name = $"Field{CurrentRequest.Payload.Count + 1}", Type = DataType.Int, Value = "0" };
            newField.PropertyChanged += OnDataFieldPropertyChanged;

            if (selectedIndexObj is int selectedIndex && selectedIndex >= 0 && selectedIndex < CurrentRequest.Payload.Count)
            {
                CurrentRequest.Payload.Insert(selectedIndex + 1, newField);
            }
            else
            {
                CurrentRequest.Payload.Add(newField);
            }

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

        private void ClearAllHeaders()
        {
            var result = System.Windows.MessageBox.Show(
                "모든 Header 필드를 삭제하시겠습니까?",
                "확인",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                foreach (var header in CurrentRequest.Headers)
                {
                    header.PropertyChanged -= OnDataFieldPropertyChanged;
                }
                CurrentRequest.Headers.Clear();
                NotifyBytesChanged();
            }
        }

        private void ClearAllPayloadFields()
        {
            var result = System.Windows.MessageBox.Show(
                "모든 Payload 필드를 삭제하시겠습니까?",
                "확인",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                foreach (var field in CurrentRequest.Payload)
                {
                    field.PropertyChanged -= OnDataFieldPropertyChanged;
                }
                CurrentRequest.Payload.Clear();
                NotifyBytesChanged();
            }
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
    }

    public class ResponseSchemaRequestEventArgs : EventArgs
    {
        public ResponseSchema? ResponseSchema { get; set; }
    }
}