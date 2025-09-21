using System.Data.SQLite;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.IO;
using System.Data;
using MMG.Models;

namespace MMG.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;

        public DatabaseService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MMG");
            Directory.CreateDirectory(appDataPath);
            _databasePath = Path.Combine(appDataPath, "MMG.db");
            _connectionString = $"Data Source={_databasePath}";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS SavedRequests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    IpAddress TEXT NOT NULL,
                    Port INTEGER NOT NULL,
                    RequestSchemaJson TEXT NOT NULL,
                    ResponseSchemaJson TEXT,
                    CreatedAt TEXT NOT NULL,
                    LastModified TEXT NOT NULL
                )";
            createTableCommand.ExecuteNonQuery();
        }

        public async Task<int> SaveRequestAsync(SavedRequest request)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            request.LastModified = DateTime.Now;

            var command = connection.CreateCommand();

            if (request.Id == 0) // Insert
            {
                command.CommandText = @"
                    INSERT INTO SavedRequests (Name, IpAddress, Port, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified)
                    VALUES (@name, @ipAddress, @port, @requestSchemaJson, @responseSchemaJson, @createdAt, @lastModified);
                    SELECT last_insert_rowid();";
            }
            else // Update
            {
                command.CommandText = @"
                    UPDATE SavedRequests 
                    SET Name = @name, IpAddress = @ipAddress, Port = @port, 
                        RequestSchemaJson = @requestSchemaJson, ResponseSchemaJson = @responseSchemaJson, 
                        LastModified = @lastModified
                    WHERE Id = @id;
                    SELECT @id;";
                command.Parameters.AddWithValue("@id", request.Id);
            }

            command.Parameters.AddWithValue("@name", request.Name);
            command.Parameters.AddWithValue("@ipAddress", request.IpAddress);
            command.Parameters.AddWithValue("@port", request.Port);
            command.Parameters.AddWithValue("@requestSchemaJson", request.RequestSchemaJson);
            command.Parameters.AddWithValue("@responseSchemaJson", request.ResponseSchemaJson ?? "");
            command.Parameters.AddWithValue("@createdAt", request.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@lastModified", request.LastModified.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = await command.ExecuteScalarAsync();
            var savedId = Convert.ToInt32(result);
            request.Id = savedId;

            return savedId;
        }

        public async Task<ObservableCollection<SavedRequest>> GetAllRequestsAsync()
        {
            var requests = new ObservableCollection<SavedRequest>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, IpAddress, Port, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified
                FROM SavedRequests
                ORDER BY LastModified DESC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var request = new SavedRequest
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    IpAddress = reader.GetString(2),
                    Port = reader.GetInt32(3),
                    RequestSchemaJson = reader.GetString(4),
                    ResponseSchemaJson = reader.GetString(5),
                    CreatedAt = DateTime.Parse(reader.GetString(6)),
                    LastModified = DateTime.Parse(reader.GetString(7))
                };
                requests.Add(request);
            }

            return requests;
        }

        public async Task<SavedRequest?> GetRequestByIdAsync(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, IpAddress, Port, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified
                FROM SavedRequests
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new SavedRequest
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    IpAddress = reader.GetString(2),
                    Port = reader.GetInt32(3),
                    RequestSchemaJson = reader.GetString(4),
                    ResponseSchemaJson = reader.GetString(5),
                    CreatedAt = DateTime.Parse(reader.GetString(6)),
                    LastModified = DateTime.Parse(reader.GetString(7))
                };
            }

            return null;
        }

        public async Task<bool> DeleteRequestAsync(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM SavedRequests WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        public string SerializeDataFields(ObservableCollection<DataField> dataFields)
        {
            var fieldData = dataFields.Select(field => new
            {
                Name = field.Name,
                Type = field.Type.ToString(),
                Value = field.Value,
                PaddingSize = field.PaddingSize
            }).ToList();

            return JsonSerializer.Serialize(fieldData);
        }

        public ObservableCollection<DataField> DeserializeDataFields(string json)
        {
            var collection = new ObservableCollection<DataField>();

            if (string.IsNullOrWhiteSpace(json))
                return collection;

            try
            {
                using var document = JsonDocument.Parse(json);

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var dataField = new DataField();

                    if (element.TryGetProperty("Name", out var nameElement))
                        dataField.Name = nameElement.GetString() ?? "";

                    if (element.TryGetProperty("Type", out var typeElement))
                    {
                        if (Enum.TryParse<DataType>(typeElement.GetString(), out var dataType))
                            dataField.Type = dataType;
                    }

                    if (element.TryGetProperty("Value", out var valueElement))
                        dataField.Value = valueElement.GetString() ?? "";

                    if (element.TryGetProperty("PaddingSize", out var paddingSizeElement))
                        dataField.PaddingSize = paddingSizeElement.GetInt32();

                    collection.Add(dataField);
                }
            }
            catch (JsonException)
            {
                // JSON 파싱 실패 시 빈 컬렉션 반환
            }

            return collection;
        }
    }
}