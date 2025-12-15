using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using MMG.Core.Interfaces;
using MMG.Core.Models.Schema;

namespace MMG.Core.Services
{
    /// <summary>
    /// UDP API 스펙 파서 - YAML 기반
    /// </summary>
    public class UdpApiSpecParser : ISpecParser
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

        public UdpApiSpecParser()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();
        }

        /// <summary>
        /// YAML 파일에서 스펙 로드
        /// </summary>
        public async Task<UdpApiSpec> ParseFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"스펙 파일을 찾을 수 없습니다: {filePath}");

            var yamlContent = await File.ReadAllTextAsync(filePath);
            return ParseYaml(yamlContent);
        }

        /// <summary>
        /// YAML 문자열에서 스펙 파싱
        /// </summary>
        public UdpApiSpec ParseYaml(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent))
                throw new ArgumentException("YAML 내용이 비어있습니다.");

            try
            {
                var spec = _deserializer.Deserialize<UdpApiSpec>(yamlContent);
                ValidateSpec(spec);
                ResolveReferences(spec);
                return spec;
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                throw new InvalidOperationException($"YAML 파싱 오류: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 스펙을 YAML 문자열로 직렬화
        /// </summary>
        public string ToYaml(UdpApiSpec spec)
        {
            return _serializer.Serialize(spec);
        }

        /// <summary>
        /// 스펙을 파일로 저장
        /// </summary>
        public async Task SaveToFileAsync(UdpApiSpec spec, string filePath)
        {
            var yaml = ToYaml(spec);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(filePath, yaml);
        }

        /// <summary>
        /// 스펙 유효성 검사
        /// </summary>
        private void ValidateSpec(UdpApiSpec spec)
        {
            if (spec == null)
                throw new InvalidOperationException("스펙 파싱 결과가 null입니다.");

            if (string.IsNullOrEmpty(spec.Info?.Title))
                throw new InvalidOperationException("스펙에 title이 필요합니다.");

            foreach (var (name, message) in spec.Messages)
            {
                if (message.Request == null)
                    throw new InvalidOperationException($"메시지 '{name}'에 request 정의가 필요합니다.");
            }
        }

        /// <summary>
        /// 참조($ref) 해결
        /// </summary>
        private void ResolveReferences(UdpApiSpec spec)
        {
            if (spec.Components == null) return;

            foreach (var (_, message) in spec.Messages)
            {
                ResolveMessageReferences(message.Request, spec.Components);
                if (message.Response != null)
                {
                    ResolveMessageReferences(message.Response, spec.Components);
                }
            }
        }

        private void ResolveMessageReferences(MessageSchema schema, ComponentsDefinition components)
        {
            // 헤더 참조 해결
            for (int i = 0; i < schema.Header.Count; i++)
            {
                var field = schema.Header[i];
                if (!string.IsNullOrEmpty(field.ComponentRef))
                {
                    var resolved = ResolveFieldReference(field.ComponentRef, components);
                    if (resolved != null)
                    {
                        schema.Header.RemoveAt(i);
                        schema.Header.InsertRange(i, resolved);
                    }
                }
            }

            // 페이로드 참조 해결
            for (int i = 0; i < schema.Payload.Count; i++)
            {
                var field = schema.Payload[i];
                if (!string.IsNullOrEmpty(field.ComponentRef))
                {
                    var resolved = ResolveFieldReference(field.ComponentRef, components);
                    if (resolved != null)
                    {
                        schema.Payload.RemoveAt(i);
                        schema.Payload.InsertRange(i, resolved);
                    }
                }
            }
        }

        private List<FieldDefinition>? ResolveFieldReference(string reference, ComponentsDefinition components)
        {
            // #/components/schemas/CommonHeader 형식
            if (reference.StartsWith("#/components/schemas/"))
            {
                var schemaName = reference.Replace("#/components/schemas/", "");
                if (components.Schemas.TryGetValue(schemaName, out var schema))
                {
                    return schema.Fields;
                }
            }
            // #/components/headers/StandardHeader 형식
            else if (reference.StartsWith("#/components/headers/"))
            {
                var headerName = reference.Replace("#/components/headers/", "");
                if (components.Headers.TryGetValue(headerName, out var fields))
                {
                    return fields;
                }
            }

            return null;
        }
    }
}
