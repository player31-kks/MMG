using System.ComponentModel;

namespace MMG.Core.Models.Protocol
{
    /// <summary>
    /// 데이터 필드 타입
    /// </summary>
    public enum FieldType
    {
        [Description("1 byte (0-255)")]
        Byte,

        [Description("2 bytes (signed)")]
        Int16,

        [Description("2 bytes (0-65535)")]
        UInt16,

        [Description("4 bytes (signed)")]
        Int32,

        [Description("4 bytes (unsigned)")]
        UInt32,

        [Description("4 bytes (floating point)")]
        Float,

        [Description("8 bytes (floating point)")]
        Double,

        [Description("Variable length padding")]
        Padding,

        [Description("Fixed length string")]
        String,

        [Description("Raw byte array")]
        ByteArray
    }

    /// <summary>
    /// 바이트 순서
    /// </summary>
    public enum Endianness
    {
        LittleEndian,
        BigEndian
    }
}
