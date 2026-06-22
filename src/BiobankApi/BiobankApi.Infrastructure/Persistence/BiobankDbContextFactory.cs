using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BiobankApi.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the context without the running
/// host or a live database. The connection string here is a placeholder used only at design time.
/// </summary>
internal sealed class BiobankDbContextFactory : IDesignTimeDbContextFactory<BiobankDbContext>
{
    public BiobankDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BiobankDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=biobank_api;Username=postgres;Password=postgres")
            .Options;

        return new BiobankDbContext(options);
    }
}
