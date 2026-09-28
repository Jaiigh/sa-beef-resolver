namespace HumanResolve.ArgumentService.Domain;

public sealed record EvidenceRecord(
    Guid EvidenceId,
    EvidenceType Type,
    string EvidenceUrl,
    string Description,
    DateTimeOffset CreatedAt);

public sealed record ArgumentRecord(
    Guid ArgumentId,
    string DisputeId,
    string PersonId,
    string Statement,
    IReadOnlyList<EvidenceRecord> Evidence,
    ArgumentStatus Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public enum ArgumentStatus
{
    Draft,
    Submitted
}

public enum EvidenceType
{
    Image,
    Document,
    Link,
    Other
}
