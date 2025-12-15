using System.ComponentModel;
using System.Windows.Input;
using System.Collections.ObjectModel;
using MMG.Models;
using MMG.Services;
using MMG.ViewModels.Base;
using MMG.ViewModels.Spec;
using MMG.Core.Utilities;

namespace MMG.ViewModels.API
{
    /// <summary>
    /// UDP 응답 관리 ViewModel
    /// </summary>
    public class ResponseViewModel : ViewModelBase
    {
        private ResponseSchema _responseSchema;
        private UdpResponse? _lastResponse;
        private string _responseText = "";

        public ResponseViewModel()
        {
            _responseSchema = new ResponseSchema();

            InitializeCommands();
            InitializeDefaultFields();
        }

        #region Properties

        public ResponseSchema ResponseSchema
        {
            get => _responseSchema;
            set => SetProperty(ref _responseSchema, value);
        }

        public UdpResponse? LastResponse
        {
            get => _lastResponse;
            set
            {
                if (SetProperty(ref _lastResponse, value))
                    UpdateResponseText();
            }
        }

        public string ResponseText
        {
            get => _responseText;
            set => SetProperty(ref _responseText, value);
        }

        public int ResponseHeaderBytes => ByteCalculator.CalculateBytes(ResponseSchema.Headers);
        public int ResponsePayloadBytes => ByteCalculator.CalculateBytes(ResponseSchema.Payload);
        public string ResponseHeaderBytesText => ByteCalculator.FormatBytesText(ResponseHeaderBytes);
        public string ResponsePayloadBytesText => ByteCalculator.FormatBytesText(ResponsePayloadBytes);

        #endregion

        #region Commands

        public ICommand AddResponseHeaderCommand { get; private set; } = null!;
        public ICommand RemoveResponseHeaderCommand { get; private set; } = null!;
        public ICommand AddResponsePayloadFieldCommand { get; private set; } = null!;
        public ICommand RemoveResponsePayloadFieldCommand { get; private set; } = null!;
        public ICommand ClearAllResponseHeadersCommand { get; private set; } = null!;
        public ICommand ClearAllResponsePayloadFieldsCommand { get; private set; } = null!;

        #endregion

        #region Initialization

        private void InitializeCommands()
        {
            AddResponseHeaderCommand = new RelayCommand<object>(AddResponseHeader);
            RemoveResponseHeaderCommand = new RelayCommand<DataField>(RemoveResponseHeader);
            AddResponsePayloadFieldCommand = new RelayCommand<object>(AddResponsePayloadField);
            RemoveResponsePayloadFieldCommand = new RelayCommand<DataField>(RemoveResponsePayloadField);
            ClearAllResponseHeadersCommand = new RelayCommand(ClearAllResponseHeaders);
            ClearAllResponsePayloadFieldsCommand = new RelayCommand(ClearAllResponsePayloadFields);
        }

        private void InitializeDefaultFields()
        {
            AddFieldWithHandler(ResponseSchema.Headers, "ResponseHeader1", DataType.Byte);
            AddFieldWithHandler(ResponseSchema.Payload, "ResponseField1", DataType.Byte);
        }

        #endregion

        #region Public Methods

        public void LoadResponseSchema(SavedRequest savedRequest)
        {
            if (string.IsNullOrEmpty(savedRequest.ResponseSchemaJson)) return;

            var parts = savedRequest.ResponseSchemaJson.Split('|');
            if (parts.Length < 2) return;

            var databaseService = new DatabaseService();
            LoadFields(ResponseSchema.Headers, databaseService.DeserializeDataFields(parts[0]));
            LoadFields(ResponseSchema.Payload, databaseService.DeserializeDataFields(parts[1]));

            NotifyBytesChanged();
        }

        public void CreateNewResponseSchema()
        {
            ClearFieldsWithHandler(ResponseSchema.Headers);
            ClearFieldsWithHandler(ResponseSchema.Payload);

            InitializeDefaultFields();
            LastResponse = null;
            NotifyBytesChanged();
        }

        /// <summary>
        /// Spec에서 응답 스키마 로드
        /// </summary>
        public void LoadFromSpec(CreateApiRequestEventArgs args)
        {
            // 기존 필드 정리
            ClearFieldsWithHandler(ResponseSchema.Headers);
            ClearFieldsWithHandler(ResponseSchema.Payload);

            // 응답 헤더 필드 로드
            foreach (var field in args.ResponseHeaders)
            {
                field.PropertyChanged += OnResponseDataFieldPropertyChanged;
                ResponseSchema.Headers.Add(field);
            }

            // 응답 페이로드 필드 로드
            foreach (var field in args.ResponsePayload)
            {
                field.PropertyChanged += OnResponseDataFieldPropertyChanged;
                ResponseSchema.Payload.Add(field);
            }

            // 응답 필드가 없으면 기본값 설정
            if (ResponseSchema.Headers.Count == 0 && ResponseSchema.Payload.Count == 0)
            {
                InitializeDefaultFields();
            }

            LastResponse = null;
            NotifyBytesChanged();
        }

        #endregion

        #region Private Methods

        private void AddResponseHeader(object? selectedIndexObj = null)
        {
            var name = $"ResponseHeader{ResponseSchema.Headers.Count + 1}";
            InsertField(ResponseSchema.Headers, name, DataType.Byte, selectedIndexObj);
        }

        private void RemoveResponseHeader(DataField? header) => RemoveFieldWithHandler(ResponseSchema.Headers, header);

        private void AddResponsePayloadField(object? selectedIndexObj = null)
        {
            var name = $"ResponseField{ResponseSchema.Payload.Count + 1}";
            InsertField(ResponseSchema.Payload, name, DataType.Int, selectedIndexObj);
        }

        private void RemoveResponsePayloadField(DataField? field) => RemoveFieldWithHandler(ResponseSchema.Payload, field);

        private void ClearAllResponseHeaders()
        {
            if (ConfirmClear("모든 Response Header 필드를 삭제하시겠습니까?"))
            {
                ClearFieldsWithHandler(ResponseSchema.Headers);
                NotifyBytesChanged();
            }
        }

        private void ClearAllResponsePayloadFields()
        {
            if (ConfirmClear("모든 Response Payload 필드를 삭제하시겠습니까?"))
            {
                ClearFieldsWithHandler(ResponseSchema.Payload);
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

            var lines = new List<string>
            {
                $"Status: {LastResponse.Status}",
                $"Received At: {LastResponse.ReceivedAt:yyyy-MM-dd HH:mm:ss}",
                $"Raw Data Length: {LastResponse.RawData.Length} bytes",
                $"Raw Data (Hex): {Convert.ToHexString(LastResponse.RawData)}",
                ""
            };

            if (LastResponse.ParsedData.Any())
            {
                lines.Add("Parsed Data:");
                foreach (var kvp in LastResponse.ParsedData)
                {
                    lines.Add($"  {kvp.Key}: {kvp.Value}");
                }
            }

            ResponseText = string.Join("\n", lines);
        }

        #endregion

        #region Helper Methods

        private void AddFieldWithHandler(ObservableCollection<DataField> collection, string name, DataType type)
        {
            var field = new DataField { Name = name, Type = type };
            field.PropertyChanged += OnResponseDataFieldPropertyChanged;
            collection.Add(field);
        }

        private void InsertField(ObservableCollection<DataField> collection, string name, DataType type, object? selectedIndexObj)
        {
            var field = new DataField { Name = name, Type = type };
            field.PropertyChanged += OnResponseDataFieldPropertyChanged;

            if (selectedIndexObj is int index && index >= 0 && index < collection.Count)
                collection.Insert(index + 1, field);
            else
                collection.Add(field);

            NotifyBytesChanged();
        }

        private void RemoveFieldWithHandler(ObservableCollection<DataField> collection, DataField? field)
        {
            if (field == null) return;

            field.PropertyChanged -= OnResponseDataFieldPropertyChanged;
            collection.Remove(field);
            NotifyBytesChanged();
        }

        private void ClearFieldsWithHandler(ObservableCollection<DataField> collection)
        {
            foreach (var field in collection)
                field.PropertyChanged -= OnResponseDataFieldPropertyChanged;
            collection.Clear();
        }

        private void LoadFields(ObservableCollection<DataField> collection, IEnumerable<DataField> fields)
        {
            ClearFieldsWithHandler(collection);
            foreach (var field in fields)
            {
                field.PropertyChanged += OnResponseDataFieldPropertyChanged;
                collection.Add(field);
            }
        }

        private static bool ConfirmClear(string message)
        {
            return System.Windows.MessageBox.Show(
                message, "확인",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;
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
            if (e.PropertyName is nameof(DataField.Type) or nameof(DataField.Value) or nameof(DataField.PaddingSize))
                NotifyBytesChanged();
        }

        #endregion
    }
}
