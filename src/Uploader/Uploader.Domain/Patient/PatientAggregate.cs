using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Patient aggregate root: the FAIR Genomes <c>Personal</c> plus <c>Clinical</c> data for one
/// patient. Its samples and imaging studies are separate aggregates linked back to it by
/// <see cref="PatientId"/>.
/// </summary>
public sealed class PatientAggregate : AggregateRoot<PatientId>
{
    private PatientAggregate()
    {
    }

    public Personal? Personal { get; private init; }
    public Clinical? Clinical { get; private init; }

    /// <summary>
    /// Whether the patient consented to their data being shared. Permission to upload, enforced by
    /// <see cref="PatientCatalogueData.IsUploadEligible"/>.
    /// </summary>
    public bool HasConsent { get; private init; }

    public static ErrorOr<PatientAggregate> Create(
        string? id,
        Personal? personal,
        Clinical? clinical,
        bool hasConsent = false)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error.Validation("Patient.Id", "Patient id is required.");
        }

        return new PatientAggregate
        {
            Id = new PatientId(id),
            Personal = personal,
            Clinical = clinical,
            HasConsent = hasConsent,
        };
    }
}
