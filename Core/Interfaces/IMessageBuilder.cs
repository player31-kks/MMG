namespace MMG.Core.Interfaces
{
    /// <summary>
    /// UDP 메시지 빌더 인터페이스
    /// </summary>
    public interface IMessageBuilder
    {
        /// <summary>
        /// 헤더와 페이로드를 바이트 배열로 변환
        /// </summary>
        byte[] Build();

        /// <summary>
        /// 메시지 총 크기 (bytes)
        /// </summary>
        int TotalSize { get; }
    }
}
