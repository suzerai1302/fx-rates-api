using FxRates.Core;
using Microsoft.EntityFrameworkCore;

namespace FxRates.Infrastructure;

public class EfRateSnapshotRepository : IRateSnapshotRepository
{
    private readonly FxRatesDbContext _db;

    public EfRateSnapshotRepository(FxRatesDbContext db) => _db = db;

    public async Task AddAsync(RateSnapshot snapshot, CancellationToken cancellationToken)
    {
        _db.Snapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // Newest first. Ordered by Id (identity), monotonic with AsOf for these
    // append-only snapshots and cheap to sort on either provider.
    public async Task<RateSnapshot?> GetLatestAsync(CancellationToken cancellationToken) =>
        await _db.Snapshots
            .Include(s => s.Sources)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RateSnapshot>> GetHistoryAsync(
        DateTime? from, DateTime? to, int limit, CancellationToken cancellationToken)
    {
        var query = _db.Snapshots.AsQueryable();
        if (from is not null) query = query.Where(s => s.AsOf >= from);
        if (to is not null) query = query.Where(s => s.AsOf <= to);

        return await query
            .OrderByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
