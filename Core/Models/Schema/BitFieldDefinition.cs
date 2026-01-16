namespace MMG.Core.Models.Schema
{
    /// <summary>
    /// 비트 필드 정의 - 바이트 내 비트 단위 필드
    /// </summary>
    public class BitFieldDefinition
    {
        /// <summary>
        /// 비트 필드 이름
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 시작 비트 위치 (0-based, LSB부터)
        /// </summary>
        public int? SingleBit { get; set; }

        /// <summary>
        /// 비트 범위 (예: "0:3" = 비트 0~3)
        /// </summary>
        public string? BitRange { get; set; }

        /// <summary>
        /// 기본값
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// 설명
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 열거형 값들 (선택적)
        /// </summary>
        public Dictionary<string, string>? EnumValues { get; set; }

        /// <summary>
        /// 시작 비트 (계산된 값)
        /// </summary>
        public int StartBit
        {
            get
            {
                if (SingleBit.HasValue) return SingleBit.Value;
                if (!string.IsNullOrEmpty(BitRange))
                {
                    var parts = BitRange.Split(':');
                    if (parts.Length >= 1 && int.TryParse(parts[0], out int start))
                        return start;
                }
                return 0;
            }
        }

        /// <summary>
        /// 끝 비트 (계산된 값)
        /// </summary>
        public int EndBit
        {
            get
            {
                if (SingleBit.HasValue) return SingleBit.Value;
                if (!string.IsNullOrEmpty(BitRange))
                {
                    var parts = BitRange.Split(':');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int end))
                        return end;
                    if (parts.Length >= 1 && int.TryParse(parts[0], out int start))
                        return start;
                }
                return 0;
            }
        }

        /// <summary>
        /// 비트 크기
        /// </summary>
        public int BitSize => EndBit - StartBit + 1;

        /// <summary>
        /// 비트 마스크 생성
        /// </summary>
        public uint BitMask
        {
            get
            {
                uint mask = 0;
                for (int i = StartBit; i <= EndBit; i++)
                {
                    mask |= (uint)(1 << i);
                }
                return mask;
            }
        }

        /// <summary>
        /// 최대 값 (비트 크기에 따른)
        /// </summary>
        public uint MaxValue => (uint)((1 << BitSize) - 1);
    }
}
