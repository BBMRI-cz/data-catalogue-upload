using Uploader.Application.Mapping;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The derived values in the biobank -> catalogue mapping: identifiers and the age. The ICD-10 dot rule
/// is a mapping-table rule now, covered in <see cref="RuleMappingTableTests"/>.
/// </summary>
public sealed class BiobankMappingTests
{
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
