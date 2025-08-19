using System.Net;
using System.Net.Sockets;
using MMG.Models;

namespace MMG.Services
{
    public class UdpClientService
    {
        public async Task<UdpResponse> SendRequestAsync(UdpRequest request, ResponseSchema? responseSchema = null)
        {
            var response = new UdpResponse();

            try
            {
                // Build message bytes
                var messageBytes = BuildMessage(request.Headers, request.Payload);

                // Create UDP client
                using var udpClient = new UdpClient();
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

        private byte[] BuildMessage(IEnumerable<DataField> headers, IEnumerable<DataField> payload)
        {
            var messageBytes = new List<byte>();

            // Add headers
            foreach (var header in headers)
            {
                messageBytes.AddRange(ConvertValueToBytes(header.Value, header.Type));
            }

            // Add payload
            foreach (var field in payload)
            {
                messageBytes.AddRange(ConvertValueToBytes(field.Value, field.Type));
            }

            return messageBytes.ToArray();
        }

        private byte[] ConvertValueToBytes(string value, DataType type)
        {
            try
            {
                return type switch
                {
                    DataType.Byte => new[] { Convert.ToByte(value) },
                    DataType.Int => BitConverter.GetBytes(Convert.ToInt32(value)),
                    DataType.UInt => BitConverter.GetBytes(Convert.ToUInt32(value)),
                    DataType.Float => BitConverter.GetBytes(Convert.ToSingle(value)),
                    _ => Array.Empty<byte>()
                };
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }

        private Dictionary<string, object> ParseResponse(byte[] data, ResponseSchema schema)
        {
            var result = new Dictionary<string, object>();
            var offset = 0;

            foreach (var field in schema.Fields)
            {
                if (offset >= data.Length) break;

                try
                {
                    var value = field.Type switch
                    {
                        DataType.Byte => data[offset],
                        DataType.Int => BitConverter.ToInt32(data, offset),
                        DataType.UInt => BitConverter.ToUInt32(data, offset),
                        DataType.Float => BitConverter.ToSingle(data, offset),
                        _ => 0
                    };

                    result[field.Name] = value;

                    offset += field.Type switch
                    {
                        DataType.Byte => 1,
                        DataType.Int => 4,
                        DataType.UInt => 4,
                        DataType.Float => 4,
                        _ => 0
                    };
                }
                catch
                {
                    result[field.Name] = "Parse Error";
                    break;
                }
            }

            return result;
        }
    }
}
