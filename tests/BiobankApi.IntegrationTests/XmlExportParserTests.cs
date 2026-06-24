using BiobankApi.Infrastructure.Xml;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// End-to-end tests for <see cref="XmlExportParser"/> over the dummy exports in
/// <c>TestData/Exports</c> (copied next to the test assembly). Covers the schema categories plus an
/// invalid record and a malformed file, asserting that bad records are reported, not dropped.
/// </summary>
public sealed class XmlExportParserTests
{
    private static readonly string ExportsPath = Path.Combine(AppContext.BaseDirectory, "TestData", "Exports");

    [Fact]
    public void ParsesEveryValidCategoryInOrdinalFileOrder()
    {
        var result = new XmlExportParser(ExportsPath).ParsePatients();

        Assert.Equal(
            ["271801", "247", "138423", "170096", "463988", "173254"],
            result.Patients.Select(patient => patient.Id.Value));
    }

    [Fact]
    public void ReportsInvalidAndMalformedRecordsWithoutDroppingThem()
    {
        var result = new XmlExportParser(ExportsPath).ParsePatients();

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Reference == "07_invalid_record.xml");
        Assert.Contains(result.Errors, error => error.Reference == "08_malformed.xml");
        Assert.All(result.Errors, error => Assert.StartsWith("xml:", error.Source));

        // The invalid record never leaks into the parsed patients.
        Assert.DoesNotContain(result.Patients, patient => patient.Id.Value == "999001");
    }

    [Fact]
    public void ParsesTheFullPatientTree()
    {
        var result = new XmlExportParser(ExportsPath).ParsePatients();

        var patient = Assert.Single(result.Patients, candidate => candidate.Id.Value == "463988");
        Assert.Equal(3, patient.Samples.Count);
        var specimen = Assert.Single(patient.DiagnosticSpecimens);
        Assert.Equal("&:2023:40063", specimen.Id.Value);
    }

    [Fact]
    public void MissingDirectoryYieldsEmptyResult()
    {
        var result = new XmlExportParser(Path.Combine(ExportsPath, "does-not-exist")).ParsePatients();

        Assert.Empty(result.Patients);
        Assert.Empty(result.Errors);
    }
}
