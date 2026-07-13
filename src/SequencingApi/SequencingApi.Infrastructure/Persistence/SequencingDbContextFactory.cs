using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the context without the running
/// host or a live database. The connection string here is a placeholder used only at design time.
/// </summary>
internal sealed class SequencingDbContextFactory : IDesignTimeDbContextFactory<SequencingDbContext>
{
    public SequencingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SequencingDbContext>()
            .UseNpgsql("Host=localhost;Port=5434;Database=sequencing_api;Username=postgres;Password=postgres")
            .Options;

        return new SequencingDbContext(options);
    }
}
