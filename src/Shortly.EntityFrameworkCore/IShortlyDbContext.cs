using Microsoft.EntityFrameworkCore;

namespace Shortly.EntityFrameworkCore;

public interface IShortlyDbContext
{
    DbSet<ShortLinkRecord> ShortLinks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
