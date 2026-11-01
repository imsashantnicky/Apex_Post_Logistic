namespace ApexPost.Application.Abstractions;

/// <summary>Tells the health check whether the data store accepts writes.</summary>
public interface IDataStoreHealth
{
    Task<bool> CanWriteAsync(CancellationToken cancellationToken);
}
