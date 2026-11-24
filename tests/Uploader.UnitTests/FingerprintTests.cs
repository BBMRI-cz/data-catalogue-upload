using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

public sealed class FingerprintTests
{
    [Fact]
    public void IsDeterministicForEqualInput()
    {
        var a = new PersonalRecord { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        var b = new PersonalRecord { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        Assert.Equal(Fingerprint.Of(a), Fingerprint.Of(b));
    }

    [Fact]
    public void ChangesWhenInputChanges()
    {
        var a = new PersonalRecord { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        var b = new PersonalRecord { PersonalIdentifier = "P1", YearOfBirth = 1981 };
        Assert.NotEqual(Fingerprint.Of(a), Fingerprint.Of(b));
    }

    [Fact]
    public void IgnoresNullArguments()
    {
        var personal = new PersonalRecord { PersonalIdentifier = "P1" };
        Assert.Equal(Fingerprint.Of(personal), Fingerprint.Of(personal, null));
    }

    [Fact]
    public void IsOrderSensitiveAcrossArguments()
    {
        var personal = new PersonalRecord { PersonalIdentifier = "P1" };
        var clinical = new ClinicalRecord { ClinicalIdentifier = "C1" };
        Assert.NotEqual(Fingerprint.Of(personal, clinical), Fingerprint.Of(clinical, personal));
    }

    [Fact]
    public void ReturnsHexSha256()
    {
        var hash = Fingerprint.Of(new PersonalRecord { PersonalIdentifier = "P1" }).Value;
        Assert.Equal(64, hash.Length);
        Assert.All(hash, character => Assert.Contains(character, "0123456789abcdef"));
    }

    [Fact]
    public void PatientRowFingerprintReflectsContent()
    {
        var unchanged = new PatientDto { PatientId = "P1", BirthYear = 1980 };
        var changed = unchanged with { BirthYear = 1990 };

        Assert.NotEqual(
            Fingerprint.Of(CataloguePatientMapper.ToPayload(unchanged, "mmci_patient_P1", "study", LiveTerms.Resolver(), [])),
            Fingerprint.Of(CataloguePatientMapper.ToPayload(changed, "mmci_patient_P1", "study", LiveTerms.Resolver(), [])));
    }

    [Fact]
    public void SequencingRowFingerprintReflectsEveryPreparation()
    {
        // A sample resequenced on a second run must read as changed, not as the same record: the
        // preparation list is what change detection has to see.
        SequencingRunDto first = new() { RunId = "R1" }, second = new() { RunId = "R2" };
        SequencingDto Sequenced(params SequencingRunDto[] runs) =>
            new() { Samples = [new SequencingSampleDto { SampleId = "p0001", Runs = runs }] };

        Assert.NotEqual(
            Fingerprint.Of(CatalogueSequencingMapper.ToPayload(Sequenced(first), "mmci_sample_S1")),
            Fingerprint.Of(CatalogueSequencingMapper.ToPayload(Sequenced(first, second), "mmci_sample_S1")));
    }
}
