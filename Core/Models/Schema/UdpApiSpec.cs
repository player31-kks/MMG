namespace MMG.Core.Models.Schema
{
    /// <summary>
    /// UDP API 스펙 - 다양한 형식(IDL, XML 등)의 UDP 메시지 정의
    /// </summary>
    public class UdpApiSpec
    {
        /// <summary>
        /// 스펙 버전 (예: "1.0.0")
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// API 정보
        /// </summary>
        public ApiInfo Info { get; set; } = new();

        /// <summary>
        /// 서버/장비 정보
        /// </summary>
        public List<ServerInfo> Servers { get; set; } = new();

        /// <summary>
        /// 메시지 정의들
        /// </summary>
        public Dictionary<string, MessageDefinition> Messages { get; set; } = new();

        /// <summary>
        /// 공통 컴포넌트 (재사용 가능한 스키마)
        /// </summary>
        public ComponentsDefinition? Components { get; set; }

        /// <summary>
        /// IDL 원본 규칙 메타데이터
        /// </summary>
        public IdlSpecMetadata IdlMetadata { get; set; } = new();
    }

    /// <summary>
    /// README 기준 IDL 전역 메타데이터
    /// </summary>
    public class IdlSpecMetadata
    {
        public int PackSize { get; set; } = 1;

        public bool IsBigEndian { get; set; } = true;

        public string HeaderStructName { get; set; } = "MsgHeader";

        public string HeaderMessageIdFieldName { get; set; } = "MsgID";
    }

    /// <summary>
    /// API 정보
    /// </summary>
    public class ApiInfo
    {
        public string Title { get; set; } = "UDP API";

        public string Description { get; set; } = string.Empty;

        public string Version { get; set; } = "1.0.0";

        public ContactInfo? Contact { get; set; }
    }

    /// <summary>
    /// 연락처 정보
    /// </summary>
    public class ContactInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// 서버/장비 정보
    /// </summary>
    public class ServerInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string IpAddress { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 8080;
    }
}
