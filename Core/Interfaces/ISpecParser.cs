using MMG.Core.Models.Schema;

namespace MMG.Core.Interfaces
{
    /// <summary>
    /// 스펙 파서 인터페이스
    /// IDL, XML 등 다양한 형식의 스펙 파서가 구현
    /// </summary>
    public interface ISpecParser
    {
        /// <summary>
        /// 파서가 지원하는 파일 확장자
        /// </summary>
        IReadOnlyList<string> SupportedExtensions { get; }

        /// <summary>
        /// 파일에서 스펙 로드
        /// </summary>
        /// <param name="filePath">파일 경로</param>
        /// <returns>파싱된 스펙</returns>
        Task<UdpApiSpec> ParseFileAsync(string filePath);

        /// <summary>
        /// 문자열에서 스펙 파싱
        /// </summary>
        /// <param name="content">스펙 내용</param>
        /// <returns>파싱된 스펙</returns>
        UdpApiSpec Parse(string content);

        /// <summary>
        /// 스펙을 문자열로 직렬화
        /// </summary>
        /// <param name="spec">스펙 객체</param>
        /// <returns>직렬화된 문자열</returns>
        string Serialize(UdpApiSpec spec);

        /// <summary>
        /// 스펙을 파일로 저장
        /// </summary>
        /// <param name="spec">스펙 객체</param>
        /// <param name="filePath">저장할 파일 경로</param>
        Task SaveToFileAsync(UdpApiSpec spec, string filePath);

        /// <summary>
        /// 새로운 기본 스펙 생성
        /// </summary>
        /// <returns>기본값이 설정된 스펙</returns>
        UdpApiSpec CreateDefaultSpec();
    }
}
