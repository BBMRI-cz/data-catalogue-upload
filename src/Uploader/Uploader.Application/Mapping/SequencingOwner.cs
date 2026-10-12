using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Mapping;

/// <summary>
/// Picks the sample a predictive number's sequencing hangs from. A predictive number belongs to a
/// case, not to a sample: every sample from one surgery carries it, tumour and healthy tissue and
/// serum alike. The catalogue links a sample preparation to exactly one biospecimen, and the biobank
/// does not record which sample was sequenced - the DNA most likely came from pathology's own block -
/// so the owner is a convention, chosen the same way on every run.
/// <para>
/// Tumour tissue first, because predictive testing sequences the tumour: malignant (<c>1</c>), then
/// metastasis (<c>2</c>), then their RNAlater counterparts (<c>53</c>, <c>55</c>), then any other
/// tissue, then serum, then genome. Ties go to the earliest collection, then to the lowest sample id,
/// so the owner moves only when the samples themselves change.
/// </para>
/// </summary>
public static class SequencingOwner
{
    public static SampleAggregate Choose(IEnumerable<(SampleDto Dto, SampleAggregate Sample)> candidates) =>
        candidates
            .OrderBy(candidate => Rank(candidate.Dto.Type, candidate.Dto.MaterialType))
            .ThenBy(candidate => candidate.Dto.CollectionYear ?? int.MaxValue)
            .ThenBy(candidate => candidate.Dto.EventNumber ?? int.MaxValue)
            .ThenBy(candidate => candidate.Sample.Id.Value, StringComparer.Ordinal)
            .First()
            .Sample;

    private static int Rank(string? sampleType, string? code) => (sampleType, code) switch
    {
        (BiobankMapping.TissueType, "1") => 0,
        (BiobankMapping.TissueType, "2") => 1,
        (BiobankMapping.TissueType, "53") => 2,
        (BiobankMapping.TissueType, "55") => 3,
        (BiobankMapping.TissueType, _) => 4,
        (BiobankMapping.SerumType, _) => 5,
        (BiobankMapping.GenomeType, _) => 6,
        _ => 7,
    };
}
