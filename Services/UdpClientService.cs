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
        public event EventHandler<DataReceivedEventArgs>? DataReceived;
        public async Task<UdpResponse> SendRequestAsync(UdpRequest request, ResponseSchema? responseSchema = null)
        {
            var response = new UdpResponse();

            try
            {
                // Build message bytes
                var messageBytes = BuildMessage(request.Headers, request.Payload);

                // Determine local port
                var localPort = GetLocalPort(request.IpAddress, request.Port);

                // Create UDP client with specific local port
                using var udpClient = new UdpClient(localPort);
                var endpoint = new IPEndPoint(IPAddress.Parse(request.IpAddress), request.Port);

                // Send message
                await udpClient.SendAsync(messageBytes, messageBytes.Length, endpoint);

                // Wait for response (with timeout)
                var receiveTask = udpClient.ReceiveAsync();
                var timeoutTask = Task.Delay(5000); // 5 second timeout

                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                if (completedTask == receiveTask)
                {
                    var result = await receiveTask;
                    response.RawData = result.Buffer;
                    response.Status = "Success";

                    // Fire data received event
                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        IpAddress = result.RemoteEndPoint.Address.ToString(),
                        Port = result.RemoteEndPoint.Port,
                        Data = result.Buffer,
                        Timestamp = DateTime.Now
                    });

                    // Parse response if schema is provided
                    if (responseSchema != null)
                    {
                        response.ParsedData = ParseResponse(result.Buffer, responseSchema);
                    }
                }
                else
                {
                    response.Status = "Timeout";
                }
            }
            catch (Exception ex)
            {
                response.Status = $"Error: {ex.Message}";
            }

            response.ReceivedAt = DateTime.Now;
            return response;
        }

        private int GetLocalPort(string ipAddress, int targetPort)
        {
            var settings = SettingsService.Instance;

            // Check if custom port is enabled
            if (settings.UseCustomPort)
            {
                // Use the custom port for all addresses
                return settings.CustomPort;
            }
            else
            {
                // Use the same port as target when custom port is disabled
                return targetPort;
            }
        }

        private bool IsLoopbackAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            // Check for common loopback representations
            if (ipAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                ipAddress.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Try to parse as IP and check if it's loopback
            if (IPAddress.TryParse(ipAddress, out var ip))
            {
                return IPAddress.IsLoopback(ip);
            }

            return false;
        }

        private int GetRandomAvailablePort()
        {
            // Create a temporary UDP client to get an available port
            using var tempClient = new UdpClient(0); // 0 means system will assign available port
            var localEndPoint = (IPEndPoint)tempClient.Client.LocalEndPoint!;
            return localEndPoint.Port;
        }

        private byte[] BuildMessage(IEnumerable<DataField> headers, IEnumerable<DataField> payload)
        {
            var messageBytes = new List<byte>();

            // Add headers
            foreach (var header in headers)
            {
                messageBytes.AddRange(ConvertValueToBytes(header));
            }

            // Add payload
            foreach (var field in payload)
            {
                messageBytes.AddRange(ConvertValueToBytes(field));
            }

            return messageBytes.ToArray();
        }

        private byte[] ConvertValueToBytes(DataField field)
        {
            try
            {
                return field.Type switch
                {
                    DataType.Byte => new[] { ParseValue<byte>(field.Value) },
                    DataType.UInt16 => BitConverter.GetBytes(ParseValue<ushort>(field.Value)),
                    DataType.Int => BitConverter.GetBytes(ParseValue<int>(field.Value)),
                    DataType.UInt => BitConverter.GetBytes(ParseValue<uint>(field.Value)),
                    DataType.Float => BitConverter.GetBytes(ParseValue<float>(field.Value)),
                    DataType.Padding => new byte[field.PaddingSize], // Creates array of zeros
                    _ => Array.Empty<byte>()
                };
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
