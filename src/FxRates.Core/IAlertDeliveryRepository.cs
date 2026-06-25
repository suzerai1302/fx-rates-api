namespace FxRates.Core;

public interface IAlertDeliveryRepository
{
    Task AddAsync(AlertDelivery delivery, CancellationToken cancellationToken);
    Task<IReadOnlyList<AlertDelivery>> GetByAlertAsync(Guid alertId, CancellationToken cancellationToken);
}
