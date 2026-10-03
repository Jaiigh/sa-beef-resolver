using HumanResolve.ArgumentService.Domain;

namespace HumanResolve.ArgumentService.Repositories;

public sealed class InMemoryArgumentRepository : IArgumentRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, ArgumentRecord> _records = new();

    public ArgumentRecord Create(string disputeId, string personId, string statement, IReadOnlyList<EvidenceRecord> evidence, ArgumentStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new ArgumentRecord(Guid.NewGuid(), disputeId, personId, statement, evidence, status, 1, now, now);
        lock (_lock) _records.Add(record.ArgumentId, record);
        return record;
    }

    public ArgumentRecord? Get(Guid argumentId)
    {
        lock (_lock) return _records.GetValueOrDefault(argumentId);
    }

    public IReadOnlyList<ArgumentRecord> GetAll(string disputeId, string? personId, int offset, int pageSize, out bool hasNextPage)
    {
        lock (_lock)
        {
            var records = _records.Values
                .Where(record => record.DisputeId == disputeId && (string.IsNullOrEmpty(personId) || record.PersonId == personId))
                .OrderByDescending(record => record.CreatedAt)
                .ToList();
            var page = records.Skip(offset).Take(pageSize).ToList();
            hasNextPage = offset + page.Count < records.Count;
            return page;
        }
    }

    public UpdateResult Update(Guid argumentId, string requesterPersonId, string statement, IReadOnlyList<EvidenceRecord> evidence, ArgumentStatus status, int expectedVersion)
    {
        lock (_lock)
        {
            if (!_records.TryGetValue(argumentId, out var existing)) return new(UpdateResultKind.NotFound);
            if (existing.PersonId != requesterPersonId) return new(UpdateResultKind.Forbidden);
            if (existing.Version != expectedVersion) return new(UpdateResultKind.VersionConflict);
            var updated = existing with { Statement = statement, Evidence = evidence, Status = status, Version = existing.Version + 1, UpdatedAt = DateTimeOffset.UtcNow };
            _records[argumentId] = updated;
            return new(UpdateResultKind.Updated, updated);
        }
    }

    public DeleteResult Delete(Guid argumentId, string requesterPersonId, int expectedVersion)
    {
        lock (_lock)
        {
            if (!_records.TryGetValue(argumentId, out var existing)) return new(DeleteResultKind.NotFound);
            if (existing.PersonId != requesterPersonId) return new(DeleteResultKind.Forbidden);
            if (existing.Version != expectedVersion) return new(DeleteResultKind.VersionConflict);
            _records.Remove(argumentId);
            return new(DeleteResultKind.Deleted);
        }
    }
}
