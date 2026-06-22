using BiobankApi.Domain;
using BiobankApi.Domain.Common;
using BiobankApi.Domain.Services;
using Xunit;

namespace BiobankApi.UnitTests;

public sealed class CleaningServiceTests
{
    private readonly BiobankCleaningService _cleaning = new();

    [Theory]
    [InlineData("  MOU ", "MOU")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void CleanText(string? raw, string? expected) => Assert.Equal(expected, _cleaning.CleanText(raw));

    [Fact]
    public void CleanCodePreservesCase()
    {
        Assert.Equal("C504", _cleaning.CleanCode(" C504 "));
        Assert.Equal("8500/32", _cleaning.CleanCode("8500/32"));
    }

    [Fact]
    public void CleanSampleIdKeepsStsAmpersandPrefix()
    {
        Assert.Equal("BBM:2023:181:1", _cleaning.CleanSampleId(" BBM:2023:181:1 "));
        Assert.Equal("&:2022:118485", _cleaning.CleanSampleId("&:2022:118485"));
    }

    [Fact]
    public void CleanAccessionsStripsAndDropsEmpty()
    {
        Assert.Equal(["RDG1", "RDG2"], _cleaning.CleanAccessions([" RDG1 ", "", "RDG2"]));
        Assert.Empty(_cleaning.CleanAccessions(null));
        Assert.Empty(_cleaning.CleanAccessions([]));
    }

    [Theory]
    [InlineData("-", null)]
    [InlineData("2023/2872-1", "2023/2872-1")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void DashToNone(string? raw, string? expected) => Assert.Equal(expected, _cleaning.DashToNone(raw));

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseConsent(string? raw, bool? expected) => Assert.Equal(expected, _cleaning.ParseConsent(raw));

    [Fact]
    public void ParseConsentRejectsUnknown() =>
        Assert.Throws<DomainException>(() => _cleaning.ParseConsent("maybe"));

    [Fact]
    public void ParseSex()
    {
        Assert.Equal(Sex.Female, _cleaning.ParseSex("female"));
        Assert.Equal(Sex.Male, _cleaning.ParseSex("MALE"));
        Assert.Null(_cleaning.ParseSex(""));
        Assert.Throws<DomainException>(() => _cleaning.ParseSex("other"));
    }

    [Fact]
    public void ParseRetrieved()
    {
        Assert.Equal(Retrieved.Operational, _cleaning.ParseRetrieved("operational"));
        Assert.Equal(Retrieved.Unknown, _cleaning.ParseRetrieved("unknown"));
        Assert.Null(_cleaning.ParseRetrieved(null));
        Assert.Throws<DomainException>(() => _cleaning.ParseRetrieved("nope"));
    }

    [Fact]
    public void ParseInt()
    {
        Assert.Equal(524, _cleaning.ParseInt(" 524 "));
        Assert.Null(_cleaning.ParseInt(""));
        Assert.Throws<FormatException>(() => _cleaning.ParseInt("x"));
    }

    [Fact]
    public void ParseYear() => Assert.Equal(2023, _cleaning.ParseYear("2023"));

    [Theory]
    [InlineData("--07", 7)]
    [InlineData("7", 7)]
    [InlineData("--12", 12)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseMonth(string? raw, int? expected) => Assert.Equal(expected, _cleaning.ParseMonth(raw));

    [Fact]
    public void ParseTemporalDateTime() =>
        Assert.Equal(new DateTime(2023, 3, 24, 11, 15, 0), _cleaning.ParseTemporal("2023-03-24T11:15:00"));

    [Fact]
    public void ParseTemporalDateOnlyBecomesMidnight() =>
        Assert.Equal(new DateTime(2023, 3, 24, 0, 0, 0), _cleaning.ParseTemporal("2023-03-24"));

    [Fact]
    public void ParseTemporalEmpty() => Assert.Null(_cleaning.ParseTemporal(""));
}
