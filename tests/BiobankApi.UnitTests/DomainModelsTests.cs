using BiobankApi.Domain.Patients;
using ErrorOr;
using Xunit;

namespace BiobankApi.UnitTests;

public sealed class DomainModelsTests
{
    // --- construction + material-type labels -----------------------------------------

    [Fact]
    public void TissueSampleMaterialTypeLabel()
    {
        var sample = TissueSample.Create("BBM:2023:181:53", "53").Value;
        Assert.Equal("Malignant tumour (RNAlater)", sample.MaterialTypeLabel);
    }

    [Fact]
    public void UnknownMaterialTypeLabelIsNull()
    {
        Assert.Null(TissueSample.Create("BBM:2023:181:1", "999").Value.MaterialTypeLabel);
        Assert.Null(MaterialTypes.Label(MaterialTypes.Tissue, "999"));
        Assert.Null(MaterialTypes.Label(MaterialTypes.Serum, null));
    }

    [Fact]
    public void SerumSampleResolvesLabel()
    {
        var sample = SerumSample.Create("BBMs:2022:3249:SD", "SD").Value;
        Assert.Equal("Serum, nitrogen-stored", sample.MaterialTypeLabel);
    }

    [Fact]
    public void DiagnosticSpecimenResolvesLabel()
    {
        var specimen = DiagnosticSpecimen.Create("&:2022:118485", materialType: "S").Value;
        Assert.Equal("Serum / surgical specimen", specimen.MaterialTypeLabel);
    }

    // --- minimal construction --------------------------------------------------------

    [Fact]
    public void PatientMinimalConstructionStaysValid()
    {
        var patient = PatientAggregate.Create("P1").Value;
        Assert.Equal("P1", patient.Id.Value);
        Assert.Empty(patient.Samples);
        Assert.Empty(patient.DiagnosticSpecimens);
    }

    // --- invariants surface as ErrorOr validation errors -----------------------------

    [Fact]
    public void PatientRejectsEmptyId() => AssertValidationError(PatientAggregate.Create("  "));

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void PatientRejectsOutOfRangeMonth(int birthMonth) =>
        AssertValidationError(PatientAggregate.Create("P1", birthMonth: birthMonth));

    [Theory]
    [InlineData(1850)]
    [InlineData(2200)]
    public void PatientRejectsOutOfRangeYear(int birthYear) =>
        AssertValidationError(PatientAggregate.Create("P1", birthYear: birthYear));

    [Fact]
    public void PatientWithoutConsentMustNotCarryData()
    {
        var serum = SerumSample.Create("BBMs:2022:3249:SD", "SD").Value;
        AssertValidationError(PatientAggregate.Create("P1", consent: false, samples: [serum]));
        AssertValidationError(PatientAggregate.Create("P1", consent: false, accessionNumbers: ["RDG1"]));
    }

    [Fact]
    public void SampleRejectsEmptyRequiredFields()
    {
        AssertValidationError(TissueSample.Create("", "1"));
        AssertValidationError(TissueSample.Create("BBM:2023:181:1", ""));
    }

    [Fact]
    public void SampleRejectsAvailableExceedingTotal() =>
        AssertValidationError(SerumSample.Create("BBMs:2022:3249:SD", "SD", samplesNo: 1, availableSamplesNo: 2));

    [Fact]
    public void SampleRejectsNegativeCounts() =>
        AssertValidationError(SerumSample.Create("BBMs:2022:3249:SD", "SD", samplesNo: -1));

    [Fact]
    public void TissueRejectsFreezeBeforeCut() =>
        AssertValidationError(TissueSample.Create(
            "BBM:2023:181:1",
            "1",
            cutTime: new DateTime(2023, 3, 24, 11, 20, 0),
            freezeTime: new DateTime(2023, 3, 24, 11, 15, 0)));

    [Fact]
    public void TissueAllowsFreezeAfterCut()
    {
        var sample = TissueSample.Create(
            "BBM:2023:181:1",
            "1",
            cutTime: new DateTime(2023, 3, 24, 0, 0, 0),
            freezeTime: new DateTime(2023, 3, 24, 11, 20, 0)).Value;
        Assert.Equal(new DateTime(2023, 3, 24, 11, 20, 0), sample.FreezeTime);
    }

    [Fact]
    public void DiagnosticSpecimenRejectsEmptyId() => AssertValidationError(DiagnosticSpecimen.Create(""));

    [Fact]
    public void DiagnosticSpecimenRejectsOutOfRangeYear() =>
        AssertValidationError(DiagnosticSpecimen.Create("&:2022:118485", year: 1800));

    private static void AssertValidationError<T>(ErrorOr<T> result)
    {
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }
}
