using Uploader.Application.Abstractions;
using Uploader.Infrastructure.Pseudonymization;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The store is what makes a pseudonym stable, and stability is what stops a second run duplicating
/// every record instead of updating it. These drive the real EF store against SQLite.
/// </summary>
public sealed class PseudonymStoreTests : IDisposable
{
    private const string Prefix = "mmci";

    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private PseudonymStore NewStore() => new(_db.NewContext(), TimeProvider.System, Prefix);

    [Fact]
    public async Task MintsAPrefixedPseudonymAndNeverEchoesTheRealId()
    {
        var pseudonym = await NewStore().PseudonymizeAsync(PseudonymKind.Patient, "271801", CancellationToken.None);

        Assert.StartsWith("mmci_patient_", pseudonym, StringComparison.Ordinal);
        Assert.DoesNotContain("271801", pseudonym, StringComparison.Ordinal);

        // A uuid, so the pseudonym carries nothing of the value it replaces.
        Assert.True(Guid.TryParse(pseudonym["mmci_patient_".Length..], out _));
    }

    [Fact]
    public async Task TheSameIdentifierResolvesToTheSamePseudonymWithinARun()
    {
        var store = NewStore();

        var first = await store.PseudonymizeAsync(PseudonymKind.Sample, "BBMs:2022:3249:SD", CancellationToken.None);
        var second = await store.PseudonymizeAsync(PseudonymKind.Sample, "BBMs:2022:3249:SD", CancellationToken.None);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// The acceptance criterion: a later run has to recognise the patient it already published, so
    /// the pseudonym has to come back from storage rather than being minted afresh.
    /// </summary>
    [Fact]
    public async Task AFreshStoreOverTheSameDatabaseReturnsTheStoredPseudonym()
    {
        var firstRun = await NewStore().PseudonymizeAsync(PseudonymKind.Patient, "271801", CancellationToken.None);
        var secondRun = await NewStore().PseudonymizeAsync(PseudonymKind.Patient, "271801", CancellationToken.None);

        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public async Task DifferentIdentifiersGetDifferentPseudonyms()
    {
        var store = NewStore();

        var one = await store.PseudonymizeAsync(PseudonymKind.Patient, "271801", CancellationToken.None);
        var other = await store.PseudonymizeAsync(PseudonymKind.Patient, "138423", CancellationToken.None);

        Assert.NotEqual(one, other);
    }

    /// <summary>
    /// A patient and a sample can carry the same string; they are separate identifier spaces and
    /// must not collapse onto one pseudonym.
    /// </summary>
    [Fact]
    public async Task ThePatientAndSampleSpacesDoNotCollide()
    {
        var store = NewStore();

        var patient = await store.PseudonymizeAsync(PseudonymKind.Patient, "247", CancellationToken.None);
        var sample = await store.PseudonymizeAsync(PseudonymKind.Sample, "247", CancellationToken.None);

        Assert.NotEqual(patient, sample);
        Assert.StartsWith("mmci_patient_", patient, StringComparison.Ordinal);
        Assert.StartsWith("mmci_sample_", sample, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePrefixIsWhateverTheBiobankIsConfiguredAs()
    {
        var store = new PseudonymStore(_db.NewContext(), TimeProvider.System, "fnol");

        var pseudonym = await store.PseudonymizeAsync(PseudonymKind.Patient, "271801", CancellationToken.None);

        Assert.StartsWith("fnol_patient_", pseudonym, StringComparison.Ordinal);
    }
}
