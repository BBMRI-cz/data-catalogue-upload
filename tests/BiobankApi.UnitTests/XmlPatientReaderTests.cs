using System.Xml.Linq;
using BiobankApi.Domain;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Xml;
using Xunit;

namespace BiobankApi.UnitTests;

/// <summary>
/// Unit tests for the pure (no-IO) XML-to-domain mapping. Each case feeds an inline
/// <c>&lt;patient&gt;</c> element so the parsing logic is exercised without touching the filesystem.
/// </summary>
public sealed class XmlPatientReaderTests
{
    private const string Ns = "xmlns=\"http://www.bbmri.cz/schemas/biobank/data\"";

    private static PatientAggregate Read(string patientXml)
    {
        var result = XmlPatientReader.Read(XElement.Parse(patientXml));
        Assert.False(result.IsError, result.IsError ? result.FirstError.Description : string.Empty);
        return result.Value;
    }

    [Fact]
    public void ConsentFalseStubParsesDemographicsOnly()
    {
        var patient = Read($"<patient {Ns} biobank=\"MOU\" consent=\"false\" id=\"271801\" month=\"--07\" sex=\"male\" year=\"1948\"/>");

        Assert.Equal("271801", patient.Id.Value);
        Assert.Equal("MOU", patient.Biobank);
        Assert.False(patient.Consent);
        Assert.Equal(Sex.Male, patient.Sex);
        Assert.Equal(1948, patient.BirthYear);
        Assert.Equal(7, patient.BirthMonth);
        Assert.Empty(patient.AccessionNumbers);
        Assert.Empty(patient.Samples);
        Assert.Empty(patient.DiagnosticSpecimens);
    }

    [Fact]
    public void EmptyLtsWithStsParsesDiagnosticSpecimen()
    {
        var patient = Read($$"""
            <patient {{Ns}} biobank="MOU" consent="true" id="247" month="--02" sex="female" year="1957">
              <LTS/>
              <STS>
                <diagnosisMaterial number="118485" sampleId="&amp;:2022:118485" year="2022">
                  <materialType>S</materialType>
                  <diagnosis>C504</diagnosis>
                  <takingDate>2022-09-20T10:44:00</takingDate>
                  <retrieved>unknown</retrieved>
                </diagnosisMaterial>
              </STS>
            </patient>
            """);

        Assert.Empty(patient.Samples);
        var specimen = Assert.Single(patient.DiagnosticSpecimens);
        Assert.Equal("&:2022:118485", specimen.Id.Value);
        Assert.Equal(118485, specimen.SpecimenNumber);
        Assert.Equal(2022, specimen.Year);
        Assert.Equal("S", specimen.MaterialType);
        Assert.Equal("C504", specimen.Diagnosis);
        Assert.Equal(new DateTime(2022, 9, 20, 10, 44, 0), specimen.TakingDate);
        Assert.Equal(Retrieved.Unknown, specimen.Retrieved);
    }

    [Fact]
    public void NonEmptyLtsParsesEachSampleType()
    {
        var patient = Read($$"""
            <patient {{Ns}} biobank="MOU" consent="true" id="463988" month="--10" sex="female" year="1977">
              <LTS>
                <genome biopsy="-" number="249" predictive_number="-" sampleId="BBMd:2023:249:PK" year="2023">
                  <samplesNo>1</samplesNo>
                  <availableSamplesNo>1</availableSamplesNo>
                  <materialType>PK</materialType>
                  <takingDate>2023-03-24</takingDate>
                  <retrieved>unknown</retrieved>
                </genome>
                <serum biopsy="2023/2872-1" number="524" predictive_number="2023/1052" sampleId="BBMs:2023:524:K" year="2023">
                  <samplesNo>1</samplesNo>
                  <availableSamplesNo>1</availableSamplesNo>
                  <materialType>K</materialType>
                  <diagnosis>C56</diagnosis>
                  <takingDate>2023-03-24</takingDate>
                </serum>
                <tissue biopsy="2023/2872-1" number="181" predictive_number="2023/1052" sampleId="BBM:2023:181:1" year="2023">
                  <samplesNo>3</samplesNo>
                  <availableSamplesNo>3</availableSamplesNo>
                  <materialType>1</materialType>
                  <pTNM>T1N0M</pTNM>
                  <morphology>8380/31</morphology>
                  <diagnosis>C56</diagnosis>
                  <cutTime>2023-03-24T11:15:00</cutTime>
                  <freezeTime>2023-03-24T11:20:00</freezeTime>
                  <retrieved>operational</retrieved>
                </tissue>
              </LTS>
            </patient>
            """);

        Assert.Equal(3, patient.Samples.Count);
        Assert.Single(patient.Samples.OfType<GenomeSample>());
        Assert.Single(patient.Samples.OfType<SerumSample>());

        var tissue = Assert.Single(patient.Samples.OfType<TissueSample>());
        Assert.Equal("BBM:2023:181:1", tissue.Id.Value);
        Assert.Equal(181, tissue.EventNumber);
        Assert.Equal(2023, tissue.CollectionYear);
        Assert.Equal("2023/2872-1", tissue.Biopsy);
        Assert.Equal("2023/1052", tissue.PredictiveNumber);
        Assert.Equal("T1N0M", tissue.PTnm);
        Assert.Equal("8380/31", tissue.Morphology);
        Assert.Equal("C56", tissue.Diagnosis);
        Assert.Equal(new DateTime(2023, 3, 24, 11, 15, 0), tissue.CutTime);
        Assert.Equal(new DateTime(2023, 3, 24, 11, 20, 0), tissue.FreezeTime);
        Assert.Equal(Retrieved.Operational, tissue.Retrieved);
    }

    [Fact]
    public void ReadsPatientAccessionsAndToleratesEmptySampleAccessions()
    {
        var patient = Read($$"""
            <patient {{Ns}} biobank="MOU" consent="true" id="170096" month="--09" sex="female" year="1965">
              <AccessionNumbers>
                <Number>RDG2004019834</Number>
              </AccessionNumbers>
              <LTS>
                <genome biopsy="-" number="1075" predictive_number="-" sampleId="BBMd:2025:1075:PS" year="2025">
                  <AccessionNumbers/>
                  <samplesNo>1</samplesNo>
                  <availableSamplesNo>1</availableSamplesNo>
                  <materialType>PS</materialType>
                  <takingDate>2025-07-29</takingDate>
                  <retrieved>unknown</retrieved>
                </genome>
              </LTS>
            </patient>
            """);

        Assert.Equal(["RDG2004019834"], patient.AccessionNumbers);
        var genome = Assert.Single(patient.Samples.OfType<GenomeSample>());
        Assert.Empty(genome.AccessionNumbers);
    }

    [Fact]
    public void OptionalMonthMayBeAbsent()
    {
        var patient = Read($"<patient {Ns} biobank=\"MOU\" consent=\"true\" id=\"500\" sex=\"male\" year=\"1970\"><LTS/></patient>");

        Assert.Null(patient.BirthMonth);
        Assert.Equal(1970, patient.BirthYear);
    }

    [Fact]
    public void InvalidSampleFailsWholePatient()
    {
        // freeze_time precedes cut_time -> the tissue, and therefore the whole patient, is invalid.
        var result = XmlPatientReader.Read(XElement.Parse($$"""
            <patient {{Ns}} biobank="MOU" consent="true" id="999001" sex="female" year="1980">
              <LTS>
                <tissue biopsy="-" number="500" predictive_number="-" sampleId="BBM:2023:500:1" year="2023">
                  <samplesNo>1</samplesNo>
                  <availableSamplesNo>1</availableSamplesNo>
                  <materialType>1</materialType>
                  <cutTime>2023-03-24T11:20:00</cutTime>
                  <freezeTime>2023-03-24T11:15:00</freezeTime>
                  <retrieved>operational</retrieved>
                </tissue>
              </LTS>
            </patient>
            """));

        Assert.True(result.IsError);
    }

    [Fact]
    public void InvalidLexicalValueFailsWholePatient()
    {
        var result = XmlPatientReader.Read(XElement.Parse(
            $"<patient {Ns} biobank=\"MOU\" consent=\"true\" id=\"123\" sex=\"other\" year=\"1980\"><LTS/></patient>"));

        Assert.True(result.IsError);
    }

    [Fact]
    public void OutOfRangeBirthYearFailsWholePatient()
    {
        var result = XmlPatientReader.Read(XElement.Parse(
            $"<patient {Ns} biobank=\"MOU\" consent=\"true\" id=\"123\" sex=\"male\" year=\"1850\"><LTS/></patient>"));

        Assert.True(result.IsError);
    }
}
