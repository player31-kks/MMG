using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MMG.Core.Models.Schema;
using MMG.Core.Services;
using MMG.ViewModels.Base;
using Microsoft.Win32;

namespace MMG.ViewModels.Spec
{
    /// <summary>
    /// UDP API Spec 관리 ViewModel
    /// YAML 스펙 파일 Import/Export 및 편집 기능 제공
    /// </summary>
    public class SpecViewModel : ViewModelBase
    {
        private readonly UdpApiSpecParser _specParser;
        private UdpApiSpec? _currentSpec;
        private string _currentFilePath = "";
        private string _specYamlContent = "";
        private bool _hasUnsavedChanges;
        private string _statusMessage = "스펙 파일을 불러오거나 새로 만드세요";
        private ObservableCollection<MessageItem> _messages = new();
        private MessageItem? _selectedMessage;

        public SpecViewModel()
        {
            _specParser = new UdpApiSpecParser();

            InitializeCommands();
        }

        #region Properties

        public UdpApiSpec? CurrentSpec
        {
            get => _currentSpec;
            set
            {
                if (SetProperty(ref _currentSpec, value))
                {
                    UpdateMessagesCollection();
                    OnPropertyChanged(nameof(HasSpec));
                    OnPropertyChanged(nameof(SpecInfo));
                }
            }
        }

        public string CurrentFilePath
        {
            get => _currentFilePath;
            set => SetProperty(ref _currentFilePath, value);
        }

        public string SpecYamlContent
        {
            get => _specYamlContent;
            set
            {
                if (SetProperty(ref _specYamlContent, value))
                    HasUnsavedChanges = true;
            }
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<MessageItem> Messages
        {
            get => _messages;
            set => SetProperty(ref _messages, value);
        }

        public MessageItem? SelectedMessage
        {
            get => _selectedMessage;
            set
            {
                if (SetProperty(ref _selectedMessage, value))
                    OnPropertyChanged(nameof(HasSelectedMessage));
            }
        }

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

        public ICommand NewSpecCommand { get; private set; } = null!;
        public ICommand ImportSpecCommand { get; private set; } = null!;
        public ICommand ExportSpecCommand { get; private set; } = null!;
        public ICommand SaveSpecCommand { get; private set; } = null!;
        public ICommand RefreshFromYamlCommand { get; private set; } = null!;

        #endregion

        #region Initialization

        private void InitializeCommands()
        {
            NewSpecCommand = new RelayCommand(CreateNewSpec);
            ImportSpecCommand = new RelayCommand(ImportSpec);
            ExportSpecCommand = new RelayCommand(ExportSpec, () => HasSpec);
            SaveSpecCommand = new RelayCommand(SaveSpec, () => HasSpec && !string.IsNullOrEmpty(CurrentFilePath));
            RefreshFromYamlCommand = new RelayCommand(RefreshFromYaml, () => !string.IsNullOrEmpty(SpecYamlContent));
        }

        #endregion

        #region Command Methods

        private void CreateNewSpec()
        {
            if (HasUnsavedChanges && !ConfirmDiscardChanges()) return;

            CurrentSpec = CreateDefaultSpec();
            CurrentFilePath = "";
            SpecYamlContent = _specParser.ToYaml(CurrentSpec);
            HasUnsavedChanges = false;
            StatusMessage = "새 스펙이 생성되었습니다";
        }

        private void ImportSpec()
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
                MessageBox.Show($"스펙 파일을 불러오는 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSpec()
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
                MessageBox.Show($"스펙 파일을 저장하는 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSpec()
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
            {
                ExportSpec();
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
                MessageBox.Show($"저장 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshFromYaml()
        {
            try
            {
                var spec = _specParser.ParseYaml(SpecYamlContent);
                CurrentSpec = spec;
                StatusMessage = "YAML에서 스펙을 새로고침했습니다";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"YAML 파싱 오류:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Helper Methods

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
            return MessageBox.Show(
                "저장하지 않은 변경사항이 있습니다. 계속하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

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
}
