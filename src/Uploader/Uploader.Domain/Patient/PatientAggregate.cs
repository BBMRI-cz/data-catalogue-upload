using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Patient aggregate root: the identity the sync planner keeps a patient's catalogue rows under, and
/// the consent that decides whether they are published at all. The rows themselves are built straight
/// from the biobank's record, outside the domain, so the planner holds the sync rules rather than a
/// second copy of the catalogue row. Its samples and imaging studies are separate aggregates linked
/// back to it by <see cref="PatientId"/>.
/// </summary>
public sealed class PatientAggregate : AggregateRoot<PatientId>
{
    private PatientAggregate()
    {
    }

    /// <summary>
    /// Whether the patient consented to their data being shared. Permission to upload, enforced by
    /// <see cref="PatientCatalogueData.IsUploadEligible"/>.
    /// </summary>
    public bool HasConsent { get; private init; }

    public static ErrorOr<PatientAggregate> Create(string? id, bool hasConsent = false)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error.Validation("Patient.Id", "Patient id is required.");
        }

        return new PatientAggregate
        {
            Id = new PatientId(id),
            HasConsent = hasConsent,
        };
    }
}
