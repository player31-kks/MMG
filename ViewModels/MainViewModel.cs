using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using MMG.Models;
using MMG.Services;

namespace MMG.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly UdpClientService _udpClientService;
        private UdpRequest _currentRequest;
        private ResponseSchema _responseSchema;
        private UdpResponse? _lastResponse;
        private bool _isSending;
        private string _responseText = "";

        public MainViewModel()
        {
            _udpClientService = new UdpClientService();
            _currentRequest = new UdpRequest();
            _responseSchema = new ResponseSchema();

            // Initialize with some default fields
            var defaultHeader = new DataField { Name = "Header1", Type = DataType.Byte, Value = "0" };
            var defaultPayload = new DataField { Name = "Field1", Type = DataType.Int, Value = "0" };

            defaultHeader.PropertyChanged += OnDataFieldPropertyChanged;
            defaultPayload.PropertyChanged += OnDataFieldPropertyChanged;

            _currentRequest.Headers.Add(defaultHeader);
            _currentRequest.Payload.Add(defaultPayload);
            _responseSchema.Headers.Add(new DataField { Name = "ResponseHeader1", Type = DataType.Int });
            _responseSchema.Payload.Add(new DataField { Name = "ResponseField1", Type = DataType.Int });

            SendCommand = new RelayCommand(async () => await SendRequest(), () => !IsSending);
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
                    DataType.Int => 4,
                    DataType.UInt => 4,
                    DataType.Float => 4,
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
            if (e.PropertyName == nameof(DataField.Type))
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
