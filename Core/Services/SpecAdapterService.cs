using MMG.Core.Interfaces;
using MMG.Core.Models.Schema;
using MMG.Core.Services;
using MMG.Models;
using System.Collections.ObjectModel;

namespace MMG.Core.Services
{
    /// <summary>
    /// 스펙과 기존 시스템을 연결하는 어댑터 서비스
    /// </summary>
    public class SpecAdapterService
    {
        private readonly UdpApiSpecParser _parser;

        public SpecAdapterService()
        {
            _parser = new UdpApiSpecParser();
        }

        /// <summary>
        /// YAML 파일에서 스펙 로드
        /// </summary>
        public async Task<UdpApiSpec> LoadSpecAsync(string filePath)
        {
            return await _parser.ParseFileAsync(filePath);
        }

        /// <summary>
        /// YAML 문자열에서 스펙 파싱
        /// </summary>
        public UdpApiSpec ParseSpec(string yamlContent)
        {
            return _parser.ParseYaml(yamlContent);
        }

        /// <summary>
        /// 메시지 정의를 기존 UdpRequest로 변환
        /// </summary>
        public UdpRequest ToUdpRequest(MessageDefinition message, ServerInfo? server = null)
        {
            var request = new UdpRequest();

            // 엔드포인트 설정
            if (message.Endpoint != null)
            {
                if (!string.IsNullOrEmpty(message.Endpoint.IpAddress))
                {
                    request.IpAddress = message.Endpoint.IpAddress;
                }
                else if (server != null)
                {
                    request.IpAddress = server.IpAddress;
                }

                if (message.Endpoint.Port.HasValue)
                {
                    request.Port = message.Endpoint.Port.Value;
                }
                else if (server != null)
                {
                    request.Port = server.Port;
                }
            }

            // 헤더 변환
            request.Headers.Clear();
            foreach (var field in message.Request.Header)
            {
                var dataField = ToDataField(field);
                request.Headers.Add(dataField);
            }

            // 페이로드 변환
            request.Payload.Clear();
            foreach (var field in message.Request.Payload)
            {
                var dataField = ToDataField(field);
                request.Payload.Add(dataField);
            }

            return request;
        }

        /// <summary>
        /// 메시지 정의의 응답 스키마를 기존 ResponseSchema로 변환
        /// </summary>
        public ResponseSchema? ToResponseSchema(MessageDefinition message)
        {
            if (message.Response == null) return null;

            var schema = new ResponseSchema();

            // 헤더 변환
            schema.Headers.Clear();
            foreach (var field in message.Response.Header)
            {
                schema.Headers.Add(ToDataField(field));
            }

            // 페이로드 변환
            schema.Payload.Clear();
            foreach (var field in message.Response.Payload)
            {
                schema.Payload.Add(ToDataField(field));
            }

            return schema;
        }

        /// <summary>
        /// 기존 UdpRequest를 메시지 정의로 변환
        /// </summary>
        public MessageDefinition FromUdpRequest(UdpRequest request, ResponseSchema? responseSchema = null, string name = "")
        {
            var message = new MessageDefinition
            {
                Description = name,
                Endpoint = new EndpointReference
                {
                    IpAddress = request.IpAddress,
                    Port = request.Port
                },
                Request = new MessageSchema()
            };

            // 헤더 변환
            foreach (var field in request.Headers)
            {
                message.Request.Header.Add(FromDataField(field));
            }

            // 페이로드 변환
            foreach (var field in request.Payload)
            {
                message.Request.Payload.Add(FromDataField(field));
            }

            // 응답 스키마 변환
            if (responseSchema != null)
            {
                message.Response = new MessageSchema();
                foreach (var field in responseSchema.Headers)
                {
                    message.Response.Header.Add(FromDataField(field));
                }
                foreach (var field in responseSchema.Payload)
                {
                    message.Response.Payload.Add(FromDataField(field));
                }
            }

            return message;
        }

        /// <summary>
        /// 스펙을 YAML로 내보내기
        /// </summary>
        public async Task ExportSpecAsync(UdpApiSpec spec, string filePath)
        {
            await _parser.SaveToFileAsync(spec, filePath);
        }

        /// <summary>
        /// 스펙을 YAML 문자열로 변환
        /// </summary>
        public string ToYaml(UdpApiSpec spec)
        {
            return _parser.ToYaml(spec);
        }

        /// <summary>
        /// FieldDefinition을 DataField로 변환
        /// </summary>
        private DataField ToDataField(FieldDefinition field)
        {
            var dataField = new DataField
            {
                Name = field.Name,
                Type = ConvertFieldType(field.FieldType),
                Value = field.Value ?? "0",
                PaddingSize = field.Size ?? 1
            };

            return dataField;
        }

        /// <summary>
        /// DataField를 FieldDefinition으로 변환
        /// </summary>
        private FieldDefinition FromDataField(DataField dataField)
        {
            return new FieldDefinition
            {
                Name = dataField.Name,
                Type = ConvertToTypeName(dataField.Type),
                Value = dataField.Value,
                Size = dataField.IsPadding ? dataField.PaddingSize : null,
                Description = ""
            };
        }

        private DataType ConvertFieldType(Models.Protocol.FieldType fieldType)
        {
            return fieldType switch
            {
                Models.Protocol.FieldType.Byte => DataType.Byte,
                Models.Protocol.FieldType.UInt16 => DataType.UInt16,
                Models.Protocol.FieldType.Int32 => DataType.Int,
                Models.Protocol.FieldType.UInt32 => DataType.UInt,
                Models.Protocol.FieldType.Float => DataType.Float,
                Models.Protocol.FieldType.Padding => DataType.Padding,
                _ => DataType.Byte
            };
        }

        private string ConvertToTypeName(DataType dataType)
        {
            return dataType switch
            {
                DataType.Byte => "byte",
                DataType.UInt16 => "uint16",
                DataType.Int => "int32",
                DataType.UInt => "uint32",
                DataType.Float => "float",
                DataType.Padding => "padding",
                _ => "byte"
            };
        }
    }
}
