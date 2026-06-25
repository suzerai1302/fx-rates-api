using FxRates.Core;
using Microsoft.EntityFrameworkCore;

namespace FxRates.Infrastructure;

public class EfAlertDeliveryRepository : IAlertDeliveryRepository
{
    private readonly FxRatesDbContext _db;

    public EfAlertDeliveryRepository(FxRatesDbContext db) => _db = db;

    public async Task AddAsync(AlertDelivery delivery, CancellationToken cancellationToken)
    {
        _db.AlertDeliveries.Add(delivery);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertDelivery>> GetByAlertAsync(Guid alertId, CancellationToken cancellationToken) =>
        await _db.AlertDeliveries.Where(d => d.AlertId == alertId).ToListAsync(cancellationToken);
}
