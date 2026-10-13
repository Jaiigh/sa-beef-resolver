using System.Data;
using System.Text.Json;
using HumanResolve.ArgumentService.Domain;
using Npgsql;

namespace HumanResolve.ArgumentService.Repositories;

public sealed class PostgresArgumentRepository(NpgsqlDataSource source) : IArgumentRepository
{
    public static void Initialize(NpgsqlDataSource source)
    {
        using var command = source.CreateCommand("""
            CREATE TABLE IF NOT EXISTS arguments (
              argument_id uuid PRIMARY KEY, dispute_id text NOT NULL, person_id text NOT NULL,
              statement text NOT NULL, status integer NOT NULL CHECK(status IN (0,1)),
              version integer NOT NULL CHECK(version > 0), created_at timestamptz NOT NULL,
              updated_at timestamptz NOT NULL, deleted boolean NOT NULL DEFAULT false);
            CREATE INDEX IF NOT EXISTS arguments_dispute ON arguments(dispute_id,created_at DESC);
            CREATE TABLE IF NOT EXISTS evidence (
              evidence_id uuid PRIMARY KEY, argument_id uuid NOT NULL REFERENCES arguments(argument_id),
              position integer NOT NULL, type integer NOT NULL, evidence_url text NOT NULL,
              description text NOT NULL, created_at timestamptz NOT NULL);
            CREATE INDEX IF NOT EXISTS evidence_argument ON evidence(argument_id);
            CREATE TABLE IF NOT EXISTS argument_revisions (
              argument_id uuid NOT NULL REFERENCES arguments(argument_id), version integer NOT NULL,
              snapshot jsonb NOT NULL, saved_at timestamptz NOT NULL DEFAULT now(),
              PRIMARY KEY(argument_id,version));
            CREATE TABLE IF NOT EXISTS ai_participants (
              dispute_id text NOT NULL, people integer NOT NULL CHECK(people IN (1,2)), person_id text NOT NULL,
              PRIMARY KEY(dispute_id,people), UNIQUE(dispute_id,person_id));
            """);
        command.ExecuteNonQuery();
    }

    private static NpgsqlCommand Cmd(NpgsqlConnection connection, string sql, params object[] values)
    {
        var cmd = new NpgsqlCommand(sql, connection);
        foreach (var value in values) cmd.Parameters.Add(new NpgsqlParameter { Value = value });
        return cmd;
    }

    public static int ImportLegacy(NpgsqlDataSource source, string path)
    {
        var json = File.ReadAllText(path);
        var records = JsonSerializer.Deserialize<ArgumentRecord[]>(json) ?? [];
        using var document = JsonDocument.Parse(json);
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var count = 0;
        foreach (var record in records)
        {
            using var insert = Cmd(connection, "INSERT INTO arguments(argument_id,dispute_id,person_id,statement,status,version,created_at,updated_at) VALUES ($1,$2,$3,$4,$5,$6,$7,$8) ON CONFLICT DO NOTHING",
                record.ArgumentId, record.DisputeId, record.PersonId, record.Statement, (int)record.Status, record.Version,
                record.CreatedAt.ToUniversalTime(), record.UpdatedAt.ToUniversalTime());
            if (insert.ExecuteNonQuery() == 0) continue;
            SaveEvidenceAndRevision(connection, record);
            var original = document.RootElement.EnumerateArray().First(x => x.GetProperty("ArgumentId").GetString() == record.ArgumentId.ToString());
            if (original.TryGetProperty("AiPeople", out var side))
            {
                using var bind = Cmd(connection, "INSERT INTO ai_participants VALUES ($1,$2,$3) ON CONFLICT DO NOTHING", record.DisputeId, side.GetInt32(), record.PersonId);
                bind.ExecuteNonQuery();
            }
            count++;
        }
        transaction.Commit();
        return count;
    }

    private static ArgumentRecord? Read(NpgsqlConnection connection, Guid id, bool forUpdate = false)
    {
        using var cmd = Cmd(connection, "SELECT argument_id,dispute_id,person_id,statement,status,version,created_at,updated_at FROM arguments WHERE argument_id=$1 AND NOT deleted" + (forUpdate ? " FOR UPDATE" : ""), id);
        ArgumentRecord record;
        using (var reader = cmd.ExecuteReader())
        {
            if (!reader.Read()) return null;
            record = new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), [],
                (ArgumentStatus)reader.GetInt32(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetFieldValue<DateTimeOffset>(7));
        }
        using var evidenceCmd = Cmd(connection, "SELECT evidence_id,type,evidence_url,description,created_at FROM evidence WHERE argument_id=$1 ORDER BY position", id);
        using var evidenceReader = evidenceCmd.ExecuteReader();
        var evidence = new List<EvidenceRecord>();
        while (evidenceReader.Read()) evidence.Add(new(evidenceReader.GetGuid(0), (EvidenceType)evidenceReader.GetInt32(1),
            evidenceReader.GetString(2), evidenceReader.GetString(3), evidenceReader.GetFieldValue<DateTimeOffset>(4)));
        return record with { Evidence = evidence };
    }

    private static void SaveEvidenceAndRevision(NpgsqlConnection connection, ArgumentRecord record)
    {
        using (var delete = Cmd(connection, "DELETE FROM evidence WHERE argument_id=$1", record.ArgumentId)) delete.ExecuteNonQuery();
        for (var i = 0; i < record.Evidence.Count; i++)
        {
            var item = record.Evidence[i];
            using var insert = Cmd(connection, "INSERT INTO evidence VALUES ($1,$2,$3,$4,$5,$6,$7)",
                item.EvidenceId, record.ArgumentId, i, (int)item.Type, item.EvidenceUrl, item.Description, item.CreatedAt.ToUniversalTime());
            insert.ExecuteNonQuery();
        }
        using var revision = Cmd(connection, "INSERT INTO argument_revisions(argument_id,version,snapshot) VALUES ($1,$2,$3::jsonb)",
            record.ArgumentId, record.Version, JsonSerializer.Serialize(record));
        revision.ExecuteNonQuery();
    }

    public ArgumentRecord Create(string disputeId, string personId, string statement, IReadOnlyList<EvidenceRecord> evidence, ArgumentStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new ArgumentRecord(Guid.NewGuid(), disputeId, personId, statement, evidence, status, 1, now, now);
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var insert = Cmd(connection, "INSERT INTO arguments(argument_id,dispute_id,person_id,statement,status,version,created_at,updated_at) VALUES ($1,$2,$3,$4,$5,$6,$7,$8)",
            record.ArgumentId, disputeId, personId, statement, (int)status, 1, now, now);
        insert.ExecuteNonQuery();
        SaveEvidenceAndRevision(connection, record);
        transaction.Commit();
        return record;
    }

    public ArgumentRecord? Get(Guid argumentId)
    {
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        var result = Read(connection, argumentId);
        transaction.Commit();
        return result;
    }

    public IReadOnlyList<ArgumentRecord> GetAll(string disputeId, string? personId, int offset, int pageSize, out bool hasNextPage)
    {
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        using var cmd = Cmd(connection, "SELECT argument_id FROM arguments WHERE dispute_id=$1 AND NOT deleted AND ($2='' OR person_id=$2) ORDER BY created_at DESC,argument_id LIMIT $3 OFFSET $4", disputeId, personId ?? "", pageSize + 1, offset);
        var ids = new List<Guid>();
        using (var reader = cmd.ExecuteReader()) while (reader.Read()) ids.Add(reader.GetGuid(0));
        hasNextPage = ids.Count > pageSize;
        var records = ids.Take(pageSize).Select(id => Read(connection, id)!).ToList();
        transaction.Commit();
        return records;
    }

    public UpdateResult Update(Guid argumentId, string requesterPersonId, string statement, IReadOnlyList<EvidenceRecord> evidence, ArgumentStatus status, int expectedVersion)
    {
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var old = Read(connection, argumentId, true);
        if (old is null) return new(UpdateResultKind.NotFound);
        if (old.PersonId != requesterPersonId) return new(UpdateResultKind.Forbidden);
        if (old.Version != expectedVersion) return new(UpdateResultKind.VersionConflict);
        var record = old with { Statement = statement, Evidence = evidence, Status = status, Version = old.Version + 1, UpdatedAt = DateTimeOffset.UtcNow };
        using var update = Cmd(connection, "UPDATE arguments SET statement=$2,status=$3,version=$4,updated_at=$5 WHERE argument_id=$1", argumentId, statement, (int)status, record.Version, record.UpdatedAt);
        update.ExecuteNonQuery();
        SaveEvidenceAndRevision(connection, record);
        transaction.Commit();
        return new(UpdateResultKind.Updated, record);
    }

    public DeleteResult Delete(Guid argumentId, string requesterPersonId, int expectedVersion)
    {
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var old = Read(connection, argumentId, true);
        if (old is null) return new(DeleteResultKind.NotFound);
        if (old.PersonId != requesterPersonId) return new(DeleteResultKind.Forbidden);
        if (old.Version != expectedVersion) return new(DeleteResultKind.VersionConflict);
        using var delete = Cmd(connection, "UPDATE arguments SET deleted=true,updated_at=now() WHERE argument_id=$1", argumentId);
        delete.ExecuteNonQuery();
        transaction.Commit();
        return new(DeleteResultKind.Deleted);
    }
}
