using System.Windows.Input;
using MMG.Models;
using MMG.ViewModels.Base;
using MMG.ViewModels.API;

namespace MMG.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public RequestViewModel RequestViewModel { get; }
        public ResponseViewModel ResponseViewModel { get; }
        public SavedRequestsViewModel SavedRequestsViewModel { get; }
        public TreeViewViewModel TreeViewViewModel { get; }

        public MainViewModel()
        {
            RequestViewModel = new RequestViewModel();
            ResponseViewModel = new ResponseViewModel();
            SavedRequestsViewModel = new SavedRequestsViewModel();
            TreeViewViewModel = new TreeViewViewModel();

            SetupViewModelConnections();
        }

        private void SetupViewModelConnections()
        {
            // 하위 ViewModel들의 PropertyChanged 이벤트를 MainViewModel에서 전파
            RequestViewModel.PropertyChanged += (sender, e) =>
            {
                // Request 관련 프로퍼티 변경 시 MainViewModel에서도 알림
                switch (e.PropertyName)
                {
                    case nameof(RequestViewModel.CurrentRequest):
                        OnPropertyChanged(nameof(CurrentRequest));
                        break;
                    case nameof(RequestViewModel.IsSending):
                        OnPropertyChanged(nameof(IsSending));
                        break;
                    case nameof(RequestViewModel.HeaderBytes):
                        OnPropertyChanged(nameof(HeaderBytes));
                        break;
                    case nameof(RequestViewModel.PayloadBytes):
                        OnPropertyChanged(nameof(PayloadBytes));
                        break;
                    case nameof(RequestViewModel.HeaderBytesText):
                        OnPropertyChanged(nameof(HeaderBytesText));
                        break;
                    case nameof(RequestViewModel.PayloadBytesText):
                        OnPropertyChanged(nameof(PayloadBytesText));
                        break;
                }
            };

            ResponseViewModel.PropertyChanged += (sender, e) =>
            {
                // Response 관련 프로퍼티 변경 시 MainViewModel에서도 알림
                switch (e.PropertyName)
                {
                    case nameof(ResponseViewModel.ResponseSchema):
                        OnPropertyChanged(nameof(ResponseSchema));
                        break;
                    case nameof(ResponseViewModel.LastResponse):
                        OnPropertyChanged(nameof(LastResponse));
                        break;
                    case nameof(ResponseViewModel.ResponseText):
                        OnPropertyChanged(nameof(ResponseText));
                        break;
                    case nameof(ResponseViewModel.ResponseHeaderBytes):
                        OnPropertyChanged(nameof(ResponseHeaderBytes));
                        break;
                    case nameof(ResponseViewModel.ResponsePayloadBytes):
                        OnPropertyChanged(nameof(ResponsePayloadBytes));
                        break;
                    case nameof(ResponseViewModel.ResponseHeaderBytesText):
                        OnPropertyChanged(nameof(ResponseHeaderBytesText));
                        break;
                    case nameof(ResponseViewModel.ResponsePayloadBytesText):
                        OnPropertyChanged(nameof(ResponsePayloadBytesText));
                        break;
                }
            };

            SavedRequestsViewModel.PropertyChanged += (sender, e) =>
            {
                // SavedRequests 관련 프로퍼티 변경 시 MainViewModel에서도 알림
                switch (e.PropertyName)
                {
                    case nameof(SavedRequestsViewModel.SavedRequests):
                        OnPropertyChanged(nameof(SavedRequests));
                        _ = TreeViewViewModel.RefreshTreeView(); // TreeView 새로고침
                        break;
                    case nameof(SavedRequestsViewModel.SelectedSavedRequest):
                        OnPropertyChanged(nameof(SelectedSavedRequest));
                        break;
                    case nameof(SavedRequestsViewModel.CurrentLoadedRequestName):
                        OnPropertyChanged(nameof(CurrentLoadedRequestName));
                        break;
                }
            };

            TreeViewViewModel.PropertyChanged += (sender, e) =>
            {
                // TreeView 관련 프로퍼티 변경 시 MainViewModel에서도 알림
                switch (e.PropertyName)
                {
                    case nameof(TreeViewViewModel.TreeItems):
                        OnPropertyChanged(nameof(TreeItems));
                        break;
                    case nameof(TreeViewViewModel.SelectedTreeItem):
                        OnPropertyChanged(nameof(SelectedTreeItem));
                        break;
                    case nameof(TreeViewViewModel.Folders):
                        OnPropertyChanged(nameof(Folders));
                        break;
                    case nameof(TreeViewViewModel.HasSelectedItem):
                        OnPropertyChanged(nameof(HasSelectedItem));
                        break;
                }
            };

            // ViewModel 간 이벤트 연결
            RequestViewModel.ResponseReceived += (sender, response) =>
            {
                ResponseViewModel.LastResponse = response;
            };

            // RequestViewModel에서 ResponseSchema가 필요할 때 ResponseViewModel에서 제공
            RequestViewModel.ResponseSchemaRequested += (sender, args) =>
            {
                args.ResponseSchema = ResponseViewModel.ResponseSchema;
            };

            SavedRequestsViewModel.RequestLoaded += (sender, savedRequest) =>
            {
                RequestViewModel.LoadRequest(savedRequest);
                ResponseViewModel.LoadResponseSchema(savedRequest);
            };

            SavedRequestsViewModel.NewRequestCreated += (sender, e) =>
            {
                RequestViewModel.CreateNewRequest();
                ResponseViewModel.CreateNewResponseSchema();
            };

            TreeViewViewModel.RequestSelected += (sender, request) =>
            {
                SavedRequestsViewModel.SelectedSavedRequest = request;
            };

            TreeViewViewModel.NewRequestCreated += (sender, e) =>
            {
                SavedRequestsViewModel.NewRequest();
            };

            SavedRequestsViewModel.SaveRequested += (sender, args) =>
            {
                args.CurrentRequest = RequestViewModel.CurrentRequest;
                args.ResponseSchema = ResponseViewModel.ResponseSchema;
            };
        }

        // 레거시 호환성을 위한 프로퍼티들
        public UdpRequest CurrentRequest => RequestViewModel.CurrentRequest;
        public ResponseSchema ResponseSchema => ResponseViewModel.ResponseSchema;
        public UdpResponse? LastResponse => ResponseViewModel.LastResponse;
        public bool IsSending => RequestViewModel.IsSending;
        public string ResponseText => ResponseViewModel.ResponseText;
        public System.Collections.ObjectModel.ObservableCollection<SavedRequest> SavedRequests => SavedRequestsViewModel.SavedRequests;
        public SavedRequest? SelectedSavedRequest
        {
            get => SavedRequestsViewModel.SelectedSavedRequest;
            set => SavedRequestsViewModel.SelectedSavedRequest = value;
        }
        public System.Collections.ObjectModel.ObservableCollection<TreeViewItemModel> TreeItems => TreeViewViewModel.TreeItems;
        public TreeViewItemModel? SelectedTreeItem
        {
            get => TreeViewViewModel.SelectedTreeItem;
            set => TreeViewViewModel.SelectedTreeItem = value;
        }
        public System.Collections.ObjectModel.ObservableCollection<Folder> Folders => TreeViewViewModel.Folders;
        public bool HasSelectedItem => TreeViewViewModel.HasSelectedItem;
        public int HeaderBytes => RequestViewModel.HeaderBytes;
        public int PayloadBytes => RequestViewModel.PayloadBytes;
        public string HeaderBytesText => RequestViewModel.HeaderBytesText;
        public string PayloadBytesText => RequestViewModel.PayloadBytesText;
        public int ResponseHeaderBytes => ResponseViewModel.ResponseHeaderBytes;
        public int ResponsePayloadBytes => ResponseViewModel.ResponsePayloadBytes;
        public string ResponseHeaderBytesText => ResponseViewModel.ResponseHeaderBytesText;
        public string ResponsePayloadBytesText => ResponseViewModel.ResponsePayloadBytesText;
        public string CurrentLoadedRequestName => SavedRequestsViewModel.CurrentLoadedRequestName;

        // 레거시 호환성을 위한 Command들
        public ICommand SendCommand => RequestViewModel.SendCommand;
        public ICommand SaveCommand => SavedRequestsViewModel.SaveRequestCommand;
        public ICommand RefreshCommand => SavedRequestsViewModel.LoadSavedRequestsCommand;
        public ICommand LoadSelectedCommand => SavedRequestsViewModel.LoadSelectedCommand;
        public ICommand LoadSelectedRequestCommand => TreeViewViewModel.LoadSelectedRequestCommand;
        public ICommand DeleteSelectedCommand => TreeViewViewModel.DeleteSelectedCommand;
        public ICommand DeleteItemCommand => TreeViewViewModel.DeleteItemCommand;
        public ICommand RenameItemCommand => TreeViewViewModel.RenameItemCommand;
        public ICommand SaveRenameCommand => TreeViewViewModel.SaveRenameCommand;
        public ICommand CancelRenameCommand => TreeViewViewModel.CancelRenameCommand;
        public ICommand CopyItemCommand => TreeViewViewModel.CopyItemCommand;
        public ICommand RenameSelectedItemCommand => TreeViewViewModel.RenameSelectedItemCommand;
        public ICommand NewRequestCommand => SavedRequestsViewModel.NewRequestCommand;
        public ICommand NewFolderCommand => TreeViewViewModel.NewFolderCommand;
        public ICommand AddHeaderCommand => RequestViewModel.AddHeaderCommand;
        public ICommand RemoveHeaderCommand => RequestViewModel.RemoveHeaderCommand;
        public ICommand AddPayloadFieldCommand => RequestViewModel.AddPayloadFieldCommand;
        public ICommand RemovePayloadFieldCommand => RequestViewModel.RemovePayloadFieldCommand;
        public ICommand AddResponseHeaderCommand => ResponseViewModel.AddResponseHeaderCommand;
        public ICommand RemoveResponseHeaderCommand => ResponseViewModel.RemoveResponseHeaderCommand;
        public ICommand AddResponsePayloadFieldCommand => ResponseViewModel.AddResponsePayloadFieldCommand;
        public ICommand RemoveResponsePayloadFieldCommand => ResponseViewModel.RemoveResponsePayloadFieldCommand;
        public ICommand ClearAllHeadersCommand => RequestViewModel.ClearAllHeadersCommand;
        public ICommand ClearAllPayloadFieldsCommand => RequestViewModel.ClearAllPayloadFieldsCommand;
        public ICommand ClearAllResponseHeadersCommand => ResponseViewModel.ClearAllResponseHeadersCommand;
        public ICommand ClearAllResponsePayloadFieldsCommand => ResponseViewModel.ClearAllResponsePayloadFieldsCommand;
        public ICommand AddResponseFieldCommand => ResponseViewModel.AddResponsePayloadFieldCommand;
        public ICommand RemoveResponseFieldCommand => ResponseViewModel.RemoveResponsePayloadFieldCommand;
    }
}