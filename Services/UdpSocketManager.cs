using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MMG.Models;

namespace MMG.Services
{
    /// <summary>
    /// UDP 소켓 관리자 - 포트별 UdpClient를 캐싱하여 재사용
    /// 하나의 포트로 송신과 수신을 동시에 처리
    /// </summary>
    public class UdpSocketManager : IDisposable
    {
        private static UdpSocketManager? _instance;
        private static readonly object _lock = new object();

        public static UdpSocketManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new UdpSocketManager();
                    }
                }
                return _instance;
            }
        }

        // 포트별 UdpClient 캐시
        private readonly ConcurrentDictionary<int, ManagedUdpClient> _portClients = new();

        // 데이터 수신 이벤트
        public event EventHandler<DataReceivedEventArgs>? DataReceived;

        private UdpSocketManager() { }

        /// <summary>
        /// 특정 포트에 바인딩 (이미 바인딩되어 있으면 기존 것 반환)
        /// </summary>
        public ManagedUdpClient BindPort(int port)
        {
            return _portClients.GetOrAdd(port, p =>
            {
                var managedClient = new ManagedUdpClient(p);
                managedClient.DataReceived += OnDataReceived;
                managedClient.StartReceiveLoop();
                return managedClient;
            });
        }

        /// <summary>
        /// 포트가 바인딩되어 있는지 확인
        /// </summary>
        public bool IsPortBound(int port)
        {
            return _portClients.ContainsKey(port) && _portClients[port].IsActive;
        }

        /// <summary>
        /// 바인딩된 포트의 UdpClient 가져오기
        /// </summary>
        public ManagedUdpClient? GetClient(int port)
        {
            return _portClients.TryGetValue(port, out var client) ? client : null;
        }

        /// <summary>
        /// 특정 포트 해제
        /// </summary>
        public void UnbindPort(int port)
        {
            if (_portClients.TryRemove(port, out var client))
            {
                client.DataReceived -= OnDataReceived;
                client.Dispose();
            }
        }

        /// <summary>
        /// 모든 포트 해제
        /// </summary>
        public void UnbindAllPorts()
        {
            foreach (var port in _portClients.Keys.ToList())
            {
                UnbindPort(port);
            }
        }

        /// <summary>
        /// 메시지 송신 (자동 포트 바인딩)
        /// </summary>
        public async Task<UdpResponse> SendAsync(UdpRequest request, int? localPort = null)
        {
            var response = new UdpResponse();

            try
            {
                // 로컬 포트 결정
                int bindPort = localPort ?? GetLocalPort(request);

                // 포트 바인딩 (없으면 자동 생성)
                var client = BindPort(bindPort);

                // 메시지 빌드
                var messageBytes = BuildMessage(request.Headers, request.Payload, request.IsBigEndian);

                // 송신
                var endpoint = new IPEndPoint(IPAddress.Parse(request.IpAddress), request.Port);
                await client.SendAsync(messageBytes, endpoint);

                response.Status = "Sent";
                response.ReceivedAt = DateTime.Now;

                // 응답 대기가 필요한 경우
                if (request.WaitForResponse)
                {
                    // 수신 대기 (5초 타임아웃)
                    var receiveResult = await client.WaitForResponseAsync(5000);

                    if (receiveResult != null)
                    {
                        response.RawData = receiveResult.Value.Data;
                        response.Status = "Success";
                    }
                    else
                    {
                        response.Status = "Timeout";
                    }
                }
            }
            catch (Exception ex)
            {
                response.Status = $"Error: {ex.Message}";
            }

            response.ReceivedAt = DateTime.Now;
            return response;
        }

        private int GetLocalPort(UdpRequest request)
        {
            // Request-specific custom local port takes priority
            if (request.UseCustomLocalPort && request.CustomLocalPort > 0)
            {
                return request.CustomLocalPort;
            }

            var settings = SettingsService.Instance;

            // Check if global custom port is enabled
            if (settings.UseCustomPort)
            {
                return settings.CustomPort;
            }
            else
            {
                // 랜덤 포트 사용 (0으로 지정하면 OS가 자동 할당)
                return 0;
            }
        }

        private void OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        public byte[] BuildMessage(IEnumerable<DataField> headers, IEnumerable<DataField> payload, bool isBigEndian)
        {
            var messageBytes = new List<byte>();

            foreach (var header in headers)
            {
                messageBytes.AddRange(ConvertValueToBytes(header, isBigEndian));
            }

            foreach (var field in payload)
            {
                messageBytes.AddRange(ConvertValueToBytes(field, isBigEndian));
            }

            return messageBytes.ToArray();
        }

        private byte[] ConvertValueToBytes(DataField field, bool isBigEndian)
        {
            try
            {
                byte[] bytes = field.Type switch
                {
                    DataType.Byte => new[] { ParseValue<byte>(field.Value) },
                    DataType.Int16 => BitConverter.GetBytes(ParseValue<short>(field.Value)),
                    DataType.UInt16 => BitConverter.GetBytes(ParseValue<ushort>(field.Value)),
                    DataType.Int => BitConverter.GetBytes(ParseValue<int>(field.Value)),
                    DataType.UInt => BitConverter.GetBytes(ParseValue<uint>(field.Value)),
                    DataType.Float => BitConverter.GetBytes(ParseValue<float>(field.Value)),
                    DataType.Padding => new byte[field.PaddingSize],
                    _ => Array.Empty<byte>()
                };

                if (isBigEndian && bytes.Length > 1 && field.Type != DataType.Padding)
                {
                    Array.Reverse(bytes);
                }

                return bytes;
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private T ParseValue<T>(string value) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return default(T);

            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ParseHexValue<T>(value.Substring(2));
            }

            if (value.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                return ParseBinaryValue<T>(value.Substring(2));
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }

        private T ParseHexValue<T>(string hexValue) where T : struct
        {
            try
            {
                hexValue = hexValue.Replace(" ", "").Replace("_", "");
                var type = typeof(T);

                if (type == typeof(byte)) return (T)(object)Convert.ToByte(hexValue, 16);
                if (type == typeof(short)) return (T)(object)Convert.ToInt16(hexValue, 16);
                if (type == typeof(ushort)) return (T)(object)Convert.ToUInt16(hexValue, 16);
                if (type == typeof(int)) return (T)(object)Convert.ToInt32(hexValue, 16);
                if (type == typeof(uint)) return (T)(object)Convert.ToUInt32(hexValue, 16);
                if (type == typeof(float))
                {
                    var intValue = Convert.ToUInt32(hexValue, 16);
                    return (T)(object)BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);
                }
                return default(T);
            }
            catch { return default(T); }
        }

        private T ParseBinaryValue<T>(string binaryValue) where T : struct
        {
            try
            {
                binaryValue = binaryValue.Replace(" ", "").Replace("_", "");
                var type = typeof(T);

                if (type == typeof(byte)) return (T)(object)Convert.ToByte(binaryValue, 2);
                if (type == typeof(short)) return (T)(object)Convert.ToInt16(binaryValue, 2);
                if (type == typeof(ushort)) return (T)(object)Convert.ToUInt16(binaryValue, 2);
                if (type == typeof(int)) return (T)(object)Convert.ToInt32(binaryValue, 2);
                if (type == typeof(uint)) return (T)(object)Convert.ToUInt32(binaryValue, 2);
                if (type == typeof(float))
                {
                    var intValue = Convert.ToUInt32(binaryValue, 2);
                    return (T)(object)BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);
                }
                return default(T);
            }
            catch { return default(T); }
        }

        public void Dispose()
        {
            UnbindAllPorts();
        }
    }

    /// <summary>
    /// 관리되는 UdpClient - 수신 루프 포함
    /// </summary>
    public class ManagedUdpClient : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly int _port;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveLoopTask;
        private readonly ConcurrentQueue<ReceivedData> _receivedQueue = new();
        private readonly SemaphoreSlim _receiveSignal = new(0);

        public event EventHandler<DataReceivedEventArgs>? DataReceived;

        public int Port => _port;
        public bool IsActive => _receiveCts != null && !_receiveCts.IsCancellationRequested;

        public ManagedUdpClient(int port)
        {
            _port = port;

            if (port == 0)
            {
                // 랜덤 포트
                _udpClient = new UdpClient(0);
                _port = ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;
            }
            else
            {
                _udpClient = new UdpClient(port);
            }
        }

        /// <summary>
        /// 실제 바인딩된 포트 (0으로 지정했을 경우 OS가 할당한 포트)
        /// </summary>
        public int ActualPort => _port;

        /// <summary>
        /// 수신 루프 시작
        /// </summary>
        public void StartReceiveLoop()
        {
            if (_receiveCts != null) return;

            _receiveCts = new CancellationTokenSource();
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(cancellationToken);

                    var receivedData = new ReceivedData
                    {
                        Data = result.Buffer,
                        RemoteEndPoint = result.RemoteEndPoint,
                        ReceivedAt = DateTime.Now
                    };

                    // 큐에 추가
                    _receivedQueue.Enqueue(receivedData);
                    _receiveSignal.Release();

                    // 이벤트 발생
                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        IpAddress = result.RemoteEndPoint.Address.ToString(),
                        Port = result.RemoteEndPoint.Port,
                        Data = result.Buffer,
                        Timestamp = DateTime.Now
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // 소켓 오류 시 루프 계속
                }
                catch (Exception)
                {
                    // 기타 오류 시 잠시 대기 후 계속
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        /// <summary>
        /// 메시지 송신
        /// </summary>
        public async Task<int> SendAsync(byte[] data, IPEndPoint remoteEndPoint)
        {
            return await _udpClient.SendAsync(data, data.Length, remoteEndPoint);
        }

        /// <summary>
        /// 응답 대기 (타임아웃 지정)
        /// </summary>
        public async Task<ReceivedData?> WaitForResponseAsync(int timeoutMs)
        {
            // 이미 큐에 데이터가 있는지 확인
            if (_receivedQueue.TryDequeue(out var existingData))
            {
                return existingData;
            }

            // 타임아웃 내에 데이터 수신 대기
            var received = await _receiveSignal.WaitAsync(timeoutMs);

            if (received && _receivedQueue.TryDequeue(out var data))
            {
                return data;
            }

            return null;
        }

        /// <summary>
        /// 수신 큐 비우기
        /// </summary>
        public void ClearReceiveQueue()
        {
            // 큐와 세마포어를 동기화하여 비움
            while (_receivedQueue.TryDequeue(out _))
            {
                // 각 아이템에 대해 세마포어 카운트도 감소
                _receiveSignal.Wait(0);
            }
            
            // 혹시 세마포어 카운트가 남아있으면 추가로 비움
            while (_receiveSignal.CurrentCount > 0)
            {
                _receiveSignal.Wait(0);
            }
        }

        public void Dispose()
        {
            _receiveCts?.Cancel();

            try
            {
                _receiveLoopTask?.Wait(1000);
            }
            catch { }

            _receiveCts?.Dispose();
            _udpClient.Dispose();
            _receiveSignal.Dispose();
        }
    }

    /// <summary>
    /// 수신된 데이터
    /// </summary>
    public struct ReceivedData
    {
        public byte[] Data { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }
        public DateTime ReceivedAt { get; set; }
    }
}
