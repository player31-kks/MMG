using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using System.IO;
using MMG.Models;

namespace MMG.Services
{
    public class TestDatabaseService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;

        public TestDatabaseService()
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

            // Create TestScenarios table
            var createScenariosTable = @"
                CREATE TABLE IF NOT EXISTS TestScenarios (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    CreatedAt TEXT NOT NULL,
                    LastRunAt TEXT,
                    IsEnabled INTEGER NOT NULL DEFAULT 1
                )";

            // Create TestSteps table
            var createStepsTable = @"
                CREATE TABLE IF NOT EXISTS TestSteps (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ScenarioId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    StepType TEXT NOT NULL,
                    SavedRequestId INTEGER,
                    DelaySeconds REAL DEFAULT 0,
                    FrequencyHz REAL DEFAULT 1.0,
                    DurationSeconds INTEGER DEFAULT 10,
                    PreDelayMs INTEGER DEFAULT 0,
                    PostDelayMs INTEGER DEFAULT 0,
                    IntervalMs INTEGER DEFAULT 100,
                    RepeatCount INTEGER DEFAULT 1,
                    ExpectedResponse TEXT,
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    [Order] INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (ScenarioId) REFERENCES TestScenarios (Id) ON DELETE CASCADE
                )";

            // Create TestResults table
            var createResultsTable = @"
                CREATE TABLE IF NOT EXISTS TestResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ScenarioId INTEGER NOT NULL,
                    StepId INTEGER NOT NULL,
                    ExecutedAt TEXT NOT NULL,
                    IsSuccess INTEGER NOT NULL,
                    RequestSent TEXT,
                    ResponseReceived TEXT,
                    ErrorMessage TEXT,
                    ExecutionTimeMs REAL,
                    FOREIGN KEY (ScenarioId) REFERENCES TestScenarios (Id) ON DELETE CASCADE,
                    FOREIGN KEY (StepId) REFERENCES TestSteps (Id) ON DELETE CASCADE
                )";

            using var command = new SQLiteCommand(createScenariosTable, connection);
            command.ExecuteNonQuery();

            command.CommandText = createStepsTable;
            command.ExecuteNonQuery();

            command.CommandText = createResultsTable;
            command.ExecuteNonQuery();

            // 새로운 컬럼 추가 (기존 데이터베이스 마이그레이션)
            AddColumnIfNotExists(connection, "TestSteps", "PreDelayMs", "INTEGER DEFAULT 0");
            AddColumnIfNotExists(connection, "TestSteps", "PostDelayMs", "INTEGER DEFAULT 0");
            AddColumnIfNotExists(connection, "TestSteps", "IntervalMs", "INTEGER DEFAULT 100");
            AddColumnIfNotExists(connection, "TestSteps", "RepeatCount", "INTEGER DEFAULT 1");
        }

        private void AddColumnIfNotExists(SQLiteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            try
            {
                var checkQuery = $"SELECT {columnName} FROM {tableName} LIMIT 1";
                using var checkCommand = new SQLiteCommand(checkQuery, connection);
                checkCommand.ExecuteNonQuery();
            }
            catch
            {
                // Column doesn't exist, add it
                var alterQuery = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
                using var alterCommand = new SQLiteCommand(alterQuery, connection);
                alterCommand.ExecuteNonQuery();
            }
        }

        // TestScenario methods
        public async Task<List<TestScenario>> GetAllScenariosAsync()
        {
            var scenarios = new List<TestScenario>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM TestScenarios ORDER BY CreatedAt DESC";
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var scenario = new TestScenario
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Name = reader["Name"].ToString() ?? string.Empty,
                    Description = reader["Description"] == DBNull.Value ? string.Empty : reader["Description"].ToString() ?? string.Empty,
                    CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString() ?? DateTime.Now.ToString()),
                    LastRunAt = reader["LastRunAt"] == DBNull.Value ? null : DateTime.Parse(reader["LastRunAt"].ToString() ?? DateTime.Now.ToString()),
                    IsEnabled = Convert.ToInt32(reader["IsEnabled"]) == 1
                };

                // Load steps for this scenario
                var steps = await GetStepsForScenarioAsync(scenario.Id);
                foreach (var step in steps)
                {
                    scenario.Steps.Add(step);
                }

                scenarios.Add(scenario);
            }

            return scenarios;
        }

        public async Task<int> CreateScenarioAsync(TestScenario scenario)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                INSERT INTO TestScenarios (Name, Description, CreatedAt, IsEnabled)
                VALUES (@Name, @Description, @CreatedAt, @IsEnabled);
                SELECT last_insert_rowid();";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@Name", scenario.Name);
            command.Parameters.AddWithValue("@Description", scenario.Description);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@IsEnabled", scenario.IsEnabled ? 1 : 0);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task DeleteScenarioAsync(int scenarioId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM TestScenarios WHERE Id = @Id";
            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", scenarioId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateScenarioAsync(TestScenario scenario)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"UPDATE TestScenarios 
                         SET Name = @Name, 
                             Description = @Description,
                             IsEnabled = @IsEnabled
                         WHERE Id = @Id";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", scenario.Id);
            command.Parameters.AddWithValue("@Name", scenario.Name);
            command.Parameters.AddWithValue("@Description", scenario.Description);
            command.Parameters.AddWithValue("@IsEnabled", scenario.IsEnabled ? 1 : 0);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<TestScenario?> GetScenarioByIdAsync(int scenarioId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM TestScenarios WHERE Id = @Id";
            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", scenarioId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new TestScenario
                {
                    Id = reader.GetInt32(0), // Id
                    Name = reader.GetString(1), // Name
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2), // Description
                    CreatedAt = DateTime.Parse(reader.GetString(3)), // CreatedAt
                    LastRunAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)), // LastRunAt
                    IsEnabled = reader.GetInt32(5) == 1 // IsEnabled
                };
            }

            return null;
        }

        // TestStep methods
        public async Task<List<TestStep>> GetStepsForScenarioAsync(int scenarioId)
        {
            var steps = new List<TestStep>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT * FROM TestSteps WHERE ScenarioId = @ScenarioId ORDER BY [Order]";
            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@ScenarioId", scenarioId);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var step = new TestStep
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    ScenarioId = Convert.ToInt32(reader["ScenarioId"]),
                    Name = reader["Name"].ToString() ?? string.Empty,
                    StepType = reader["StepType"].ToString() ?? "Immediate",
                    SavedRequestId = reader["SavedRequestId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SavedRequestId"]),
                    DelaySeconds = Convert.ToDouble(reader["DelaySeconds"]),
                    FrequencyHz = Convert.ToDouble(reader["FrequencyHz"]),
                    DurationSeconds = Convert.ToInt32(reader["DurationSeconds"]),
                    PreDelayMs = GetIntOrDefault(reader, "PreDelayMs", 0),
                    PostDelayMs = GetIntOrDefault(reader, "PostDelayMs", 0),
                    IntervalMs = GetIntOrDefault(reader, "IntervalMs", 100),
                    RepeatCount = GetIntOrDefault(reader, "RepeatCount", 1),
                    ExpectedResponse = reader["ExpectedResponse"] == DBNull.Value ? string.Empty : reader["ExpectedResponse"].ToString() ?? string.Empty,
                    IsEnabled = Convert.ToInt32(reader["IsEnabled"]) == 1,
                    Order = Convert.ToInt32(reader["Order"])
                };

                steps.Add(step);
            }

            return steps;
        }

        private int GetIntOrDefault(System.Data.Common.DbDataReader reader, string columnName, int defaultValue)
        {
            try
            {
                var ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : Convert.ToInt32(reader[columnName]);
            }
            catch
            {
                return defaultValue;
            }
        }

        public async Task<int> CreateStepAsync(TestStep step)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                INSERT INTO TestSteps (ScenarioId, Name, StepType, SavedRequestId, DelaySeconds, FrequencyHz, DurationSeconds, PreDelayMs, PostDelayMs, IntervalMs, RepeatCount, ExpectedResponse, IsEnabled, [Order])
                VALUES (@ScenarioId, @Name, @StepType, @SavedRequestId, @DelaySeconds, @FrequencyHz, @DurationSeconds, @PreDelayMs, @PostDelayMs, @IntervalMs, @RepeatCount, @ExpectedResponse, @IsEnabled, @Order);
                SELECT last_insert_rowid();";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@ScenarioId", step.ScenarioId);
            command.Parameters.AddWithValue("@Name", step.Name);
            command.Parameters.AddWithValue("@StepType", step.StepType);
            command.Parameters.AddWithValue("@SavedRequestId", step.SavedRequestId > 0 ? step.SavedRequestId : (object)DBNull.Value);
            command.Parameters.AddWithValue("@DelaySeconds", step.DelaySeconds);
            command.Parameters.AddWithValue("@FrequencyHz", step.FrequencyHz);
            command.Parameters.AddWithValue("@DurationSeconds", step.DurationSeconds);
            command.Parameters.AddWithValue("@PreDelayMs", step.PreDelayMs);
            command.Parameters.AddWithValue("@PostDelayMs", step.PostDelayMs);
            command.Parameters.AddWithValue("@IntervalMs", step.IntervalMs);
            command.Parameters.AddWithValue("@RepeatCount", step.RepeatCount);
            command.Parameters.AddWithValue("@ExpectedResponse", step.ExpectedResponse);
            command.Parameters.AddWithValue("@IsEnabled", step.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@Order", step.Order);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateStepAsync(TestStep step)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                UPDATE TestSteps 
                SET Name = @Name, StepType = @StepType, SavedRequestId = @SavedRequestId, 
                    DelaySeconds = @DelaySeconds, FrequencyHz = @FrequencyHz, DurationSeconds = @DurationSeconds,
                    PreDelayMs = @PreDelayMs, PostDelayMs = @PostDelayMs, IntervalMs = @IntervalMs, RepeatCount = @RepeatCount,
                    ExpectedResponse = @ExpectedResponse, IsEnabled = @IsEnabled, [Order] = @Order
                WHERE Id = @Id";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", step.Id);
            command.Parameters.AddWithValue("@Name", step.Name);
            command.Parameters.AddWithValue("@StepType", step.StepType);
            command.Parameters.AddWithValue("@SavedRequestId", step.SavedRequestId > 0 ? step.SavedRequestId : (object)DBNull.Value);
            command.Parameters.AddWithValue("@DelaySeconds", step.DelaySeconds);
            command.Parameters.AddWithValue("@FrequencyHz", step.FrequencyHz);
            command.Parameters.AddWithValue("@DurationSeconds", step.DurationSeconds);
            command.Parameters.AddWithValue("@PreDelayMs", step.PreDelayMs);
            command.Parameters.AddWithValue("@PostDelayMs", step.PostDelayMs);
            command.Parameters.AddWithValue("@IntervalMs", step.IntervalMs);
            command.Parameters.AddWithValue("@RepeatCount", step.RepeatCount);
            command.Parameters.AddWithValue("@ExpectedResponse", step.ExpectedResponse);
            command.Parameters.AddWithValue("@IsEnabled", step.IsEnabled ? 1 : 0);
            command.Parameters.AddWithValue("@Order", step.Order);

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteStepAsync(int stepId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM TestSteps WHERE Id = @Id";
            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@Id", stepId);

            await command.ExecuteNonQueryAsync();
        }

        // TestResult methods
        public async Task SaveTestResultAsync(TestResult result)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                INSERT INTO TestResults (ScenarioId, StepId, ExecutedAt, IsSuccess, RequestSent, ResponseReceived, ErrorMessage, ExecutionTimeMs)
                VALUES (@ScenarioId, @StepId, @ExecutedAt, @IsSuccess, @RequestSent, @ResponseReceived, @ErrorMessage, @ExecutionTimeMs)";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@ScenarioId", result.ScenarioId);
            command.Parameters.AddWithValue("@StepId", result.StepId);
            command.Parameters.AddWithValue("@ExecutedAt", result.ExecutedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@IsSuccess", result.IsSuccess ? 1 : 0);
            command.Parameters.AddWithValue("@RequestSent", result.RequestSent);
            command.Parameters.AddWithValue("@ResponseReceived", result.ResponseReceived);
            command.Parameters.AddWithValue("@ErrorMessage", result.ErrorMessage);
            command.Parameters.AddWithValue("@ExecutionTimeMs", result.ExecutionTimeMs);

            await command.ExecuteNonQueryAsync();
        }
    }
}