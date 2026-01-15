namespace MMG.Core.Models.Schema
{
    /// <summary>
    /// 메시지 정의
    /// </summary>
    public class MessageDefinition
    {
        /// <summary>
        /// 메시지 설명
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 메시지 그룹/카테고리
        /// </summary>
        public string Group { get; set; } = string.Empty;

        /// <summary>
        /// 타겟 엔드포인트 (서버 참조 또는 직접 지정)
        /// </summary>
        public EndpointReference? Endpoint { get; set; }

        /// <summary>
        /// 요청 메시지 스키마
        /// </summary>
        public MessageSchema Request { get; set; } = new();

        /// <summary>
        /// 응답 메시지 스키마
        /// </summary>
        public MessageSchema? Response { get; set; }

        /// <summary>
        /// 타임아웃 (ms)
        /// </summary>
        public int TimeoutMs { get; set; } = 5000;
    }

    /// <summary>
    /// 엔드포인트 참조
    /// </summary>
    public class EndpointReference
    {
        /// <summary>
        /// 서버 참조 (servers에 정의된 이름)
        /// </summary>
        public string? ServerRef { get; set; }

        /// <summary>
        /// 직접 IP 지정
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// 직접 포트 지정
        /// </summary>
        public int? Port { get; set; }
    }

    /// <summary>
    /// 메시지 스키마 (헤더 + 페이로드)
    /// </summary>
    public class MessageSchema
    {
        /// <summary>
        /// 헤더 필드들
        /// </summary>
        public List<FieldDefinition> Header { get; set; } = new();

        /// <summary>
        /// 페이로드 필드들
        /// </summary>
        public List<FieldDefinition> Payload { get; set; } = new();

        /// <summary>
        /// 전체 메시지 크기 계산
        /// </summary>
        public int TotalSize => Header.Sum(f => f.ByteSize) + Payload.Sum(f => f.ByteSize);
    }
}
