using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

public sealed class UploaderMapperTests
{
    [Fact]
    public void MapsPersonalClinicalAndMaterial()
    {
        var patient = PatientMapper.ToPatient(new PatientDto
        {
            PatientId = "P1",
            Consent = true,
            Sex = "male",
            BirthYear = 1980,
            Samples =
            [
                new SampleDto { SampleId = "S1", Type = "tissue", Diagnosis = "C504" },
                new SampleDto { SampleId = "S2", Type = "serum", Diagnosis = "C51" },
            ],
        }).Value;
        var sample = SampleMapper.ToSample(
            new SampleDto { SampleId = "S1", Type = "tissue", MaterialType = "1" },
            new PatientId("P1"),
            biobank: "MOU").Value;

        Assert.Equal("P1", patient.Personal!.PersonalIdentifier);
        Assert.Equal("male", patient.Personal.GenderAtBirth);
        Assert.Equal(1980, patient.Personal.YearOfBirth);
        Assert.True(patient.HasConsent);
        Assert.Equal(["C50.4", "C51"], patient.Clinical!.ClinicalDiagnosis);
        Assert.Equal("clinical_P1", patient.Clinical.ClinicalIdentifier);
        Assert.Equal("S1", sample.Material!.MaterialIdentifier);
        Assert.Equal("1", sample.Material.BiospecimenType);
        Assert.Equal("MOU", sample.Material.PhysicalLocation);
    }

    [Fact]
    public void TissueTakesCutAndFreezeTimesWhileOthersTakeTheTakingDate()
    {
        var tissue = SampleMapper.ToSample(
            new SampleDto
            {
                SampleId = "S1",
                Type = "tissue",
                CutTime = new DateTime(2020, 1, 2, 3, 4, 5),
                FreezeTime = new DateTime(2020, 1, 2, 4, 0, 0),
                TakingDate = new DateTime(1999, 1, 1),
            },
            new PatientId("P1")).Value;

        var serum = SampleMapper.ToSample(
            new SampleDto { SampleId = "S2", Type = "serum", TakingDate = new DateTime(2021, 5, 6, 7, 8, 9) },
            new PatientId("P1")).Value;

        Assert.Equal("2020-01-02T03:04:05", tissue.Material!.SamplingTimestamp);
        Assert.Equal("2020-01-02T04:00:00", tissue.Material.RegistrationTimestamp);
        Assert.Equal("2021-05-06T07:08:09", serum.Material!.SamplingTimestamp);
        Assert.Equal("2021-05-06T07:08:09", serum.Material.RegistrationTimestamp);
    }

    [Fact]
    public void GenomeDiagnosisIsLeftOutWhileSpecimenDiagnosisIsCarried()
    {
        var patient = PatientMapper.ToPatient(new PatientDto
        {
            PatientId = "P1",
            Samples =
            [
                new SampleDto { SampleId = "S1", Type = "genome", Diagnosis = "C999" },
                new SampleDto { SampleId = "S2", Type = "tissue", Diagnosis = "C504" },
                new SampleDto { SampleId = "S3", Type = "serum", Diagnosis = "C504" },
            ],
            DiagnosticSpecimens = [new SpecimenDto { SpecimenId = "D1", Diagnosis = "C777" }],
        }).Value;

        // Duplicates collapse and the order is fixed, so the fingerprint doesn't move with the payload.
        Assert.Equal(["C50.4", "C77.7"], patient.Clinical!.ClinicalDiagnosis);
    }

    [Fact]
    public void AgeAtDiagnosisComesFromTheEarliestSampleEvent()
    {
        var patient = PatientMapper.ToPatient(new PatientDto
        {
            PatientId = "P1",
            BirthYear = 1980,
            BirthMonth = 6,
            Samples =
            [
                new SampleDto { SampleId = "S1", Type = "serum", TakingDate = new DateTime(2021, 5, 6) },
                new SampleDto
                {
                    SampleId = "S2",
                    Type = "tissue",
                    FreezeTime = new DateTime(2020, 1, 2),
                    CutTime = new DateTime(2020, 1, 2),
                },
            ],
        }).Value;

        // Earliest event is 2020-01-02; born 1980-06-01, so the 40th birthday has not happened yet.
        Assert.Equal(39, patient.Clinical!.AgeAtDiagnosis);
    }

    [Fact]
    public void BlankPatientIdIsAValidationError()
    {
        var result = PatientMapper.ToPatient(new PatientDto { PatientId = "" });

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void SampleCarriesTypedReferences()
    {
        var sample = SampleMapper.ToSample(
            new SampleDto { SampleId = "S1", PredictiveNumber = "PRED1", Biopsy = "2023/2872-1" },
            new PatientId("P1")).Value;

        Assert.Equal(new SampleId("S1"), sample.Id);
        Assert.Equal(new PatientId("P1"), sample.PatientId);
        Assert.Equal(new SequencingId("PRED1"), sample.SequencingId);

        // The biobank serves no bioptic number; the WSI link waits for #31.
        Assert.Null(sample.WsiId);
    }

    // Sequencing lives in SequencingMapperTests: its source serves a nested list rather than one
    // record, so its cardinality and drops need a file of their own.

    [Fact]
    public void WsiReturnsNullFixedBlockWhenNoKeys()
    {
        var wsi = WsiMapper.ToWsi(new WsiDto(), new WsiId("BIO1"), new SampleId("S1")).Value;

        Assert.Equal(new WsiId("BIO1"), wsi.Id);
        Assert.Null(wsi.FixedBlock);
    }

    [Fact]
    public void WsiBuildsNestedPipeline()
    {
        var dto = new WsiDto
        {
            BlockIdentifier = "FB1",
            SlideContainer = new SlideContainerDto
            {
                ContainerType = "glass",
                SlidePreparationAssay = new SlidePreparationAssayDto
                {
                    StainingMethod = "H&E",
                    WholeSlideImaging = new WholeSlideImagingDto { ImagingDevice = "Scanner" },
                },
            },
        };

        var wsi = WsiMapper.ToWsi(dto, new WsiId("BIO1"), new SampleId("S1")).Value;

        Assert.Equal(new FixedBlockId("FB1"), wsi.FixedBlock!.Id);
        Assert.Equal("glass", wsi.FixedBlock.SlideContainer!.ContainerType);
        Assert.Equal("H&E", wsi.FixedBlock.SlideContainer.SlidePreparationAssay!.StainingMethod);
        Assert.Equal("Scanner", wsi.FixedBlock.SlideContainer.SlidePreparationAssay.WholeSlideImaging!.ImagingDevice);
    }

    [Fact]
    public void ImagingStudyMapsCtSeriesOnly()
    {
        var dto = new ImagingStudyDto
        {
            AccessionNumber = "ACC1",
            ImagingModality = ["CT"],
            CtSeries = new CtSeriesDto { SeriesIdentifier = "CT-1", TubeVoltageKvp = 120 },
        };

        var study = ImagingStudyMapper.ToImagingStudy(dto, new PatientId("P1")).Value;

        Assert.Equal(new AccessionNumber("ACC1"), study.Id);
        Assert.Equal(new PatientId("P1"), study.PatientId);
        Assert.Equal(["CT"], study.ImagingModality);
        Assert.NotNull(study.CtSeries);
        Assert.Equal("CT-1", study.CtSeries!.SeriesIdentifier);
        Assert.Equal(120, study.CtSeries.TubeVoltageKvp);
        Assert.Null(study.MrSeries);
    }
}
