namespace MMG.Core.Models.Idl
{
    /// <summary>
    /// IDL 문서의 메타 정보 (Compiler Directive)
    /// //+PACK_SIZE=n, //+MOST_BYTE=true/false
    /// </summary>
    public class IdlDirectives
    {
        /// <summary>
        /// 패킹 크기 (#pragma pack(n))
        /// </summary>
        public int PackSize { get; set; } = 1;

        /// <summary>
        /// Big Endian 여부 (true = Big Endian, false = Little Endian)
        /// </summary>
        public bool IsBigEndian { get; set; } = true;
    }

    /// <summary>
    /// IDL 문서 전체를 나타내는 클래스
    /// </summary>
    public class IdlDocument
    {
        /// <summary>
        /// 컴파일러 지시자 (PACK_SIZE, MOST_BYTE)
        /// </summary>
        public IdlDirectives Directives { get; set; } = new();

        /// <summary>
        /// 메시지 헤더 구조체 (//$()$ 마커가 있는 struct)
        /// </summary>
        public IdlStruct? HeaderStruct { get; set; }

        /// <summary>
        /// 사용자 정의 구조체들 (일반 struct)
        /// </summary>
        public List<IdlStruct> UserStructs { get; set; } = new();

        /// <summary>
        /// 메시지 구조체들 (//$(MsgID)$ 마커가 있는 struct)
        /// </summary>
        public List<IdlMessageStruct> MessageStructs { get; set; } = new();

        /// <summary>
        /// 파일 경로
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 이름으로 구조체 찾기
        /// </summary>
        public IdlStruct? FindStruct(string name)
        {
            if (HeaderStruct?.Name == name) return HeaderStruct;
            return UserStructs.FirstOrDefault(s => s.Name == name);
        }
    }

    /// <summary>
    /// IDL 구조체 정의
    /// </summary>
    public class IdlStruct
    {
        /// <summary>
        /// 구조체 이름
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 필드 목록
        /// </summary>
        public List<IdlField> Fields { get; set; } = new();

        /// <summary>
        /// 주석
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// 전체 바이트 크기 계산
        /// </summary>
        public int CalculateSize(IdlDocument doc)
        {
            int size = 0;
            foreach (var field in Fields)
            {
                size += field.CalculateSize(doc);
            }
            return size;
        }
    }

    /// <summary>
    /// IDL 메시지 구조체 (//$(MsgID)$ 마커가 있는 struct)
    /// </summary>
    public class IdlMessageStruct : IdlStruct
    {
        /// <summary>
        /// 메시지 ID (10진수 또는 16진수로 파싱된 값)
        /// </summary>
        public int MessageId { get; set; }

        /// <summary>
        /// 원본 메시지 ID 문자열 (예: "1", "0x0001")
        /// </summary>
        public string MessageIdString { get; set; } = string.Empty;
    }

    /// <summary>
    /// IDL 필드 정의
    /// </summary>
    public class IdlField
    {
        /// <summary>
        /// 필드 이름
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 필드 타입 (unsigned char, short, int, 또는 사용자 정의 struct 이름)
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 배열 크기 (배열이 아니면 null)
        /// </summary>
        public int? ArraySize { get; set; }

        /// <summary>
        /// 비트 필드 크기 (비트 필드가 아니면 null)
        /// </summary>
        public int? BitFieldSize { get; set; }

        /// <summary>
        /// MsgID 마커가 있는지 여부 (//$()$)
        /// </summary>
        public bool IsMsgIdField { get; set; }

        /// <summary>
        /// 주석
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// 파싱된 기본 타입
        /// </summary>
        public IdlPrimitiveType PrimitiveType => ParsePrimitiveType();

        /// <summary>
        /// 구조체 타입 여부
        /// </summary>
        public bool IsStructType => PrimitiveType == IdlPrimitiveType.Struct;

        /// <summary>
        /// 배열 여부
        /// </summary>
        public bool IsArray => ArraySize.HasValue && ArraySize.Value > 0;

        /// <summary>
        /// 비트 필드 여부
        /// </summary>
        public bool IsBitField => BitFieldSize.HasValue && BitFieldSize.Value > 0;

        private IdlPrimitiveType ParsePrimitiveType()
        {
            var normalizedType = TypeName.ToLowerInvariant().Replace(" ", "");
            return normalizedType switch
            {
                "unsignedchar" or "uint8" or "byte" => IdlPrimitiveType.UnsignedChar,
                "char" or "int8" => IdlPrimitiveType.Char,
                "unsignedshort" or "uint16" or "ushort" => IdlPrimitiveType.UnsignedShort,
                "short" or "int16" => IdlPrimitiveType.Short,
                "unsignedint" or "uint32" or "uint" => IdlPrimitiveType.UnsignedInt,
                "int" or "int32" => IdlPrimitiveType.Int,
                "unsignedlong" or "ulong" => IdlPrimitiveType.UnsignedLong,
                "long" or "int64" => IdlPrimitiveType.Long,
                "float" => IdlPrimitiveType.Float,
                "double" => IdlPrimitiveType.Double,
                _ => IdlPrimitiveType.Struct
            };
        }

        /// <summary>
        /// 단일 요소의 바이트 크기
        /// </summary>
        public int GetElementSize()
        {
            return PrimitiveType switch
            {
                IdlPrimitiveType.Char or IdlPrimitiveType.UnsignedChar => 1,
                IdlPrimitiveType.Short or IdlPrimitiveType.UnsignedShort => 2,
                IdlPrimitiveType.Int or IdlPrimitiveType.UnsignedInt or IdlPrimitiveType.Float => 4,
                IdlPrimitiveType.Long or IdlPrimitiveType.UnsignedLong or IdlPrimitiveType.Double => 8,
                _ => 0 // Struct는 별도 계산 필요
            };
        }

        /// <summary>
        /// 전체 바이트 크기 계산 (배열 포함)
        /// </summary>
        public int CalculateSize(IdlDocument doc)
        {
            if (IsBitField)
            {
                // 비트 필드는 개별 계산하지 않고 그룹으로 계산해야 함
                return 0;
            }

            int elementSize;
            if (IsStructType)
            {
                var structDef = doc.FindStruct(TypeName);
                elementSize = structDef?.CalculateSize(doc) ?? 0;
            }
            else
            {
                elementSize = GetElementSize();
            }

            return IsArray ? elementSize * ArraySize!.Value : elementSize;
        }
    }

    /// <summary>
    /// IDL 기본 타입
    /// </summary>
    public enum IdlPrimitiveType
    {
        Char,           // char (1 byte, signed)
        UnsignedChar,   // unsigned char (1 byte)
        Short,          // short (2 bytes, signed)
        UnsignedShort,  // unsigned short (2 bytes)
        Int,            // int (4 bytes, signed)
        UnsignedInt,    // unsigned int (4 bytes)
        Long,           // long (8 bytes, signed)
        UnsignedLong,   // unsigned long (8 bytes)
        Float,          // float (4 bytes)
        Double,         // double (8 bytes)
        Struct          // 사용자 정의 구조체
    }
}
