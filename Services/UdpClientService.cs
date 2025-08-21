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
                    DataType.Byte => new[] { Convert.ToByte(field.Value) },
                    DataType.UInt16 => BitConverter.GetBytes(Convert.ToUInt16(field.Value)),
                    DataType.Int => BitConverter.GetBytes(Convert.ToInt32(field.Value)),
                    DataType.UInt => BitConverter.GetBytes(Convert.ToUInt32(field.Value)),
                    DataType.Float => BitConverter.GetBytes(Convert.ToSingle(field.Value)),
                    DataType.Padding => new byte[field.PaddingSize], // Creates array of zeros
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
