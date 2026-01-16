using MMG.Core.Models.Schema;
using MMG.Core.Models.Protocol;

namespace MMG.Core.Services
{
    /// <summary>
    /// 스펙 기반 응답 파서
    /// </summary>
    public class ResponseParser
    {
        private readonly MessageSchema _schema;

        public ResponseParser(MessageSchema schema)
        {
            _schema = schema;
        }

        /// <summary>
        /// 바이트 배열을 파싱하여 딕셔너리로 반환
        /// </summary>
        public Dictionary<string, ParsedValue> Parse(byte[] data)
        {
            var result = new Dictionary<string, ParsedValue>();
            var offset = 0;

            // 헤더 파싱
            foreach (var field in _schema.Header)
            {
                if (offset >= data.Length) break;

                var parsed = ParseField(data, ref offset, field);
                result[$"header.{field.Name}"] = parsed;
            }

            // 페이로드 파싱
            foreach (var field in _schema.Payload)
            {
                if (offset >= data.Length) break;

                var parsed = ParseField(data, ref offset, field);
                result[$"payload.{field.Name}"] = parsed;
            }

            return result;
        }

        /// <summary>
        /// 단일 필드 파싱
        /// </summary>
        private ParsedValue ParseField(byte[] data, ref int offset, FieldDefinition field)
        {
            var isBigEndian = field.Endian.ToLowerInvariant() == "big";
            var size = field.ByteSize;

            if (offset + size > data.Length)
            {
                return new ParsedValue { RawBytes = Array.Empty<byte>(), DisplayValue = "N/A" };
            }

            var bytes = new byte[size];
            Array.Copy(data, offset, bytes, 0, size);
            offset += size;

            if (isBigEndian && size > 1)
            {
                Array.Reverse(bytes);
            }

            var displayValue = ConvertToString(bytes, field);

            return new ParsedValue
            {
                RawBytes = bytes,
                DisplayValue = displayValue,
                FieldName = field.Name,
                FieldType = field.Type
            };
        }

        /// <summary>
        /// 바이트를 문자열로 변환
        /// </summary>
        private string ConvertToString(byte[] bytes, FieldDefinition field)
        {
            try
            {
                var value = field.FieldType switch
                {
                    FieldType.Byte => bytes[0].ToString(),
                    FieldType.Int16 => BitConverter.ToInt16(bytes, 0).ToString(),
                    FieldType.UInt16 => BitConverter.ToUInt16(bytes, 0).ToString(),
                    FieldType.Int32 => BitConverter.ToInt32(bytes, 0).ToString(),
                    FieldType.UInt32 => BitConverter.ToUInt32(bytes, 0).ToString(),
                    FieldType.Float => BitConverter.ToSingle(bytes, 0).ToString("F4"),
                    FieldType.Double => BitConverter.ToDouble(bytes, 0).ToString("F6"),
                    FieldType.Padding => $"[{bytes.Length} bytes padding]",
                    FieldType.String => System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0'),
                    FieldType.ByteArray => BitConverter.ToString(bytes).Replace("-", " "),
                    _ => bytes[0].ToString()
                };

                // Hex 형식이면 Hex로도 표시
                if (field.Format.ToLowerInvariant() == "hex")
                {
                    value = FormatAsHex(bytes, field.FieldType);
                }

                // Enum 값이 있으면 매핑
                if (field.EnumValues != null && field.EnumValues.TryGetValue(value, out var enumName))
                {
                    value = $"{enumName} ({value})";
                }

                return value;
            }
            catch
            {
                return BitConverter.ToString(bytes);
            }
        }

        private string FormatAsHex(byte[] bytes, FieldType type)
        {
            return type switch
            {
                FieldType.Byte => $"0x{bytes[0]:X2}",
                FieldType.Int16 or FieldType.UInt16 => $"0x{BitConverter.ToUInt16(bytes, 0):X4}",
                FieldType.Int32 or FieldType.UInt32 => $"0x{BitConverter.ToUInt32(bytes, 0):X8}",
                _ => BitConverter.ToString(bytes).Replace("-", " ")
            };
        }
    }

    /// <summary>
    /// 파싱된 값
    /// </summary>
    public class ParsedValue
    {
        public byte[] RawBytes { get; init; }
        public string DisplayValue { get; init; } = string.Empty;
        public string FieldName { get; init; } = string.Empty;
        public string FieldType { get; init; } = string.Empty;

        /// <summary>
        /// Hex 형식으로 표시
        /// </summary>
        public string HexValue => BitConverter.ToString(RawBytes).Replace("-", " ");
    }
}
