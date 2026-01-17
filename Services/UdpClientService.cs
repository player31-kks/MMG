using System.Net;
using System.Net.Sockets;
using MMG.Models;

namespace MMG.Services
{
    public class DataReceivedEventArgs : EventArgs
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class UdpClientService
    {
        private readonly UdpSocketManager _socketManager;

        public event EventHandler<DataReceivedEventArgs>? DataReceived;

        // 현재 바인딩된 포트 (UI 표시용)
        public int? CurrentBoundPort { get; private set; }
        public bool IsPortBound => CurrentBoundPort.HasValue && _socketManager.IsPortBound(CurrentBoundPort.Value);

        public UdpClientService()
        {
            _socketManager = UdpSocketManager.Instance;
            _socketManager.DataReceived += OnSocketDataReceived;
        }

        private void OnSocketDataReceived(object? sender, DataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        /// <summary>
        /// 특정 포트에 바인딩 (지속적인 송수신 가능)
        /// </summary>
        public int BindPort(int port)
        {
            var client = _socketManager.BindPort(port);
            CurrentBoundPort = client.ActualPort;
            return client.ActualPort;
        }

        /// <summary>
        /// 현재 바인딩된 포트 해제
        /// </summary>
        public void UnbindPort()
        {
            if (CurrentBoundPort.HasValue)
            {
                _socketManager.UnbindPort(CurrentBoundPort.Value);
                CurrentBoundPort = null;
            }
        }

        /// <summary>
        /// 특정 포트 해제
        /// </summary>
        public void UnbindPort(int port)
        {
            _socketManager.UnbindPort(port);
            if (CurrentBoundPort == port)
            {
                CurrentBoundPort = null;
            }
        }

        /// <summary>
        /// 모든 포트 해제
        /// </summary>
        public void UnbindAllPorts()
        {
            _socketManager.UnbindAllPorts();
            CurrentBoundPort = null;
        }

        public async Task<UdpResponse> SendRequestAsync(UdpRequest request, ResponseSchema? responseSchema = null)
        {
            var response = new UdpResponse();

            try
            {
                // 로컬 포트 결정
                int localPort = GetLocalPort(request);

                // 바인딩된 포트가 있고 요청의 로컬 포트와 같으면 해당 포트 사용
                // 아니면 새로 바인딩
                ManagedUdpClient client;

                if (CurrentBoundPort.HasValue && 
                    (localPort == 0 || localPort == CurrentBoundPort.Value))
                {
                    // 기존 바인딩된 포트 사용
                    client = _socketManager.GetClient(CurrentBoundPort.Value)!;
                }
                else if (localPort > 0)
                {
                    // 특정 포트로 바인딩
                    client = _socketManager.BindPort(localPort);
                }
                else
                {
                    // 새 랜덤 포트로 바인딩
                    client = _socketManager.BindPort(0);
                }

                // 수신 큐 비우기 (이전 응답 무시)
                client.ClearReceiveQueue();

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
                    var receiveResult = await client.WaitForResponseAsync(5000);

                    if (receiveResult != null)
                    {
                        response.RawData = receiveResult.Value.Data;
                        response.Status = "Success";

                        // Parse response if schema is provided
                        if (responseSchema != null)
                        {
                            response.ParsedData = ParseResponse(receiveResult.Value.Data, responseSchema);
                        }
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
                // Use the global custom port
                return settings.CustomPort;
            }
            else
            {
                // Use the same port as target when custom port is disabled
                return request.Port;
            }
        }

        private byte[] BuildMessage(IEnumerable<DataField> headers, IEnumerable<DataField> payload, bool isBigEndian)
        {
            var messageBytes = new List<byte>();

            // Add headers
            foreach (var header in headers)
            {
                messageBytes.AddRange(ConvertValueToBytes(header, isBigEndian));
            }

            // Add payload
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
                    DataType.Padding => new byte[field.PaddingSize], // Creates array of zeros
                    _ => Array.Empty<byte>()
                };

                // BigEndian 처리: 1바이트 초과하는 타입만 바이트 순서 변환
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

            // Check for hexadecimal format (0xFF, 0x1A, etc.)
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                var hexValue = value.Substring(2);
                return ParseHexValue<T>(hexValue);
            }

            // Check for binary format (0b1010, 0B1111, etc.)
            if (value.StartsWith("0b", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("0B", StringComparison.OrdinalIgnoreCase))
            {
                var binaryValue = value.Substring(2);
                return ParseBinaryValue<T>(binaryValue);
            }

            // Default decimal parsing
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private T ParseHexValue<T>(string hexValue) where T : struct
        {
            try
            {
                // Remove any spaces or underscores for readability
                hexValue = hexValue.Replace(" ", "").Replace("_", "");

                var type = typeof(T);
                if (type == typeof(byte))
                    return (T)(object)Convert.ToByte(hexValue, 16);
                else if (type == typeof(short))
                    return (T)(object)Convert.ToInt16(hexValue, 16);
                else if (type == typeof(ushort))
                    return (T)(object)Convert.ToUInt16(hexValue, 16);
                else if (type == typeof(int))
                    return (T)(object)Convert.ToInt32(hexValue, 16);
                else if (type == typeof(uint))
                    return (T)(object)Convert.ToUInt32(hexValue, 16);
                else if (type == typeof(float))
                {
                    // For float, parse as uint first then convert to float bytes
                    var intValue = Convert.ToUInt32(hexValue, 16);
                    return (T)(object)BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);
                }
                else
                    return default(T);
            }
            catch
            {
                return default(T);
            }
        }

        private T ParseBinaryValue<T>(string binaryValue) where T : struct
        {
            try
            {
                // Remove any spaces or underscores for readability
                binaryValue = binaryValue.Replace(" ", "").Replace("_", "");

                var type = typeof(T);
                if (type == typeof(byte))
                    return (T)(object)Convert.ToByte(binaryValue, 2);
                else if (type == typeof(short))
                    return (T)(object)Convert.ToInt16(binaryValue, 2);
                else if (type == typeof(ushort))
                    return (T)(object)Convert.ToUInt16(binaryValue, 2);
                else if (type == typeof(int))
                    return (T)(object)Convert.ToInt32(binaryValue, 2);
                else if (type == typeof(uint))
                    return (T)(object)Convert.ToUInt32(binaryValue, 2);
                else if (type == typeof(float))
                {
                    // For float, parse as uint first then convert to float bytes
                    var intValue = Convert.ToUInt32(binaryValue, 2);
                    return (T)(object)BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);
                }
                else
                    return default(T);
            }
            catch
            {
                return default(T);
            }
        }

        private Dictionary<string, object> ParseResponse(byte[] data, ResponseSchema schema)
        {
            var result = new Dictionary<string, object>();
            var offset = 0;

            // Parse Headers first
            foreach (var field in schema.Headers)
            {
                if (offset >= data.Length) break;

                try
                {
                    var (value, fieldSize) = ParseField(data, offset, field);
                    result[$"Header.{field.Name}"] = value;
                    offset += fieldSize;
                }
                catch
                {
                    result[$"Header.{field.Name}"] = "Parse Error";
                    break;
                }
            }

            // Then parse Payload
            foreach (var field in schema.Payload)
            {
                if (offset >= data.Length) break;

                try
                {
                    var (value, fieldSize) = ParseField(data, offset, field);
                    result[$"Payload.{field.Name}"] = value;
                    offset += fieldSize;
                }
                catch
                {
                    result[$"Payload.{field.Name}"] = "Parse Error";
                    break;
                }
            }

            return result;
        }

        private (object value, int size) ParseField(byte[] data, int offset, DataField field)
        {
            int size = field.Type switch
            {
                DataType.Byte => 1,
                DataType.Int16 => 2,
                DataType.UInt16 => 2,
                DataType.Int => 4,
                DataType.UInt => 4,
                DataType.Float => 4,
                DataType.Padding => field.PaddingSize,
                _ => 0
            };

            // Check if we have enough data to read
            if (offset + size > data.Length)
            {
                throw new ArgumentOutOfRangeException($"Not enough data to read {field.Type} at offset {offset}");
            }

            object value = field.Type switch
            {
                DataType.Byte => data[offset],
                DataType.Int16 => BitConverter.ToInt16(data, offset),
                DataType.UInt16 => BitConverter.ToUInt16(data, offset),
                DataType.Int => BitConverter.ToInt32(data, offset),
                DataType.UInt => BitConverter.ToUInt32(data, offset),
                DataType.Float => BitConverter.ToSingle(data, offset),
                DataType.Padding => $"Padding({field.PaddingSize} bytes)",
                _ => 0
            };

            return (value, size);
        }
    }
}
