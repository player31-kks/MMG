using YamlDotNet.Serialization;

namespace MMG.Core.Models.Schema
{
    /// <summary>
    /// UDP API 스펙 - OpenAPI 스타일의 UDP 메시지 정의
    /// </summary>
    public class UdpApiSpec
    {
        /// <summary>
        /// 스펙 버전 (예: "1.0.0")
        /// </summary>
        [YamlMember(Alias = "udpapi")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// API 정보
        /// </summary>
        [YamlMember(Alias = "info")]
        public ApiInfo Info { get; set; } = new();

        /// <summary>
        /// 서버/장비 정보
        /// </summary>
        [YamlMember(Alias = "servers")]
        public List<ServerInfo> Servers { get; set; } = new();

        /// <summary>
        /// 메시지 정의들
        /// </summary>
        [YamlMember(Alias = "messages")]
        public Dictionary<string, MessageDefinition> Messages { get; set; } = new();

        /// <summary>
        /// 공통 컴포넌트 (재사용 가능한 스키마)
        /// </summary>
        [YamlMember(Alias = "components")]
        public ComponentsDefinition? Components { get; set; }
    }

    /// <summary>
    /// API 정보
    /// </summary>
    public class ApiInfo
    {
        [YamlMember(Alias = "title")]
        public string Title { get; set; } = "UDP API";

        [YamlMember(Alias = "description")]
        public string Description { get; set; } = string.Empty;

        [YamlMember(Alias = "version")]
        public string Version { get; set; } = "1.0.0";

        [YamlMember(Alias = "contact")]
        public ContactInfo? Contact { get; set; }
    }

    /// <summary>
    /// 연락처 정보
    /// </summary>
    public class ContactInfo
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "email")]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// 서버/장비 정보
    /// </summary>
    public class ServerInfo
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "description")]
        public string Description { get; set; } = string.Empty;

        [YamlMember(Alias = "ip")]
        public string IpAddress { get; set; } = "127.0.0.1";

        [YamlMember(Alias = "port")]
        public int Port { get; set; } = 8080;
    }
}
