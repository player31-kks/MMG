using System.Collections.ObjectModel;
using MMG.Core.Models.Schema;
using MMG.Core.Interfaces;
using MMG.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MMG.ViewModels.Spec
{
    /// <summary>
    /// ReDoc 스타일 문서 뷰어 ViewModel
    /// </summary>
    public partial class DocViewerViewModel : ObservableObject
    {
        private readonly ISpecParserFactory _parserFactory;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HtmlContent), nameof(MenuItems))]
        private UdpApiSpec? currentSpec;

        [ObservableProperty]
        private string htmlContent = "";

        [ObservableProperty]
        private string selectedMessageId = "";

        [ObservableProperty]
        private ObservableCollection<DocMenuItem> menuItems = new();

        public DocViewerViewModel(ISpecParserFactory parserFactory)
        {
            _parserFactory = parserFactory;
        }

        partial void OnCurrentSpecChanged(UdpApiSpec? value)
        {
            GenerateDocumentation();
            UpdateMenuItems();
        }

        partial void OnSelectedMessageIdChanged(string value)
        {
            ScrollToMessage(value);
        }

        #region Commands

        [RelayCommand]
        private void RefreshDoc() => GenerateDocumentation();

        #endregion

        #region Public Methods

        public void LoadSpec(UdpApiSpec spec)
        {
            CurrentSpec = spec;
        }

        public void LoadFromContent(string content, string filePath)
        {
            try
            {
                var parser = _parserFactory.CreateParser(filePath);
                var spec = parser.Parse(content);
                CurrentSpec = spec;
            }
            catch (Exception ex)
            {
                HtmlContent = GenerateErrorHtml(ex.Message);
            }
        }

        public void LoadFromIdl(string idlContent)
        {
            try
            {
                var parser = _parserFactory.CreateParser(SpecParserType.Idl);
                var spec = parser.Parse(idlContent);
                CurrentSpec = spec;
            }
            catch (Exception ex)
            {
                HtmlContent = GenerateErrorHtml(ex.Message);
            }
        }

        #endregion

        #region Private Methods

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
            var sidebarHtml = GenerateSidebarHtml(spec);
            var contentHtml = GenerateContentHtml(spec);

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>{spec.Info.Title}</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        html, body {{ height: 100%; width: 100%; overflow: hidden; }}
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #fff; color: #333; line-height: 1.7; font-size: 16px; }}
        
        /* Layout */
        .layout {{ display: flex; width: 100%; height: 100%; }}
        
        /* Sidebar */
        .sidebar {{ width: 400px; min-width: 400px; background: #1e1e2e; border-right: 1px solid #313244; display: flex; flex-direction: column; height: 100%; }}
        .sidebar-header {{ padding: 28px; border-bottom: 1px solid #313244; background: #181825; }}
        .sidebar-title {{ font-size: 24px; font-weight: 700; color: #cdd6f4; margin-bottom: 8px; }}
        .sidebar-version {{ font-size: 16px; color: #6c7086; }}
        
        /* Search */
        .search-box {{ padding: 20px; border-bottom: 1px solid #313244; }}
        .search-input {{ width: 100%; padding: 14px 18px; border: 1px solid #45475a; border-radius: 10px; font-size: 16px; background: #313244; color: #cdd6f4; }}
        .search-input:focus {{ outline: none; border-color: #89b4fa; }}
        .search-input::placeholder {{ color: #6c7086; }}
        
        /* Menu */
        .menu {{ flex: 1; overflow-y: auto; padding: 16px 0; }}
        .menu-group {{ margin-bottom: 12px; }}
        .menu-group-header {{ display: flex; align-items: center; justify-content: space-between; padding: 14px 24px; font-size: 15px; font-weight: 600; color: #a6adc8; cursor: pointer; user-select: none; text-transform: uppercase; letter-spacing: 0.5px; }}
        .menu-group-header:hover {{ background: #313244; }}
        .menu-group-header .arrow {{ font-size: 14px; color: #6c7086; transition: transform 0.2s; }}
        .menu-group.collapsed .arrow {{ transform: rotate(-90deg); }}
        .menu-group.collapsed .menu-items {{ display: none; }}
        
        .menu-item {{ display: flex; align-items: center; padding: 14px 24px 14px 36px; cursor: pointer; font-size: 16px; color: #bac2de; text-decoration: none; border-left: 4px solid transparent; transition: all 0.15s; }}
        .menu-item:hover {{ background: #313244; color: #cdd6f4; }}
        .menu-item.active {{ background: #45475a; border-left-color: #89b4fa; color: #89b4fa; }}
        .menu-item .msg-name {{ flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }}
        
        /* Message ID Badge - 색상 구분 */
        .msg-id {{ font-size: 13px; font-weight: 600; padding: 5px 12px; border-radius: 6px; margin-left: 12px; font-family: 'Consolas', 'Courier New', monospace; }}
        .msg-id.req {{ background: #1e66f5; color: #fff; }}
        .msg-id.res {{ background: #40a02b; color: #fff; }}
        .msg-id.cmd {{ background: #df8e1d; color: #fff; }}
        .msg-id.default {{ background: #6c7086; color: #fff; }}
        
        /* Content Area */
        .content {{ flex: 1; overflow-y: auto; background: #fff; height: 100%; }}
        .content-inner {{ max-width: 100%; padding: 40px 56px; }}
        
        /* Message Detail */
        .message-detail {{ margin-bottom: 56px; padding: 36px; background: #fff; border: 1px solid #e1e4e8; border-radius: 16px; box-shadow: 0 2px 4px rgba(0,0,0,0.08); }}
        .message-detail:last-child {{ margin-bottom: 40px; }}
        
        .detail-header {{ margin-bottom: 32px; padding-bottom: 20px; border-bottom: 1px solid #e1e4e8; }}
        .detail-title {{ display: flex; align-items: center; gap: 16px; margin-bottom: 16px; flex-wrap: wrap; }}
        .detail-title h2 {{ font-size: 28px; font-weight: 700; color: #1e1e2e; font-family: 'Consolas', 'Courier New', monospace; }}
        .detail-id {{ font-size: 15px; font-weight: 600; padding: 7px 16px; border-radius: 8px; }}
        .detail-id.req {{ background: #1e66f5; color: #fff; }}
        .detail-id.res {{ background: #40a02b; color: #fff; }}
        .detail-id.cmd {{ background: #df8e1d; color: #fff; }}
        .detail-id.default {{ background: #6c7086; color: #fff; }}
        .detail-desc {{ color: #5c5f77; font-size: 17px; line-height: 1.8; }}
        
        /* Info Box */
        .info-box {{ background: #f8f9fc; border: 1px solid #e1e4e8; border-radius: 14px; padding: 24px 28px; margin-bottom: 32px; }}
        .info-row {{ display: flex; align-items: center; padding: 10px 0; font-size: 16px; }}
        .info-label {{ width: 140px; color: #6c6f85; font-weight: 600; }}
        .info-value {{ color: #1e1e2e; font-family: 'Consolas', 'Courier New', monospace; font-weight: 500; font-size: 16px; }}
        
        /* Schema Section */
        .schema-section {{ margin-bottom: 32px; }}
        .schema-header {{ display: flex; align-items: center; gap: 14px; margin-bottom: 18px; padding-bottom: 14px; border-bottom: 2px solid #e1e4e8; }}
        .schema-title {{ font-size: 20px; font-weight: 700; color: #1e1e2e; }}
        .schema-badge {{ font-size: 14px; padding: 5px 14px; border-radius: 14px; background: #1e66f5; color: #fff; font-weight: 600; }}
        .schema-size {{ font-size: 15px; color: #6c6f85; margin-left: auto; font-weight: 500; }}
        
        /* Fields */
        .field-list {{ border: 1px solid #e1e4e8; border-radius: 14px; overflow: hidden; }}
        .field-item {{ display: flex; padding: 18px 24px; border-bottom: 1px solid #e1e4e8; background: #fff; }}
        .field-item:last-child {{ border-bottom: none; }}
        .field-item:hover {{ background: #f8f9fc; }}
        .field-item.header-field {{ background: #eff6ff; }}
        
        .field-info {{ flex: 1; }}
        .field-name-row {{ display: flex; align-items: center; gap: 14px; margin-bottom: 10px; flex-wrap: wrap; }}
        .field-name {{ font-family: 'Consolas', 'Courier New', monospace; font-weight: 700; color: #1e66f5; font-size: 17px; }}
        .field-type {{ font-family: 'Consolas', 'Courier New', monospace; font-size: 15px; color: #8839ef; background: #f3e8ff; padding: 4px 12px; border-radius: 6px; font-weight: 500; }}
        .field-desc {{ font-size: 15px; color: #6c6f85; line-height: 1.6; }}
        
        .field-meta {{ text-align: right; min-width: 110px; }}
        .field-size {{ font-size: 15px; color: #5c5f77; font-weight: 600; }}
        
        /* Bitfield */
        .bitfield-expand {{ margin-top: 18px; padding: 18px; background: #fef9c3; border: 1px solid #fde047; border-radius: 12px; }}
        .bitfield-title {{ font-size: 15px; font-weight: 700; color: #854d0e; margin-bottom: 14px; }}
        .bit-row {{ display: flex; align-items: center; padding: 8px 0; font-size: 15px; }}
        .bit-name {{ font-family: 'Consolas', 'Courier New', monospace; color: #7c3aed; min-width: 180px; font-weight: 600; }}
        .bit-pos {{ font-family: 'Consolas', 'Courier New', monospace; color: #ea580c; min-width: 90px; font-weight: 500; }}
        .bit-desc {{ color: #5c5f77; }}
        
        /* Empty State */
        .empty-state {{ display: flex; flex-direction: column; align-items: center; justify-content: center; height: 400px; color: #6c6f85; text-align: center; padding: 40px; }}
        .empty-state h3 {{ margin-bottom: 16px; color: #1e1e2e; font-size: 24px; }}
        .empty-state p {{ font-size: 17px; }}
        
        /* Server Info */
        .server-section {{ margin-bottom: 40px; padding: 32px; background: #f8f9fc; border: 1px solid #e1e4e8; border-radius: 16px; }}
        .server-title {{ font-size: 20px; font-weight: 700; color: #1e1e2e; margin-bottom: 20px; }}
        .server-item {{ display: flex; align-items: center; gap: 20px; padding: 14px 0; font-size: 16px; border-bottom: 1px solid #e1e4e8; }}
        .server-item:last-child {{ border-bottom: none; }}
        .server-name {{ font-weight: 600; color: #1e1e2e; min-width: 160px; }}
        .server-addr {{ font-family: 'Consolas', 'Courier New', monospace; color: #fff; background: #40a02b; padding: 6px 16px; border-radius: 8px; font-weight: 500; font-size: 15px; }}
        
        /* Scrollbar */
        .menu::-webkit-scrollbar, .content::-webkit-scrollbar {{ width: 12px; }}
        .menu::-webkit-scrollbar-track {{ background: #1e1e2e; }}
        .menu::-webkit-scrollbar-thumb {{ background: #45475a; border-radius: 6px; }}
        .content::-webkit-scrollbar-track {{ background: #f1f1f1; }}
        .content::-webkit-scrollbar-thumb {{ background: #c1c1c1; border-radius: 6px; }}
        
        /* Color Legend */
        .color-legend {{ padding: 16px 24px; border-top: 1px solid #313244; background: #181825; }}
        .legend-title {{ font-size: 12px; color: #6c7086; margin-bottom: 10px; text-transform: uppercase; letter-spacing: 0.5px; }}
        .legend-items {{ display: flex; flex-wrap: wrap; gap: 12px; }}
        .legend-item {{ display: flex; align-items: center; gap: 6px; font-size: 13px; color: #a6adc8; }}
        .legend-dot {{ width: 10px; height: 10px; border-radius: 3px; }}
        .legend-dot.req {{ background: #1e66f5; }}
        .legend-dot.res {{ background: #40a02b; }}
        .legend-dot.cmd {{ background: #df8e1d; }}
    </style>
</head>
<body>
    <div class='layout'>
        <div class='sidebar'>
            <div class='sidebar-header'>
                <div class='sidebar-title'>{spec.Info.Title}</div>
                <div class='sidebar-version'>v{spec.Info.Version}</div>
            </div>
            <div class='search-box'>
                <input type='text' class='search-input' placeholder='Search messages...' id='searchInput'>
            </div>
            <div class='menu' id='menuList'>
                {sidebarHtml}
            </div>
            <div class='color-legend'>
                <div class='legend-title'>Color Legend</div>
                <div class='legend-items'>
                    <div class='legend-item'><span class='legend-dot req'></span>Request</div>
                    <div class='legend-item'><span class='legend-dot res'></span>Response</div>
                    <div class='legend-item'><span class='legend-dot cmd'></span>Command</div>
                </div>
            </div>
        </div>
        <div class='content' id='contentArea'>
            <div class='content-inner'>
                {contentHtml}
            </div>
        </div>
    </div>
    <script>
        (function() {{
            var searchInput = document.getElementById('searchInput');
            if (searchInput) {{
                searchInput.addEventListener('keyup', function() {{
                    var filter = this.value.toLowerCase();
                    var items = document.getElementsByClassName('menu-item');
                    for (var i = 0; i < items.length; i++) {{
                        var text = items[i].textContent.toLowerCase();
                        items[i].style.display = text.indexOf(filter) > -1 ? 'flex' : 'none';
                    }}
                }});
            }}
            
            var menuItems = document.getElementsByClassName('menu-item');
            for (var i = 0; i < menuItems.length; i++) {{
                menuItems[i].addEventListener('click', function(e) {{
                    e.preventDefault();
                    var targetId = this.getAttribute('data-target');
                    var element = document.getElementById(targetId);
                    if (element) {{
                        element.scrollIntoView({{ behavior: 'smooth', block: 'start' }});
                        var allItems = document.getElementsByClassName('menu-item');
                        for (var j = 0; j < allItems.length; j++) {{
                            allItems[j].classList.remove('active');
                        }}
                        this.classList.add('active');
                    }}
                }});
            }}
            
            var groupHeaders = document.getElementsByClassName('menu-group-header');
            for (var i = 0; i < groupHeaders.length; i++) {{
                groupHeaders[i].addEventListener('click', function() {{
                    this.parentElement.classList.toggle('collapsed');
                }});
            }}
        }})();
    </script>
</body>
</html>";
            return html;
        }

        private static string GenerateSidebarHtml(UdpApiSpec spec)
        {
            if (spec.Messages == null || spec.Messages.Count == 0)
                return "<div class='empty-state'><p>메시지가 없습니다</p></div>";

            var sb = new System.Text.StringBuilder();

            // Group messages
            var groups = spec.Messages
                .GroupBy(m => string.IsNullOrEmpty(m.Value.Group) ? "Messages" : m.Value.Group)
                .OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                sb.Append($@"
                <div class='menu-group'>
                    <div class='menu-group-header'>
                        <span>{group.Key}</span>
                        <span class='arrow'>▼</span>
                    </div>
                    <div class='menu-items'>");

                foreach (var msg in group)
                {
                    var msgIdClass = GetMessageIdClass(msg.Key);
                    var shortId = ExtractMessageId(msg.Value.Description);
                    
                    sb.Append($@"
                        <div class='menu-item' data-target='{msg.Key}'>
                            <span class='msg-name'>{msg.Key}</span>
                            <span class='msg-id {msgIdClass}'>{shortId}</span>
                        </div>");
                }

                sb.Append(@"
                    </div>
                </div>");
            }

            return sb.ToString();
        }

        private static string GenerateContentHtml(UdpApiSpec spec)
        {
            var sb = new System.Text.StringBuilder();

            // Server info
            if (spec.Servers != null && spec.Servers.Count > 0)
            {
                sb.Append(@"<div class='server-section'><div class='server-title'>🖥️ Servers</div>");
                foreach (var server in spec.Servers)
                {
                    sb.Append($@"
                    <div class='server-item'>
                        <span class='server-name'>{server.Name}</span>
                        <span class='server-addr'>{server.IpAddress}:{server.Port}</span>
                        <span style='color: #6a737d;'>{server.Description}</span>
                    </div>");
                }
                sb.Append("</div>");
            }

            // Messages
            if (spec.Messages == null || spec.Messages.Count == 0)
            {
                return "<div class='empty-state'><h3>📄 정의된 메시지가 없습니다</h3><p>IDL 파일을 불러오면 메시지가 표시됩니다.</p></div>";
            }

            foreach (var kvp in spec.Messages)
            {
                var name = kvp.Key;
                var msg = kvp.Value;
                var msgId = ExtractMessageId(msg.Description);
                var msgIdClass = GetMessageIdClass(name);

                sb.Append($@"
                <div class='message-detail' id='{name}'>
                    <div class='detail-header'>
                        <div class='detail-title'>
                            <h2>{name}</h2>
                            <span class='detail-id {msgIdClass}'>ID: {msgId}</span>
                        </div>
                        <p class='detail-desc'>{msg.Description}</p>
                    </div>
                    
                    <div class='info-box'>
                        <div class='info-row'>
                            <span class='info-label'>Total Size</span>
                            <span class='info-value'>{msg.Request?.TotalSize ?? 0} bytes</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Timeout</span>
                            <span class='info-value'>{msg.TimeoutMs} ms</span>
                        </div>
                    </div>
                    
                    {GenerateSchemaSection("📋 Header Fields", msg.Request?.Header, true)}
                    {GenerateSchemaSection("📦 Payload Fields", msg.Request?.Payload, false)}
                </div>");
            }

            return sb.ToString();
        }

        private static string GenerateSchemaSection(string title, List<FieldDefinition>? fields, bool isHeader)
        {
            if (fields == null || fields.Count == 0) return "";

            var totalSize = fields.Sum(f => f.ByteSize);
            var sb = new System.Text.StringBuilder();

            sb.Append($@"
            <div class='schema-section'>
                <div class='schema-header'>
                    <span class='schema-title'>{title}</span>
                    <span class='schema-badge'>{fields.Count} fields</span>
                    <span class='schema-size'>{totalSize} bytes</span>
                </div>
                <div class='field-list'>");

            foreach (var field in fields)
            {
                var typeDisplay = field.Type + (field.Size > 1 ? $"[{field.Size}]" : "");
                var fieldClass = isHeader ? " header-field" : "";

                sb.Append($@"
                    <div class='field-item{fieldClass}'>
                        <div class='field-info'>
                            <div class='field-name-row'>
                                <span class='field-name'>{field.Name}</span>
                                <span class='field-type'>{typeDisplay}</span>
                            </div>
                            <div class='field-desc'>{field.Description}</div>
                            {GenerateBitfieldHtml(field)}
                        </div>
                        <div class='field-meta'>
                            <div class='field-size'>{field.ByteSize} bytes</div>
                        </div>
                    </div>");
            }

            sb.Append(@"
                </div>
            </div>");

            return sb.ToString();
        }

        private static string GenerateBitfieldHtml(FieldDefinition field)
        {
            if (!field.HasBitFields || field.BitFields == null) return "";

            var sb = new System.Text.StringBuilder();
            sb.Append($@"
                <div class='bitfield-expand'>
                    <div class='bitfield-title'>⚡ Bit Fields ({field.ByteSize * 8} bits)</div>");

            foreach (var bit in field.BitFields)
            {
                var bitPos = bit.SingleBit.HasValue ? $"[{bit.SingleBit}]" : $"[{bit.BitRange}]";
                sb.Append($@"
                    <div class='bit-row'>
                        <span class='bit-name'>{bit.Name}</span>
                        <span class='bit-pos'>{bitPos}</span>
                        <span class='bit-desc'>{bit.Description}</span>
                    </div>");
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private static string GetMessageIdClass(string messageName)
        {
            var lower = messageName.ToLower();
            if (lower.Contains("request") || lower.Contains("req")) return "req";
            if (lower.Contains("response") || lower.Contains("res")) return "res";
            if (lower.Contains("command") || lower.Contains("cmd")) return "cmd";
            return "default";
        }

        private static string ExtractMessageId(string description)
        {
            // "Message ID: 0x0001 (Name)" 형식에서 ID 추출
            if (string.IsNullOrEmpty(description)) return "N/A";
            
            var match = System.Text.RegularExpressions.Regex.Match(description, @"(?:ID[:\s]*)?(?:Message ID[:\s]*)?(0x[0-9A-Fa-f]+|\d+)");
            if (match.Success) return match.Groups[1].Value;
            
            // 이름에서 숫자 추출 시도
            match = System.Text.RegularExpressions.Regex.Match(description, @"(\d+)");
            return match.Success ? match.Groups[1].Value : "MSG";
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

        private static string GenerateEmptyHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; background: #fafbfc; color: #6a737d; }
        .empty { text-align: center; }
        .empty-icon { font-size: 64px; margin-bottom: 16px; }
        .empty h2 { color: #24292e; margin-bottom: 8px; font-weight: 600; }
        .empty p { color: #6a737d; }
    </style>
</head>
<body>
    <div class='empty'>
        <div class='empty-icon'>📄</div>
        <h2>문서가 없습니다</h2>
        <p>IDL 스펙 파일을 불러오면 문서가 표시됩니다.</p>
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
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 40px; background: #fff5f5; }}
        .error {{ background: white; border-left: 4px solid #dc3545; padding: 24px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); max-width: 600px; margin: 0 auto; }}
        .error-header {{ display: flex; align-items: center; gap: 12px; margin-bottom: 16px; }}
        .error-icon {{ font-size: 24px; }}
        .error h3 {{ color: #dc3545; font-size: 18px; }}
        .error pre {{ background: #f6f8fa; padding: 16px; border-radius: 6px; overflow-x: auto; font-size: 13px; color: #24292e; border: 1px solid #e1e4e8; }}
    </style>
</head>
<body>
    <div class='error'>
        <div class='error-header'>
            <span class='error-icon'>⚠️</span>
            <h3>파싱 오류</h3>
        </div>
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
