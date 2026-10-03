using HumanResolve.ArgumentService.Domain;

namespace HumanResolve.ArgumentService.Repositories;

public interface IArgumentRepository
{
    ArgumentRecord Create(string disputeId, string personId, string statement, IReadOnlyList<EvidenceRecord> evidence, ArgumentStatus status);
    ArgumentRecord? Get(Guid argumentId);
    IReadOnlyList<ArgumentRecord> GetAll(string disputeId, string? personId, int offset, int pageSize, out bool hasNextPage);
    UpdateResult Update(Guid argumentId, string requesterPersonId, string statement, IReadOnlyList<EvidenceRecord> evidence, ArgumentStatus status, int expectedVersion);
    DeleteResult Delete(Guid argumentId, string requesterPersonId, int expectedVersion);
}

public enum UpdateResultKind { Updated, NotFound, Forbidden, VersionConflict }
public sealed record UpdateResult(UpdateResultKind Kind, ArgumentRecord? Argument = null);
public enum DeleteResultKind { Deleted, NotFound, Forbidden, VersionConflict }
public sealed record DeleteResult(DeleteResultKind Kind);
