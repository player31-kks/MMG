using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MMG.Core.Interfaces;
using MMG.Core.Models.Schema;
using MMG.Core.Services;

namespace MMG.ViewModels.Spec
{
    /// <summary>
    /// WPF 네이티브 문서 뷰어 ViewModel
    /// </summary>
    public partial class DocViewerViewModel : ObservableObject
    {
        private readonly ISpecParserFactory _parserFactory;
        private readonly List<DocMessageItem> _allMessages = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSpec), nameof(HasSelectedMessage), nameof(IsOverviewVisible), nameof(IsOverviewSelected), nameof(ComponentCount))]
        private UdpApiSpec? currentSpec;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError), nameof(IsOverviewVisible), nameof(IsDocumentEmpty))]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOverviewVisible), nameof(HasSelectedMessage), nameof(IsOverviewSelected), nameof(SelectedMessageIdBadge), nameof(SelectedMessageTitle), nameof(SelectedMessageDescription), nameof(SelectedMessageHeaderCount), nameof(SelectedMessagePayloadCount), nameof(SelectedMessageTotalSize), nameof(SelectedMessageEndpoint), nameof(SelectedMessageEndian), nameof(SelectedMessageTimeout), nameof(SelectedMessageStructName))]
        private DocMessageItem? selectedMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SearchResultSummary))]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SearchResultSummary))]
        private int filteredMessageCount;

        public ObservableCollection<DocSummaryCard> OverviewCards { get; } = new();

        public ObservableCollection<DocMessageGroup> MessageGroups { get; } = new();

        public ObservableCollection<ServerInfo> Servers { get; } = new();

        public event EventHandler<DocMessageActionEventArgs>? CreateApiRequestRequested;

        public DocViewerViewModel(ISpecParserFactory parserFactory)
        {
            _parserFactory = parserFactory;
        }

        public bool HasSpec => CurrentSpec != null;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasSelectedMessage => SelectedMessage != null;

        public bool IsOverviewVisible => !HasError && SelectedMessage == null;

        public bool IsOverviewSelected => !HasError && SelectedMessage == null;

        public bool IsDocumentEmpty => !HasError && CurrentSpec != null && _allMessages.Count == 0;

        public int ComponentCount => CurrentSpec?.Components?.Schemas?.Count ?? 0;

        public string SearchResultSummary => $"{FilteredMessageCount}/{_allMessages.Count} messages";

        public string SelectedMessageTitle => SelectedMessage?.DisplayName ?? "Spec Overview";

        public string SelectedMessageIdBadge => SelectedMessage?.MessageIdLiteral ?? string.Empty;

        public string SelectedMessageDescription => SelectedMessage?.Description ?? "선택한 메시지의 구조와 필드를 한눈에 볼 수 있습니다.";

        public string SelectedMessageStructName => SelectedMessage?.StructName ?? "-";

        public string SelectedMessageTotalSize => SelectedMessage?.TotalSizeText ?? "-";

        public string SelectedMessageTimeout => SelectedMessage?.TimeoutText ?? "-";

        public string SelectedMessageEndpoint => SelectedMessage?.EndpointText ?? "-";

        public string SelectedMessageEndian => SelectedMessage?.EndianDisplay ?? (CurrentSpec?.IdlMetadata.IsBigEndian == true ? "Big Endian" : "Little Endian");

        public string SelectedMessageHeaderCount => SelectedMessage?.HeaderCount.ToString() ?? "0";

        public string SelectedMessagePayloadCount => SelectedMessage?.PayloadCount.ToString() ?? "0";

        partial void OnCurrentSpecChanged(UdpApiSpec? value)
        {
            var selectedKey = SelectedMessage?.MessageKey;
            ErrorMessage = string.Empty;
            BuildDocumentModel(selectedKey);
        }

        partial void OnSelectedMessageChanged(DocMessageItem? value)
        {
            ApplySelectionState();
            CreateApiRequestCommand.NotifyCanExecuteChanged();
        }

        partial void OnSearchQueryChanged(string value)
        {
            UpdateMessageGroups(SelectedMessage?.MessageKey);
        }

        [RelayCommand]
        private void RefreshDoc()
        {
            BuildDocumentModel(SelectedMessage?.MessageKey);
        }

        [RelayCommand]
        private void SelectOverview()
        {
            SelectedMessage = null;
        }

        [RelayCommand]
        private void SelectMessage(DocMessageItem? message)
        {
            if (message == null)
            {
                return;
            }

            SelectedMessage = message;
        }

        [RelayCommand(CanExecute = nameof(CanCreateApiRequest))]
        private void CreateApiRequest()
        {
            if (SelectedMessage == null)
            {
                return;
            }

            CreateApiRequestRequested?.Invoke(this, new DocMessageActionEventArgs(SelectedMessage.MessageKey));
        }

        private bool CanCreateApiRequest() => SelectedMessage != null;

        public void LoadSpec(UdpApiSpec spec)
        {
            ErrorMessage = string.Empty;
            CurrentSpec = spec;
        }

        public bool LoadFromContent(string content, string filePath)
        {
            try
            {
                var parser = _parserFactory.CreateParser(filePath);
                var spec = parser.Parse(content);
                LoadSpec(spec);
                return true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return false;
            }
        }

        public bool LoadFromIdl(string idlContent)
        {
            try
            {
                var parser = _parserFactory.CreateParser(SpecParserType.Idl);
                var spec = parser.Parse(idlContent);
                LoadSpec(spec);
                return true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return false;
            }
        }

        public void ShowError(string message)
        {
            CurrentSpec = null;
            ErrorMessage = message;
            SelectedMessage = null;
            OverviewCards.Clear();
            MessageGroups.Clear();
            Servers.Clear();
            _allMessages.Clear();
            FilteredMessageCount = 0;
            OnPropertyChanged(nameof(ComponentCount));
            OnPropertyChanged(nameof(IsDocumentEmpty));
        }

        private void BuildDocumentModel(string? selectedMessageKey)
        {
            OverviewCards.Clear();
            MessageGroups.Clear();
            Servers.Clear();
            _allMessages.Clear();

            if (CurrentSpec == null)
            {
                FilteredMessageCount = 0;
                SelectedMessage = null;
                return;
            }

            foreach (var server in CurrentSpec.Servers)
            {
                Servers.Add(server);
            }

            BuildOverviewCards(CurrentSpec);

            foreach (var (messageKey, messageDefinition) in CurrentSpec.Messages)
            {
                _allMessages.Add(CreateMessageItem(CurrentSpec, messageKey, messageDefinition));
            }

            UpdateMessageGroups(selectedMessageKey);
            OnPropertyChanged(nameof(ComponentCount));
            OnPropertyChanged(nameof(IsDocumentEmpty));
        }

        private void BuildOverviewCards(UdpApiSpec spec)
        {
            OverviewCards.Add(new DocSummaryCard("Messages", spec.Messages.Count.ToString(), "등록된 메시지 수"));
            OverviewCards.Add(new DocSummaryCard("Servers", spec.Servers.Count.ToString(), "연결 가능한 서버 수"));
            OverviewCards.Add(new DocSummaryCard("Components", (spec.Components?.Schemas?.Count ?? 0).ToString(), "재사용 구조체 수"));
            OverviewCards.Add(new DocSummaryCard("PACK_SIZE", spec.IdlMetadata.PackSize.ToString(), "IDL 패킹 크기"));
            OverviewCards.Add(new DocSummaryCard("Endian", spec.IdlMetadata.IsBigEndian ? "Big" : "Little", "기본 바이트 순서"));
            OverviewCards.Add(new DocSummaryCard("Header", spec.IdlMetadata.HeaderStructName, "공통 헤더 구조체"));
        }

        private void UpdateMessageGroups(string? selectedMessageKey)
        {
            MessageGroups.Clear();

            var filteredMessages = _allMessages
                .Where(MatchesSearch)
                .OrderBy(msg => msg.NumericMessageId)
                .ThenBy(msg => msg.DisplayName)
                .ToList();

            FilteredMessageCount = filteredMessages.Count;

            foreach (var group in filteredMessages.GroupBy(msg => msg.GroupName).OrderBy(group => group.Key))
            {
                var groupItem = new DocMessageGroup(group.Key, group.Count());
                foreach (var message in group)
                {
                    groupItem.Messages.Add(message);
                }

                MessageGroups.Add(groupItem);
            }

            SelectedMessage = !string.IsNullOrWhiteSpace(selectedMessageKey)
                ? _allMessages.FirstOrDefault(message => message.MessageKey == selectedMessageKey)
                : SelectedMessage;

            if (SelectedMessage != null && !filteredMessages.Contains(SelectedMessage))
            {
                SelectedMessage = null;
            }

            ApplySelectionState();
        }

        private bool MatchesSearch(DocMessageItem message)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                return true;
            }

            var query = SearchQuery.Trim();
            return ContainsIgnoreCase(message.DisplayName, query)
                || ContainsIgnoreCase(message.MessageIdLiteral, query)
                || ContainsIgnoreCase(message.Description, query)
                || ContainsIgnoreCase(message.StructName, query)
                || ContainsIgnoreCase(message.MessageKey, query);
        }

        private static bool ContainsIgnoreCase(string? source, string query)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySelectionState()
        {
            foreach (var message in _allMessages)
            {
                message.IsSelected = message == SelectedMessage;
            }

            OnPropertyChanged(nameof(IsOverviewVisible));
            OnPropertyChanged(nameof(IsOverviewSelected));
            OnPropertyChanged(nameof(HasSelectedMessage));
            OnPropertyChanged(nameof(SelectedMessageTitle));
            OnPropertyChanged(nameof(SelectedMessageIdBadge));
            OnPropertyChanged(nameof(SelectedMessageDescription));
            OnPropertyChanged(nameof(SelectedMessageStructName));
            OnPropertyChanged(nameof(SelectedMessageTotalSize));
            OnPropertyChanged(nameof(SelectedMessageTimeout));
            OnPropertyChanged(nameof(SelectedMessageEndpoint));
            OnPropertyChanged(nameof(SelectedMessageEndian));
            OnPropertyChanged(nameof(SelectedMessageHeaderCount));
            OnPropertyChanged(nameof(SelectedMessagePayloadCount));
        }

        private static DocMessageItem CreateMessageItem(UdpApiSpec spec, string messageKey, MessageDefinition definition)
        {
            var headerFields = definition.Request.Header
                .Select(field => CreateFieldItem(field, true, spec.IdlMetadata.HeaderMessageIdFieldName))
                .ToList();
            var payloadFields = definition.Request.Payload
                .Select(field => CreateFieldItem(field, false, spec.IdlMetadata.HeaderMessageIdFieldName))
                .ToList();
            var totalBytes = definition.Request.TotalSize;
            var messageIdLiteral = string.IsNullOrWhiteSpace(definition.IdlMetadata.MessageIdLiteral)
                ? definition.IdlMetadata.MessageId.ToString()
                : definition.IdlMetadata.MessageIdLiteral;

            return new DocMessageItem
            {
                MessageKey = messageKey,
                DisplayName = string.IsNullOrWhiteSpace(definition.IdlMetadata.StructName) ? messageKey : definition.IdlMetadata.StructName,
                MessageIdLiteral = messageIdLiteral,
                NumericMessageId = definition.IdlMetadata.MessageId,
                StructName = string.IsNullOrWhiteSpace(definition.IdlMetadata.StructName) ? messageKey : definition.IdlMetadata.StructName,
                GroupName = ResolveGroupName(definition),
                Description = string.IsNullOrWhiteSpace(definition.Description) ? "설명이 없습니다." : definition.Description,
                TotalSizeText = $"{totalBytes} bytes",
                TimeoutText = $"{definition.TimeoutMs} ms",
                HeaderCount = headerFields.Count,
                PayloadCount = payloadFields.Count,
                HeaderFields = new ObservableCollection<DocFieldItem>(headerFields),
                PayloadFields = new ObservableCollection<DocFieldItem>(payloadFields),
                EndpointText = ResolveEndpointText(definition),
                EndianDisplay = headerFields.Concat(payloadFields).Any(field => field.Endian == "Big") ? "Big Endian" : "Little Endian",
                Kind = ResolveMessageKind(definition.IdlMetadata.StructName)
            };
        }

        private static DocFieldItem CreateFieldItem(FieldDefinition field, bool isHeader, string headerMessageIdFieldName)
        {
            var typeDisplay = field.Type;
            if (field.Size.HasValue && (field.Type.Equals("string", StringComparison.OrdinalIgnoreCase) || field.Type.Equals("bytes", StringComparison.OrdinalIgnoreCase) || field.Type.Equals("bytearray", StringComparison.OrdinalIgnoreCase)))
            {
                typeDisplay = $"{field.Type}[{field.Size}]";
            }

            return new DocFieldItem
            {
                Name = field.Name,
                TypeDisplay = typeDisplay,
                ByteSize = field.ByteSize,
                ByteSizeText = $"{field.ByteSize} B",
                DefaultValue = string.IsNullOrWhiteSpace(field.Value) ? "-" : field.Value,
                Endian = field.Endian.Equals("big", StringComparison.OrdinalIgnoreCase) ? "Big" : "Little",
                Description = string.IsNullOrWhiteSpace(field.Description) ? "설명 없음" : field.Description,
                IsMessageIdField = isHeader && field.Name.Equals(headerMessageIdFieldName, StringComparison.OrdinalIgnoreCase),
                BitFields = new ObservableCollection<DocBitFieldItem>((field.BitFields ?? new List<BitFieldDefinition>()).Select(bit => new DocBitFieldItem
                {
                    Name = bit.Name,
                    BitRange = bit.SingleBit.HasValue ? $"[{bit.SingleBit}]" : $"[{bit.StartBit}:{bit.EndBit}]",
                    Description = string.IsNullOrWhiteSpace(bit.Description) ? "설명 없음" : bit.Description,
                    MaxValueText = bit.MaxValue.ToString()
                }))
            };
        }

        private static string ResolveEndpointText(MessageDefinition definition)
        {
            if (definition.Endpoint == null)
            {
                return "기본 서버";
            }

            if (!string.IsNullOrWhiteSpace(definition.Endpoint.ServerRef))
            {
                return definition.Endpoint.ServerRef.Replace("#/servers/", string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(definition.Endpoint.IpAddress) || definition.Endpoint.Port.HasValue)
            {
                return $"{definition.Endpoint.IpAddress ?? "-"}:{definition.Endpoint.Port?.ToString() ?? "-"}";
            }

            return "기본 서버";
        }

        private static string ResolveGroupName(MessageDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.Group))
            {
                return definition.Group;
            }

            var id = definition.IdlMetadata.MessageId;
            return id switch
            {
                < 1000 => "Common Messages",
                < 2000 => "Controller 1000",
                < 3000 => "Controller 2000",
                < 4000 => "GUI Messages",
                _ => "Other Messages"
            };
        }

        private static string ResolveMessageKind(string? name)
        {
            var lower = name?.ToLowerInvariant() ?? string.Empty;
            if (lower.Contains("response") || lower.Contains("res"))
            {
                return "Response";
            }

            if (lower.Contains("command") || lower.Contains("cmd"))
            {
                return "Command";
            }

            if (lower.Contains("request") || lower.Contains("req"))
            {
                return "Request";
            }

            return "Message";
        }
    }

    public partial class DocMessageGroup : ObservableObject
    {
        public DocMessageGroup(string name, int count)
        {
            Name = name;
            MessageCount = count;
        }

        public string Name { get; }

        public int MessageCount { get; }

        public ObservableCollection<DocMessageItem> Messages { get; } = new();

        [ObservableProperty]
        private bool isExpanded = true;
    }

    public partial class DocMessageItem : ObservableObject
    {
        public string MessageKey { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string MessageIdLiteral { get; set; } = string.Empty;

        public int NumericMessageId { get; set; }

        public string StructName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string GroupName { get; set; } = string.Empty;

        public string TotalSizeText { get; set; } = string.Empty;

        public string TimeoutText { get; set; } = string.Empty;

        public string EndpointText { get; set; } = string.Empty;

        public string EndianDisplay { get; set; } = string.Empty;

        public string Kind { get; set; } = "Message";

        public int HeaderCount { get; set; }

        public int PayloadCount { get; set; }

        public ObservableCollection<DocFieldItem> HeaderFields { get; set; } = new();

        public ObservableCollection<DocFieldItem> PayloadFields { get; set; } = new();

        [ObservableProperty]
        private bool isSelected;
    }

    public class DocFieldItem
    {
        public string Name { get; set; } = string.Empty;

        public string TypeDisplay { get; set; } = string.Empty;

        public int ByteSize { get; set; }

        public string ByteSizeText { get; set; } = string.Empty;

        public string DefaultValue { get; set; } = string.Empty;

        public string Endian { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsMessageIdField { get; set; }

        public bool HasBitFields => BitFields.Count > 0;

        public ObservableCollection<DocBitFieldItem> BitFields { get; set; } = new();
    }

    public class DocBitFieldItem
    {
        public string Name { get; set; } = string.Empty;

        public string BitRange { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string MaxValueText { get; set; } = string.Empty;
    }

    public class DocSummaryCard
    {
        public DocSummaryCard(string title, string value, string description)
        {
            Title = title;
            Value = value;
            Description = description;
        }

        public string Title { get; }

        public string Value { get; }

        public string Description { get; }
    }

    public class DocMessageActionEventArgs : EventArgs
    {
        public DocMessageActionEventArgs(string messageKey)
        {
            MessageKey = messageKey;
        }

        public string MessageKey { get; }
    }
}
