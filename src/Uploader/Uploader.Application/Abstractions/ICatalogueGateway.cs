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
