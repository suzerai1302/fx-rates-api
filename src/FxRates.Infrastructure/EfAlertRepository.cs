using FxRates.Core;
using Microsoft.EntityFrameworkCore;

namespace FxRates.Infrastructure;

public class EfAlertRepository : IAlertRepository
{
    private readonly FxRatesDbContext _db;

    public EfAlertRepository(FxRatesDbContext db) => _db = db;

    public async Task AddAsync(Alert alert, CancellationToken cancellationToken)
    {
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        await _db.Alerts.Where(a => a.OwnerUserId == ownerUserId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken) =>
        await _db.Alerts.ToListAsync(cancellationToken);

    public async Task UpdateAsync(Alert alert, CancellationToken cancellationToken)
    {
        _db.Alerts.Update(alert);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(
            a => a.Id == id && a.OwnerUserId == ownerUserId, cancellationToken);
        if (alert is null) return false;

        _db.Alerts.Remove(alert);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
