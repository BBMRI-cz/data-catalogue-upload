using ErrorOr;
using Uploader.Application.Dtos;

namespace Uploader.Application.Abstractions;

/// <summary>
/// Writes and deletes rows in the data catalogue. Returns errors instead of throwing.
/// <para>
/// One method per aggregate, not per catalogue table: an aggregate becomes several rows across
/// several tables (a patient is a <c>Personal</c>, an <c>IndividualConsent</c> and a
/// <c>Clinical</c>), and those rows reference each other, so the order they are written in is the
/// gateway's business rather than the caller's. Deletes run that order backwards for the same
/// reason - the catalogue refuses to delete a row another row still points at.
/// </para>
/// <para>
/// The upserts take payloads, not aggregates: aggregates carry the real identifiers and a payload
/// carries the pseudonyms, so the type says which side of that line a value is on. There is no
/// method for WSI or imaging studies because no source fills them - see the sync handler, which
/// refuses to upload either rather than publish a real id.
/// </para>
/// </summary>
public interface ICatalogueGateway
{
    /// <summary>
    /// Upserts the one study every patient references. Called once at the start of a run, before
    /// any patient, because <c>Personal</c> cannot reference a study that is not there yet.
    /// </summary>
    Task<ErrorOr<string>> UpsertStudyAsync(StudyRecord study, CancellationToken cancellationToken);

    /// <summary>
    /// The live term list of each of <paramref name="fields"/>, read once at the start of a run so
    /// every value is resolved before its row is fingerprinted. The same read serves the gateway's own
    /// term check on the way out, which stays as a safety net for a value that reaches it without being
    /// resolved. Sharing one read means neither notices a term removed during the run: that row is
    /// rejected, and the next run's fresh lists leave the value out.
    /// <para>
    /// An error when a list cannot be read, or when a field is not an ontology column in the live
    /// schema: resolving against a list that is not there would leave every value of that field out,
    /// and a save replaces the whole row, so the published terms would be erased.
    /// </para>
    /// </summary>
    Task<ErrorOr<TermLists>> ReadTermListsAsync(
        IReadOnlyCollection<OntologyField> fields,
        CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertPatientAsync(CataloguePatientPayload payload, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSampleAsync(CatalogueSamplePayload payload, CancellationToken cancellationToken);

    Task<ErrorOr<string>> UpsertSequencingAsync(CatalogueSequencingPayload payload, CancellationToken cancellationToken);

    /// <summary>Deletes a patient's <c>Clinical</c>, <c>IndividualConsent</c> and <c>Personal</c> rows.</summary>
    Task<ErrorOr<Deleted>> DeletePatientAsync(string patientPseudonym, CancellationToken cancellationToken);

    /// <summary>Deletes a sample's <c>Biospecimens</c> and <c>Material</c> rows.</summary>
    Task<ErrorOr<Deleted>> DeleteSampleAsync(string samplePseudonym, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the <c>Analysis</c>, <c>Sequencing</c> and <c>SamplePreparation</c> rows hanging off
    /// a sample's biospecimen. Their keys come from the run tree rather than from anything the
    /// uploader stores, so the gateway asks the catalogue which rows exist before removing them.
    /// </summary>
    Task<ErrorOr<Deleted>> DeleteSequencingAsync(string samplePseudonym, CancellationToken cancellationToken);
}

/// <summary>
/// One ontology column the uploader fills, named the way the catalogue spells it: the EMX2 table id
/// and the column's GraphQL name, <c>Material.materialType</c>. It is what a term list is read for,
/// what the mapping table is keyed on and what an unmapped value is reported under, so all three name
/// the same column the same way. The fields themselves are declared in <c>OntologyFields</c>.
/// </summary>
public sealed record OntologyField(string Table, string Column)
{
    public override string ToString() => $"{Table}.{Column}";
}

/// <summary>
/// The live term list of each ontology field, read from the catalogue once at the start of a run. A
/// term is a foreign key in EMX2, so membership is exact: what the set says, character for character.
/// </summary>
public sealed class TermLists
{
    private readonly IReadOnlyDictionary<OntologyField, IReadOnlySet<string>> _lists;

    public TermLists(IReadOnlyDictionary<OntologyField, IReadOnlySet<string>> lists) => _lists = lists;

    /// <summary>
    /// Whether <paramref name="term"/> is a term of <paramref name="field"/>'s list. A run reads the
    /// list of every field the uploader fills before it resolves anything, so asking about one it did
    /// not read is a programmer error rather than an empty list.
    /// </summary>
    public bool Contains(OntologyField field, string term) =>
        _lists.TryGetValue(field, out var terms)
            ? terms.Contains(term)
            : throw new InvalidOperationException($"No term list was read for {field}.");
}
