using Microsoft.EntityFrameworkCore;
using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;
using Uploader.Infrastructure.Persistence;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The deployed uploader builds its schema from the migrations (<c>RUN_MIGRATIONS</c>), while every
/// other test here builds it from the model. These close that gap: a model change without its
/// migration, or a migration that does not create what the repositories write, fails here rather than
/// on the server.
/// </summary>
public sealed class MigrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheMigrationsMatchTheModel()
    {
        using var context = new UploaderDbContext(
            new DbContextOptionsBuilder<UploaderDbContext>().UseNpgsql("Host=unused").Options);

        Assert.False(context.Database.HasPendingModelChanges(), "The uploader model has changes no migration applies.");
    }

    [Fact]
    public async Task TheMigratedSchemaHoldsTheUnmappedList()
    {
        using var db = new SqliteDatabase();
        var miss = new UnmappedValue(OntologyFields.StorageConditions, "9", TermContext.Material("tissue", null));

        await new SqlUnmappedValueRepository(db.NewMigratedContext(), new SettableClock(Now))
            .ReplaceAsync("run-1", new Dictionary<UnmappedValue, int> { [miss] = 3 }, default);

        var entry = Assert.Single(
            await new SqlUnmappedValueRepository(db.NewMigratedContext(), TimeProvider.System).GetAllAsync(default));
        Assert.Equal(new UnmappedListEntry(miss.Field, "9", miss.Context, 3, Now), entry);
    }
}
