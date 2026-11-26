using Uploader.Application.Abstractions;
using Uploader.Application.Features.Ontology;
using Uploader.Application.Mapping;
using Uploader.Host.Endpoints;
using Uploader.Host.Mapping;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The mapper copies each entry by hand into the exchange format, so a dropped field or a context key
/// carried into the wrong slot is not a compile error - it is the student receiving the wrong input.
/// </summary>
public sealed class UnmappedValuesResponseMapperTests
{
    private static readonly DateTimeOffset FirstSeen = new(2026, 11, 23, 2, 0, 4, TimeSpan.Zero);

    [Fact]
    public void CarriesEveryFieldOfEachEntryThroughInOrder()
    {
        var response = UnmappedValuesResponseMapper.ToResponse(new GetUnmappedValuesQueryResult(
        [
            new UnmappedListEntry(
                OntologyFields.StorageConditions, "PS", TermContext.Material("genome", "Whole blood"), 412, FirstSeen),
            new UnmappedListEntry(
                OntologyFields.LibraryPreparationKit, "KAPA HyperPlus Kit", TermContext.Empty, 2075, FirstSeen.AddDays(2)),
        ]));

        Assert.Equal(
            [
                new UnmappedValueResponse(
                    "Biospecimens.storageConditions",
                    "PS",
                    new UnmappedContextResponse(SampleType: "genome", Label: "Whole blood"),
                    412,
                    FirstSeen),
                new UnmappedValueResponse(
                    "SamplePreparation.libraryPreparationKit",
                    "KAPA HyperPlus Kit",
                    new UnmappedContextResponse(SampleType: null, Label: null),
                    2075,
                    FirstSeen.AddDays(2)),
            ],
            response);
    }
}
