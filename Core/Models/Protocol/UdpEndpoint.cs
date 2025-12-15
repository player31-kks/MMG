namespace MMG.Core.Models.Protocol
{
    /// <summary>
    /// UDP 엔드포인트 정보
    /// </summary>
    public class UdpEndpoint
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;

        public UdpEndpoint() { }

        public UdpEndpoint(string ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;
        }

        public override string ToString() => $"{IpAddress}:{Port}";
    }
}
