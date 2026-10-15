using System.Globalization;
using DragonKMG.Application;
using DragonKMG.Domain;
using Microsoft.Data.Sqlite;

namespace DragonKMG.Infrastructure;

public sealed class SqlitePlatformStore(string databasePath) : IPlatformStore
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS roles (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, description TEXT NOT NULL, instructions TEXT NOT NULL, is_system INTEGER NOT NULL, version INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS skills (id TEXT PRIMARY KEY, key TEXT NOT NULL UNIQUE, name TEXT NOT NULL, description TEXT NOT NULL, version INTEGER NOT NULL, enabled INTEGER NOT NULL, type INTEGER NOT NULL, instructions TEXT NOT NULL, tools_json TEXT NOT NULL, trust_level TEXT NOT NULL, source_type TEXT NOT NULL DEFAULT 'user', source_ref TEXT NOT NULL DEFAULT '');
            CREATE TABLE IF NOT EXISTS connections (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, kind INTEGER NOT NULL, endpoint TEXT NOT NULL, secret_reference TEXT NOT NULL, enabled INTEGER NOT NULL, trust_zone INTEGER NOT NULL, cloud_egress_allowed INTEGER NOT NULL, created_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS agents (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, description TEXT NOT NULL, role_id TEXT NOT NULL REFERENCES roles(id), instructions TEXT NOT NULL, enabled INTEGER NOT NULL, model_id TEXT NOT NULL, connection_id TEXT NOT NULL REFERENCES connections(id), version INTEGER NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL, mode INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS agent_skill_assignments (agent_id TEXT NOT NULL REFERENCES agents(id), skill_id TEXT NOT NULL REFERENCES skills(id), enabled INTEGER NOT NULL, priority INTEGER NOT NULL, configuration_json TEXT NOT NULL, version INTEGER NOT NULL, PRIMARY KEY(agent_id, skill_id));
            CREATE TABLE IF NOT EXISTS agent_tool_grants (agent_id TEXT NOT NULL REFERENCES agents(id), tool_id TEXT NOT NULL, requires_approval INTEGER NOT NULL, version INTEGER NOT NULL, PRIMARY KEY(agent_id, tool_id));
            CREATE TABLE IF NOT EXISTS runs (id TEXT PRIMARY KEY, agent_id TEXT NOT NULL REFERENCES agents(id), agent_version INTEGER NOT NULL, input TEXT NOT NULL, effective_instructions TEXT NOT NULL, status INTEGER NOT NULL, output TEXT NULL, error_code TEXT NULL, started_at TEXT NOT NULL, finished_at TEXT NULL);
            CREATE TABLE IF NOT EXISTS audit_events (id TEXT PRIMARY KEY, occurred_at TEXT NOT NULL, actor TEXT NOT NULL, action TEXT NOT NULL, status_code INTEGER NOT NULL, outcome TEXT NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "skills", "source_type", "TEXT NOT NULL DEFAULT 'user'", cancellationToken);
        await EnsureColumnAsync(connection, "skills", "source_ref", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await EnsureColumnAsync(connection, "agents", "mode", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        foreach (var role in DefaultCouncil.Roles) await SaveRoleAsync(role, cancellationToken);
        foreach (var skill in DefaultSkillCatalog.Skills) await SaveSkillAsync(skill, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderConnection>> ListConnectionsAsync(CancellationToken ct) => await ReadManyAsync("SELECT * FROM connections ORDER BY name", ReadConnection, ct);
    public async Task<ProviderConnection?> FindConnectionAsync(Guid id, CancellationToken ct) => await ReadOneAsync("SELECT * FROM connections WHERE id=$id", "$id", id, ReadConnection, ct);
    public async Task SaveConnectionAsync(ProviderConnection value, CancellationToken ct) => await ExecuteAsync("INSERT INTO connections VALUES($id,$name,$kind,$endpoint,$secret,$enabled,$trust,$egress,$created) ON CONFLICT(id) DO UPDATE SET name=excluded.name,kind=excluded.kind,endpoint=excluded.endpoint,secret_reference=excluded.secret_reference,enabled=excluded.enabled,trust_zone=excluded.trust_zone,cloud_egress_allowed=excluded.cloud_egress_allowed", [ ("$id", value.Id), ("$name", value.Name), ("$kind", (int)value.Kind), ("$endpoint", value.Endpoint.ToString()), ("$secret", value.SecretReference), ("$enabled", value.Enabled), ("$trust", (int)value.TrustZone), ("$egress", value.CloudEgressAllowed), ("$created", value.CreatedAt) ], ct);
    public async Task<IReadOnlyList<AgentRole>> ListRolesAsync(CancellationToken ct) => await ReadManyAsync("SELECT * FROM roles ORDER BY name", ReadRole, ct);
    public async Task<AgentRole?> FindRoleAsync(Guid id, CancellationToken ct) => await ReadOneAsync("SELECT * FROM roles WHERE id=$id", "$id", id, ReadRole, ct);
    public async Task SaveRoleAsync(AgentRole value, CancellationToken ct) => await ExecuteAsync("INSERT INTO roles VALUES($id,$name,$description,$instructions,$system,$version) ON CONFLICT(id) DO UPDATE SET name=excluded.name,description=excluded.description,instructions=excluded.instructions,is_system=excluded.is_system,version=excluded.version", [ ("$id",value.Id),("$name",value.Name),("$description",value.Description),("$instructions",value.DefaultInstructions),("$system",value.IsSystem),("$version",value.Version) ], ct);
    public async Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken ct) => await ReadManyAsync("SELECT * FROM skills ORDER BY name", ReadSkill, ct);
    public async Task<SkillDefinition?> FindSkillAsync(Guid id, CancellationToken ct) => await ReadOneAsync("SELECT * FROM skills WHERE id=$id", "$id", id, ReadSkill, ct);
    public async Task SaveSkillAsync(SkillDefinition value, CancellationToken ct) => await ExecuteAsync("INSERT INTO skills VALUES($id,$key,$name,$description,$version,$enabled,$type,$instructions,$tools,$trust,$sourceType,$sourceRef) ON CONFLICT(id) DO UPDATE SET key=excluded.key,name=excluded.name,description=excluded.description,version=excluded.version,enabled=excluded.enabled,type=excluded.type,instructions=excluded.instructions,tools_json=excluded.tools_json,trust_level=excluded.trust_level,source_type=excluded.source_type,source_ref=excluded.source_ref", [ ("$id",value.Id),("$key",value.Key),("$name",value.Name),("$description",value.Description),("$version",value.Version),("$enabled",value.Enabled),("$type",(int)value.Type),("$instructions",value.InstructionContent),("$tools",value.RequiredToolIdsJson),("$trust",value.TrustLevel),("$sourceType",value.SourceType),("$sourceRef",value.SourceRef) ], ct);
    public async Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(CancellationToken ct) => await ReadManyAsync("SELECT * FROM agents ORDER BY name", ReadAgent, ct);
    public async Task<AgentDefinition?> FindAgentAsync(Guid id, CancellationToken ct) => await ReadOneAsync("SELECT * FROM agents WHERE id=$id", "$id", id, ReadAgent, ct);
    public async Task SaveAgentAsync(AgentDefinition value, CancellationToken ct) => await ExecuteAsync("INSERT INTO agents VALUES($id,$name,$description,$role,$instructions,$enabled,$model,$connection,$version,$created,$updated,$mode) ON CONFLICT(id) DO UPDATE SET name=excluded.name,description=excluded.description,role_id=excluded.role_id,instructions=excluded.instructions,enabled=excluded.enabled,model_id=excluded.model_id,connection_id=excluded.connection_id,version=excluded.version,updated_at=excluded.updated_at,mode=excluded.mode", [ ("$id",value.Id),("$name",value.Name),("$description",value.Description),("$role",value.RoleId),("$instructions",value.Instructions),("$enabled",value.Enabled),("$model",value.ModelId),("$connection",value.ConnectionId),("$version",value.Version),("$created",value.CreatedAt),("$updated",value.UpdatedAt),("$mode",(int)value.Mode) ], ct);
    public async Task<IReadOnlyList<AgentSkillAssignment>> ListAssignmentsAsync(Guid agentId, CancellationToken ct) => await ReadManyAsync("SELECT * FROM agent_skill_assignments WHERE agent_id=$id ORDER BY priority", ReadAssignment, ct, ("$id", agentId));
    public async Task ReplaceAssignmentsAsync(Guid agentId, IReadOnlyList<AgentSkillAssignment> values, CancellationToken ct)
    {
        if (values.Any(value => value.AgentId != agentId)) throw new ArgumentException("Every assignment must belong to the target agent.", nameof(values));
        if (values.GroupBy(value => value.SkillId).Any(group => group.Count() > 1)) throw new ArgumentException("A skill can only be assigned once per agent.", nameof(values));
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        await using (var delete = Command(connection, "DELETE FROM agent_skill_assignments WHERE agent_id=$id", ("$id", agentId)))
        {
            delete.Transaction = transaction;
            await delete.ExecuteNonQueryAsync(ct);
        }
        foreach (var value in values)
        {
            await using var insert = Command(connection, "INSERT INTO agent_skill_assignments VALUES($agent,$skill,$enabled,$priority,$config,$version)", ("$agent", value.AgentId), ("$skill", value.SkillId), ("$enabled", value.Enabled), ("$priority", value.Priority), ("$config", value.ConfigurationJson), ("$version", value.Version));
            insert.Transaction = transaction;
            await insert.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<AgentToolGrant>> ListToolGrantsAsync(Guid agentId, CancellationToken ct) => await ReadManyAsync("SELECT * FROM agent_tool_grants WHERE agent_id=$id ORDER BY tool_id", ReadToolGrant, ct, ("$id", agentId));
    public async Task ReplaceToolGrantsAsync(Guid agentId, IReadOnlyList<AgentToolGrant> grants, CancellationToken ct)
    {
        if (grants.Any(grant => grant.AgentId != agentId) || grants.Any(grant => string.IsNullOrWhiteSpace(grant.ToolId)) || grants.GroupBy(grant => grant.ToolId, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)) throw new ArgumentException("Tool grants must have unique non-empty tool IDs and belong to the target agent.", nameof(grants));
        await using var connection = await OpenAsync(ct); await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
        await using (var delete = Command(connection, "DELETE FROM agent_tool_grants WHERE agent_id=$id", ("$id", agentId))) { delete.Transaction = transaction; await delete.ExecuteNonQueryAsync(ct); }
        foreach (var grant in grants) { await using var insert = Command(connection, "INSERT INTO agent_tool_grants VALUES($agent,$tool,$approval,$version)", ("$agent", grant.AgentId), ("$tool", grant.ToolId.Trim()), ("$approval", grant.RequiresApproval), ("$version", grant.Version)); insert.Transaction = transaction; await insert.ExecuteNonQueryAsync(ct); }
        await transaction.CommitAsync(ct);
    }
    public async Task SaveRunAsync(AgentRun value, CancellationToken ct) => await ExecuteAsync("INSERT INTO runs VALUES($id,$agent,$version,$input,$instructions,$status,$output,$error,$started,$finished) ON CONFLICT(id) DO UPDATE SET status=excluded.status,output=excluded.output,error_code=excluded.error_code,finished_at=excluded.finished_at", [ ("$id",value.Id),("$agent",value.AgentId),("$version",value.AgentVersion),("$input",value.Input),("$instructions",value.EffectiveInstructions),("$status",(int)value.Status),("$output",value.Output),("$error",value.ErrorCode),("$started",value.StartedAt),("$finished",value.FinishedAt) ], ct);
    public async Task<IReadOnlyList<AgentRun>> ListRunsAsync(Guid agentId, CancellationToken ct) => await ReadManyAsync("SELECT * FROM runs WHERE agent_id=$id ORDER BY started_at DESC", ReadRun, ct, ("$id", agentId));
    public async Task<int> RecoverInterruptedRunsAsync(DateTimeOffset recoveredAt, CancellationToken ct) => await ExecuteCountAsync("UPDATE runs SET status=$failed,error_code=$error,finished_at=$finished WHERE status=$running AND finished_at IS NULL", [ ("$failed", (int)RunStatus.Failed), ("$error", "InterruptedByRestart"), ("$finished", recoveredAt), ("$running", (int)RunStatus.Running) ], ct);
    public async Task SaveAuditEventAsync(AuditEvent value, CancellationToken ct) => await ExecuteAsync("INSERT INTO audit_events VALUES($id,$occurred,$actor,$action,$status,$outcome)", [ ("$id", value.Id), ("$occurred", value.OccurredAt), ("$actor", value.Actor), ("$action", value.Action), ("$status", value.StatusCode), ("$outcome", value.Outcome) ], ct);
    public async Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(int limit, CancellationToken ct) => await ReadManyAsync("SELECT * FROM audit_events ORDER BY occurred_at DESC LIMIT $limit", ReadAuditEvent, ct, ("$limit", Math.Clamp(limit, 1, 500)));

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct) { var connection = new SqliteConnection(_connectionString); await connection.OpenAsync(ct); return connection; }
    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition, CancellationToken ct) { var command = connection.CreateCommand(); command.CommandText = $"PRAGMA table_info({table})"; await using var reader = await command.ExecuteReaderAsync(ct); var exists = false; while (await reader.ReadAsync(ct)) if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) { exists = true; break; } if (!exists) { await using var alter = connection.CreateCommand(); alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}"; await alter.ExecuteNonQueryAsync(ct); } }
    private async Task ExecuteAsync(string sql, (string Key, object? Value)[] values, CancellationToken ct) { await using var connection = await OpenAsync(ct); await using var command = Command(connection, sql, values); await command.ExecuteNonQueryAsync(ct); }
    private async Task<int> ExecuteCountAsync(string sql, (string Key, object? Value)[] values, CancellationToken ct) { await using var connection = await OpenAsync(ct); await using var command = Command(connection, sql, values); return await command.ExecuteNonQueryAsync(ct); }
    private async Task<IReadOnlyList<T>> ReadManyAsync<T>(string sql, Func<SqliteDataReader,T> read, CancellationToken ct, params (string Key, object? Value)[] values) { var result = new List<T>(); await using var connection = await OpenAsync(ct); await using var command = Command(connection,sql,values); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(read(reader)); return result; }
    private async Task<T?> ReadOneAsync<T>(string sql, string key, object value, Func<SqliteDataReader,T> read, CancellationToken ct) where T : class { var all = await ReadManyAsync(sql,read,ct,(key,value)); return all.FirstOrDefault(); }
    private static SqliteCommand Command(SqliteConnection connection, string sql, params (string Key, object? Value)[] values) { var command=connection.CreateCommand(); command.CommandText=sql; foreach(var value in values) command.Parameters.AddWithValue(value.Key, value.Value ?? DBNull.Value); return command; }
    private static Guid Id(SqliteDataReader r, string n) => Guid.Parse(r.GetString(r.GetOrdinal(n)));
    private static DateTimeOffset Time(SqliteDataReader r, string n) => DateTimeOffset.Parse(r.GetString(r.GetOrdinal(n)), CultureInfo.InvariantCulture);
    private static ProviderConnection ReadConnection(SqliteDataReader r) => new(Id(r,"id"),r.GetString(r.GetOrdinal("name")),(ConnectionKind)r.GetInt32(r.GetOrdinal("kind")),new Uri(r.GetString(r.GetOrdinal("endpoint"))),r.GetString(r.GetOrdinal("secret_reference")),r.GetBoolean(r.GetOrdinal("enabled")),(TrustZone)r.GetInt32(r.GetOrdinal("trust_zone")),r.GetBoolean(r.GetOrdinal("cloud_egress_allowed")),Time(r,"created_at"));
    private static AgentRole ReadRole(SqliteDataReader r) => new(Id(r,"id"),r.GetString(1),r.GetString(2),r.GetString(3),r.GetBoolean(4),r.GetInt32(5));
    private static SkillDefinition ReadSkill(SqliteDataReader r) => new(Id(r,"id"),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetBoolean(5),(SkillType)r.GetInt32(6),r.GetString(7),r.GetString(8),r.GetString(9),r.GetString(10),r.GetString(11));
    private static AgentDefinition ReadAgent(SqliteDataReader r) => new(Id(r,"id"),r.GetString(1),r.GetString(2),Id(r,"role_id"),r.GetString(4),r.GetBoolean(5),r.GetString(6),Id(r,"connection_id"),r.GetInt32(8),Time(r,"created_at"),Time(r,"updated_at"),(AgentMode)r.GetInt32(r.GetOrdinal("mode")));
    private static AgentSkillAssignment ReadAssignment(SqliteDataReader r) => new(Id(r,"agent_id"),Id(r,"skill_id"),r.GetBoolean(2),r.GetInt32(3),r.GetString(4),r.GetInt32(5));
    private static AgentToolGrant ReadToolGrant(SqliteDataReader r) => new(Id(r,"agent_id"),r.GetString(1),r.GetBoolean(2),r.GetInt32(3));
    private static AgentRun ReadRun(SqliteDataReader r) => new(Id(r,"id"),Id(r,"agent_id"),r.GetInt32(2),r.GetString(3),r.GetString(4),(RunStatus)r.GetInt32(5),r.IsDBNull(6)?null:r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),Time(r,"started_at"),r.IsDBNull(9)?null:Time(r,"finished_at"));
    private static AuditEvent ReadAuditEvent(SqliteDataReader r) => new(Id(r,"id"), Time(r,"occurred_at"), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetString(5));
}

public sealed class EnvironmentSecretResolver : ISecretResolver
{
    public string? Resolve(string secretReference) => secretReference.StartsWith("env:", StringComparison.OrdinalIgnoreCase) ? Environment.GetEnvironmentVariable(secretReference[4..]) : null;
}
