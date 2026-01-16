using MMG.Core.Models.Protocol;

namespace MMG.Core.Models.Schema
{
    /// <summary>
    /// 필드 정의
    /// </summary>
    public class FieldDefinition
    {
        /// <summary>
        /// 필드 이름
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 필드 타입 (byte, uint16, int32, uint32, float, double, padding, string, bytes)
        /// </summary>
        public string Type { get; set; } = "byte";

        /// <summary>
        /// 기본값 (문자열로 표현)
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// 크기 (padding, string, bytes에서 사용)
        /// </summary>
        public int? Size { get; set; }

        /// <summary>
        /// 바이트 순서
        /// </summary>
        public string Endian { get; set; } = "little";

        /// <summary>
        /// 필드 설명
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 값 형식 (hex, binary, decimal)
        /// </summary>
        public string Format { get; set; } = "decimal";

        /// <summary>
        /// 열거형 값들 (선택적)
        /// </summary>
        public Dictionary<string, string>? EnumValues { get; set; }

        /// <summary>
        /// 비트 필드 정의 (바이트/워드 내 비트 단위 분할)
        /// </summary>
        public List<BitFieldDefinition>? BitFields { get; set; }

        /// <summary>
        /// 컴포넌트 참조 ($ref)
        /// </summary>
        public string? ComponentRef { get; set; }

        /// <summary>
        /// 비트 필드가 있는지 여부
        /// </summary>
        public bool HasBitFields => BitFields != null && BitFields.Count > 0;

        /// <summary>
        /// 실제 바이트 크기 계산
        /// </summary>
        public int ByteSize => GetByteSize();

        /// <summary>
        /// FieldType enum으로 변환
        /// </summary>
        public FieldType FieldType => ParseFieldType();

        private int GetByteSize()
        {
            return Type.ToLowerInvariant() switch
            {
                "byte" or "uint8" or "int8" => 1,
                "uint16" or "int16" or "short" => 2,
                "int32" or "int" or "uint32" or "uint" => 4,
                "float" or "float32" => 4,
                "double" or "float64" => 8,
                "padding" => Size ?? 1,
                "string" => Size ?? 16,
                "bytes" or "bytearray" => Size ?? 16,
                _ => 1
            };
        }

        private FieldType ParseFieldType()
        {
            return Type.ToLowerInvariant() switch
            {
                "byte" or "uint8" => Protocol.FieldType.Byte,
                "uint16" => Protocol.FieldType.UInt16,
                "int32" or "int" => Protocol.FieldType.Int32,
                "uint32" or "uint" => Protocol.FieldType.UInt32,
                "float" or "float32" => Protocol.FieldType.Float,
                "double" or "float64" => Protocol.FieldType.Double,
                "padding" => Protocol.FieldType.Padding,
                "string" => Protocol.FieldType.String,
                "bytes" or "bytearray" => Protocol.FieldType.ByteArray,
                _ => Protocol.FieldType.Byte
            };
        }

        /// <summary>
        /// MessageField로 변환
        /// </summary>
        public MessageField ToMessageField()
        {
            return new MessageField
            {
                Name = Name,
                Type = FieldType,
                Value = Value ?? "0",
                Size = Size ?? ByteSize,
                Description = Description,
                Endianness = Endian.ToLowerInvariant() == "big"
                    ? Endianness.BigEndian
                    : Endianness.LittleEndian
            };
        }
    }
}
