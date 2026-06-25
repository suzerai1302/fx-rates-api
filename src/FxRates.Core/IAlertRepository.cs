namespace FxRates.Core;

public interface IAlertRepository
{
    Task AddAsync(Alert alert, CancellationToken cancellationToken);
    Task<IReadOnlyList<Alert>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken);
    Task UpdateAsync(Alert alert, CancellationToken cancellationToken);

    // Deletes the alert only if it belongs to ownerUserId. Returns false if it does
    // not exist or is owned by someone else.
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken);
}
