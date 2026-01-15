using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MMG.Core.Interfaces;
using MMG.Core.Models.Idl;
using MMG.Core.Models.Schema;
using MMG.Core.Models.Protocol;

namespace MMG.Core.Services
{
    /// <summary>
    /// GKS-IDL 형식의 스펙 파서
    /// IDL(Interface Definition Language) 문서를 파싱하여 UdpApiSpec으로 변환
    /// </summary>
    public class IdlSpecParser : ISpecParser
    {
        // 정규식 패턴들
        private static readonly Regex DirectivePattern = new(@"//\+(\w+)=(\w+)", RegexOptions.Compiled);
        private static readonly Regex StructPattern = new(@"struct\s+(\w+)\s*(//\$\(([^)]*)\)\$)?", RegexOptions.Compiled);
        private static readonly Regex FieldPattern = new(@"^\s*([\w\s]+)\s+(\w+)\s*(?:\[(\d+)\])?\s*(?::\s*(\d+))?\s*;\s*(//\$\(([^)]*)\)\$)?(.*)$", RegexOptions.Compiled);
        private static readonly Regex CommentPattern = new(@"//(.*)$", RegexOptions.Compiled);

        public IReadOnlyList<string> SupportedExtensions => new[] { ".idl", ".gidl" };

        /// <summary>
        /// 파일에서 IDL 스펙 로드
        /// </summary>
        public async Task<UdpApiSpec> ParseFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"스펙 파일을 찾을 수 없습니다: {filePath}");

            var content = await File.ReadAllTextAsync(filePath);
            var idlDoc = ParseIdl(content);
            idlDoc.FilePath = filePath;

            return ConvertToUdpApiSpec(idlDoc, filePath);
        }

        /// <summary>
        /// IDL 문자열에서 스펙 파싱
        /// </summary>
        public UdpApiSpec Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("IDL 내용이 비어있습니다.");

            var idlDoc = ParseIdl(content);
            return ConvertToUdpApiSpec(idlDoc);
        }

        /// <summary>
        /// 스펙을 IDL 문자열로 직렬화
        /// </summary>
        public string Serialize(UdpApiSpec spec)
        {
            return ConvertToIdl(spec);
        }

        /// <summary>
        /// 스펙을 파일로 저장
        /// </summary>
        public async Task SaveToFileAsync(UdpApiSpec spec, string filePath)
        {
            var idl = Serialize(spec);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllTextAsync(filePath, idl);
        }

        /// <summary>
        /// 새로운 기본 스펙 생성
        /// </summary>
        public UdpApiSpec CreateDefaultSpec()
        {
            return new UdpApiSpec
            {
                Version = "1.0.0",
                Info = new ApiInfo
                {
                    Title = "New UDP API",
                    Description = "새로운 UDP API 스펙",
                    Version = "1.0.0"
                },
                Servers = new List<ServerInfo>
                {
                    new ServerInfo
                    {
                        Name = "기본 서버",
                        IpAddress = "127.0.0.1",
                        Port = 8080
                    }
                }
            };
        }

        #region IDL Parsing

        /// <summary>
        /// IDL 텍스트를 IdlDocument로 파싱
        /// </summary>
        private IdlDocument ParseIdl(string content)
        {
            var doc = new IdlDocument();
            var lines = content.Split('\n');
            var currentIndex = 0;

            // 1. 지시자 파싱 (//+PACK_SIZE=n, //+MOST_BYTE=true)
            while (currentIndex < lines.Length)
            {
                var line = lines[currentIndex].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                {
                    var directiveMatch = DirectivePattern.Match(line);
                    if (directiveMatch.Success)
                    {
                        var key = directiveMatch.Groups[1].Value.ToUpperInvariant();
                        var value = directiveMatch.Groups[2].Value;

                        switch (key)
                        {
                            case "PACK_SIZE":
                                if (int.TryParse(value, out int packSize))
                                    doc.Directives.PackSize = packSize;
                                break;
                            case "MOST_BYTE":
                                doc.Directives.IsBigEndian = value.ToLowerInvariant() == "true";
                                break;
                        }
                    }
                    currentIndex++;
                }
                else
                {
                    break;
                }
            }

            // 2. 구조체들 파싱
            while (currentIndex < lines.Length)
            {
                var line = lines[currentIndex].Trim();

                if (string.IsNullOrEmpty(line) || (line.StartsWith("//") && !line.Contains("struct")))
                {
                    currentIndex++;
                    continue;
                }

                // struct 선언 찾기
                var structMatch = StructPattern.Match(line);
                if (structMatch.Success)
                {
                    var structName = structMatch.Groups[1].Value;
                    var marker = structMatch.Groups[3].Value;
                    var hasMarker = structMatch.Groups[2].Success;

                    // 구조체 본문 파싱
                    currentIndex++;
                    var fields = ParseStructBody(lines, ref currentIndex);

                    if (hasMarker && string.IsNullOrEmpty(marker))
                    {
                        // Header struct: //$()$
                        var headerStruct = new IdlStruct
                        {
                            Name = structName,
                            Fields = fields
                        };

                        // MsgID 필드 마킹
                        foreach (var field in fields)
                        {
                            if (field.IsMsgIdField)
                            {
                                // MsgID 필드 발견
                            }
                        }

                        doc.HeaderStruct = headerStruct;
                    }
                    else if (hasMarker && !string.IsNullOrEmpty(marker))
                    {
                        // Message struct: //$(MsgID)$
                        var msgStruct = new IdlMessageStruct
                        {
                            Name = structName,
                            Fields = fields,
                            MessageIdString = marker
                        };

                        // 메시지 ID 파싱 (10진수 또는 16진수)
                        msgStruct.MessageId = ParseMessageId(marker);

                        doc.MessageStructs.Add(msgStruct);
                    }
                    else
                    {
                        // 일반 사용자 정의 구조체
                        var userStruct = new IdlStruct
                        {
                            Name = structName,
                            Fields = fields
                        };
                        doc.UserStructs.Add(userStruct);
                    }
                }
                else
                {
                    currentIndex++;
                }
            }

            return doc;
        }

        /// <summary>
        /// 구조체 본문 파싱 (필드들)
        /// </summary>
        private List<IdlField> ParseStructBody(string[] lines, ref int currentIndex)
        {
            var fields = new List<IdlField>();

            // { 찾기
            while (currentIndex < lines.Length && !lines[currentIndex].Contains("{"))
            {
                currentIndex++;
            }
            currentIndex++; // { 다음 줄로

            // } 까지 필드들 파싱
            while (currentIndex < lines.Length)
            {
                var line = lines[currentIndex].Trim();

                if (line.StartsWith("}"))
                {
                    currentIndex++;
                    break;
                }

                if (string.IsNullOrEmpty(line) || (line.StartsWith("//") && !line.Contains(";")))
                {
                    currentIndex++;
                    continue;
                }

                var field = ParseField(line);
                if (field != null)
                {
                    fields.Add(field);
                }

                currentIndex++;
            }

            return fields;
        }

        /// <summary>
        /// 필드 라인 파싱
        /// </summary>
        private IdlField? ParseField(string line)
        {
            // 세미콜론이 없으면 필드가 아님
            if (!line.Contains(";")) return null;

            var field = new IdlField();

            // //$()$ 마커 체크
            if (line.Contains("//$()$"))
            {
                field.IsMsgIdField = true;
                line = line.Replace("//$()$", "").Trim();
            }

            // 주석 분리
            var commentMatch = CommentPattern.Match(line);
            if (commentMatch.Success)
            {
                field.Comment = commentMatch.Groups[1].Value.Trim();
                line = line.Substring(0, commentMatch.Index).Trim();
            }

            // 세미콜론 제거
            line = line.TrimEnd(';').Trim();

            // 비트 필드 처리: type name : bits
            var bitFieldParts = line.Split(':');
            if (bitFieldParts.Length == 2)
            {
                if (int.TryParse(bitFieldParts[1].Trim(), out int bitSize))
                {
                    field.BitFieldSize = bitSize;
                    line = bitFieldParts[0].Trim();
                }
            }

            // 배열 처리: type name[size]
            var arrayMatch = Regex.Match(line, @"(.+)\[(\d+)\]$");
            if (arrayMatch.Success)
            {
                if (int.TryParse(arrayMatch.Groups[2].Value, out int arraySize))
                {
                    field.ArraySize = arraySize;
                    line = arrayMatch.Groups[1].Value.Trim();
                }
            }

            // 타입과 이름 분리 (마지막 단어가 이름)
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return null;

            field.Name = parts[^1]; // 마지막 요소가 이름
            field.TypeName = string.Join(" ", parts[..^1]); // 나머지가 타입

            return field;
        }

        /// <summary>
        /// 메시지 ID 파싱 (10진수 또는 16진수)
        /// </summary>
        private int ParseMessageId(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt32(value, 16);
            }
            return int.TryParse(value, out int result) ? result : 0;
        }

        #endregion

        #region Conversion to UdpApiSpec

        /// <summary>
        /// IdlDocument를 UdpApiSpec으로 변환
        /// </summary>
        private UdpApiSpec ConvertToUdpApiSpec(IdlDocument idlDoc, string? filePath = null)
        {
            var spec = new UdpApiSpec
            {
                Version = "1.0.0",
                Info = new ApiInfo
                {
                    Title = string.IsNullOrEmpty(filePath)
                        ? "IDL Specification"
                        : Path.GetFileNameWithoutExtension(filePath),
                    Description = $"IDL에서 변환됨 (Pack Size: {idlDoc.Directives.PackSize}, Endian: {(idlDoc.Directives.IsBigEndian ? "Big" : "Little")})",
                    Version = "1.0.0"
                }
            };

            // 컴포넌트에 사용자 정의 구조체 추가
            if (idlDoc.UserStructs.Any() || idlDoc.HeaderStruct != null)
            {
                spec.Components = new ComponentsDefinition();

                // 헤더 구조체를 공통 헤더로 등록
                if (idlDoc.HeaderStruct != null)
                {
                    var headerFields = ConvertFields(idlDoc.HeaderStruct.Fields, idlDoc);
                    spec.Components.Headers["CommonHeader"] = headerFields;
                }

                // 사용자 정의 구조체들 등록
                foreach (var userStruct in idlDoc.UserStructs)
                {
                    var fields = ConvertFields(userStruct.Fields, idlDoc);
                    spec.Components.Schemas[userStruct.Name] = new SchemaDefinition
                    {
                        Description = userStruct.Comment,
                        Fields = fields
                    };
                }
            }

            // 메시지 구조체들을 메시지로 변환
            foreach (var msgStruct in idlDoc.MessageStructs)
            {
                var message = ConvertMessageStruct(msgStruct, idlDoc);
                var messageName = $"Msg_{msgStruct.MessageId:D4}_{msgStruct.Name}";
                spec.Messages[messageName] = message;
            }

            return spec;
        }

        /// <summary>
        /// 메시지 구조체를 MessageDefinition으로 변환
        /// </summary>
        private MessageDefinition ConvertMessageStruct(IdlMessageStruct msgStruct, IdlDocument idlDoc)
        {
            var message = new MessageDefinition
            {
                Description = $"Message ID: {msgStruct.MessageIdString} ({msgStruct.Name})",
                TimeoutMs = 5000,
                Request = new MessageSchema()
            };

            var headerFields = new List<FieldDefinition>();
            var payloadFields = new List<FieldDefinition>();

            bool inHeader = false;
            string? headerTypeName = idlDoc.HeaderStruct?.Name;

            foreach (var field in msgStruct.Fields)
            {
                // MsgHeader 타입의 필드는 헤더로 처리
                if (field.TypeName == headerTypeName && idlDoc.HeaderStruct != null)
                {
                    // 헤더 구조체의 필드들을 직접 추가
                    headerFields.AddRange(ConvertFields(idlDoc.HeaderStruct.Fields, idlDoc));

                    // MsgID 필드에 메시지 ID 값 설정
                    var msgIdField = headerFields.FirstOrDefault(f => f.Name == "MsgID" || f.Name == "msgId");
                    if (msgIdField != null)
                    {
                        msgIdField.Value = msgStruct.MessageIdString;
                    }

                    inHeader = true;
                }
                else
                {
                    // 나머지는 페이로드
                    var convertedFields = ConvertField(field, idlDoc);
                    payloadFields.AddRange(convertedFields);
                }
            }

            message.Request.Header = headerFields;
            message.Request.Payload = payloadFields;

            // 엔디안 설정
            foreach (var field in message.Request.Header.Concat(message.Request.Payload))
            {
                field.Endian = idlDoc.Directives.IsBigEndian ? "big" : "little";
            }

            return message;
        }

        /// <summary>
        /// 필드 목록을 FieldDefinition 목록으로 변환
        /// </summary>
        private List<FieldDefinition> ConvertFields(List<IdlField> fields, IdlDocument idlDoc)
        {
            var result = new List<FieldDefinition>();
            foreach (var field in fields)
            {
                result.AddRange(ConvertField(field, idlDoc));
            }
            return result;
        }

        /// <summary>
        /// 단일 필드를 FieldDefinition으로 변환
        /// </summary>
        private List<FieldDefinition> ConvertField(IdlField field, IdlDocument idlDoc)
        {
            var results = new List<FieldDefinition>();

            // 비트 필드 처리
            if (field.IsBitField)
            {
                var bitField = new FieldDefinition
                {
                    Name = field.Name,
                    Type = ConvertPrimitiveType(field.PrimitiveType),
                    Value = "0",
                    Description = field.Comment,
                    Endian = idlDoc.Directives.IsBigEndian ? "big" : "little",
                    BitFields = new List<BitFieldDefinition>
                    {
                        new BitFieldDefinition
                        {
                            Name = field.Name,
                            BitRange = $"0:{field.BitFieldSize!.Value - 1}",
                            Description = field.Comment
                        }
                    }
                };
                results.Add(bitField);
                return results;
            }

            // 구조체 타입 처리
            if (field.IsStructType)
            {
                var structDef = idlDoc.FindStruct(field.TypeName);
                if (structDef != null)
                {
                    int count = field.IsArray ? field.ArraySize!.Value : 1;
                    for (int i = 0; i < count; i++)
                    {
                        foreach (var structField in structDef.Fields)
                        {
                            var convertedFields = ConvertField(structField, idlDoc);
                            foreach (var cf in convertedFields)
                            {
                                cf.Name = count > 1
                                    ? $"{field.Name}[{i}].{cf.Name}"
                                    : $"{field.Name}.{cf.Name}";
                            }
                            results.AddRange(convertedFields);
                        }
                    }
                }
                return results;
            }

            // 배열 처리
            if (field.IsArray)
            {
                for (int i = 0; i < field.ArraySize!.Value; i++)
                {
                    var arrayField = new FieldDefinition
                    {
                        Name = $"{field.Name}[{i}]",
                        Type = ConvertPrimitiveType(field.PrimitiveType),
                        Value = "0",
                        Description = i == 0 ? field.Comment : "",
                        Endian = idlDoc.Directives.IsBigEndian ? "big" : "little"
                    };
                    results.Add(arrayField);
                }
                return results;
            }

            // 일반 필드
            var fieldDef = new FieldDefinition
            {
                Name = field.Name,
                Type = ConvertPrimitiveType(field.PrimitiveType),
                Value = "0",
                Description = field.Comment,
                Endian = idlDoc.Directives.IsBigEndian ? "big" : "little"
            };
            results.Add(fieldDef);

            return results;
        }

        /// <summary>
        /// IDL 기본 타입을 문자열 타입명으로 변환
        /// </summary>
        private string ConvertPrimitiveType(IdlPrimitiveType type)
        {
            return type switch
            {
                IdlPrimitiveType.Char => "int8",
                IdlPrimitiveType.UnsignedChar => "byte",
                IdlPrimitiveType.Short => "int16",
                IdlPrimitiveType.UnsignedShort => "uint16",
                IdlPrimitiveType.Int => "int32",
                IdlPrimitiveType.UnsignedInt => "uint32",
                IdlPrimitiveType.Long => "int64",
                IdlPrimitiveType.UnsignedLong => "uint64",
                IdlPrimitiveType.Float => "float",
                IdlPrimitiveType.Double => "double",
                _ => "byte"
            };
        }

        #endregion

        #region Conversion to IDL

        /// <summary>
        /// UdpApiSpec을 IDL 문자열로 변환
        /// </summary>
        private string ConvertToIdl(UdpApiSpec spec)
        {
            var sb = new StringBuilder();

            // 기본 지시자
            sb.AppendLine("//+PACK_SIZE=1");
            sb.AppendLine("//+MOST_BYTE=true");
            sb.AppendLine();

            // 헤더 구조체 생성
            sb.AppendLine("// Message Header");
            sb.AppendLine("struct MsgHeader  //$()$");
            sb.AppendLine("{");
            sb.AppendLine("    unsigned short MsgID;   //$()$");
            sb.AppendLine("    unsigned short Length;");
            sb.AppendLine("};");
            sb.AppendLine();

            // 컴포넌트의 스키마들을 사용자 정의 구조체로 변환
            if (spec.Components?.Schemas != null)
            {
                foreach (var (name, schema) in spec.Components.Schemas)
                {
                    sb.AppendLine($"// {schema.Description}");
                    sb.AppendLine($"struct {name}");
                    sb.AppendLine("{");
                    foreach (var field in schema.Fields)
                    {
                        sb.AppendLine($"    {ConvertFieldToIdl(field)};");
                    }
                    sb.AppendLine("};");
                    sb.AppendLine();
                }
            }

            // 메시지들을 메시지 구조체로 변환
            int msgId = 1;
            foreach (var (name, message) in spec.Messages)
            {
                sb.AppendLine($"// {message.Description}");
                sb.AppendLine($"struct {SanitizeStructName(name)}   //$({msgId})$");
                sb.AppendLine("{");
                sb.AppendLine("    MsgHeader header;");

                foreach (var field in message.Request.Payload)
                {
                    sb.AppendLine($"    {ConvertFieldToIdl(field)};");
                }

                sb.AppendLine("};");
                sb.AppendLine();
                msgId++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// FieldDefinition을 IDL 필드 선언으로 변환
        /// </summary>
        private string ConvertFieldToIdl(FieldDefinition field)
        {
            var idlType = ConvertToIdlType(field.Type);
            var description = string.IsNullOrEmpty(field.Description) ? "" : $"  // {field.Description}";
            return $"{idlType} {field.Name}{description}";
        }

        /// <summary>
        /// 문자열 타입을 IDL 타입으로 변환
        /// </summary>
        private string ConvertToIdlType(string type)
        {
            return type.ToLowerInvariant() switch
            {
                "byte" or "uint8" => "unsigned char",
                "int8" => "char",
                "uint16" => "unsigned short",
                "int16" or "short" => "short",
                "uint32" or "uint" => "unsigned int",
                "int32" or "int" => "int",
                "float" or "float32" => "float",
                "double" or "float64" => "double",
                _ => "unsigned char"
            };
        }

        /// <summary>
        /// 구조체 이름 정리 (특수문자 제거)
        /// </summary>
        private string SanitizeStructName(string name)
        {
            return Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");
        }

        #endregion
    }
}
