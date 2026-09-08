using Microsoft.EntityFrameworkCore;
using Uploader.Application.Abstractions;
using Uploader.Infrastructure.Persistence;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Pseudonymization;

/// <summary>
/// Mints a pseudonym the first time an identifier is seen and remembers it, so the same real id
/// always publishes under the same pseudonym and a second run updates a catalogue record rather than
/// duplicating it.
/// </summary>
/// <remarks>
/// The pseudonymizer's own mapping files are never opened. Its patient and sample tables cover only
/// the sequenced subset of the biobank, and nothing downstream references them; its predictive
/// numbers arrive already applied, on the sequencing API's responses. So this owns the identifier
/// spaces it mints and reads nothing from the mount.
/// </remarks>
internal sealed class PseudonymStore : IPseudonymMap
{
    private readonly UploaderDbContext _database;
    private readonly TimeProvider _timeProvider;
    private readonly string _prefix;

    public PseudonymStore(UploaderDbContext database, TimeProvider timeProvider, string prefix)
    {
        _database = database;
        _timeProvider = timeProvider;
        _prefix = prefix;
    }

    // ponytail: one lookup per identifier against localhost Postgres, no preloading and no cache
    // beyond EF's change tracker. The run already makes at least one HTTP call per patient, so this
    // is not the cost that matters. Batch or preload if a profile ever says otherwise.
    //
    // No locking either: the sync is a single-process console job and only one runs at a time. The
    // unique index on (kind, pseudonym) is what would catch it if that ever stopped being true.
    public async Task<string> PseudonymizeAsync(PseudonymKind kind, string realId, CancellationToken cancellationToken)
    {
        var kindName = Name(kind);

        var stored = await _database.Pseudonyms
            .FirstOrDefaultAsync(row => row.Kind == kindName && row.RealId == realId, cancellationToken);

        if (stored is not null)
        {
            return stored.Pseudonym;
        }

        var minted = $"{_prefix}_{kindName}_{Guid.NewGuid()}";
        _database.Pseudonyms.Add(new PseudonymEntity
        {
            Kind = kindName,
            RealId = realId,
            Pseudonym = minted,
            CreatedAt = _timeProvider.GetUtcNow(),
        });

        await _database.SaveChangesAsync(cancellationToken);
        return minted;
    }

    /// <summary>
    /// The word the pseudonym carries between prefix and uuid. Lower-cased to match the form the
    /// pseudonymizer produces (<c>mmci_patient_...</c>), which the derived FAIR identifiers rewrite.
    /// </summary>
    private static string Name(PseudonymKind kind) => kind switch
    {
        PseudonymKind.Patient => "patient",
        PseudonymKind.Sample => "sample",
        _ => throw new InvalidOperationException($"Unsupported pseudonym kind: {kind}"),
    };
}
