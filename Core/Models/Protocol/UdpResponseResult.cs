namespace MMG.Core.Models.Protocol
{
    /// <summary>
    /// UDP 응답 결과
    /// </summary>
    public class UdpResponseResult
    {
        public bool IsSuccess { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string? ErrorMessage { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.Now;
        public TimeSpan ResponseTime { get; set; }

        public static UdpResponseResult Success(byte[] data, TimeSpan responseTime) => new()
        {
            IsSuccess = true,
            Data = data,
            ResponseTime = responseTime
        };

        public static UdpResponseResult Timeout() => new()
        {
            IsSuccess = false,
            ErrorMessage = "응답 시간 초과"
        };

        public static UdpResponseResult Error(string message) => new()
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }
}
