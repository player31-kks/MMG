using MMG.Core.Models.Schema;
using MMG.Core.Models.Protocol;

namespace MMG.Core.Services
{
    /// <summary>
    /// 스펙 기반 메시지 빌더
    /// </summary>
    public class MessageBuilder
    {
        private readonly MessageSchema _schema;
        private readonly Dictionary<string, string> _values = new();

        public MessageBuilder(MessageSchema schema)
        {
            _schema = schema;
            InitializeDefaultValues();
        }

        /// <summary>
        /// 기본값으로 초기화
        /// </summary>
        private void InitializeDefaultValues()
        {
            foreach (var field in _schema.Header)
            {
                _values[$"header.{field.Name}"] = field.Value ?? "0";
            }
            foreach (var field in _schema.Payload)
            {
                _values[$"payload.{field.Name}"] = field.Value ?? "0";
            }
        }

        /// <summary>
        /// 필드 값 설정
        /// </summary>
        public MessageBuilder SetHeaderValue(string fieldName, string value)
        {
            _values[$"header.{fieldName}"] = value;
            return this;
        }

        /// <summary>
        /// 페이로드 필드 값 설정
        /// </summary>
        public MessageBuilder SetPayloadValue(string fieldName, string value)
        {
            _values[$"payload.{fieldName}"] = value;
            return this;
        }

        /// <summary>
        /// 바이트 배열로 빌드
        /// </summary>
        public byte[] Build()
        {
            var bytes = new List<byte>();

            // 헤더 빌드
            foreach (var field in _schema.Header)
            {
                var value = _values.GetValueOrDefault($"header.{field.Name}", field.Value ?? "0");
                bytes.AddRange(ConvertToBytes(field, value));
            }

            // 페이로드 빌드
            foreach (var field in _schema.Payload)
            {
                var value = _values.GetValueOrDefault($"payload.{field.Name}", field.Value ?? "0");
                bytes.AddRange(ConvertToBytes(field, value));
            }

            return bytes.ToArray();
        }

        /// <summary>
        /// 전체 메시지 크기
        /// </summary>
        public int TotalSize => _schema.TotalSize;

        /// <summary>
        /// 필드 정의를 바이트로 변환
        /// </summary>
        private byte[] ConvertToBytes(FieldDefinition field, string value)
        {
            var isBigEndian = field.Endian.ToLowerInvariant() == "big";

            try
            {
                var bytes = field.FieldType switch
                {
                    FieldType.Byte => new[] { ParseByte(value, field.Format) },
                    FieldType.Int16 => BitConverter.GetBytes(ParseInt16(value, field.Format)),
                    FieldType.UInt16 => BitConverter.GetBytes(ParseUInt16(value, field.Format)),
                    FieldType.Int32 => BitConverter.GetBytes(ParseInt32(value, field.Format)),
                    FieldType.UInt32 => BitConverter.GetBytes(ParseUInt32(value, field.Format)),
                    FieldType.Float => BitConverter.GetBytes(ParseFloat(value)),
                    FieldType.Double => BitConverter.GetBytes(ParseDouble(value)),
                    FieldType.Padding => new byte[field.Size ?? 1],
                    FieldType.String => ConvertString(value, field.Size ?? 16),
                    FieldType.ByteArray => ConvertByteArray(value, field.Size ?? 16),
                    _ => new[] { ParseByte(value, field.Format) }
                };

                // Big Endian 처리
                if (isBigEndian && bytes.Length > 1)
                {
                    Array.Reverse(bytes);
                }

                return bytes;
            }
            catch
            {
                // 변환 실패 시 0으로 채움
                return new byte[field.ByteSize];
            }
        }

        private byte ParseByte(string value, string format)
        {
            return format.ToLowerInvariant() switch
            {
                "hex" => Convert.ToByte(value.Replace("0x", ""), 16),
                "binary" => Convert.ToByte(value.Replace("0b", ""), 2),
                _ => byte.Parse(value)
            };
        }

        private short ParseInt16(string value, string format)
        {
            return format.ToLowerInvariant() switch
            {
                "hex" => Convert.ToInt16(value.Replace("0x", ""), 16),
                "binary" => Convert.ToInt16(value.Replace("0b", ""), 2),
                _ => short.Parse(value)
            };
        }

        private ushort ParseUInt16(string value, string format)
        {
            return format.ToLowerInvariant() switch
            {
                "hex" => Convert.ToUInt16(value.Replace("0x", ""), 16),
                "binary" => Convert.ToUInt16(value.Replace("0b", ""), 2),
                _ => ushort.Parse(value)
            };
        }

        private int ParseInt32(string value, string format)
        {
            return format.ToLowerInvariant() switch
            {
                "hex" => Convert.ToInt32(value.Replace("0x", ""), 16),
                "binary" => Convert.ToInt32(value.Replace("0b", ""), 2),
                _ => int.Parse(value)
            };
        }

        private uint ParseUInt32(string value, string format)
        {
            return format.ToLowerInvariant() switch
            {
                "hex" => Convert.ToUInt32(value.Replace("0x", ""), 16),
                "binary" => Convert.ToUInt32(value.Replace("0b", ""), 2),
                _ => uint.Parse(value)
            };
        }

        private float ParseFloat(string value) => float.Parse(value);
        private double ParseDouble(string value) => double.Parse(value);

        private byte[] ConvertString(string value, int size)
        {
            var bytes = new byte[size];
            var strBytes = System.Text.Encoding.ASCII.GetBytes(value ?? "");
            Array.Copy(strBytes, bytes, Math.Min(strBytes.Length, size));
            return bytes;
        }

        private byte[] ConvertByteArray(string value, int size)
        {
            var bytes = new byte[size];
            if (string.IsNullOrEmpty(value)) return bytes;

            // "0x01 0x02 0x03" 또는 "01 02 03" 형식
            var hexValues = value.Replace("0x", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Math.Min(hexValues.Length, size); i++)
            {
                bytes[i] = Convert.ToByte(hexValues[i], 16);
            }
            return bytes;
        }
    }
}
