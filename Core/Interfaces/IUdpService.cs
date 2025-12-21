using MMG.Core.Models.Protocol;

namespace MMG.Core.Interfaces
{
    /// <summary>
    /// UDP 통신 서비스 인터페이스
    /// </summary>
    public interface IUdpService
    {
        /// <summary>
        /// UDP 메시지 전송 및 응답 수신
        /// </summary>
        Task<UdpResponseResult> SendAsync(UdpEndpoint endpoint, byte[] data, int timeoutMs = 5000);

        /// <summary>
        /// 데이터 수신 이벤트
        /// </summary>
        event EventHandler<UdpDataReceivedEventArgs>? DataReceived;
    }

    /// <summary>
    /// UDP 데이터 수신 이벤트 인자
    /// </summary>
    public class UdpDataReceivedEventArgs : EventArgs
    {
        public string RemoteAddress { get; init; }
        public int RemotePort { get; init; }
        public byte[] Data { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.Now;
    }
}
