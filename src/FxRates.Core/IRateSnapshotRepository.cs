namespace FxRates.Core;

public interface IRateSnapshotRepository
{
    Task AddAsync(RateSnapshot snapshot, CancellationToken cancellationToken);

    Task<RateSnapshot?> GetLatestAsync(CancellationToken cancellationToken);

    // Slice #5 (history). from/to are inclusive bounds; limit caps the result count.
    Task<IReadOnlyList<RateSnapshot>> GetHistoryAsync(
        DateTime? from, DateTime? to, int limit, CancellationToken cancellationToken);
}
