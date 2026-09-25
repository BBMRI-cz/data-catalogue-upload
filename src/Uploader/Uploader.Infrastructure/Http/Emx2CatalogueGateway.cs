using System.Text.Json;
using System.Text.Json.Nodes;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;

namespace Uploader.Infrastructure.Http;

/// <summary>
/// MOLGENIS EMX2 catalogue gateway. Everything goes through one GraphQL endpoint,
/// <c>POST /{schema}/api/graphql</c>, authenticated with an <c>x-molgenis-token</c> header.
/// <para>
/// Three things about EMX2 shape this class, all of them recorded with a transcript in
/// <c>docs/catalogue-api-contract.md</c>: <c>save</c> is a true upsert but <b>replaces</b> the row
/// rather than merging into it, so a payload must always be complete; a mutation is transactional,
/// so one bad value costs the whole call; and an ontology reference is a plain foreign key, so a
/// term that is not already in the lookup table rejects the row. The last two are why
/// <see cref="Emx2Schema"/> exists.
/// </para>
/// </summary>
internal sealed class Emx2CatalogueGateway : ICatalogueGateway
{
    private readonly Emx2Client _client;
    private readonly Emx2Schema _schema;
    private readonly ILogger<Emx2CatalogueGateway> _logger;

    public Emx2CatalogueGateway(Emx2Client client, Emx2Schema schema, ILogger<Emx2CatalogueGateway> logger)
    {
        _client = client;
        _schema = schema;
        _logger = logger;
    }

    public Task<ErrorOr<string>> UpsertStudyAsync(StudyRecord study, CancellationToken cancellationToken) =>
        SaveAsync(study.Identifier, cancellationToken, (Emx2Tables.Study, study));

    public Task<ErrorOr<string>> UpsertPatientAsync(
        CataloguePatientPayload payload,
        CancellationToken cancellationToken) =>
        SaveAsync(
            payload.ExternalId,
            cancellationToken,
            (Emx2Tables.Personal, payload.Personal),
            (Emx2Tables.IndividualConsent, payload.Consent),
            (Emx2Tables.Clinical, payload.Clinical));

    public Task<ErrorOr<string>> UpsertSampleAsync(
        CatalogueSamplePayload payload,
        CancellationToken cancellationToken) =>
        SaveAsync(
            payload.ExternalId,
            cancellationToken,
            (Emx2Tables.Material, payload.Material),
            (Emx2Tables.Biospecimens, payload.Biospecimen));

    public async Task<ErrorOr<string>> UpsertSequencingAsync(
        CatalogueSequencingPayload payload,
        CancellationToken cancellationToken)
    {
        // The chain is three tables deep and every level references the one above, so it is written
        // level by level rather than row by row: all preparations, then all their sequencing rows,
        // then all analyses.
        var preparations = payload.SamplePreparations;
        var runs = preparations.Select(preparation => preparation.Sequencing).OfType<SequencingRecord>().ToList();
        var analyses = runs.SelectMany(run => run.Analyses).ToList();

        var saved = await SaveAsync(
            payload.ExternalId,
            cancellationToken,
            (Emx2Tables.SamplePreparation, preparations));
        if (saved.IsError)
        {
            return saved;
        }

        saved = await SaveAsync(payload.ExternalId, cancellationToken, (Emx2Tables.Sequencing, runs));
        if (saved.IsError)
        {
            return saved;
        }

        return await SaveAsync(payload.ExternalId, cancellationToken, (Emx2Tables.Analysis, analyses));
    }

    public Task<ErrorOr<Deleted>> DeletePatientAsync(
        string patientPseudonym,
        CancellationToken cancellationToken) =>
        DeleteAsync(
            cancellationToken,
            (Emx2Tables.Clinical, Emx2Tables.ClinicalKey, [BiobankMapping.ClinicalIdentifier(patientPseudonym) ?? string.Empty]),
            (Emx2Tables.IndividualConsent, Emx2Tables.ConsentKey, [BiobankMapping.ConsentIdentifier(patientPseudonym) ?? string.Empty]),
            (Emx2Tables.Personal, Emx2Tables.PersonalKey, [patientPseudonym]));

    public Task<ErrorOr<Deleted>> DeleteSampleAsync(
        string samplePseudonym,
        CancellationToken cancellationToken) =>
        DeleteAsync(
            cancellationToken,
            (Emx2Tables.Biospecimens, Emx2Tables.BiospecimenKey, [BiobankMapping.BiospecimenIdentifier(samplePseudonym) ?? string.Empty]),
            (Emx2Tables.Material, Emx2Tables.MaterialKey, [samplePseudonym]));

    public async Task<ErrorOr<Deleted>> DeleteSequencingAsync(
        string samplePseudonym,
        CancellationToken cancellationToken)
    {
        // Preparation, sequencing and analysis keys come from the run tree, not from anything the
        // uploader stores, so the only way to know which rows exist is to ask. One query walks the
        // biospecimen's REFBACK chain down to the analyses.
        var found = await _client.PostAsync(
            Emx2Queries.SequencingSubtree,
            new { key = BiobankMapping.BiospecimenIdentifier(samplePseudonym) },
            cancellationToken);
        if (found.IsError)
        {
            return found.Errors;
        }

        var subtree = Emx2Queries.ReadSequencingSubtree(found.Value);
        if (subtree.Preparations.Count == 0)
        {
            return Result.Deleted;
        }

        return await DeleteAsync(
            cancellationToken,
            (Emx2Tables.Analysis, Emx2Tables.AnalysisKey, subtree.Analyses),
            (Emx2Tables.Sequencing, Emx2Tables.SequencingKey, subtree.Runs),
            (Emx2Tables.SamplePreparation, Emx2Tables.PreparationKey, subtree.Preparations));
    }

    /// <summary>
    /// Saves each table in the order given, stopping at the first failure. A null or empty row set
    /// is skipped rather than sent - EMX2 rejects an empty <c>save</c>.
    /// </summary>
    private async Task<ErrorOr<string>> SaveAsync(
        string externalId,
        CancellationToken cancellationToken,
        params (string Table, object? Rows)[] tables)
    {
        foreach (var (table, rows) in tables)
        {
            var payload = ToRowArray(rows);
            if (payload.Count == 0)
            {
                continue;
            }

            var prepared = await _schema.PrepareAsync(table, payload, _logger, cancellationToken);
            if (prepared.IsError)
            {
                return prepared.Errors;
            }

            var saved = await MutateAsync(
                $"mutation Save($rows:[{table}Input]){{ save({table}:$rows){{ status message }} }}",
                new { rows = payload },
                "save",
                cancellationToken);
            if (saved.IsError)
            {
                return saved.Errors;
            }
        }

        // EMX2 assigns no identifier: the key we sent is the key the row has. Recording it is what
        // lets the next run find the same row again.
        return externalId;
    }

    private async Task<ErrorOr<Deleted>> DeleteAsync(
        CancellationToken cancellationToken,
        params (string Table, string KeyColumn, IReadOnlyList<string> Keys)[] tables)
    {
        foreach (var (table, keyColumn, keys) in tables)
        {
            var rows = keys
                .Where(key => !string.IsNullOrEmpty(key))
                .Select(key => new Dictionary<string, object?> { [keyColumn] = key })
                .ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            var deleted = await MutateAsync(
                $"mutation Del($rows:[{table}Input]){{ delete({table}:$rows){{ status message }} }}",
                new { rows },
                "delete",
                cancellationToken);
            if (deleted.IsError)
            {
                return deleted.Errors;
            }
        }

        return Result.Deleted;
    }

    /// <summary>Rows are always sent as an array, whether the caller had one record or a list.</summary>
    private static List<JsonNode> ToRowArray(object? rows)
    {
        if (rows is null)
        {
            return [];
        }

        var serialized = JsonSerializer.SerializeToNode(rows, rows.GetType(), Emx2Client.PayloadOptions);
        return serialized switch
        {
            JsonArray array => [.. array.Where(node => node is not null).Select(node => node!.DeepClone())],
            JsonNode node => [node.DeepClone()],
            _ => [],
        };
    }

    private async Task<ErrorOr<string>> MutateAsync(
        string document,
        object variables,
        string field,
        CancellationToken cancellationToken)
    {
        var answered = await _client.PostAsync(document, variables, cancellationToken);
        if (answered.IsError)
        {
            return answered.Errors;
        }

        // A mutation can answer 200 with a status of ERROR, so the body decides, not the status code.
        var status = answered.Value?[field]?["status"]?.GetValue<string>();
        if (!string.Equals(status, "SUCCESS", StringComparison.Ordinal))
        {
            var message = answered.Value?[field]?["message"]?.GetValue<string>() ?? "no message";
            return Error.Failure("Catalogue.Rejected", $"{field} was not accepted: {message}");
        }

        return string.Empty;
    }

}
