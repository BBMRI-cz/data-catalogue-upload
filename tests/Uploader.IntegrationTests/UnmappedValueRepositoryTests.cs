using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;
using Uploader.Infrastructure.Persistence;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The <c>unmapped_value</c> table is what <c>GET /admin/ontology/unmapped</c> serves, so it has to
/// hold the list as the runs left it: replaced by a run that heard from every source, merged into by
/// one that did not, and keeping the moment each value was first seen either way.
/// </summary>
public sealed class UnmappedValueRepositoryTests
{
    private static readonly DateTimeOffset FirstRun = new(2026, 11, 23, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondRun = FirstRun.AddDays(2);

    private static readonly UnmappedValue Sex = new(OntologyFields.GenderAtBirth, "x", TermContext.Empty);
    private static readonly UnmappedValue Kit = new(OntologyFields.LibraryPreparationKit, "KAPA HyperPlus Kit", TermContext.Empty);
    private static readonly UnmappedValue Code = new(
        OntologyFields.StorageConditions, "9", TermContext.Material("tissue", "Malignant tumour (RNAlater)"));

    private static Dictionary<UnmappedValue, int> Misses(params (UnmappedValue Miss, int Rows)[] misses) =>
        misses.ToDictionary(miss => miss.Miss, miss => miss.Rows);

    private static async Task<List<UnmappedListEntry>> ReadAsync(SqliteDatabase db) =>
        [.. (await new SqlUnmappedValueRepository(db.NewContext(), TimeProvider.System).GetAllAsync(default))
            .OrderBy(entry => entry.Field.ToString(), StringComparer.Ordinal)
            .ThenBy(entry => entry.Value, StringComparer.Ordinal)];

    [Fact]
    public async Task BeforeTheFirstRunTheListIsEmpty()
    {
        using var db = new SqliteDatabase();

        Assert.Empty(await ReadAsync(db));
    }

    [Fact]
    public async Task AValueReadsBackWithItsContextCountAndFirstSighting()
    {
        using var db = new SqliteDatabase();
        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(FirstRun))
            .ReplaceAsync("run-1", Misses((Code, 3), (Sex, 2)), default);

        Assert.Equal(
            [
                new UnmappedListEntry(Code.Field, "9", Code.Context, 3, FirstRun),
                new UnmappedListEntry(Sex.Field, "x", TermContext.Empty, 2, FirstRun),
            ],
            await ReadAsync(db));
    }

    /// <summary>
    /// A clean run's misses are the list: one still present takes the new count and keeps the moment
    /// it was first seen, so the student can tell a new value from a long-standing one, and one the run
    /// did not meet has left the sources and drops off.
    /// </summary>
    [Fact]
    public async Task AReplaceKeepsTheFirstSightingOfAValueStillPresentAndDropsTheRest()
    {
        using var db = new SqliteDatabase();
        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(FirstRun))
            .ReplaceAsync("run-1", Misses((Sex, 2), (Kit, 5)), default);

        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(SecondRun))
            .ReplaceAsync("run-2", Misses((Sex, 4), (Code, 1)), default);

        Assert.Equal(
            [
                new UnmappedListEntry(Code.Field, "9", Code.Context, 1, SecondRun),
                new UnmappedListEntry(Sex.Field, "x", TermContext.Empty, 4, FirstRun),
            ],
            await ReadAsync(db));
    }

    [Fact]
    public async Task AReplaceWithNothingEmptiesTheList()
    {
        using var db = new SqliteDatabase();
        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(FirstRun))
            .ReplaceAsync("run-1", Misses((Sex, 2)), default);

        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(SecondRun))
            .ReplaceAsync("run-2", Misses(), default);

        Assert.Empty(await ReadAsync(db));
    }

    /// <summary>
    /// A run a source did not answer adds what it met and updates the counts of what it met again, but
    /// a value it did not meet may only have been hidden by the outage, so it stays as it was.
    /// </summary>
    [Fact]
    public async Task AMergeAddsAndUpdatesAndRemovesNothing()
    {
        using var db = new SqliteDatabase();
        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(FirstRun))
            .ReplaceAsync("run-1", Misses((Sex, 2), (Kit, 5)), default);

        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(SecondRun))
            .MergeAsync("run-2", Misses((Sex, 4), (Code, 1)), default);

        Assert.Equal(
            [
                new UnmappedListEntry(Code.Field, "9", Code.Context, 1, SecondRun),
                new UnmappedListEntry(Sex.Field, "x", TermContext.Empty, 4, FirstRun),
                new UnmappedListEntry(Kit.Field, "KAPA HyperPlus Kit", TermContext.Empty, 5, FirstRun),
            ],
            await ReadAsync(db));
    }

    /// <summary>
    /// The list is keyed as the mapping table is - field, value and the whole context - so one code in
    /// two sample types, or with and without the biobank's label, is two entries.
    /// </summary>
    [Fact]
    public async Task AValueIsKeyedOnItsFieldValueAndWholeContext()
    {
        using var db = new SqliteDatabase();
        var serum = Code with { Context = TermContext.Material("serum", "Malignant tumour (RNAlater)") };
        var unlabelled = Code with { Context = TermContext.Material("tissue", null) };
        var otherField = Code with { Field = OntologyFields.PathologicalState };

        await new SqlUnmappedValueRepository(db.NewContext(), new SettableClock(FirstRun))
            .ReplaceAsync("run-1", Misses((Code, 1), (serum, 2), (unlabelled, 3), (otherField, 4)), default);

        var entries = await ReadAsync(db);
        Assert.Equal(4, entries.Count);
        Assert.Equal(
            [Code.Context, serum.Context, unlabelled.Context, otherField.Context],
            [
                entries.Single(entry => entry.Count == 1).Context,
                entries.Single(entry => entry.Count == 2).Context,
                entries.Single(entry => entry.Count == 3).Context,
                entries.Single(entry => entry.Count == 4).Context,
            ]);
        Assert.Equal(OntologyFields.PathologicalState, entries.Single(entry => entry.Count == 4).Field);
    }
}
