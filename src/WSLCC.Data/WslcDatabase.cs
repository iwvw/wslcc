using Microsoft.Data.Sqlite;

namespace WSLCC.Data;

public sealed class WslcDatabase
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public WslcDatabase(string databasePath)
    {
        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public static WslcDatabase OpenDefault()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WSLCC", "wslcc.db");
        return new WslcDatabase(path);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        await ExecuteWriteAsync(async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS audit_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ts TEXT NOT NULL,
                    category TEXT NOT NULL,
                    action TEXT NOT NULL,
                    detail TEXT,
                    result TEXT NOT NULL,
                    message TEXT,
                    duration_ms INTEGER
                );
                CREATE TABLE IF NOT EXISTS pull_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    image_ref TEXT NOT NULL,
                    status TEXT NOT NULL,
                    error TEXT,
                    started_at TEXT NOT NULL,
                    finished_at TEXT
                );
                CREATE TABLE IF NOT EXISTS container_snapshot (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    image TEXT,
                    state TEXT,
                    status TEXT,
                    ports TEXT,
                    observed_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_snapshot_name_time ON container_snapshot(name, observed_at);
                CREATE TABLE IF NOT EXISTS session_state (
                    name TEXT PRIMARY KEY,
                    storage_path TEXT NOT NULL,
                    cpu_count INTEGER,
                    memory_mb INTEGER,
                    created_at TEXT,
                    last_started_at TEXT,
                    last_terminated_at TEXT
                );
                CREATE TABLE IF NOT EXISTS compose_deployment (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_name TEXT NOT NULL,
                    service_name TEXT NOT NULL,
                    container_name TEXT NOT NULL,
                    compose_file_path TEXT,
                    deployed_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_compose_project ON compose_deployment(project_name);
                """;
            await cmd.ExecuteNonQueryAsync();
            await AddComposeFilePathColumnIfMissingAsync(conn);
            return 0;
        });
    }

    private static async Task AddComposeFilePathColumnIfMissingAsync(SqliteConnection conn)
    {
        await using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(compose_deployment);";
        await using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), "compose_file_path", StringComparison.OrdinalIgnoreCase))
                return;
        }
        await using var alter = conn.CreateCommand();
        alter.CommandText = "ALTER TABLE compose_deployment ADD COLUMN compose_file_path TEXT;";
        await alter.ExecuteNonQueryAsync();
    }

    public async Task<SqliteConnection> OpenAsync()
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        await pragma.ExecuteNonQueryAsync();
        return conn;
    }

    public async Task<T> ExecuteWriteAsync<T>(Func<SqliteConnection, Task<T>> action)
    {
        await _writeLock.WaitAsync();
        try
        {
            await using var conn = await OpenAsync();
            return await action(conn);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<T> ExecuteReadAsync<T>(Func<SqliteConnection, Task<T>> action)
    {
        await using var conn = await OpenAsync();
        return await action(conn);
    }
}