using System.Collections.ObjectModel;
using MMG.Models;

namespace MMG.Core.Utilities
{
    /// <summary>
    /// 바이트 계산 유틸리티
    /// </summary>
    public static class ByteCalculator
    {
        /// <summary>
        /// DataField 컬렉션의 총 바이트 수 계산
        /// </summary>
        public static int CalculateBytes(IEnumerable<DataField> fields)
        {
            int totalBytes = 0;
            foreach (var field in fields)
            {
                totalBytes += GetFieldSize(field.Type, field.PaddingSize);
            }
            return totalBytes;
        }

        /// <summary>
        /// 필드 타입별 바이트 크기 반환
        /// </summary>
        public static int GetFieldSize(DataType type, int paddingSize = 1)
        {
            return type switch
            {
                DataType.Byte => 1,
                DataType.Int16 => 2,
                DataType.UInt16 => 2,
                DataType.Int => 4,
                DataType.UInt => 4,
                DataType.Float => 4,
                DataType.Padding => paddingSize,
                _ => 0
            };
        }

        /// <summary>
        /// 바이트 텍스트 포맷
        /// </summary>
        public static string FormatBytesText(int bytes) => $"Total: {bytes} bytes";
    }
}
