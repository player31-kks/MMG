using System.ComponentModel;
using System.Collections.ObjectModel;
using MMG.Models;
using MMG.Services;
using MMG.ViewModels.Spec;
using MMG.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MMG.ViewModels.API
{
    /// <summary>
    /// UDP 요청 관리 ViewModel
    /// </summary>
    public partial class RequestViewModel : ObservableObject
    {
        private readonly UdpClientService _udpClientService;

        [ObservableProperty]
        private UdpRequest currentRequest;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        private bool isSending;

        public RequestViewModel()
        {
            _udpClientService = new UdpClientService();
            currentRequest = new UdpRequest();

            InitializeDefaultFields();
        }

        #region Properties

        public int HeaderBytes => ByteCalculator.CalculateBytes(CurrentRequest.Headers);
        public int PayloadBytes => ByteCalculator.CalculateBytes(CurrentRequest.Payload);
        public string HeaderBytesText => ByteCalculator.FormatBytesText(HeaderBytes);
        public string PayloadBytesText => ByteCalculator.FormatBytesText(PayloadBytes);

        #endregion

        #region Events

        public event EventHandler<UdpResponse>? ResponseReceived;
        public event EventHandler<ResponseSchemaRequestEventArgs>? ResponseSchemaRequested;

        #endregion

        #region Initialization

        partial void OnCurrentRequestChanged(UdpRequest value)
        {
            NotifyBytesChanged();
        }

        private void InitializeDefaultFields()
        {
            AddFieldWithHandler(CurrentRequest.Headers, "Header1", DataType.Byte, "0");
            AddFieldWithHandler(CurrentRequest.Payload, "Field1", DataType.Byte, "0");
        }

        #endregion

        #region Public Methods

        public void LoadRequest(SavedRequest savedRequest)
        {
            CurrentRequest.IpAddress = savedRequest.IpAddress;
            CurrentRequest.Port = savedRequest.Port;
            CurrentRequest.IsBigEndian = savedRequest.IsBigEndian;

            var parts = savedRequest.RequestSchemaJson.Split('|');
            if (parts.Length < 2) return;

            var databaseService = new DatabaseService();
            LoadFields(CurrentRequest.Headers, databaseService.DeserializeDataFields(parts[0]));
            LoadFields(CurrentRequest.Payload, databaseService.DeserializeDataFields(parts[1]));

            NotifyBytesChanged();
        }

        public void CreateNewRequest()
        {
            CurrentRequest.IpAddress = "127.0.0.1";
            CurrentRequest.Port = 8080;
            CurrentRequest.IsBigEndian = true;

            ClearFieldsWithHandler(CurrentRequest.Headers);
            ClearFieldsWithHandler(CurrentRequest.Payload);

            InitializeDefaultFields();
            NotifyBytesChanged();
        }

        /// <summary>
        /// Spec에서 요청 데이터 로드
        /// </summary>
        public void LoadFromSpec(CreateApiRequestEventArgs args)
        {
            CurrentRequest.IpAddress = args.IpAddress;
            CurrentRequest.Port = args.Port;

            // 기존 필드 정리
            ClearFieldsWithHandler(CurrentRequest.Headers);
            ClearFieldsWithHandler(CurrentRequest.Payload);

            // 헤더 필드 로드
            foreach (var field in args.Headers)
            {
                field.PropertyChanged += OnDataFieldPropertyChanged;
                CurrentRequest.Headers.Add(field);
            }

            // 페이로드 필드 로드
            foreach (var field in args.Payload)
            {
                field.PropertyChanged += OnDataFieldPropertyChanged;
                CurrentRequest.Payload.Add(field);
            }

            NotifyBytesChanged();
        }

        #endregion

        #region Private Methods

        [RelayCommand(CanExecute = nameof(CanSend))]
        private async Task Send()
        {
            IsSending = true;
            try
            {
                var args = new ResponseSchemaRequestEventArgs();
                ResponseSchemaRequested?.Invoke(this, args);

                var response = await _udpClientService.SendRequestAsync(
                    CurrentRequest,
                    args.ResponseSchema ?? new ResponseSchema());

                ResponseReceived?.Invoke(this, response);
            }
            finally
            {
                IsSending = false;
            }
        }

        private bool CanSend() => !IsSending;

        [RelayCommand]
        private void AddHeader(object? selectedIndexObj = null)
        {
            var name = $"Header{CurrentRequest.Headers.Count + 1}";
            InsertField(CurrentRequest.Headers, name, DataType.Byte, "0", selectedIndexObj);
        }

        [RelayCommand]
        private void RemoveHeader(DataField? header) => RemoveFieldWithHandler(CurrentRequest.Headers, header);

        [RelayCommand]
        private void AddPayloadField(object? selectedIndexObj = null)
        {
            var name = $"Field{CurrentRequest.Payload.Count + 1}";
            InsertField(CurrentRequest.Payload, name, DataType.Int, "0", selectedIndexObj);
        }

        [RelayCommand]
        private void RemovePayloadField(DataField? field) => RemoveFieldWithHandler(CurrentRequest.Payload, field);

        [RelayCommand]
        private void ClearAllHeaders()
        {
            if (ConfirmClear("모든 Header 필드를 삭제하시겠습니까?"))
            {
                ClearFieldsWithHandler(CurrentRequest.Headers);
                NotifyBytesChanged();
            }
        }

        [RelayCommand]
        private void ClearAllPayloadFields()
        {
            if (ConfirmClear("모든 Payload 필드를 삭제하시겠습니까?"))
            {
                ClearFieldsWithHandler(CurrentRequest.Payload);
                NotifyBytesChanged();
            }
        }

        #endregion

        #region Helper Methods

        private void AddFieldWithHandler(ObservableCollection<DataField> collection, string name, DataType type, string value)
        {
            var field = new DataField { Name = name, Type = type, Value = value };
            field.PropertyChanged += OnDataFieldPropertyChanged;
            collection.Add(field);
        }

        private void InsertField(ObservableCollection<DataField> collection, string name, DataType type, string value, object? selectedIndexObj)
        {
            var field = new DataField { Name = name, Type = type, Value = value };
            field.PropertyChanged += OnDataFieldPropertyChanged;

            if (selectedIndexObj is int index && index >= 0 && index < collection.Count)
                collection.Insert(index + 1, field);
            else
                collection.Add(field);

            NotifyBytesChanged();
        }

        private void RemoveFieldWithHandler(ObservableCollection<DataField> collection, DataField? field)
        {
            if (field == null) return;

            field.PropertyChanged -= OnDataFieldPropertyChanged;
            collection.Remove(field);
            NotifyBytesChanged();
        }

        private void ClearFieldsWithHandler(ObservableCollection<DataField> collection)
        {
            foreach (var field in collection)
                field.PropertyChanged -= OnDataFieldPropertyChanged;
            collection.Clear();
        }

        private void LoadFields(ObservableCollection<DataField> collection, IEnumerable<DataField> fields)
        {
            ClearFieldsWithHandler(collection);
            foreach (var field in fields)
            {
                field.PropertyChanged += OnDataFieldPropertyChanged;
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
            OnPropertyChanged(nameof(HeaderBytes));
            OnPropertyChanged(nameof(PayloadBytes));
            OnPropertyChanged(nameof(HeaderBytesText));
            OnPropertyChanged(nameof(PayloadBytesText));
        }

        private void OnDataFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(DataField.Type) or nameof(DataField.Value) or nameof(DataField.PaddingSize))
                NotifyBytesChanged();
        }

        #endregion
    }

    /// <summary>
    /// ResponseSchema 요청 이벤트 인자
    /// </summary>
    public class ResponseSchemaRequestEventArgs : EventArgs
    {
        public ResponseSchema? ResponseSchema { get; set; }
    }
}
