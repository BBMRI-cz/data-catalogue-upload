using Uploader.Application.Mapping;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The three derived values in the biobank -> domain mapping. Everything else is carried verbatim,
/// so this covers all of the mapping's logic.
/// </summary>
public sealed class BiobankMappingTests
{
    [Theory]
    [InlineData("C504", "C50.4")]      // four characters: the last one is the subcategory
    [InlineData("C50", "C50")]         // three: nothing to separate
    [InlineData(" C774 ", "C77.4")]    // trimmed first
    [InlineData("", null)]
    [InlineData(null, null)]
    public void DiagnosisAppliesTheIcd10DotRule(string? code, string? expected) =>
        Assert.Equal(expected, BiobankMapping.Diagnosis(code));

    [Theory]
    [InlineData("247", "clinical_247")]
    [InlineData("mmci_patient_abc", "mmci_clinical_abc")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ClinicalIdentifierIsDerivedFromThePatientId(string? patientId, string? expected) =>
        Assert.Equal(expected, BiobankMapping.ClinicalIdentifier(patientId));

    [Fact]
    public void AgeUsesTheBirthMonthWhenTheExportCarriesOne()
    {
        var event2020 = new DateTime(2020, 1, 2);

        // Born June 1980: the 40th birthday is still months away in January 2020.
        Assert.Equal(39, BiobankMapping.AgeInYears(1980, 6, event2020));

        // No month recorded: January is assumed, so the birthday has already passed.
        Assert.Equal(40, BiobankMapping.AgeInYears(1980, null, event2020));
    }

    [Fact]
    public void AgeIsNullWithoutABirthYear() =>
        Assert.Null(BiobankMapping.AgeInYears(null, 6, new DateTime(2020, 1, 2)));

    [Fact]
    public void AgeIsNullWithoutAnEventDate() =>
        Assert.Null(BiobankMapping.AgeInYears(1980, 6, null));

    [Fact]
    public void AgeIsNullWhenTheEventPrecedesTheBirth() =>
        Assert.Null(BiobankMapping.AgeInYears(1980, 1, new DateTime(1975, 1, 1)));

    [Theory]
    [InlineData("tissue", true)]
    [InlineData("TISSUE", true)]
    [InlineData("serum", false)]
    [InlineData(null, false)]
    public void TissueIsRecognisedCaseInsensitively(string? type, bool expected) =>
        Assert.Equal(expected, BiobankMapping.IsTissue(type));
}
