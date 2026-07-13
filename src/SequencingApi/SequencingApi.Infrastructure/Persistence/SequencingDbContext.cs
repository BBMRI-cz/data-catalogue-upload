using Microsoft.EntityFrameworkCore;

namespace SequencingApi.Infrastructure.Persistence;

/// <summary>EF Core context for the sequencing_api database.</summary>
/// <remarks>
/// ponytail: no DbSets yet — the sequencing schema and its first migration land with #30. The context
/// exists so the host, options and design-time factory are wired in the same shape as BiobankApi.
/// </remarks>
public sealed class SequencingDbContext : DbContext
{
    public SequencingDbContext(DbContextOptions<SequencingDbContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Source dates are zone-less calendar values; timestamp-without-time-zone accepts
        // DateTimeKind.Unspecified, unlike timestamptz which only accepts UTC.
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
    }
}
