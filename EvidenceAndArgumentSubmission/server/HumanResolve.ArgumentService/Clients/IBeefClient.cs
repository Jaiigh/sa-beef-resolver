namespace HumanResolve.ArgumentService.Clients;

public sealed record BeefInfo(long Id, string PersonA, string PersonB, string Status);

public interface IBeefClient
{
    /// <summary>Returns the beef (dispute), or null when it does not exist. Throws BeefServiceUnavailableException when the REST API cannot be reached.</summary>
    Task<BeefInfo?> GetBeefAsync(long beefId, CancellationToken cancellationToken);
}

public sealed class BeefServiceUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
