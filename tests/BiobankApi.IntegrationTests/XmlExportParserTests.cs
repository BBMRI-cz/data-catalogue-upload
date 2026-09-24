using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain;
using BiobankApi.Infrastructure.Xml;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// End-to-end tests for <see cref="XmlExportParser"/> over the dummy exports in
/// <c>TestData/Exports</c> (copied next to the test assembly). Covers the schema categories plus an
/// invalid record and a malformed file, asserting that bad records are reported, not dropped. The
/// merge cases write one patient's successive weekly files to a temp dir instead (<c>ParseFiles</c>).
/// </summary>
public sealed class XmlExportParserTests
{
    private static readonly string ExportsPath = Path.Join(AppContext.BaseDirectory, "TestData", "Exports");

    [Fact]
    public void ParsesEveryValidCategoryInOrdinalFileOrder()
    {
        var result = new XmlExportParser(ExportsPath).ParsePatients().Value;

        Assert.Equal(
            ["271801", "247", "138423", "170096", "463988", "173254"],
            result.Patients.Select(patient => patient.Id.Value));
    }

    [Fact]
    public void ReportsInvalidAndMalformedRecordsWithoutDroppingThem()
    {
        var result = new XmlExportParser(ExportsPath).ParsePatients().Value;

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
        var result = new XmlExportParser(ExportsPath).ParsePatients().Value;

        var patient = Assert.Single(result.Patients, candidate => candidate.Id.Value == "463988");
        Assert.Equal(3, patient.Samples.Count);
        var specimen = Assert.Single(patient.DiagnosticSpecimens);
        Assert.Equal("&:2023:40063", specimen.Id.Value);
    }

    [Fact]
    public void MissingDirectoryReportsFailure()
    {
        var result = new XmlExportParser(Path.Join(ExportsPath, "does-not-exist")).ParsePatients();

        Assert.True(result.IsError);
        Assert.Equal("Export.DirectoryMissing", result.FirstError.Code);
    }

    [Fact]
    public void DiscoversUppercaseXmlExtension()
    {
        // Server exports are uppercase `.XML`; the glob must match them (case-sensitive on Linux).
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.Copy(Path.Join(ExportsPath, "05_lts_full.xml"), Path.Join(dir, "PATIENT.XML"));

            var result = new XmlExportParser(dir).ParsePatients();

            Assert.False(result.IsError);
            Assert.Single(result.Value.Patients);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void KeepsSamplesThatAgeOutOfLaterFiles()
    {
        // The export lists only the last ~60 days of samples: the newer file no longer names the serum.
        var result = ParseFiles(
            ("BBM230301230001-000001.XML", Patient("true", Lts(Serum("BBMs:2023:900:K", "-", "2023/9001")) + Sts(Specimen("&:2023:9001")))),
            ("BBM230601230001-000001.XML", Patient("true", "<LTS/>" + Sts(Specimen("&:2023:9002")))));

        var patient = Assert.Single(result.Patients);
        var sample = Assert.Single(patient.Samples);
        Assert.Equal("2023/9001", sample.PredictiveNumber);
        Assert.Equal(["&:2023:9001", "&:2023:9002"], patient.DiagnosticSpecimens.Select(specimen => specimen.Id.Value));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void NewerListingOfASampleReplacesAllItsOlderRows()
    {
        // Biopsy is filled in later, and one sampleId expands into a row per biopsy: the newer rows
        // replace the older one as a set, so no stale biopsy-less row survives beside them.
        var result = ParseFiles(
            ("BBM230301230001-000001.XML", Patient("true", Lts(Serum("BBMs:2023:900:K", "-", "-")))),
            ("BBM230308230001-000001.XML", Patient("true", Lts(
                Serum("BBMs:2023:900:K", "2023/90-1", "2023/9001") + Serum("BBMs:2023:900:K", "2023/90-2", "2023/9001")))));

        var patient = Assert.Single(result.Patients);
        Assert.Equal(["2023/90-1", "2023/90-2"], patient.Samples.Select(sample => sample.Biopsy));
        Assert.All(patient.Samples, sample => Assert.Equal("BBMs:2023:900:K", sample.Id.Value));
    }

    [Fact]
    public void PatientFieldsComeFromTheNewestFile()
    {
        var result = ParseFiles(
            ("BBM230301230001-000001.XML", Patient("true", Accessions("A1") + Lts(Serum("BBMs:2023:900:K", "-", "-")), sex: "male", year: 1960)),
            ("BBM230601230001-000001.XML", Patient("true", Accessions("A1", "A2") + "<LTS/>", sex: "female", year: 1961)));

        var patient = Assert.Single(result.Patients);
        Assert.Equal(Sex.Female, patient.Sex);
        Assert.Equal(1961, patient.BirthYear);
        Assert.Equal(["A1", "A2"], patient.AccessionNumbers);
        Assert.Single(patient.Samples);
    }

    [Fact]
    public void NewestConsentFalseDropsEarlierSamples()
    {
        var result = ParseFiles(
            ("BBM230301230001-000001.XML", Patient("true", Accessions("A1") + Lts(Serum("BBMs:2023:900:K", "-", "2023/9001")) + Sts(Specimen("&:2023:9001")))),
            ("BBM230601230001-000001.XML", Patient("false", string.Empty)));

        var patient = Assert.Single(result.Patients);
        Assert.False(patient.Consent);
        Assert.Empty(patient.Samples);
        Assert.Empty(patient.DiagnosticSpecimens);
        Assert.Empty(patient.AccessionNumbers);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void InvalidNewerFileKeepsTheLastValidHeader()
    {
        var result = ParseFiles(
            ("BBM230301230001-000001.XML", Patient("true", Lts(Serum("BBMs:2023:900:K", "-", "-")), year: 1960)),
            ("BBM230601230001-000001.XML", Patient("true", "<LTS/>", year: 1697)));

        var patient = Assert.Single(result.Patients);
        Assert.Equal(1960, patient.BirthYear);
        Assert.Single(patient.Samples);
        var error = Assert.Single(result.Errors);
        Assert.Equal("BBM230601230001-000001.XML", error.Reference);
    }

    [Fact]
    public void EmptyDirectoryReportsFailure()
    {
        var emptyDir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var result = new XmlExportParser(emptyDir).ParsePatients();

            Assert.True(result.IsError);
            Assert.Equal("Export.NoFiles", result.FirstError.Code);
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }

    // Writes one patient's successive weekly exports to a temp dir and parses them together.
    private static ExportParseResult ParseFiles(params (string Name, string Xml)[] files)
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            foreach (var (name, xml) in files)
            {
                File.WriteAllText(Path.Join(dir, name), xml);
            }

            return new XmlExportParser(dir).ParsePatients().Value;
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string Patient(string consent, string body, string sex = "female", int year = 1960) =>
        $"""
        <patient biobank="MOU" consent="{consent}" id="900001" month="--05" sex="{sex}" year="{year}"
          xmlns="http://www.bbmri.cz/schemas/biobank/data">{body}</patient>
        """;

    private static string Accessions(params string[] numbers) =>
        $"<AccessionNumbers>{string.Concat(numbers.Select(number => $"<Number>{number}</Number>"))}</AccessionNumbers>";

    private static string Lts(string samples) => $"<LTS>{samples}</LTS>";

    private static string Sts(string specimens) => $"<STS>{specimens}</STS>";

    private static string Serum(string sampleId, string biopsy, string predictiveNumber) =>
        $"""
        <serum biopsy="{biopsy}" number="900" predictive_number="{predictiveNumber}" sampleId="{sampleId}" year="2023">
          <samplesNo>1</samplesNo><availableSamplesNo>1</availableSamplesNo><materialType>K</materialType>
        </serum>
        """;

    private static string Specimen(string sampleId) =>
        $"""
        <diagnosisMaterial number="{sampleId[^4..]}" sampleId="{sampleId.Replace("&", "&amp;", StringComparison.Ordinal)}" year="2023">
          <materialType>S</materialType>
        </diagnosisMaterial>
        """;
}
