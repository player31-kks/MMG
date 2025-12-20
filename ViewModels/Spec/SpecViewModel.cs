using System.Collections.ObjectModel;
using System.IO;
using MMG.Core.Models.Schema;
using MMG.Core.Services;
using MMG.Models;
using MMG.Views.Common;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MMG.ViewModels.Spec
{
    /// <summary>
    /// UDP API Spec 관리 ViewModel
    /// YAML 스펙 파일 Import/Export 및 편집 기능 제공
    /// </summary>
    public partial class SpecViewModel : ObservableObject
    {
        private readonly UdpApiSpecParser _specParser;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSpec), nameof(SpecInfo))]
        private UdpApiSpec? currentSpec;

        [ObservableProperty]
        private string currentFilePath = "";

        [ObservableProperty]
        private string specYamlContent = "";

        [ObservableProperty]
        private bool hasUnsavedChanges;

        [ObservableProperty]
        private string statusMessage = "스펙 파일을 불러오거나 새로 만드세요";

        [ObservableProperty]
        private ObservableCollection<MessageItem> messages = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedMessage))]
        private MessageItem? selectedMessage;

        public SpecViewModel()
        {
            _specParser = new UdpApiSpecParser();
        }

        partial void OnCurrentSpecChanged(UdpApiSpec? value)
        {
            UpdateMessagesCollection();
        }

        partial void OnSpecYamlContentChanged(string value)
        {
            HasUnsavedChanges = true;
        }

        #region Properties

        public bool HasSpec => CurrentSpec != null;
        public bool HasSelectedMessage => SelectedMessage != null;

        public string SpecInfo
        {
            get
            {
                if (CurrentSpec == null) return "";
                return $"UDP API: {CurrentSpec.Info.Title} v{CurrentSpec.Info.Version}";
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void NewSpec() => CreateNewSpecInternal();

        [RelayCommand]
        private void ImportSpec() => ImportSpecInternal();

        [RelayCommand(CanExecute = nameof(HasSpec))]
        private void ExportSpec() => ExportSpecInternal();

        [RelayCommand(CanExecute = nameof(CanSaveSpec))]
        private void SaveSpec() => SaveSpecInternal();

        private bool CanSaveSpec() => HasSpec && !string.IsNullOrEmpty(CurrentFilePath);

        [RelayCommand(CanExecute = nameof(CanRefreshFromYaml))]
        private void RefreshFromYaml() => RefreshFromYamlInternal();

        private bool CanRefreshFromYaml() => !string.IsNullOrEmpty(SpecYamlContent);

        [RelayCommand]
        private void CreateApiRequest(MessageItem? msg)
        {
            if (msg != null)
                CreateApiRequestInternal(msg);
        }

        #region Events

        /// <summary>
        /// API 요청 생성 요청 이벤트 (NavigationViewModel에서 처리)
        /// </summary>
        public event EventHandler<CreateApiRequestEventArgs>? CreateApiRequestRequested;

        #endregion

        #region Command Methods

        private void CreateNewSpecInternal()
        {
            if (HasUnsavedChanges && !ConfirmDiscardChanges()) return;

            CurrentSpec = CreateDefaultSpec();
            CurrentFilePath = "";
            SpecYamlContent = _specParser.ToYaml(CurrentSpec);
            HasUnsavedChanges = false;
            StatusMessage = "새 스펙이 생성되었습니다";
        }

        private void ImportSpecInternal()
        {
            if (HasUnsavedChanges && !ConfirmDiscardChanges()) return;

            var dialog = new OpenFileDialog
            {
                Filter = "YAML 파일 (*.yaml;*.yml)|*.yaml;*.yml|모든 파일 (*.*)|*.*",
                Title = "UDP API 스펙 파일 가져오기"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var yamlContent = File.ReadAllText(dialog.FileName);
                var spec = _specParser.ParseYaml(yamlContent);

                CurrentSpec = spec;
                CurrentFilePath = dialog.FileName;
                SpecYamlContent = yamlContent;
                HasUnsavedChanges = false;
                StatusMessage = $"스펙을 불러왔습니다: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"스펙 파일을 불러오는 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류");
            }
        }

        private void ExportSpecInternal()
        {
            if (CurrentSpec == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "YAML 파일 (*.yaml)|*.yaml|YML 파일 (*.yml)|*.yml",
                Title = "UDP API 스펙 파일 내보내기",
                FileName = $"{CurrentSpec.Info.Title.Replace(" ", "-").ToLower()}-spec.yaml"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var yaml = string.IsNullOrEmpty(SpecYamlContent)
                    ? _specParser.ToYaml(CurrentSpec)
                    : SpecYamlContent;

                File.WriteAllText(dialog.FileName, yaml);
                CurrentFilePath = dialog.FileName;
                HasUnsavedChanges = false;
                StatusMessage = $"스펙을 저장했습니다: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"스펙 파일을 저장하는 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류");
            }
        }

        private void SaveSpecInternal()
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
            {
                ExportSpecInternal();
                return;
            }

            try
            {
                File.WriteAllText(CurrentFilePath, SpecYamlContent);
                HasUnsavedChanges = false;
                StatusMessage = $"저장됨: {Path.GetFileName(CurrentFilePath)}";
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"저장 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류");
            }
        }

        private void RefreshFromYamlInternal()
        {
            try
            {
                var spec = _specParser.ParseYaml(SpecYamlContent);
                CurrentSpec = spec;
                StatusMessage = "YAML에서 스펙을 새로고침했습니다";
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"YAML 파싱 오류:\n{ex.Message}",
                    "오류");
            }
        }

        private void CreateApiRequestInternal(MessageItem? messageItem)
        {
            if (messageItem?.Definition == null) return;

            var definition = messageItem.Definition;
            var args = new CreateApiRequestEventArgs
            {
                MessageId = messageItem.MessageId
            };

            // 엔드포인트 정보 설정
            if (definition.Endpoint != null)
            {
                if (!string.IsNullOrEmpty(definition.Endpoint.ServerRef) && CurrentSpec?.Servers != null)
                {
                    // $ref로 서버 참조
                    var serverName = definition.Endpoint.ServerRef.Replace("#/servers/", "");
                    var server = CurrentSpec.Servers.FirstOrDefault(s => s.Name == serverName);
                    if (server != null)
                    {
                        args.IpAddress = server.IpAddress;
                        args.Port = server.Port;
                    }
                }
                else
                {
                    // 직접 지정
                    if (!string.IsNullOrEmpty(definition.Endpoint.IpAddress))
                        args.IpAddress = definition.Endpoint.IpAddress;
                    if (definition.Endpoint.Port.HasValue)
                        args.Port = definition.Endpoint.Port.Value;
                }
            }
            else if (CurrentSpec?.Servers != null && CurrentSpec.Servers.Count > 0)
            {
                // 기본 서버 사용
                args.IpAddress = CurrentSpec.Servers[0].IpAddress;
                args.Port = CurrentSpec.Servers[0].Port;
            }

            // 요청 헤더 변환
            if (definition.Request?.Header != null)
            {
                foreach (var field in definition.Request.Header)
                {
                    args.Headers.Add(ConvertToDataField(field));
                }
            }

            // 요청 페이로드 변환
            if (definition.Request?.Payload != null)
            {
                foreach (var field in definition.Request.Payload)
                {
                    args.Payload.Add(ConvertToDataField(field));
                }
            }

            // 응답 헤더 변환
            if (definition.Response?.Header != null)
            {
                foreach (var field in definition.Response.Header)
                {
                    args.ResponseHeaders.Add(ConvertToDataField(field));
                }
            }

            // 응답 페이로드 변환
            if (definition.Response?.Payload != null)
            {
                foreach (var field in definition.Response.Payload)
                {
                    args.ResponsePayload.Add(ConvertToDataField(field));
                }
            }

            // 이벤트 발생
            CreateApiRequestRequested?.Invoke(this, args);
            StatusMessage = $"API 요청 생성됨: {messageItem.MessageId}";
        }

        #endregion

        #region Helper Methods

        private static DataField ConvertToDataField(FieldDefinition fieldDef)
        {
            var dataType = fieldDef.Type.ToLowerInvariant() switch
            {
                "byte" or "uint8" or "int8" => DataType.Byte,
                "uint16" or "int16" or "short" => DataType.UInt16,
                "int32" or "int" => DataType.Int,
                "uint32" or "uint" => DataType.UInt,
                "float" or "float32" => DataType.Float,
                "padding" => DataType.Padding,
                _ => DataType.Byte
            };

            var dataField = new DataField
            {
                Name = fieldDef.Name,
                Type = dataType,
                Value = fieldDef.Value ?? GetDefaultValue(dataType)
            };

            if (dataType == DataType.Padding && fieldDef.Size.HasValue)
            {
                dataField.PaddingSize = fieldDef.Size.Value;
            }

            return dataField;
        }

        private static string GetDefaultValue(DataType type)
        {
            return type switch
            {
                DataType.Float => "0.0",
                _ => "0"
            };
        }

        private void UpdateMessagesCollection()
        {
            Messages.Clear();
            if (CurrentSpec?.Messages == null) return;

            foreach (var kvp in CurrentSpec.Messages)
            {
                var message = kvp.Value;
                Messages.Add(new MessageItem
                {
                    MessageId = kvp.Key,
                    Description = message.Description,
                    HeaderCount = message.Request?.Header?.Count ?? 0,
                    PayloadCount = message.Request?.Payload?.Count ?? 0,
                    Definition = message
                });
            }
        }

        private static UdpApiSpec CreateDefaultSpec()
        {
            return new UdpApiSpec
            {
                Version = "1.0.0",
                Info = new ApiInfo
                {
                    Title = "New UDP API",
                    Version = "1.0.0",
                    Description = "UDP API 스펙 설명"
                },
                Servers = new List<ServerInfo>
                {
                    new ServerInfo
                    {
                        Name = "default",
                        IpAddress = "127.0.0.1",
                        Port = 5000,
                        Description = "기본 서버"
                    }
                },
                Messages = new Dictionary<string, MessageDefinition>
                {
                    ["sample_request"] = new MessageDefinition
                    {
                        Description = "샘플 요청 메시지",
                        Request = new MessageSchema
                        {
                            Header = new List<FieldDefinition>
                            {
                                new() { Name = "MessageType", Type = "byte", Description = "메시지 타입" }
                            },
                            Payload = new List<FieldDefinition>
                            {
                                new() { Name = "Data", Type = "int", Description = "데이터" }
                            }
                        }
                    }
                }
            };
        }

        private static bool ConfirmDiscardChanges()
        {
            return ModernMessageDialog.ShowConfirm(
                "저장하지 않은 변경사항이 있습니다. 계속하시겠습니까?",
                "확인") == true;
        }

        #endregion

        #endregion
    }

    /// <summary>
    /// UI 바인딩용 메시지 아이템
    /// </summary>
    public class MessageItem
    {
        public string MessageId { get; set; } = "";
        public string Description { get; set; } = "";
        public int HeaderCount { get; set; }
        public int PayloadCount { get; set; }
        public MessageDefinition? Definition { get; set; }
    }

    /// <summary>
    /// API 요청 생성 이벤트 인자
    /// </summary>
    public class CreateApiRequestEventArgs : EventArgs
    {
        public string MessageId { get; set; } = "";
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
        public ObservableCollection<DataField> Headers { get; set; } = new();
        public ObservableCollection<DataField> Payload { get; set; } = new();
        public ObservableCollection<DataField> ResponseHeaders { get; set; } = new();
        public ObservableCollection<DataField> ResponsePayload { get; set; } = new();
    }
}
