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

            // Folders 테이블 생성
            var createFoldersTableCommand = connection.CreateCommand();
            createFoldersTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Folders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    ParentId INTEGER,
                    CreatedAt TEXT NOT NULL,
                    LastModified TEXT NOT NULL,
                    FOREIGN KEY (ParentId) REFERENCES Folders (Id)
                )";
            createFoldersTableCommand.ExecuteNonQuery();

            // SavedRequests 테이블 생성 (FolderId 추가)
            var createRequestsTableCommand = connection.CreateCommand();
            createRequestsTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS SavedRequests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    IpAddress TEXT NOT NULL,
                    Port INTEGER NOT NULL,
                    IsBigEndian INTEGER NOT NULL DEFAULT 1,
                    FolderId INTEGER,
                    RequestSchemaJson TEXT NOT NULL,
                    ResponseSchemaJson TEXT,
                    CreatedAt TEXT NOT NULL,
                    LastModified TEXT NOT NULL,
                    FOREIGN KEY (FolderId) REFERENCES Folders (Id)
                )";
            createRequestsTableCommand.ExecuteNonQuery();

            // 기존 테이블이 있는 경우 FolderId 컬럼 추가
            var checkColumnCommand = connection.CreateCommand();
            checkColumnCommand.CommandText = "PRAGMA table_info(SavedRequests)";
            var reader = checkColumnCommand.ExecuteReader();

            bool hasFolderId = false;
            bool hasIsBigEndian = false;
            bool hasUseCustomLocalPort = false;
            bool hasCustomLocalPort = false;
            while (reader.Read())
            {
                var columnName = reader["name"].ToString();
                if (columnName == "FolderId")
                {
                    hasFolderId = true;
                }
                if (columnName == "IsBigEndian")
                {
                    hasIsBigEndian = true;
                }
                if (columnName == "UseCustomLocalPort")
                {
                    hasUseCustomLocalPort = true;
                }
                if (columnName == "CustomLocalPort")
                {
                    hasCustomLocalPort = true;
                }
            }
            reader.Close();

            if (!hasFolderId)
            {
                var alterTableCommand = connection.CreateCommand();
                alterTableCommand.CommandText = "ALTER TABLE SavedRequests ADD COLUMN FolderId INTEGER";
                alterTableCommand.ExecuteNonQuery();
            }

            if (!hasIsBigEndian)
            {
                var alterTableCommand = connection.CreateCommand();
                alterTableCommand.CommandText = "ALTER TABLE SavedRequests ADD COLUMN IsBigEndian INTEGER NOT NULL DEFAULT 1";
                alterTableCommand.ExecuteNonQuery();
            }

            if (!hasUseCustomLocalPort)
            {
                var alterTableCommand = connection.CreateCommand();
                alterTableCommand.CommandText = "ALTER TABLE SavedRequests ADD COLUMN UseCustomLocalPort INTEGER NOT NULL DEFAULT 0";
                alterTableCommand.ExecuteNonQuery();
            }

            if (!hasCustomLocalPort)
            {
                var alterTableCommand = connection.CreateCommand();
                alterTableCommand.CommandText = "ALTER TABLE SavedRequests ADD COLUMN CustomLocalPort INTEGER NOT NULL DEFAULT 0";
                alterTableCommand.ExecuteNonQuery();
            }
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
                    INSERT INTO SavedRequests (Name, IpAddress, Port, IsBigEndian, UseCustomLocalPort, CustomLocalPort, FolderId, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified)
                    VALUES (@name, @ipAddress, @port, @isBigEndian, @useCustomLocalPort, @customLocalPort, @folderId, @requestSchemaJson, @responseSchemaJson, @createdAt, @lastModified);
                    SELECT last_insert_rowid();";
            }
            else // Update
            {
                command.CommandText = @"
                    UPDATE SavedRequests 
                    SET Name = @name, IpAddress = @ipAddress, Port = @port, IsBigEndian = @isBigEndian, UseCustomLocalPort = @useCustomLocalPort, CustomLocalPort = @customLocalPort, FolderId = @folderId,
                        RequestSchemaJson = @requestSchemaJson, ResponseSchemaJson = @responseSchemaJson, 
                        LastModified = @lastModified
                    WHERE Id = @id;
                    SELECT @id;";
                command.Parameters.AddWithValue("@id", request.Id);
            }

            command.Parameters.AddWithValue("@name", request.Name);
            command.Parameters.AddWithValue("@ipAddress", request.IpAddress);
            command.Parameters.AddWithValue("@port", request.Port);
            command.Parameters.AddWithValue("@isBigEndian", request.IsBigEndian ? 1 : 0);
            command.Parameters.AddWithValue("@useCustomLocalPort", request.UseCustomLocalPort ? 1 : 0);
            command.Parameters.AddWithValue("@customLocalPort", request.CustomLocalPort);
            command.Parameters.AddWithValue("@folderId", request.FolderId.HasValue ? (object)request.FolderId.Value : DBNull.Value);
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
                SELECT Id, Name, IpAddress, Port, IsBigEndian, UseCustomLocalPort, CustomLocalPort, FolderId, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified
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
                    IsBigEndian = reader.IsDBNull(4) ? true : reader.GetInt32(4) == 1,
                    UseCustomLocalPort = reader.IsDBNull(5) ? false : reader.GetInt32(5) == 1,
                    CustomLocalPort = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    FolderId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    RequestSchemaJson = reader.GetString(8),
                    ResponseSchemaJson = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10)),
                    LastModified = DateTime.Parse(reader.GetString(11))
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
                SELECT Id, Name, IpAddress, Port, IsBigEndian, UseCustomLocalPort, CustomLocalPort, FolderId, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified
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
                    IsBigEndian = reader.IsDBNull(4) ? true : reader.GetInt32(4) == 1,
                    UseCustomLocalPort = reader.IsDBNull(5) ? false : reader.GetInt32(5) == 1,
                    CustomLocalPort = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    FolderId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    RequestSchemaJson = reader.GetString(8),
                    ResponseSchemaJson = reader.GetString(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10)),
                    LastModified = DateTime.Parse(reader.GetString(11))
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

        /// <summary>
        /// Request의 폴더를 변경 (드래그 앤 드롭용)
        /// </summary>
        public async Task<bool> MoveRequestToFolderAsync(int requestId, int? targetFolderId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE SavedRequests 
                SET FolderId = @folderId, LastModified = @lastModified
                WHERE Id = @id";
            command.Parameters.AddWithValue("@id", requestId);
            command.Parameters.AddWithValue("@folderId", targetFolderId.HasValue ? (object)targetFolderId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@lastModified", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

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

        // 폴더 관련 메서드들
        public async Task<int> SaveFolderAsync(Folder folder)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            folder.LastModified = DateTime.Now;

            var command = connection.CreateCommand();

            if (folder.Id == 0) // Insert
            {
                command.CommandText = @"
                    INSERT INTO Folders (Name, ParentId, CreatedAt, LastModified)
                    VALUES (@name, @parentId, @createdAt, @lastModified);
                    SELECT last_insert_rowid();";
            }
            else // Update
            {
                command.CommandText = @"
                    UPDATE Folders 
                    SET Name = @name, ParentId = @parentId, LastModified = @lastModified
                    WHERE Id = @id;
                    SELECT @id;";
                command.Parameters.AddWithValue("@id", folder.Id);
            }

            command.Parameters.AddWithValue("@name", folder.Name);
            command.Parameters.AddWithValue("@parentId", folder.ParentId.HasValue ? (object)folder.ParentId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@createdAt", folder.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@lastModified", folder.LastModified.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = await command.ExecuteScalarAsync();
            var savedId = Convert.ToInt32(result);
            folder.Id = savedId;

            return savedId;
        }

        public async Task<ObservableCollection<Folder>> GetAllFoldersAsync()
        {
            var folders = new ObservableCollection<Folder>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, ParentId, CreatedAt, LastModified
                FROM Folders
                ORDER BY Name ASC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var folder = new Folder
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ParentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    CreatedAt = DateTime.Parse(reader.GetString(3)),
                    LastModified = DateTime.Parse(reader.GetString(4))
                };
                folders.Add(folder);
            }

            return folders;
        }

        public async Task<bool> DeleteFolderAsync(int id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // 하위 폴더와 요청들도 함께 삭제
            var transaction = connection.BeginTransaction();

            try
            {
                // 먼저 이 폴더에 속한 모든 요청들을 삭제
                var deleteRequestsCommand = connection.CreateCommand();
                deleteRequestsCommand.CommandText = "UPDATE SavedRequests SET FolderId = NULL WHERE FolderId = @id";
                deleteRequestsCommand.Parameters.AddWithValue("@id", id);
                await deleteRequestsCommand.ExecuteNonQueryAsync();

                // 하위 폴더들의 ParentId를 NULL로 설정 (또는 재귀적으로 삭제할 수도 있음)
                var updateSubFoldersCommand = connection.CreateCommand();
                updateSubFoldersCommand.CommandText = "UPDATE Folders SET ParentId = NULL WHERE ParentId = @id";
                updateSubFoldersCommand.Parameters.AddWithValue("@id", id);
                await updateSubFoldersCommand.ExecuteNonQueryAsync();

                // 폴더 삭제
                var deleteFolderCommand = connection.CreateCommand();
                deleteFolderCommand.CommandText = "DELETE FROM Folders WHERE Id = @id";
                deleteFolderCommand.Parameters.AddWithValue("@id", id);
                var rowsAffected = await deleteFolderCommand.ExecuteNonQueryAsync();

                transaction.Commit();
                return rowsAffected > 0;
            }
            catch
            {
                transaction.Rollback();
                return false;
            }
        }

        public async Task<ObservableCollection<SavedRequest>> GetRequestsByFolderIdAsync(int? folderId)
        {
            var requests = new ObservableCollection<SavedRequest>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            if (folderId.HasValue)
            {
                command.CommandText = @"
                    SELECT Id, Name, IpAddress, Port, FolderId, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified
                    FROM SavedRequests
                    WHERE FolderId = @folderId
                    ORDER BY Name ASC";
                command.Parameters.AddWithValue("@folderId", folderId.Value);
            }
            else
            {
                command.CommandText = @"
                    SELECT Id, Name, IpAddress, Port, FolderId, RequestSchemaJson, ResponseSchemaJson, CreatedAt, LastModified
                    FROM SavedRequests
                    WHERE FolderId IS NULL
                    ORDER BY Name ASC";
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var request = new SavedRequest
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    IpAddress = reader.GetString(2),
                    Port = reader.GetInt32(3),
                    FolderId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    RequestSchemaJson = reader.GetString(5),
                    ResponseSchemaJson = reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7)),
                    LastModified = DateTime.Parse(reader.GetString(8))
                };
                requests.Add(request);
            }

            return requests;
        }
    }
}