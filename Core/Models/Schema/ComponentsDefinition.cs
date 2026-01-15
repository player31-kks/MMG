namespace MMG.Core.Models.Schema
{
    /// <summary>
    /// 재사용 가능한 컴포넌트 정의
    /// </summary>
    public class ComponentsDefinition
    {
        /// <summary>
        /// 재사용 가능한 스키마 정의
        /// </summary>
        public Dictionary<string, SchemaDefinition> Schemas { get; set; } = new();

        /// <summary>
        /// 재사용 가능한 헤더 정의
        /// </summary>
        public Dictionary<string, List<FieldDefinition>> Headers { get; set; } = new();
    }

    /// <summary>
    /// 스키마 정의 (재사용 가능한 필드 그룹)
    /// </summary>
    public class SchemaDefinition
    {
        public string Description { get; set; } = string.Empty;

        public List<FieldDefinition> Fields { get; set; } = new();
    }
}
