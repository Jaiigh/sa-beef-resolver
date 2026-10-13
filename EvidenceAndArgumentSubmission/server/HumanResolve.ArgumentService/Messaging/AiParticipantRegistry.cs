using Npgsql;

namespace HumanResolve.ArgumentService.Messaging;

public sealed class AiParticipantRegistry(NpgsqlDataSource source)
{
    public bool TryBind(string session, string person, int side)
    {
        using var connection = source.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var insert = new NpgsqlCommand("INSERT INTO ai_participants(dispute_id,people,person_id) VALUES ($1,$2,$3) ON CONFLICT DO NOTHING", connection);
        insert.Parameters.Add(new() { Value = session });
        insert.Parameters.Add(new() { Value = side });
        insert.Parameters.Add(new() { Value = person });
        insert.ExecuteNonQuery();
        using var query = new NpgsqlCommand("SELECT person_id FROM ai_participants WHERE dispute_id=$1 AND people=$2", connection);
        query.Parameters.Add(new() { Value = session });
        query.Parameters.Add(new() { Value = side });
        var bound = query.ExecuteScalar() as string;
        transaction.Commit();
        return bound == person;
    }
}
