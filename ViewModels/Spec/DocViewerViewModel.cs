using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MMG.Core.Models.Schema;
using MMG.Core.Services;
using MMG.ViewModels.Base;

namespace MMG.ViewModels.Spec
{
    /// <summary>
    /// ReDoc 스타일 문서 뷰어 ViewModel
    /// </summary>
    public class DocViewerViewModel : ViewModelBase
    {
        private readonly UdpApiSpecParser _specParser;
        private UdpApiSpec? _currentSpec;
        private string _htmlContent = "";
        private string _selectedMessageId = "";
        private ObservableCollection<DocMenuItem> _menuItems = new();

        public DocViewerViewModel()
        {
            _specParser = new UdpApiSpecParser();

            RefreshDocCommand = new RelayCommand(RefreshDocumentation);
        }

        #region Properties

        public UdpApiSpec? CurrentSpec
        {
            get => _currentSpec;
            set
            {
                if (SetProperty(ref _currentSpec, value))
                {
                    GenerateDocumentation();
                    UpdateMenuItems();
                }
            }
        }

        public string HtmlContent
        {
            get => _htmlContent;
            set => SetProperty(ref _htmlContent, value);
        }

        public string SelectedMessageId
        {
            get => _selectedMessageId;
            set
            {
                if (SetProperty(ref _selectedMessageId, value))
                    ScrollToMessage(value);
            }
        }

        public ObservableCollection<DocMenuItem> MenuItems
        {
            get => _menuItems;
            set => SetProperty(ref _menuItems, value);
        }

        #endregion

        #region Commands

        public ICommand RefreshDocCommand { get; }

        #endregion

        #region Public Methods

        public void LoadSpec(UdpApiSpec spec)
        {
            CurrentSpec = spec;
        }

        public void LoadFromYaml(string yamlContent)
        {
            try
            {
                var spec = _specParser.ParseYaml(yamlContent);
                CurrentSpec = spec;
            }
            catch (Exception ex)
            {
                HtmlContent = GenerateErrorHtml(ex.Message);
            }
        }

        #endregion

        #region Private Methods

        private void RefreshDocumentation()
        {
            GenerateDocumentation();
        }

        private void GenerateDocumentation()
        {
            if (CurrentSpec == null)
            {
                HtmlContent = GenerateEmptyHtml();
                return;
            }

            HtmlContent = GenerateReDocStyleHtml(CurrentSpec);
        }

        private void UpdateMenuItems()
        {
            MenuItems.Clear();
            if (CurrentSpec?.Messages == null) return;

            // Group messages by their group property
            var groups = CurrentSpec.Messages
                .GroupBy(m => string.IsNullOrEmpty(m.Value.Group) ? "기타" : m.Value.Group)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                var groupItem = new DocMenuItem { Name = group.Key, IsGroup = true };
                foreach (var message in group)
                {
                    groupItem.Children.Add(new DocMenuItem
                    {
                        Name = message.Key,
                        Description = message.Value.Description,
                        MessageId = message.Key
                    });
                }
                MenuItems.Add(groupItem);
            }
        }

        private void ScrollToMessage(string messageId)
        {
            // WebView2가 있으면 JavaScript로 스크롤
            // 현재는 HTML 앵커 사용
        }

        private static string GenerateReDocStyleHtml(UdpApiSpec spec)
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>{spec.Info.Title}</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #fafafa; color: #333; line-height: 1.6; }}
        .container {{ max-width: 1200px; margin: 0 auto; padding: 20px; }}
        
        /* Header */
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 40px 20px; margin-bottom: 30px; border-radius: 8px; }}
        .header h1 {{ font-size: 2.5em; margin-bottom: 10px; color: #333; }}
        .header .version {{ background: rgba(255,255,255,0.2); padding: 4px 12px; border-radius: 20px; font-size: 0.9em; display: inline-block; color: #333; }}
        .header .description {{ margin-top: 15px; opacity: 0.9; color: #333; }}
        
        /* Servers */
        .servers {{ background: white; border-radius: 8px; padding: 20px; margin-bottom: 30px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .servers h2 {{ color: #667eea; margin-bottom: 15px; font-size: 1.3em; }}
        .server-item {{ display: flex; align-items: center; padding: 10px; background: #f8f9fa; border-radius: 4px; margin-bottom: 8px; }}
        .server-name {{ font-weight: 600; color: #495057; min-width: 120px; }}
        .server-url {{ font-family: 'Consolas', monospace; color: #28a745; }}
        
        /* Messages */
        .message {{ background: white; border-radius: 8px; margin-bottom: 20px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .message-header {{ background: #f8f9fa; padding: 15px 20px; border-bottom: 1px solid #e9ecef; display: flex; align-items: center; }}
        .message-name {{ font-size: 1.2em; font-weight: 600; color: #333; font-family: 'Consolas', monospace; }}
        .message-group {{ background: #667eea; color: white; padding: 2px 8px; border-radius: 3px; font-size: 0.75em; margin-left: 10px; }}
        .message-body {{ padding: 20px; }}
        .message-description {{ color: #666; margin-bottom: 15px; }}
        
        /* Schema */
        .schema {{ margin-top: 15px; }}
        .schema-title {{ font-weight: 600; color: #495057; margin-bottom: 10px; display: flex; align-items: center; }}
        .schema-title .badge {{ background: #17a2b8; color: white; padding: 2px 8px; border-radius: 3px; font-size: 0.75em; margin-left: 8px; }}
        
        /* Fields Table */
        .fields-table {{ width: 100%; border-collapse: collapse; }}
        .fields-table th {{ background: #f8f9fa; padding: 10px; text-align: left; font-weight: 600; color: #495057; border-bottom: 2px solid #dee2e6; }}
        .fields-table td {{ padding: 10px; border-bottom: 1px solid #e9ecef; }}
        .fields-table tr:hover {{ background: #f8f9fa; }}
        .field-name {{ font-family: 'Consolas', monospace; color: #d63384; }}
        .field-type {{ font-family: 'Consolas', monospace; color: #0d6efd; }}
        .field-size {{ color: #6c757d; font-size: 0.9em; }}
        
        /* Footer */
        .footer {{ text-align: center; padding: 30px; color: #adb5bd; font-size: 0.9em; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>{spec.Info.Title}</h1>
            <span class='version'>v{spec.Info.Version}</span>
            <p class='description'>{spec.Info.Description}</p>
        </div>
        
        {GenerateServersHtml(spec.Servers)}
        
        <h2 style='color: #495057; margin-bottom: 20px;'>📨 Messages</h2>
        {GenerateMessagesHtml(spec.Messages)}
        
        <div class='footer'>
            Generated by MMG - UDP API Documentation
        </div>
    </div>
</body>
</html>";
            return html;
        }

        private static string GenerateServersHtml(List<ServerInfo>? servers)
        {
            if (servers == null || servers.Count == 0) return "";

            var serverItems = string.Join("", servers.Select(s => $@"
                <div class='server-item'>
                    <span class='server-name'>{s.Name}</span>
                    <span class='server-url'>{s.IpAddress}:{s.Port}</span>
                    <span style='margin-left: 15px; color: #6c757d;'>{s.Description}</span>
                </div>"));

            return $@"
        <div class='servers'>
            <h2>🖥️ Servers</h2>
            {serverItems}
        </div>";
        }

        private static string GenerateMessagesHtml(Dictionary<string, MessageDefinition>? messages)
        {
            if (messages == null || messages.Count == 0)
                return "<p style='color: #6c757d;'>정의된 메시지가 없습니다.</p>";

            var messageHtml = new System.Text.StringBuilder();

            foreach (var kvp in messages)
            {
                var name = kvp.Key;
                var message = kvp.Value;

                var groupBadge = string.IsNullOrEmpty(message.Group) ? "" :
                    $"<span class='message-group'>{message.Group}</span>";

                var requestSchema = GenerateSchemaHtml("Request", message.Request);
                var responseSchema = message.Response != null
                    ? GenerateSchemaHtml("Response", message.Response)
                    : "";

                messageHtml.Append($@"
        <div class='message' id='{name}'>
            <div class='message-header'>
                <span class='message-name'>{name}</span>
                {groupBadge}
            </div>
            <div class='message-body'>
                <p class='message-description'>{message.Description}</p>
                {requestSchema}
                {responseSchema}
            </div>
        </div>");
            }

            return messageHtml.ToString();
        }

        private static string GenerateSchemaHtml(string title, MessageSchema? schema)
        {
            if (schema == null) return "";

            var headerRows = GenerateFieldRows(schema.Header, "Header");
            var payloadRows = GenerateFieldRows(schema.Payload, "Payload");

            var headerSection = schema.Header?.Count > 0 ? $@"
                <div class='schema'>
                    <div class='schema-title'>📋 Header <span class='badge'>{schema.Header.Count} fields</span></div>
                    <table class='fields-table'>
                        <thead><tr><th>Name</th><th>Type</th><th>Size</th><th>Description</th></tr></thead>
                        <tbody>{headerRows}</tbody>
                    </table>
                </div>" : "";

            var payloadSection = schema.Payload?.Count > 0 ? $@"
                <div class='schema'>
                    <div class='schema-title'>📦 Payload <span class='badge'>{schema.Payload.Count} fields</span></div>
                    <table class='fields-table'>
                        <thead><tr><th>Name</th><th>Type</th><th>Size</th><th>Description</th></tr></thead>
                        <tbody>{payloadRows}</tbody>
                    </table>
                </div>" : "";

            return $@"
            <h4 style='margin-top: 20px; color: #333;'>{title} Schema (Total: {schema.TotalSize} bytes)</h4>
            {headerSection}
            {payloadSection}";
        }

        private static string GenerateFieldRows(List<FieldDefinition>? fields, string section)
        {
            if (fields == null || fields.Count == 0) return "";

            return string.Join("", fields.Select(f => $@"
                <tr>
                    <td class='field-name'>{f.Name}</td>
                    <td class='field-type'>{f.Type}{(f.Size > 1 ? $"[{f.Size}]" : "")}</td>
                    <td class='field-size'>{f.ByteSize} bytes</td>
                    <td>{f.Description}</td>
                </tr>"));
        }

        private static string GenerateEmptyHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; background: #f8f9fa; color: #6c757d; }
        .empty { text-align: center; }
        .empty h2 { color: #adb5bd; margin-bottom: 10px; }
    </style>
</head>
<body>
    <div class='empty'>
        <h2>📄 문서가 없습니다</h2>
        <p>YAML 스펙 파일을 불러오면 문서가 표시됩니다.</p>
    </div>
</body>
</html>";
        }

        private static string GenerateErrorHtml(string error)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 40px; background: #fff5f5; }}
        .error {{ background: white; border-left: 4px solid #dc3545; padding: 20px; border-radius: 4px; }}
        .error h3 {{ color: #dc3545; margin-bottom: 10px; }}
        .error pre {{ background: #f8f9fa; padding: 15px; border-radius: 4px; overflow-x: auto; }}
    </style>
</head>
<body>
    <div class='error'>
        <h3>⚠️ 오류 발생</h3>
        <pre>{error}</pre>
    </div>
</body>
</html>";
        }

        #endregion
    }

    /// <summary>
    /// 문서 메뉴 아이템
    /// </summary>
    public class DocMenuItem
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string MessageId { get; set; } = "";
        public bool IsGroup { get; set; }
        public ObservableCollection<DocMenuItem> Children { get; set; } = new();
    }
}
