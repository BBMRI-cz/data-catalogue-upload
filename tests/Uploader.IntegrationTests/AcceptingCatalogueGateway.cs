using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;

namespace Uploader.IntegrationTests;

/// <summary>A catalogue that accepts everything, so the host tests never leave the process.</summary>
internal sealed class AcceptingCatalogueGateway : ICatalogueGateway
{
    public Task<ErrorOr<string>> UpsertStudyAsync(StudyRecord study, CancellationToken cancellationToken) =>
        Accepted(study.Identifier);

    public Task<ErrorOr<string>> UpsertPatientAsync(CataloguePatientPayload payload, CancellationToken cancellationToken) =>
        Accepted(payload.ExternalId);

    public Task<ErrorOr<string>> UpsertSampleAsync(CatalogueSamplePayload payload, CancellationToken cancellationToken) =>
        Accepted(payload.ExternalId);

    public Task<ErrorOr<string>> UpsertSequencingAsync(CatalogueSequencingPayload payload, CancellationToken cancellationToken) =>
        Accepted(payload.SampleId);

    public Task<ErrorOr<Deleted>> DeletePatientAsync(string patientPseudonym, CancellationToken cancellationToken) =>
        Task.FromResult<ErrorOr<Deleted>>(Result.Deleted);

    public Task<ErrorOr<Deleted>> DeleteSampleAsync(string samplePseudonym, CancellationToken cancellationToken) =>
        Task.FromResult<ErrorOr<Deleted>>(Result.Deleted);

    public Task<ErrorOr<Deleted>> DeleteSequencingAsync(string samplePseudonym, CancellationToken cancellationToken) =>
        Task.FromResult<ErrorOr<Deleted>>(Result.Deleted);

    private static Task<ErrorOr<string>> Accepted(string id) => Task.FromResult<ErrorOr<string>>(id);
}
