using MMG.Core.Models.Schema;

namespace MMG.Core.Interfaces
{
    /// <summary>
    /// UDP OpenAPI 스펙 파서 인터페이스
    /// </summary>
    public interface ISpecParser
    {
        /// <summary>
        /// YAML 파일에서 스펙 로드
        /// </summary>
        Task<UdpApiSpec> ParseFileAsync(string filePath);

        /// <summary>
        /// YAML 문자열에서 스펙 파싱
        /// </summary>
        UdpApiSpec ParseYaml(string yamlContent);

        /// <summary>
        /// 스펙을 YAML 문자열로 직렬화
        /// </summary>
        string ToYaml(UdpApiSpec spec);

        /// <summary>
        /// 스펙을 파일로 저장
        /// </summary>
        Task SaveToFileAsync(UdpApiSpec spec, string filePath);
    }
}
