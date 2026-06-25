using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using MMG.Core.Interfaces;
using MMG.Models;
using MMG.Models.Import;

namespace MMG.Services
{
    public class JsonApiImportHandler : IApiImportHandler
    {
        public ApiImportFormat Format => ApiImportFormat.Json;

        public async Task<IReadOnlyList<ImportedApiDefinition>> ImportAsync(IReadOnlyList<string> filePaths)
        {
            var importedDefinitions = new List<ImportedApiDefinition>();

            foreach (var filePath in filePaths)
            {
                using var stream = File.OpenRead(filePath);
                using var document = await JsonDocument.ParseAsync(stream);

                importedDefinitions.AddRange(ParseFile(document.RootElement, Path.GetFileNameWithoutExtension(filePath)));
            }

            return importedDefinitions;
        }

        private IEnumerable<ImportedApiDefinition> ParseFile(JsonElement root, string fileName)
        {
            var entries = ResolveEntries(root);
            var importedDefinitions = new List<ImportedApiDefinition>();
            var index = 0;

            foreach (var entry in entries.EnumerateArray())
            {
                index++;
                importedDefinitions.Add(ParseEntry(entry, fileName, index, entries.GetArrayLength()));
            }

            return importedDefinitions;
        }

        private JsonElement ResolveEntries(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root;
            }

            if (TryGetProperty(root, out var dataElement, "data"))
            {
                if (dataElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("'data'는 배열이어야 합니다.");
                }

                return dataElement;
            }

            throw new InvalidDataException("JSON 루트에는 'data' 배열이 필요합니다.");
        }

        private ImportedApiDefinition ParseEntry(JsonElement entry, string fileName, int index, int totalCount)
        {
            var requestElement = GetRequiredObject(entry, "request", "reqeust");
            var responseElement = GetRequiredObject(entry, "response");

            return new ImportedApiDefinition
            {
                Name = ResolveRequestName(entry, fileName, index, totalCount),
                IpAddress = GetOptionalString(entry, "ipAddress", "ip", "host") ?? "127.0.0.1",
                Port = GetOptionalInt(entry, "port") ?? 8080,
                IsBigEndian = GetOptionalBool(entry, "isBigEndian", "bigEndian") ?? true,
                WaitForResponse = GetOptionalBool(entry, "waitForResponse") ?? true,
                RequestHeaders = ParseFields(requestElement, false, "header", "headers"),
                RequestPayload = ParseFields(requestElement, false, "payload", "paylaod", "payloads"),
                ResponseHeaders = ParseFields(responseElement, true, "header", "headers"),
                ResponsePayload = ParseFields(responseElement, true, "payload", "paylaod", "payloads")
            };
        }

        private ObservableCollection<DataField> ParseFields(JsonElement parent, bool isResponseField, params string[] propertyNames)
        {
            var fields = new ObservableCollection<DataField>();

            if (!TryGetProperty(parent, out var fieldsElement, propertyNames))
            {
                return fields;
            }

            if (fieldsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"'{propertyNames[0]}'는 배열이어야 합니다.");
            }

            foreach (var fieldElement in fieldsElement.EnumerateArray())
            {
                fields.Add(ParseField(fieldElement, isResponseField));
            }

            return fields;
        }

        private DataField ParseField(JsonElement fieldElement, bool isResponseField)
        {
            var field = new DataField
            {
                Name = GetOptionalString(fieldElement, "name") ?? string.Empty,
                Type = ParseDataType(GetOptionalString(fieldElement, "type") ?? "byte")
            };

            if (field.Type == DataType.Padding)
            {
                field.PaddingSize = GetOptionalInt(fieldElement, "paddingSize", "size", "value") ?? 1;
                field.Value = field.PaddingSize.ToString();
            }
            else if (isResponseField)
            {
                field.Value = string.Empty;
            }
            else
            {
                if (!TryGetProperty(fieldElement, out _, "value"))
                {
                    throw new InvalidDataException($"request 필드 '{field.Name}'에는 value가 필요합니다.");
                }

                field.Value = ConvertJsonValueToString(fieldElement, "value");
            }

            return field;
        }

        private string ResolveRequestName(JsonElement entry, string fileName, int index, int totalCount)
        {
            var explicitName = GetOptionalString(entry, "name", "apiName", "title", "id");
            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                return explicitName;
            }

            return totalCount == 1 ? fileName : $"{fileName}_{index}";
        }

        private DataType ParseDataType(string typeValue)
        {
            return typeValue.Trim().ToLowerInvariant() switch
            {
                "byte" => DataType.Byte,
                "int16" or "short" => DataType.Int16,
                "uint16" or "ushort" => DataType.UInt16,
                "int" or "int32" or "integer" => DataType.Int,
                "uint" or "uint32" => DataType.UInt,
                "float" or "single" => DataType.Float,
                "double" or "float64" => DataType.Double,
                "padding" or "pad" => DataType.Padding,
                _ => throw new InvalidDataException($"지원하지 않는 데이터 타입입니다: {typeValue}")
            };
        }

        private JsonElement GetRequiredObject(JsonElement parent, params string[] propertyNames)
        {
            if (!TryGetProperty(parent, out var element, propertyNames) || element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"'{propertyNames[0]}' 객체가 필요합니다.");
            }

            return element;
        }

        private string ConvertJsonValueToString(JsonElement parent, params string[] propertyNames)
        {
            if (!TryGetProperty(parent, out var valueElement, propertyNames))
            {
                return string.Empty;
            }

            return valueElement.ValueKind switch
            {
                JsonValueKind.String => valueElement.GetString() ?? string.Empty,
                JsonValueKind.Number => valueElement.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Null => string.Empty,
                _ => valueElement.GetRawText()
            };
        }

        private string? GetOptionalString(JsonElement parent, params string[] propertyNames)
        {
            if (!TryGetProperty(parent, out var valueElement, propertyNames))
            {
                return null;
            }

            return valueElement.ValueKind switch
            {
                JsonValueKind.String => valueElement.GetString(),
                JsonValueKind.Number => valueElement.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        private int? GetOptionalInt(JsonElement parent, params string[] propertyNames)
        {
            if (!TryGetProperty(parent, out var valueElement, propertyNames))
            {
                return null;
            }

            if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt32(out var numberValue))
            {
                return numberValue;
            }

            if (valueElement.ValueKind == JsonValueKind.String && int.TryParse(valueElement.GetString(), out var parsedValue))
            {
                return parsedValue;
            }

            return null;
        }

        private bool? GetOptionalBool(JsonElement parent, params string[] propertyNames)
        {
            if (!TryGetProperty(parent, out var valueElement, propertyNames))
            {
                return null;
            }

            if (valueElement.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (valueElement.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt32(out var numberValue))
            {
                return numberValue != 0;
            }

            if (valueElement.ValueKind == JsonValueKind.String)
            {
                var stringValue = valueElement.GetString();
                if (bool.TryParse(stringValue, out var boolValue))
                {
                    return boolValue;
                }

                if (int.TryParse(stringValue, out var intValue))
                {
                    return intValue != 0;
                }
            }

            return null;
        }

        private bool TryGetProperty(JsonElement parent, out JsonElement value, params string[] propertyNames)
        {
            foreach (var property in parent.EnumerateObject())
            {
                if (propertyNames.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}