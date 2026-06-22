using BiobankApi.Domain;
using BiobankApi.Domain.Common;
using BiobankApi.Domain.Patients;
using Xunit;

namespace BiobankApi.UnitTests;

public sealed class DomainModelsTests
{
    // --- construction + material-type labels -----------------------------------------

    [Fact]
    public void TissueSampleMaterialTypeLabel()
    {
        var sample = new TissueSample("BBM:2023:181:53", "53");
        Assert.Equal("Malignant tumour (RNAlater)", sample.MaterialTypeLabel);
    }

    [Fact]
    public void UnknownMaterialTypeLabelIsNull()
    {
        Assert.Null(new TissueSample("BBM:2023:181:1", "999").MaterialTypeLabel);
        Assert.Null(MaterialTypes.Label(MaterialTypes.Tissue, "999"));
        Assert.Null(MaterialTypes.Label(MaterialTypes.Serum, null));
    }

    [Fact]
    public void SerumSampleResolvesLabel()
    {
        var sample = new SerumSample("BBMs:2022:3249:SD", "SD");
        Assert.Equal("Serum, nitrogen-stored", sample.MaterialTypeLabel);
    }

    [Fact]
    public void DiagnosticSpecimenResolvesLabel()
    {
        var specimen = new DiagnosticSpecimen("&:2022:118485", materialType: "S");
        Assert.Equal("Serum / surgical specimen", specimen.MaterialTypeLabel);
    }

    // --- minimal construction --------------------------------------------------------

    [Fact]
    public void PatientMinimalConstructionStaysValid()
    {
        var patient = new Patient("P1");
        Assert.Empty(patient.Samples);
        Assert.Empty(patient.DiagnosticSpecimens);
    }

    // --- invariants ------------------------------------------------------------------

    [Fact]
    public void PatientRejectsEmptyId() =>
        Assert.Throws<DomainException>(() => new Patient("  "));

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void PatientRejectsOutOfRangeMonth(int birthMonth) =>
        Assert.Throws<DomainException>(() => new Patient("P1", birthMonth: birthMonth));

    [Theory]
    [InlineData(1850)]
    [InlineData(2200)]
    public void PatientRejectsOutOfRangeYear(int birthYear) =>
        Assert.Throws<DomainException>(() => new Patient("P1", birthYear: birthYear));

    [Fact]
    public void PatientWithoutConsentMustNotCarryData()
    {
        var serum = new SerumSample("BBMs:2022:3249:SD", "SD");
        Assert.Throws<DomainException>(() => new Patient("P1", consent: false, samples: [serum]));
        Assert.Throws<DomainException>(() => new Patient("P1", consent: false, accessionNumbers: ["RDG1"]));
    }

    [Fact]
    public void SampleRejectsEmptyRequiredFields()
    {
        Assert.Throws<DomainException>(() => new TissueSample("", "1"));
        Assert.Throws<DomainException>(() => new TissueSample("BBM:2023:181:1", ""));
    }

    [Fact]
    public void SampleRejectsAvailableExceedingTotal() =>
        Assert.Throws<DomainException>(() =>
            new SerumSample("BBMs:2022:3249:SD", "SD", samplesNo: 1, availableSamplesNo: 2));

    [Fact]
    public void SampleRejectsNegativeCounts() =>
        Assert.Throws<DomainException>(() => new SerumSample("BBMs:2022:3249:SD", "SD", samplesNo: -1));

    [Fact]
    public void TissueRejectsFreezeBeforeCut() =>
        Assert.Throws<DomainException>(() => new TissueSample(
            "BBM:2023:181:1",
            "1",
            cutTime: new DateTime(2023, 3, 24, 11, 20, 0),
            freezeTime: new DateTime(2023, 3, 24, 11, 15, 0)));

    [Fact]
    public void TissueAllowsFreezeAfterCut()
    {
        var sample = new TissueSample(
            "BBM:2023:181:1",
            "1",
            cutTime: new DateTime(2023, 3, 24, 0, 0, 0),
            freezeTime: new DateTime(2023, 3, 24, 11, 20, 0));
        Assert.Equal(new DateTime(2023, 3, 24, 11, 20, 0), sample.FreezeTime);
    }

    [Fact]
    public void DiagnosticSpecimenRejectsEmptyId() =>
        Assert.Throws<DomainException>(() => new DiagnosticSpecimen(""));

    [Fact]
    public void DiagnosticSpecimenRejectsOutOfRangeYear() =>
        Assert.Throws<DomainException>(() => new DiagnosticSpecimen("&:2022:118485", year: 1800));
}
