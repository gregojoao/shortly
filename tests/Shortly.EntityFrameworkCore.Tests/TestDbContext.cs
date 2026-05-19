using Microsoft.EntityFrameworkCore;
using Shortly.EntityFrameworkCore;

namespace Shortly.EntityFrameworkCore.Tests;

public sealed class TestDbContext : DbContext, IShortlyDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<ShortLinkRecord> ShortLinks => Set<ShortLinkRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddShortlyModel();
    }
}
