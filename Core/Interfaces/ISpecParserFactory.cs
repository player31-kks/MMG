namespace MMG.Core.Interfaces
{
    /// <summary>
    /// 스펙 파서 팩토리 인터페이스
    /// 파일 확장자에 따라 적절한 파서를 생성
    /// </summary>
    public interface ISpecParserFactory
    {
        /// <summary>
        /// 파일 확장자에 따라 적절한 파서 생성
        /// </summary>
        /// <param name="filePath">파일 경로</param>
        /// <returns>해당 파일 형식에 맞는 파서</returns>
        ISpecParser CreateParser(string filePath);

        /// <summary>
        /// 지정된 파서 타입에 해당하는 파서 생성
        /// </summary>
        /// <param name="parserType">파서 타입</param>
        /// <returns>해당 타입의 파서</returns>
        ISpecParser CreateParser(SpecParserType parserType);

        /// <summary>
        /// 파일 형식이 지원되는지 확인
        /// </summary>
        /// <param name="filePath">파일 경로</param>
        /// <returns>지원 여부</returns>
        bool IsSupported(string filePath);

        /// <summary>
        /// 지원되는 파일 확장자 목록 반환
        /// </summary>
        IReadOnlyList<string> SupportedExtensions { get; }

        /// <summary>
        /// 파일 필터 문자열 반환 (OpenFileDialog용)
        /// </summary>
        string FileFilter { get; }
    }

    /// <summary>
    /// 스펙 파서 타입
    /// </summary>
    public enum SpecParserType
    {
        /// <summary>
        /// IDL (Interface Definition Language) 형식
        /// </summary>
        Idl,

        /// <summary>
        /// XML 형식 (향후 지원 예정)
        /// </summary>
        Xml
    }
}
