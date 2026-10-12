using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// Which of a case's samples its sequencing hangs from. The choice is a convention, so these pin it:
/// tumour tissue first, then down the order, with ties broken the same way every run.
/// </summary>
public sealed class SequencingOwnerTests
{
    [Fact]
    public void TheMalignantTumourOwnsTheSequencingOfItsCase()
    {
        var owner = SequencingOwner.Choose(
        [
            Candidate("SERUM", "serum", "K"),
            Candidate("HEALTHY", "tissue", "4"),
            Candidate("HEALTHY_RNALATER", "tissue", "54"),
            Candidate("TUMOUR_RNALATER", "tissue", "53"),
            Candidate("TUMOUR", "tissue", "1"),
        ]);

        Assert.Equal("TUMOUR", owner.Id.Value);
    }

    /// <summary>Each pair is one step down the order: the first is chosen over the second.</summary>
    [Theory]
    [InlineData("tissue", "1", "tissue", "2")]
    [InlineData("tissue", "2", "tissue", "53")]
    [InlineData("tissue", "53", "tissue", "55")]
    [InlineData("tissue", "55", "tissue", "4")]
    [InlineData("tissue", "4", "serum", "K")]
    [InlineData("serum", "SD", "genome", "gD")]
    [InlineData("genome", "PK", null, null)]
    public void OneStepDownTheOrder(string? preferredType, string? preferredCode, string? otherType, string? otherCode)
    {
        // The preferred sample comes second and has the higher id, so neither decides it.
        var owner = SequencingOwner.Choose(
        [
            Candidate("A", otherType, otherCode),
            Candidate("B", preferredType, preferredCode),
        ]);

        Assert.Equal("B", owner.Id.Value);
    }

    [Fact]
    public void ACaseWithoutTissueFallsBackToSerum()
    {
        var owner = SequencingOwner.Choose(
        [
            Candidate("GENOME", "genome", "PK"),
            Candidate("SERUM", "serum", "SD"),
        ]);

        Assert.Equal("SERUM", owner.Id.Value);
    }

    [Fact]
    public void TheEarliestCollectionWinsATie()
    {
        var owner = SequencingOwner.Choose(
        [
            Candidate("A", "tissue", "1", year: 2024, eventNumber: 1),
            Candidate("B", "tissue", "1", year: 2023, eventNumber: 900),
            Candidate("C", "tissue", "1"),
        ]);

        Assert.Equal("B", owner.Id.Value);
    }

    [Fact]
    public void TheEarlierEventWinsWithinAYear()
    {
        var owner = SequencingOwner.Choose(
        [
            Candidate("A", "tissue", "1", year: 2023, eventNumber: 524),
            Candidate("B", "tissue", "1", year: 2023, eventNumber: 181),
        ]);

        Assert.Equal("B", owner.Id.Value);
    }

    [Fact]
    public void TheLowestSampleIdSettlesAnExactTie()
    {
        var owner = SequencingOwner.Choose(
        [
            Candidate("BBM:2023:181:54", "tissue", "1", year: 2023, eventNumber: 181),
            Candidate("BBM:2023:181:1", "tissue", "1", year: 2023, eventNumber: 181),
        ]);

        Assert.Equal("BBM:2023:181:1", owner.Id.Value);
    }

    private static (SampleDto Dto, SampleAggregate Sample) Candidate(
        string sampleId,
        string? type,
        string? code,
        int? year = null,
        int? eventNumber = null)
    {
        var dto = new SampleDto
        {
            SampleId = sampleId,
            Type = type,
            MaterialType = code,
            CollectionYear = year,
            EventNumber = eventNumber,
            PredictiveNumber = "2023/1052",
        };
        return (dto, SampleMapper.ToSample(dto, new PatientId("P1")).Value);
    }
}
