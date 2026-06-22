using Uploader.Domain;
using Uploader.Domain.Services;
using Xunit;

namespace Uploader.UnitTests;

public sealed class FingerprintCalculatorTests
{
    private readonly FingerprintCalculator _calculator = new();

    [Fact]
    public void IsDeterministicForEqualInput()
    {
        var a = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        var b = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        Assert.Equal(_calculator.Compute(a), _calculator.Compute(b));
    }

    [Fact]
    public void ChangesWhenInputChanges()
    {
        var a = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1980 };
        var b = new Personal { PersonalIdentifier = "P1", YearOfBirth = 1981 };
        Assert.NotEqual(_calculator.Compute(a), _calculator.Compute(b));
    }

    [Fact]
    public void IgnoresNullArguments()
    {
        var personal = new Personal { PersonalIdentifier = "P1" };
        Assert.Equal(_calculator.Compute(personal), _calculator.Compute(personal, null));
    }

    [Fact]
    public void IsOrderSensitiveAcrossArguments()
    {
        var personal = new Personal { PersonalIdentifier = "P1" };
        var clinical = new Clinical { ClinicalIdentifier = "C1" };
        Assert.NotEqual(_calculator.Compute(personal, clinical), _calculator.Compute(clinical, personal));
    }

    [Fact]
    public void ReturnsHexSha256()
    {
        var hash = _calculator.Compute(new Personal { PersonalIdentifier = "P1" });
        Assert.Equal(64, hash.Length);
        Assert.All(hash, character => Assert.Contains(character, "0123456789abcdef"));
    }
}
