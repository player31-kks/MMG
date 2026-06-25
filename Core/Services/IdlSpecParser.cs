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
        private static readonly Regex DirectivePattern = new(@"^//\+(\w+)=(\w+)\s*$", RegexOptions.Compiled);
        private static readonly Regex StructPattern = new(@"^struct\s+([A-Za-z_][A-Za-z0-9_]*)\s*(//\s*\$\(([^)]*)\)\$)?\s*$", RegexOptions.Compiled);
        private static readonly Regex FieldPattern = new(@"^(?<type>[A-Za-z_][A-Za-z0-9_]*(?:\s+[A-Za-z_][A-Za-z0-9_]*)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\[(?<array>\d+)\])?(?:\s*:\s*(?<bits>\d+))?\s*;\s*(?://(?<comment>.*))?$", RegexOptions.Compiled);
        private static readonly Regex CommentPattern = new(@"//(.*)$", RegexOptions.Compiled);
        private static readonly Regex BlockCommentPattern = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex MsgIdMarkerPattern = new(@"//\s*\$\(\)\$", RegexOptions.Compiled);
        private static readonly Regex StructInheritancePattern = new(@"^struct\s+[A-Za-z_][A-Za-z0-9_]*\s*:", RegexOptions.Compiled);
        private static readonly Regex IdentifierPattern = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

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
                },
                IdlMetadata = new IdlSpecMetadata
                {
                    PackSize = 1,
                    IsBigEndian = true,
                    HeaderStructName = "MsgHeader",
                    HeaderMessageIdFieldName = "MsgID"
                }
            };
        }

        #region IDL Parsing

        /// <summary>
        /// IDL 텍스트를 IdlDocument로 파싱
        /// </summary>
        private IdlDocument ParseIdl(string content)
        {
            content = StripBlockComments(content);

            var doc = new IdlDocument();
            var lines = content.Split('\n');
            var currentIndex = 0;
            var directiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var definedStructNames = new HashSet<string>(StringComparer.Ordinal);
            var messageIds = new HashSet<int>();

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
                                {
                                    doc.Directives.PackSize = packSize;
                                    directiveKeys.Add(key);
                                }
                                else
                                {
                                    throw new FormatException($"PACK_SIZE 값이 올바르지 않습니다: {value}");
                                }
                                break;
                            case "MOST_BYTE":
                                if (!value.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                                    !value.Equals("false", StringComparison.OrdinalIgnoreCase))
                                {
                                    throw new FormatException($"MOST_BYTE 값은 true 또는 false 여야 합니다: {value}");
                                }

                                doc.Directives.IsBigEndian = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                                directiveKeys.Add(key);
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

            if (!directiveKeys.Contains("PACK_SIZE") || !directiveKeys.Contains("MOST_BYTE"))
            {
                throw new FormatException("IDL 최상단에는 //+PACK_SIZE 와 //+MOST_BYTE 지시자가 모두 필요합니다.");
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

                ValidateTopLevelLine(line);

                var structSignature = StripOpeningBrace(line);

                if (StructInheritancePattern.IsMatch(structSignature))
                {
                    throw new FormatException($"Header 상속 문법은 허용되지 않습니다: {line}");
                }

                // struct 선언 찾기
                var structMatch = StructPattern.Match(structSignature);
                if (structMatch.Success)
                {
                    var structName = structMatch.Groups[1].Value;
                    var marker = structMatch.Groups[3].Value;
                    var hasMarker = structMatch.Groups[2].Success;

                    if (!IdentifierPattern.IsMatch(structName) || !definedStructNames.Add(structName))
                    {
                        throw new FormatException($"구조체 이름이 올바르지 않거나 중복되었습니다: {structName}");
                    }

                    // 구조체 본문 파싱
                    var fields = ParseStructBody(lines, ref currentIndex, definedStructNames, doc.HeaderStruct?.Name, line.Contains("{"));

                    if (hasMarker && string.IsNullOrEmpty(marker))
                    {
                        if (doc.HeaderStruct != null)
                        {
                            throw new FormatException("Header 구조체는 반드시 1개만 존재해야 합니다.");
                        }

                        // Header struct: //$()$
                        var headerStruct = new IdlStruct
                        {
                            Name = structName,
                            Fields = fields
                        };

                        if (fields.Count(f => f.IsMsgIdField) != 1)
                        {
                            throw new FormatException("Header 구조체에는 메시지 ID 필드 마커(// $()$)가 정확히 1개 필요합니다.");
                        }

                        doc.HeaderStruct = headerStruct;
                    }
                    else if (hasMarker && !string.IsNullOrEmpty(marker))
                    {
                        if (doc.HeaderStruct == null)
                        {
                            throw new FormatException("메시지 구조체보다 먼저 Header 구조체가 정의되어야 합니다.");
                        }

                        // Message struct: //$(MsgID)$
                        var msgStruct = new IdlMessageStruct
                        {
                            Name = structName,
                            Fields = fields,
                            MessageIdString = marker
                        };

                        // 메시지 ID 파싱 (10진수 또는 16진수)
                        msgStruct.MessageId = ParseMessageId(marker);

                        if (!messageIds.Add(msgStruct.MessageId))
                        {
                            throw new FormatException($"중복된 메시지 ID는 허용되지 않습니다: {marker}");
                        }

                        if (fields.Count == 0 || fields[0].TypeName != doc.HeaderStruct.Name)
                        {
                            throw new FormatException($"메시지 구조체 '{structName}'의 첫 번째 필드는 반드시 {doc.HeaderStruct.Name} 타입이어야 합니다.");
                        }

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
                    throw new FormatException($"지원되지 않는 struct 선언입니다: {line}");
                }
            }

            if (doc.HeaderStruct == null)
            {
                throw new FormatException("Header 구조체가 필요합니다. struct 선언부에 // $()$ 마커를 지정하세요.");
            }

            return doc;
        }

        /// <summary>
        /// 구조체 본문 파싱 (필드들)
        /// </summary>
        private List<IdlField> ParseStructBody(
            string[] lines,
            ref int currentIndex,
            HashSet<string> definedStructNames,
            string? headerStructName,
            bool hasOpeningBraceOnSignatureLine)
        {
            var fields = new List<IdlField>();

            // { 찾기
            if (!hasOpeningBraceOnSignatureLine)
            {
                currentIndex++;
                while (currentIndex < lines.Length && !lines[currentIndex].Contains("{"))
                {
                    var braceLine = lines[currentIndex].Trim();
                    if (!string.IsNullOrEmpty(braceLine) && !braceLine.StartsWith("//"))
                    {
                        throw new FormatException($"struct 본문 시작 전에 허용되지 않는 구문이 있습니다: {braceLine}");
                    }
                    currentIndex++;
                }
            }

            if (currentIndex >= lines.Length || !lines[currentIndex].Contains("{"))
            {
                throw new FormatException("struct 본문 시작 '{'를 찾을 수 없습니다.");
            }

            currentIndex++; // { 다음 줄로

            // } 까지 필드들 파싱
            while (currentIndex < lines.Length)
            {
                var line = lines[currentIndex].Trim();

                if (line.StartsWith("}"))
                {
                    if (!Regex.IsMatch(line, @"^}\s*;\s*$"))
                    {
                        throw new FormatException($"struct 종료는 '}};' 형식이어야 합니다: {line}");
                    }

                    currentIndex++;
                    break;
                }

                if (string.IsNullOrEmpty(line) || (line.StartsWith("//") && !line.Contains(";")))
                {
                    currentIndex++;
                    continue;
                }

                fields.Add(ParseField(line, definedStructNames, headerStructName));

                currentIndex++;
            }

            if (currentIndex > lines.Length)
            {
                throw new FormatException("struct 종료 '};'를 찾을 수 없습니다.");
            }

            return fields;
        }

        /// <summary>
        /// 필드 라인 파싱
        /// </summary>
        private IdlField ParseField(string line, HashSet<string> definedStructNames, string? headerStructName)
        {
            var field = new IdlField();
            var fieldLine = line;

            // //$()$ 마커 체크 (공백 허용: // $()$ 또는 //$()$)
            if (MsgIdMarkerPattern.IsMatch(fieldLine))
            {
                field.IsMsgIdField = true;
                fieldLine = MsgIdMarkerPattern.Replace(fieldLine, "").Trim();
            }

            ValidateFieldLine(fieldLine);

            var match = FieldPattern.Match(fieldLine);
            if (!match.Success)
            {
                throw new FormatException($"유효하지 않은 필드 선언입니다: {fieldLine}");
            }

            field.TypeName = match.Groups["type"].Value.Trim();
            field.Name = match.Groups["name"].Value.Trim();

            if (match.Groups["array"].Success)
            {
                field.ArraySize = int.Parse(match.Groups["array"].Value);
            }

            if (match.Groups["bits"].Success)
            {
                field.BitFieldSize = int.Parse(match.Groups["bits"].Value);
            }

            field.Comment = match.Groups["comment"].Value.Trim();

            ValidateFieldTypeReference(field, definedStructNames, headerStructName);

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

            if (int.TryParse(value, out int result))
            {
                return result;
            }

            throw new FormatException($"메시지 ID 형식이 올바르지 않습니다: {value}");
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
                },
                IdlMetadata = new IdlSpecMetadata
                {
                    PackSize = idlDoc.Directives.PackSize,
                    IsBigEndian = idlDoc.Directives.IsBigEndian,
                    HeaderStructName = idlDoc.HeaderStruct?.Name ?? "MsgHeader",
                    HeaderMessageIdFieldName = idlDoc.HeaderMessageIdFieldName
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
                    spec.Components.Headers[idlDoc.HeaderStruct.Name] = headerFields;
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
                Request = new MessageSchema(),
                IdlMetadata = new IdlMessageMetadata
                {
                    StructName = msgStruct.Name,
                    MessageId = msgStruct.MessageId,
                    MessageIdLiteral = msgStruct.MessageIdString
                }
            };

            var headerFields = new List<FieldDefinition>();
            var payloadFields = new List<FieldDefinition>();

            string? headerTypeName = idlDoc.HeaderStruct?.Name;

            foreach (var field in msgStruct.Fields)
            {
                // MsgHeader 타입의 필드는 헤더로 처리
                if (field.TypeName == headerTypeName && idlDoc.HeaderStruct != null)
                {
                    // 헤더 구조체의 필드들을 직접 추가
                    headerFields.AddRange(ConvertFields(idlDoc.HeaderStruct.Fields, idlDoc));

                    // MsgID 필드에 메시지 ID 값 설정
                    var msgIdField = headerFields.FirstOrDefault(f => f.Name == idlDoc.HeaderMessageIdFieldName);
                    if (msgIdField != null)
                    {
                        msgIdField.Value = msgStruct.MessageIdString;
                    }
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
            var metadata = spec.IdlMetadata ?? new IdlSpecMetadata();
            var headerStructName = string.IsNullOrWhiteSpace(metadata.HeaderStructName) ? "MsgHeader" : metadata.HeaderStructName;
            var headerMsgIdFieldName = string.IsNullOrWhiteSpace(metadata.HeaderMessageIdFieldName) ? "MsgID" : metadata.HeaderMessageIdFieldName;

            // 기본 지시자
            sb.AppendLine($"//+PACK_SIZE={metadata.PackSize}");
            sb.AppendLine($"//+MOST_BYTE={(metadata.IsBigEndian ? "true" : "false")}");
            sb.AppendLine();

            // 헤더 구조체 생성
            sb.AppendLine("// Message Header");
            sb.AppendLine($"struct {headerStructName}  //$()$");
            sb.AppendLine("{");
            var headerFields = GetHeaderFields(spec);
            foreach (var field in headerFields)
            {
                sb.AppendLine($"    {ConvertFieldToIdl(field, field.Name == headerMsgIdFieldName)};");
            }
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
            foreach (var (name, message) in spec.Messages.OrderBy(kvp => kvp.Value.IdlMetadata?.MessageId ?? int.MaxValue).ThenBy(kvp => kvp.Key))
            {
                var structName = string.IsNullOrWhiteSpace(message.IdlMetadata?.StructName)
                    ? SanitizeStructName(name)
                    : message.IdlMetadata.StructName;
                var messageIdLiteral = GetMessageIdLiteral(message);

                sb.AppendLine($"// {message.Description}");
                sb.AppendLine($"struct {structName}   //$({messageIdLiteral})$");
                sb.AppendLine("{");
                sb.AppendLine($"    {headerStructName} header;");

                foreach (var field in message.Request.Payload)
                {
                    sb.AppendLine($"    {ConvertFieldToIdl(field)};");
                }

                sb.AppendLine("};");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// FieldDefinition을 IDL 필드 선언으로 변환
        /// </summary>
        private string ConvertFieldToIdl(FieldDefinition field, bool isHeaderMessageIdField = false)
        {
            var idlType = ConvertToIdlType(field.Type);
            var suffix = string.Empty;

            if (isHeaderMessageIdField)
            {
                suffix = string.IsNullOrEmpty(field.Description)
                    ? "  // $()$"
                    : $"  // $()$ // {field.Description}";
            }
            else if (!string.IsNullOrEmpty(field.Description))
            {
                suffix = $"  // {field.Description}";
            }

            return $"{idlType} {field.Name}{suffix}";
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
                "uint64" => "uint64_t",
                "int64" => "int64_t",
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

        private static void ValidateTopLevelLine(string line)
        {
            ValidateNoForbiddenConstructs(line);

            if (!line.StartsWith("struct ", StringComparison.Ordinal))
            {
                throw new FormatException($"전역 영역에는 struct 선언만 허용됩니다: {line}");
            }
        }

        private static void ValidateFieldLine(string line)
        {
            ValidateNoForbiddenConstructs(line);

            if (!line.Contains(';'))
            {
                throw new FormatException($"모든 필드는 ';'로 종료되어야 합니다: {line}");
            }

            if (line.Contains('*'))
            {
                throw new FormatException($"포인터 문법은 허용되지 않습니다: {line}");
            }

            if (line.Contains('='))
            {
                throw new FormatException($"초기값 지정은 허용되지 않습니다: {line}");
            }

            if (line.Contains('(') || line.Contains(')'))
            {
                throw new FormatException($"함수 선언 문법은 허용되지 않습니다: {line}");
            }
        }

        private static void ValidateNoForbiddenConstructs(string line)
        {
            if (Regex.IsMatch(line, @"\b(enum|union|typedef)\b", RegexOptions.IgnoreCase))
            {
                throw new FormatException($"금지된 문법이 포함되어 있습니다: {line}");
            }

            if (Regex.IsMatch(line, @"#?\s*pragma\s+pack", RegexOptions.IgnoreCase))
            {
                throw new FormatException($"pragma pack 문법은 허용되지 않습니다: {line}");
            }
        }

        private static void ValidateFieldTypeReference(IdlField field, HashSet<string> definedStructNames, string? headerStructName)
        {
            if (!IdentifierPattern.IsMatch(field.Name))
            {
                throw new FormatException($"필드 식별자가 올바르지 않습니다: {field.Name}");
            }

            if (!field.IsStructType)
            {
                return;
            }

            if (!IdentifierPattern.IsMatch(field.TypeName))
            {
                throw new FormatException($"지원되지 않는 타입 선언입니다: {field.TypeName}");
            }

            if (field.TypeName == headerStructName || definedStructNames.Contains(field.TypeName))
            {
                return;
            }

            throw new FormatException($"구조체 타입은 사용 전에 정의되어야 합니다: {field.TypeName}");
        }

        private static string StripOpeningBrace(string line)
        {
            var braceIndex = line.IndexOf('{');
            return braceIndex >= 0 ? line[..braceIndex].TrimEnd() : line;
        }

        private static string StripBlockComments(string content)
        {
            return BlockCommentPattern.Replace(content, match =>
            {
                var preservedLineBreaks = new StringBuilder(match.Length);
                foreach (var ch in match.Value)
                {
                    if (ch == '\r' || ch == '\n')
                    {
                        preservedLineBreaks.Append(ch);
                    }
                }

                return preservedLineBreaks.ToString();
            });
        }

        private static List<FieldDefinition> GetHeaderFields(UdpApiSpec spec)
        {
            if (spec.Components?.Headers == null || spec.Components.Headers.Count == 0)
            {
                return new List<FieldDefinition>
                {
                    new() { Name = "MsgID", Type = "uint16" },
                    new() { Name = "Length", Type = "uint16" }
                };
            }

            var metadata = spec.IdlMetadata ?? new IdlSpecMetadata();
            if (spec.Components.Headers.TryGetValue(metadata.HeaderStructName, out var namedHeader))
            {
                return namedHeader;
            }

            if (spec.Components.Headers.TryGetValue("CommonHeader", out var commonHeader))
            {
                return commonHeader;
            }

            return spec.Components.Headers.Values.First();
        }

        private static string GetMessageIdLiteral(MessageDefinition message)
        {
            if (!string.IsNullOrWhiteSpace(message.IdlMetadata?.MessageIdLiteral))
            {
                return message.IdlMetadata.MessageIdLiteral;
            }

            var match = Regex.Match(message.Description ?? string.Empty, @"(0x[0-9A-Fa-f]+|\d+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return message.IdlMetadata?.MessageId > 0
                ? message.IdlMetadata.MessageId.ToString()
                : "1";
        }

        #endregion
    }
}
