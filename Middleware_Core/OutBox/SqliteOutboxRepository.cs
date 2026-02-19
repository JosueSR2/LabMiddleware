using Microsoft.Data.Sqlite;

namespace Middleware_Core.Outbox
{
    public class SqliteOutboxRepository : IOutboxRepository
    {
        private readonly string _connectionString;

        public SqliteOutboxRepository(string dbPath)
        {
            var fullPath = Path.GetFullPath(dbPath);
            var folder = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _connectionString = $"Data Source={fullPath}";
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var createTable = connection.CreateCommand();
            createTable.CommandText = @"
CREATE TABLE IF NOT EXISTS Outbox (
    Id TEXT PRIMARY KEY,
    Fingerprint TEXT NOT NULL UNIQUE,
    PayloadJson TEXT NOT NULL,
    Status TEXT NOT NULL,
    RetryCount INTEGER NOT NULL,
    NextAttemptUtc TEXT NOT NULL,
    LastError TEXT NULL,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Outbox_Status_NextAttemptUtc ON Outbox(Status, NextAttemptUtc);
";
            await createTable.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<bool> TryAddAsync(OutboxRecord record, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Outbox (Id, Fingerprint, PayloadJson, Status, RetryCount, NextAttemptUtc, LastError, CreatedUtc, UpdatedUtc)
VALUES ($id, $fingerprint, $payload, $status, $retry, $nextAttempt, $lastError, $created, $updated);
";
            cmd.Parameters.AddWithValue("$id", record.Id);
            cmd.Parameters.AddWithValue("$fingerprint", record.Fingerprint);
            cmd.Parameters.AddWithValue("$payload", record.PayloadJson);
            cmd.Parameters.AddWithValue("$status", record.Status);
            cmd.Parameters.AddWithValue("$retry", record.RetryCount);
            cmd.Parameters.AddWithValue("$nextAttempt", record.NextAttemptUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$lastError", (object?)record.LastError ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$created", record.CreatedUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$updated", record.UpdatedUtc.ToString("O"));

            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return false;
            }
        }

        public Task<List<OutboxRecord>> GetDuePendingAsync(int limit, DateTime nowUtc, CancellationToken cancellationToken = default)
            => QueryAsync("WHERE Status = 'Pending' AND NextAttemptUtc <= $p1", "$p1", nowUtc.ToString("O"), limit, cancellationToken);

        public Task<List<OutboxRecord>> GetByStatusAsync(string status, int limit, CancellationToken cancellationToken = default)
            => QueryAsync("WHERE Status = $p1", "$p1", status, limit, cancellationToken);

        public async Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Status, COUNT(*) FROM Outbox GROUP BY Status;";

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result[reader.GetString(0)] = reader.GetInt32(1);

            return result;
        }

        public async Task<int> RequeueAsync(string id, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
UPDATE Outbox
SET Status = 'Pending', RetryCount = 0, NextAttemptUtc = $nextAttempt, LastError = NULL, UpdatedUtc = $updatedUtc
WHERE Id = $id;
";
            cmd.Parameters.AddWithValue("$nextAttempt", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$id", id);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<int> RequeueRangeAsync(DateTime fromUtc, DateTime toUtc, bool includeSent, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            var filter = includeSent ? "Status IN ('Failed','Sent')" : "Status = 'Failed'";
            cmd.CommandText = $@"
UPDATE Outbox
SET Status = 'Pending', RetryCount = 0, NextAttemptUtc = $nextAttempt, LastError = NULL, UpdatedUtc = $updatedUtc
WHERE CreatedUtc >= $fromUtc AND CreatedUtc <= $toUtc AND {filter};
";
            cmd.Parameters.AddWithValue("$nextAttempt", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$fromUtc", fromUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$toUtc", toUtc.ToString("O"));
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public Task MarkSentAsync(string id, CancellationToken cancellationToken = default)
            => UpdateStatusAsync(id, "Sent", null, null, null, cancellationToken);

        public Task MarkRetryAsync(string id, int retryCount, DateTime nextAttemptUtc, string? error, CancellationToken cancellationToken = default)
            => UpdateStatusAsync(id, "Pending", retryCount, nextAttemptUtc, error, cancellationToken);

        public Task MarkFailedAsync(string id, string? error, CancellationToken cancellationToken = default)
            => UpdateStatusAsync(id, "Failed", null, null, error, cancellationToken);

        private async Task<List<OutboxRecord>> QueryAsync(string whereClause, string p1Name, object p1Value, int limit, CancellationToken cancellationToken)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
SELECT Id, Fingerprint, PayloadJson, Status, RetryCount, NextAttemptUtc, LastError, CreatedUtc, UpdatedUtc
FROM Outbox
{whereClause}
ORDER BY CreatedUtc
LIMIT $limit;
";
            cmd.Parameters.AddWithValue(p1Name, p1Value);
            cmd.Parameters.AddWithValue("$limit", limit);

            var records = new List<OutboxRecord>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                records.Add(ReadRecord(reader));

            return records;
        }

        private static OutboxRecord ReadRecord(SqliteDataReader reader)
        {
            return new OutboxRecord
            {
                Id = reader.GetString(0),
                Fingerprint = reader.GetString(1),
                PayloadJson = reader.GetString(2),
                Status = reader.GetString(3),
                RetryCount = reader.GetInt32(4),
                NextAttemptUtc = DateTime.Parse(reader.GetString(5)).ToUniversalTime(),
                LastError = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedUtc = DateTime.Parse(reader.GetString(7)).ToUniversalTime(),
                UpdatedUtc = DateTime.Parse(reader.GetString(8)).ToUniversalTime()
            };
        }

        private async Task UpdateStatusAsync(string id, string status, int? retryCount, DateTime? nextAttemptUtc, string? error, CancellationToken cancellationToken)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
UPDATE Outbox
SET Status = $status,
    RetryCount = COALESCE($retryCount, RetryCount),
    NextAttemptUtc = COALESCE($nextAttempt, NextAttemptUtc),
    LastError = $lastError,
    UpdatedUtc = $updatedUtc
WHERE Id = $id;
";
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$retryCount", (object?)retryCount ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$nextAttempt", nextAttemptUtc.HasValue ? nextAttemptUtc.Value.ToString("O") : DBNull.Value);
            cmd.Parameters.AddWithValue("$lastError", (object?)error ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$id", id);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
