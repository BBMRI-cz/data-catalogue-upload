using Uploader.Domain;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

public sealed class FingerprintTests
{
    [Fact]
    public void IsDeterministicForEqualInput()
    {
        var a = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        var b = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        Assert.Equal(Fingerprint.Of(a), Fingerprint.Of(b));
    }

    [Fact]
    public void ChangesWhenInputChanges()
    {
        var a = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        var b = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1981 };
        Assert.NotEqual(Fingerprint.Of(a), Fingerprint.Of(b));
    }

    [Fact]
    public void IgnoresNullArguments()
    {
        var personal = new Personal { PersonalIdentifier = "P1" };
        Assert.Equal(Fingerprint.Of(personal), Fingerprint.Of(personal, null));
    }

    [Fact]
    public void IsOrderSensitiveAcrossArguments()
    {
        var personal = new Personal { PersonalIdentifier = "P1" };
        var clinical = new Clinical { ClinicalIdentifier = "C1" };
        Assert.NotEqual(Fingerprint.Of(personal, clinical), Fingerprint.Of(clinical, personal));
    }

    [Fact]
    public void ReturnsHexSha256()
    {
        var hash = Fingerprint.Of(new Personal { PersonalIdentifier = "P1" }).Value;
        Assert.Equal(64, hash.Length);
        Assert.All(hash, character => Assert.Contains(character, "0123456789abcdef"));
    }

    [Fact]
    public void PatientFingerprintReflectsContent()
    {
        var unchanged = PatientAggregate.Create("P1", new Personal { YearOfBirth = 1980 }, null).Value;
        var changed = PatientAggregate.Create("P1", new Personal { YearOfBirth = 1990 }, null).Value;
        Assert.NotEqual(unchanged.ComputeFingerprint(), changed.ComputeFingerprint());
    }
}
