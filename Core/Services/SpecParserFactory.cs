using System.IO;
using MMG.Core.Interfaces;

namespace MMG.Core.Services
{
    /// <summary>
    /// 스펙 파서 팩토리 구현
    /// 파일 확장자에 따라 적절한 파서를 생성
    /// </summary>
    public class SpecParserFactory : ISpecParserFactory
    {
        private readonly Dictionary<string, Func<ISpecParser>> _parserCreators;

        public SpecParserFactory()
        {
            _parserCreators = new Dictionary<string, Func<ISpecParser>>(StringComparer.OrdinalIgnoreCase)
            {
                { ".idl", () => new IdlSpecParser() },
                { ".gidl", () => new IdlSpecParser() },
                // 향후 XML 지원 시:
                // { ".xml", () => new XmlSpecParser() }
            };
        }

        /// <summary>
        /// 지원되는 파일 확장자 목록
        /// </summary>
        public IReadOnlyList<string> SupportedExtensions => _parserCreators.Keys.ToList();

        /// <summary>
        /// 파일 필터 문자열 (OpenFileDialog용)
        /// </summary>
        public string FileFilter
        {
            get
            {
                var filters = new List<string>
                {
                    "IDL 파일 (*.idl;*.gidl)|*.idl;*.gidl",
                    // 향후 XML 지원 시:
                    // "XML 파일 (*.xml)|*.xml",
                    "모든 파일 (*.*)|*.*"
                };
                return string.Join("|", filters);
            }
        }

        /// <summary>
        /// 파일 경로에 따라 적절한 파서 생성
        /// </summary>
        /// <param name="filePath">파일 경로</param>
        /// <returns>해당 파일 형식에 맞는 파서</returns>
        public ISpecParser CreateParser(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("파일 경로가 비어있습니다.", nameof(filePath));

            var extension = Path.GetExtension(filePath);

            if (string.IsNullOrEmpty(extension))
                throw new ArgumentException($"파일 확장자를 확인할 수 없습니다: {filePath}", nameof(filePath));

            if (_parserCreators.TryGetValue(extension, out var creator))
            {
                return creator();
            }

            throw new NotSupportedException($"지원하지 않는 파일 형식입니다: {extension}\n지원 형식: {string.Join(", ", SupportedExtensions)}");
        }

        /// <summary>
        /// 지정된 파서 타입에 해당하는 파서 생성
        /// </summary>
        /// <param name="parserType">파서 타입</param>
        /// <returns>해당 타입의 파서</returns>
        public ISpecParser CreateParser(SpecParserType parserType)
        {
            return parserType switch
            {
                SpecParserType.Idl => new IdlSpecParser(),
                // 향후 XML 지원 시:
                // SpecParserType.Xml => new XmlSpecParser(),
                _ => throw new NotSupportedException($"지원하지 않는 파서 타입입니다: {parserType}")
            };
        }

        /// <summary>
        /// 파일 형식이 지원되는지 확인
        /// </summary>
        /// <param name="filePath">파일 경로</param>
        /// <returns>지원 여부</returns>
        public bool IsSupported(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var extension = Path.GetExtension(filePath);
            return !string.IsNullOrEmpty(extension) && _parserCreators.ContainsKey(extension);
        }

        /// <summary>
        /// 기본 파서 반환 (IDL)
        /// </summary>
        /// <returns>기본 IDL 파서</returns>
        public ISpecParser GetDefaultParser()
        {
            return new IdlSpecParser();
        }
    }
}
