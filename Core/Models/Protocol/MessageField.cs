using System.ComponentModel;

namespace MMG.Core.Models.Protocol
{
    /// <summary>
    /// 메시지 필드 정의
    /// </summary>
    public class MessageField : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private FieldType _type = FieldType.Byte;
        private string _value = "0";
        private int _size = 1;
        private string _description = string.Empty;
        private Endianness _endianness = Endianness.LittleEndian;

        /// <summary>
        /// 필드 이름
        /// </summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        /// <summary>
        /// 필드 타입
        /// </summary>
        public FieldType Type
        {
            get => _type;
            set
            {
                _type = value;
                _size = GetDefaultSize(value);
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(Size));
                OnPropertyChanged(nameof(IsPadding));
                OnPropertyChanged(nameof(IsVariableSize));
            }
        }

        /// <summary>
        /// 필드 값 (문자열 표현)
        /// </summary>
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        /// <summary>
        /// 필드 크기 (bytes) - Padding, String, ByteArray에서 사용
        /// </summary>
        public int Size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(nameof(Size)); }
        }

        /// <summary>
        /// 필드 설명
        /// </summary>
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        /// <summary>
        /// 바이트 순서
        /// </summary>
        public Endianness Endianness
        {
            get => _endianness;
            set { _endianness = value; OnPropertyChanged(nameof(Endianness)); }
        }

        /// <summary>
        /// Padding 타입 여부
        /// </summary>
        public bool IsPadding => Type == FieldType.Padding;

        /// <summary>
        /// 가변 크기 타입 여부
        /// </summary>
        public bool IsVariableSize => Type is FieldType.Padding or FieldType.String or FieldType.ByteArray;

        /// <summary>
        /// 필드의 바이트 크기 계산
        /// </summary>
        public int ByteSize => GetByteSize();

        private int GetByteSize()
        {
            return Type switch
            {
                FieldType.Byte => 1,
                FieldType.UInt16 => 2,
                FieldType.Int32 => 4,
                FieldType.UInt32 => 4,
                FieldType.Float => 4,
                FieldType.Double => 8,
                FieldType.Padding => Size,
                FieldType.String => Size,
                FieldType.ByteArray => Size,
                _ => 1
            };
        }

        private static int GetDefaultSize(FieldType type)
        {
            return type switch
            {
                FieldType.Byte => 1,
                FieldType.UInt16 => 2,
                FieldType.Int32 => 4,
                FieldType.UInt32 => 4,
                FieldType.Float => 4,
                FieldType.Double => 8,
                FieldType.Padding => 1,
                FieldType.String => 16,
                FieldType.ByteArray => 16,
                _ => 1
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 기존 DataField와의 호환을 위한 변환
        /// </summary>
        public static MessageField FromLegacyDataField(MMG.Models.DataField dataField)
        {
            return new MessageField
            {
                Name = dataField.Name,
                Type = ConvertLegacyType(dataField.Type),
                Value = dataField.Value,
                Size = dataField.PaddingSize
            };
        }

        private static FieldType ConvertLegacyType(MMG.Models.DataType legacyType)
        {
            return legacyType switch
            {
                MMG.Models.DataType.Byte => FieldType.Byte,
                MMG.Models.DataType.UInt16 => FieldType.UInt16,
                MMG.Models.DataType.Int => FieldType.Int32,
                MMG.Models.DataType.UInt => FieldType.UInt32,
                MMG.Models.DataType.Float => FieldType.Float,
                MMG.Models.DataType.Padding => FieldType.Padding,
                _ => FieldType.Byte
            };
        }
    }
}
