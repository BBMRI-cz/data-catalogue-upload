using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Uploader.Infrastructure.Persistence;

/// <summary>Design-time factory so <c>dotnet ef</c> can build the context without the running host.</summary>
internal sealed class UploaderDbContextFactory : IDesignTimeDbContextFactory<UploaderDbContext>
{
    public UploaderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UploaderDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=data_catalogue_upload;Username=postgres;Password=postgres")
            .Options;

        return new UploaderDbContext(options);
    }
}
