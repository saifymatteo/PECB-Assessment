using System.Data;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Api.Domain;

namespace SupportDesk.Api.Infrastructure;

/// <summary>
/// Generates unique human-readable references (TCK-YYYY-NNNN) from a per-year counter row.
/// The serializable transaction serializes concurrent creations on the counter row.
/// </summary>
public class TicketReferenceGenerator(AppDbContext db)
{
    public async Task<string> NextReferenceAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var year = utcNow.Year;

        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var counter = await db.TicketCounters
            .SingleOrDefaultAsync(c => c.Year == year, cancellationToken);

        if (counter is null)
        {
            counter = new TicketCounter(year);
            db.TicketCounters.Add(counter);
        }

        var number = counter.Next();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return $"TCK-{year}-{number:D4}";
    }
}
