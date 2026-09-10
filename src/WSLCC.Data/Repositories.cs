using Microsoft.Data.Sqlite;

namespace WSLCC.Data;

public sealed class SettingsRepository
{
    private readonly WslcDatabase _db;

    public SettingsRepository(WslcDatabase db) => _db = db;

    public async Task<string?> GetAsync(string key)
        => await _db.ExecuteReadAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = $k;";
            cmd.Parameters.AddWithValue("$k", key);
            return await cmd.ExecuteScalarAsync() as string;
        });

    public async Task<Dictionary<string, string>> GetAllAsync()
        => await _db.ExecuteReadAsync(async conn =>
        {
            var result = new Dictionary<string, string>();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT key, value FROM app_settings;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result[reader.GetString(0)] = reader.GetString(1);
            return result;
        });

    public async Task SetAsync(string key, string value)
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO app_settings (key, value, updated_at) VALUES ($k, $v, $t)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;
                """;
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.Now.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
            return 0;
        });
}

public sealed class AuditLogRepository
{
    private readonly WslcDatabase _db;

    public AuditLogRepository(WslcDatabase db) => _db = db;

    public async Task AddAsync(string category, string action, string? detail, bool success, string? message, long? durationMs)
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO audit_log (ts, category, action, detail, result, message, duration_ms)
                VALUES ($t, $c, $a, $d, $r, $m, $ms);
                """;
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.Now.ToString("O"));
            cmd.Parameters.AddWithValue("$c", category);
            cmd.Parameters.AddWithValue("$a", action);
            cmd.Parameters.AddWithValue("$d", (object?)detail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$r", success ? "success" : "failure");
            cmd.Parameters.AddWithValue("$m", (object?)message ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ms", (object?)durationMs ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return 0;
        });

    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(int limit = 200)
        => await _db.ExecuteReadAsync(async conn =>
        {
            var list = new List<AuditLogEntry>();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, ts, category, action, detail, result, message, duration_ms
                FROM audit_log ORDER BY id DESC LIMIT $l;
                """;
            cmd.Parameters.AddWithValue("$l", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AuditLogEntry(
                    reader.GetInt64(0),
                    ParseTime(reader.GetString(1)),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetInt64(7)));
            }
            return list;
        });

    public async Task<long> CountAsync()
        => await _db.ExecuteReadAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM audit_log;";
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        });

    public async Task ClearAsync()
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM audit_log;";
            await cmd.ExecuteNonQueryAsync();
            return 0;
        });

    private static DateTimeOffset ParseTime(string iso)
        => DateTimeOffset.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)
            ? t : DateTimeOffset.MinValue;
}

public sealed class PullHistoryRepository
{
    private readonly WslcDatabase _db;

    public PullHistoryRepository(WslcDatabase db) => _db = db;

    public async Task AddAsync(string imageRef, bool success, string? error, DateTimeOffset startedAt, DateTimeOffset? finishedAt)
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO pull_history (image_ref, status, error, started_at, finished_at)
                VALUES ($r, $s, $e, $st, $ft);
                """;
            cmd.Parameters.AddWithValue("$r", imageRef);
            cmd.Parameters.AddWithValue("$s", success ? "success" : "failure");
            cmd.Parameters.AddWithValue("$e", (object?)error ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$st", startedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$ft", finishedAt?.ToString("O") ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return 0;
        });

    public async Task<IReadOnlyList<PullHistoryEntry>> QueryAsync(int limit = 100)
        => await _db.ExecuteReadAsync(async conn =>
        {
            var list = new List<PullHistoryEntry>();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, image_ref, status, error, started_at, finished_at
                FROM pull_history ORDER BY id DESC LIMIT $l;
                """;
            cmd.Parameters.AddWithValue("$l", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PullHistoryEntry(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4)),
                    reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5))));
            }
            return list;
        });

    public async Task<long> CountAsync()
        => await _db.ExecuteReadAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pull_history;";
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        });
}

public sealed class ContainerSnapshotRepository
{
    private readonly WslcDatabase _db;

    public ContainerSnapshotRepository(WslcDatabase db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<ContainerSnapshotEntry> entries)
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
            foreach (var entry in entries)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO container_snapshot (name, image, state, status, ports, observed_at)
                    VALUES ($n, $i, $s, $st, $p, $t);
                    """;
                cmd.Parameters.AddWithValue("$n", entry.Name);
                cmd.Parameters.AddWithValue("$i", (object?)entry.Image ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$s", (object?)entry.State ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$st", (object?)entry.Status ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$p", (object?)entry.Ports ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$t", entry.ObservedAt.ToString("O"));
                await cmd.ExecuteNonQueryAsync();
            }

            await using var cleanup = conn.CreateCommand();
            cleanup.Transaction = tx;
            cleanup.CommandText = "DELETE FROM container_snapshot WHERE observed_at < $cutoff;";
            cleanup.Parameters.AddWithValue("$cutoff", DateTimeOffset.Now.AddDays(-30).ToString("O"));
            await cleanup.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return 0;
        });

    public async Task<IReadOnlyList<ContainerSnapshotEntry>> QueryLatestAsync(int limit = 100)
        => await _db.ExecuteReadAsync(async conn =>
        {
            var list = new List<ContainerSnapshotEntry>();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT name, image, state, status, ports, observed_at
                FROM container_snapshot ORDER BY id DESC LIMIT $l;
                """;
            cmd.Parameters.AddWithValue("$l", limit);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ContainerSnapshotEntry(
                    reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    DateTimeOffset.Parse(reader.GetString(5))));
            }
            return list;
        });

    public async Task<long> CountAsync()
        => await _db.ExecuteReadAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM container_snapshot;";
            return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        });
}

public sealed record ComposeDeploymentEntry(long Id, string ProjectName, string ServiceName, string ContainerName, string? ComposeFilePath, DateTimeOffset DeployedAt);

public sealed class ComposeDeploymentRepository
{
    private readonly WslcDatabase _db;

    public ComposeDeploymentRepository(WslcDatabase db) => _db = db;

    public async Task AddAsync(string projectName, string serviceName, string containerName, string? composeFilePath)
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();

            await using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = "DELETE FROM compose_deployment WHERE container_name = $c;";
            del.Parameters.AddWithValue("$c", containerName);
            await del.ExecuteNonQueryAsync();

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO compose_deployment (project_name, service_name, container_name, compose_file_path, deployed_at)
                VALUES ($p, $s, $c, $f, $t);
                """;
            cmd.Parameters.AddWithValue("$p", projectName);
            cmd.Parameters.AddWithValue("$s", serviceName);
            cmd.Parameters.AddWithValue("$c", containerName);
            cmd.Parameters.AddWithValue("$f", (object?)composeFilePath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$t", DateTimeOffset.Now.ToString("O"));
            await cmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
            return 0;
        });

    public async Task<IReadOnlyList<ComposeDeploymentEntry>> QueryAsync()
        => await _db.ExecuteReadAsync(async conn =>
        {
            var list = new List<ComposeDeploymentEntry>();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, project_name, service_name, container_name, compose_file_path, deployed_at FROM compose_deployment ORDER BY project_name, id;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ComposeDeploymentEntry(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    DateTimeOffset.Parse(reader.GetString(5))));
            }
            return list;
        });

    public async Task ClearProjectAsync(string projectName)
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM compose_deployment WHERE project_name = $p;";
            cmd.Parameters.AddWithValue("$p", projectName);
            await cmd.ExecuteNonQueryAsync();
            return 0;
        });

    public async Task UpdateComposeFilePathAsync(string projectName, string composeFilePath)
        => await _db.ExecuteWriteAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE compose_deployment SET compose_file_path = $f WHERE project_name = $p;";
            cmd.Parameters.AddWithValue("$p", projectName);
            cmd.Parameters.AddWithValue("$f", composeFilePath);
            await cmd.ExecuteNonQueryAsync();
            return 0;
        });

    public async Task<IReadOnlyList<string>> GetProjectNamesAsync()
        => await _db.ExecuteReadAsync(async conn =>
        {
            var list = new List<string>();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT project_name FROM compose_deployment ORDER BY project_name;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                list.Add(reader.GetString(0));
            return list;
        });
}

