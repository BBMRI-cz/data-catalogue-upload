using BiobankApi.Domain;
using BiobankApi.Infrastructure.Xml;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// Unit-level checks for the XML source-format decoder. It lives here (not in the unit-test
/// project) because <see cref="XmlValueReader"/> is internal to the infrastructure assembly.
/// </summary>
public sealed class XmlValueReaderTests
{
    [Theory]
    [InlineData("  MOU ", "MOU")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void CleanText(string? raw, string? expected) => Assert.Equal(expected, XmlValueReader.CleanText(raw));

    [Fact]
    public void CleanCodePreservesCase()
    {
        Assert.Equal("C504", XmlValueReader.CleanCode(" C504 "));
        Assert.Equal("8500/32", XmlValueReader.CleanCode("8500/32"));
    }

    [Fact]
    public void CleanSampleIdKeepsStsAmpersandPrefix()
    {
        Assert.Equal("BBM:2023:181:1", XmlValueReader.CleanSampleId(" BBM:2023:181:1 "));
        Assert.Equal("&:2022:118485", XmlValueReader.CleanSampleId("&:2022:118485"));
    }

    [Fact]
    public void CleanAccessionsStripsAndDropsEmpty()
    {
        Assert.Equal(["RDG1", "RDG2"], XmlValueReader.CleanAccessions([" RDG1 ", "", "RDG2"]));
        Assert.Empty(XmlValueReader.CleanAccessions(null));
        Assert.Empty(XmlValueReader.CleanAccessions([]));
    }

    [Theory]
    [InlineData("-", null)]
    [InlineData("2023/2872-1", "2023/2872-1")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void DashToNone(string? raw, string? expected) => Assert.Equal(expected, XmlValueReader.DashToNone(raw));

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseConsent(string? raw, bool? expected) =>
        Assert.Equal(expected, XmlValueReader.ParseConsent(raw).Value);

    [Fact]
    public void ParseConsentRejectsUnknown() => Assert.True(XmlValueReader.ParseConsent("maybe").IsError);

    [Fact]
    public void ParseSex()
    {
        Assert.Equal(Sex.Female, XmlValueReader.ParseSex("female").Value);
        Assert.Equal(Sex.Male, XmlValueReader.ParseSex("MALE").Value);
        Assert.Null(XmlValueReader.ParseSex("").Value);
        Assert.True(XmlValueReader.ParseSex("other").IsError);
    }

    [Fact]
    public void ParseRetrieved()
    {
        Assert.Equal(Retrieved.Operational, XmlValueReader.ParseRetrieved("operational").Value);
        Assert.Equal(Retrieved.Unknown, XmlValueReader.ParseRetrieved("unknown").Value);
        Assert.Null(XmlValueReader.ParseRetrieved(null).Value);
        Assert.True(XmlValueReader.ParseRetrieved("nope").IsError);
    }

    [Fact]
    public void ParseInt()
    {
        Assert.Equal(524, XmlValueReader.ParseInt(" 524 ").Value);
        Assert.Null(XmlValueReader.ParseInt("").Value);
        Assert.True(XmlValueReader.ParseInt("x").IsError);
    }

    [Fact]
    public void ParseYear() => Assert.Equal(2023, XmlValueReader.ParseYear("2023").Value);

    [Theory]
    [InlineData("--07", 7)]
    [InlineData("7", 7)]
    [InlineData("--12", 12)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseMonth(string? raw, int? expected) => Assert.Equal(expected, XmlValueReader.ParseMonth(raw).Value);

    [Fact]
    public void ParseTemporalDateTime() =>
        Assert.Equal(new DateTime(2023, 3, 24, 11, 15, 0), XmlValueReader.ParseTemporal("2023-03-24T11:15:00").Value);

    [Fact]
    public void ParseTemporalDateOnlyBecomesMidnight() =>
        Assert.Equal(new DateTime(2023, 3, 24, 0, 0, 0), XmlValueReader.ParseTemporal("2023-03-24").Value);

    [Fact]
    public void ParseTemporalEmpty() => Assert.Null(XmlValueReader.ParseTemporal("").Value);
}
