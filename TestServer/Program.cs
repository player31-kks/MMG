using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MMG.Test
{
    public class SimpleUdpServer
    {
        private UdpClient? _udpClient;
        private IPEndPoint? _endpoint;
        private bool _isRunning;

        public async Task StartAsync(int port = 8080)
        {
            _endpoint = new IPEndPoint(IPAddress.Any, port);
            _udpClient = new UdpClient(_endpoint);
            _isRunning = true;

            Console.WriteLine($"UDP Server started on port {port}");

            try
            {
                while (_isRunning)
                {
                    var result = await _udpClient.ReceiveAsync();
                    Console.WriteLine($"Received {result.Buffer.Length} bytes from {result.RemoteEndPoint}");
                    Console.WriteLine($"Data (Hex): {Convert.ToHexString(result.Buffer)}");

                    // Echo back with some additional data
                    var responseData = new List<byte>();
                    responseData.AddRange(result.Buffer); // Echo original data
                    responseData.AddRange(BitConverter.GetBytes(DateTime.Now.Ticks)); // Add timestamp
                    responseData.AddRange(BitConverter.GetBytes(42)); // Add some test int

                    await _udpClient.SendAsync(responseData.ToArray(), responseData.Count, result.RemoteEndPoint);
                    Console.WriteLine($"Sent {responseData.Count} bytes back to {result.RemoteEndPoint}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _udpClient?.Close();
            _udpClient?.Dispose();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new SimpleUdpServer();

            Console.WriteLine("Starting UDP Test Server...");
            Console.WriteLine("Press 'q' to quit");

            var serverTask = server.StartAsync(8080);

            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                {
                    server.Stop();
                    break;
                }
            }

            Console.WriteLine("Server stopped.");
        }
    }
}
