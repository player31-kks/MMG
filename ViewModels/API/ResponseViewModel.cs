using System.ComponentModel;
using System.Windows.Input;
using MMG.Models;
using MMG.Services;
using MMG.ViewModels.Base;
using System.Collections.ObjectModel;

namespace MMG.ViewModels.API
{
    public class ResponseViewModel : ViewModelBase
    {
        private ResponseSchema _responseSchema;
        private UdpResponse? _lastResponse;
        private string _responseText = "";

        public ResponseViewModel()
        {
            _responseSchema = new ResponseSchema();

            AddResponseHeaderCommand = new RelayCommand<object>(AddResponseHeader);
            RemoveResponseHeaderCommand = new RelayCommand<DataField>(RemoveResponseHeader);
            AddResponsePayloadFieldCommand = new RelayCommand<object>(AddResponsePayloadField);
            RemoveResponsePayloadFieldCommand = new RelayCommand<DataField>(RemoveResponsePayloadField);
            ClearAllResponseHeadersCommand = new RelayCommand(ClearAllResponseHeaders);
            ClearAllResponsePayloadFieldsCommand = new RelayCommand(ClearAllResponsePayloadFields);

            InitializeDefaultFields();
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

        public string ResponseText
        {
            get => _responseText;
            set
            {
                _responseText = value;
                OnPropertyChanged(nameof(ResponseText));
            }
        }

        public int ResponseHeaderBytes => CalculateBytes(ResponseSchema.Headers);
        public int ResponsePayloadBytes => CalculateBytes(ResponseSchema.Payload);
        public string ResponseHeaderBytesText => $"Total: {ResponseHeaderBytes} bytes";
        public string ResponsePayloadBytesText => $"Total: {ResponsePayloadBytes} bytes";

        public ICommand AddResponseHeaderCommand { get; }
        public ICommand RemoveResponseHeaderCommand { get; }
        public ICommand AddResponsePayloadFieldCommand { get; }
        public ICommand RemoveResponsePayloadFieldCommand { get; }
        public ICommand ClearAllResponseHeadersCommand { get; }
        public ICommand ClearAllResponsePayloadFieldsCommand { get; }

        private void InitializeDefaultFields()
        {
            var defaultResponseHeader = new DataField { Name = "ResponseHeader1", Type = DataType.Byte };
            defaultResponseHeader.PropertyChanged += OnResponseDataFieldPropertyChanged;
            ResponseSchema.Headers.Add(defaultResponseHeader);

            var defaultResponsePayload = new DataField { Name = "ResponseField1", Type = DataType.Byte };
            defaultResponsePayload.PropertyChanged += OnResponseDataFieldPropertyChanged;
            ResponseSchema.Payload.Add(defaultResponsePayload);
        }

        public void LoadResponseSchema(SavedRequest savedRequest)
        {
            if (!string.IsNullOrEmpty(savedRequest.ResponseSchemaJson))
            {
                var responseSchemaParts = savedRequest.ResponseSchemaJson.Split('|');
                if (responseSchemaParts.Length >= 2)
                {
                    var databaseService = new DatabaseService();
                    var responseHeadersJson = responseSchemaParts[0];
                    var responsePayloadJson = responseSchemaParts[1];

                    ResponseSchema.Headers.Clear();
                    var responseHeaders = databaseService.DeserializeDataFields(responseHeadersJson);
                    foreach (var header in responseHeaders)
                    {
                        header.PropertyChanged += OnResponseDataFieldPropertyChanged;
                        ResponseSchema.Headers.Add(header);
                    }

                    ResponseSchema.Payload.Clear();
                    var responsePayload = databaseService.DeserializeDataFields(responsePayloadJson);
                    foreach (var field in responsePayload)
                    {
                        field.PropertyChanged += OnResponseDataFieldPropertyChanged;
                        ResponseSchema.Payload.Add(field);
                    }
                }
            }

            NotifyBytesChanged();
        }

        public void CreateNewResponseSchema()
        {
            // Clear existing fields
            foreach (var header in ResponseSchema.Headers)
            {
                header.PropertyChanged -= OnResponseDataFieldPropertyChanged;
            }
            foreach (var field in ResponseSchema.Payload)
            {
                field.PropertyChanged -= OnResponseDataFieldPropertyChanged;
            }

            ResponseSchema.Headers.Clear();
            ResponseSchema.Payload.Clear();

            // Add default fields
            var defaultResponseHeader = new DataField { Name = "ResponseHeader1", Type = DataType.Byte };
            defaultResponseHeader.PropertyChanged += OnResponseDataFieldPropertyChanged;
            ResponseSchema.Headers.Add(defaultResponseHeader);

            var defaultResponsePayload = new DataField { Name = "ResponseField1", Type = DataType.Byte };
            defaultResponsePayload.PropertyChanged += OnResponseDataFieldPropertyChanged;
            ResponseSchema.Payload.Add(defaultResponsePayload);

            LastResponse = null;
            NotifyBytesChanged();
        }

        private void AddResponseHeader(object? selectedIndexObj = null)
        {
            var newHeader = new DataField { Name = $"ResponseHeader{ResponseSchema.Headers.Count + 1}", Type = DataType.Byte };
            newHeader.PropertyChanged += OnResponseDataFieldPropertyChanged;

            if (selectedIndexObj is int selectedIndex && selectedIndex >= 0 && selectedIndex < ResponseSchema.Headers.Count)
            {
                ResponseSchema.Headers.Insert(selectedIndex + 1, newHeader);
            }
            else
            {
                ResponseSchema.Headers.Add(newHeader);
            }

            NotifyBytesChanged();
        }

        private void RemoveResponseHeader(DataField? header)
        {
            if (header != null)
            {
                header.PropertyChanged -= OnResponseDataFieldPropertyChanged;
                ResponseSchema.Headers.Remove(header);
                NotifyBytesChanged();
            }
        }

        private void AddResponsePayloadField(object? selectedIndexObj = null)
        {
            var newField = new DataField { Name = $"ResponseField{ResponseSchema.Payload.Count + 1}", Type = DataType.Int };
            newField.PropertyChanged += OnResponseDataFieldPropertyChanged;

            if (selectedIndexObj is int selectedIndex && selectedIndex >= 0 && selectedIndex < ResponseSchema.Payload.Count)
            {
                ResponseSchema.Payload.Insert(selectedIndex + 1, newField);
            }
            else
            {
                ResponseSchema.Payload.Add(newField);
            }

            NotifyBytesChanged();
        }

        private void RemoveResponsePayloadField(DataField? field)
        {
            if (field != null)
            {
                field.PropertyChanged -= OnResponseDataFieldPropertyChanged;
                ResponseSchema.Payload.Remove(field);
                NotifyBytesChanged();
            }
        }

        private void ClearAllResponseHeaders()
        {
            var result = System.Windows.MessageBox.Show(
                "모든 Response Header 필드를 삭제하시겠습니까?",
                "확인",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                foreach (var header in ResponseSchema.Headers)
                {
                    header.PropertyChanged -= OnResponseDataFieldPropertyChanged;
                }
                ResponseSchema.Headers.Clear();
                NotifyBytesChanged();
            }
        }

        private void ClearAllResponsePayloadFields()
        {
            var result = System.Windows.MessageBox.Show(
                "모든 Response Payload 필드를 삭제하시겠습니까?",
                "확인",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                foreach (var field in ResponseSchema.Payload)
                {
                    field.PropertyChanged -= OnResponseDataFieldPropertyChanged;
                }
                ResponseSchema.Payload.Clear();
                NotifyBytesChanged();
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
            OnPropertyChanged(nameof(ResponseHeaderBytes));
            OnPropertyChanged(nameof(ResponsePayloadBytes));
            OnPropertyChanged(nameof(ResponseHeaderBytesText));
            OnPropertyChanged(nameof(ResponsePayloadBytesText));
        }

        private void OnResponseDataFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataField.Type) ||
                e.PropertyName == nameof(DataField.Value) ||
                e.PropertyName == nameof(DataField.PaddingSize))
            {
                NotifyBytesChanged();
            }
        }
    }
}