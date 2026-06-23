using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

public sealed class SourceMapperTests
{
    private readonly SourceMapper _mapper = new();

    [Fact]
    public void MapsPersonalClinicalAndMaterial()
    {
        var patient = _mapper.ToPatient(new PatientDto
        {
            PatientId = "P1",
            PersonalIdentifier = "P1",
            YearOfBirth = 1980,
            ClinicalDiagnosis = ["C50", "C51"],
        }).Value;
        var sample = _mapper.ToSample(
            new SampleDto { SampleId = "S1", PercentageTumorCells = 12.5, BiospecimenType = "tissue" },
            new PatientId("P1")).Value;

        Assert.Equal("P1", patient.Personal!.PersonalIdentifier);
        Assert.Equal(1980, patient.Personal.YearOfBirth);
        Assert.Equal(["C50", "C51"], patient.Clinical!.ClinicalDiagnosis);
        Assert.Equal(12.5, sample.Material!.PercentageTumorCells);
        Assert.Equal("tissue", sample.Material.BiospecimenType);
    }

    [Fact]
    public void BlankPatientIdIsAValidationError()
    {
        var result = _mapper.ToPatient(new PatientDto { PatientId = "" });

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void SampleCarriesTypedReferences()
    {
        var sample = _mapper.ToSample(
            new SampleDto { SampleId = "S1", PredictiveNumber = "PRED1", BiopticNumber = "BIO1" },
            new PatientId("P1")).Value;

        Assert.Equal(new SampleId("S1"), sample.Id);
        Assert.Equal(new PatientId("P1"), sample.PatientId);
        Assert.Equal(new SequencingId("PRED1"), sample.SequencingId);
        Assert.Equal(new WsiId("BIO1"), sample.WsiId);
    }

    [Fact]
    public void SequencingEntryHasNoPrepWhenAbsent()
    {
        var sequencing = _mapper.ToSequencing(new SequencingDto(), new SequencingId("PRED1"), new SampleId("S1")).Value;

        var entry = Assert.Single(sequencing.Entries);
        Assert.Null(entry.SamplePreparation);
        Assert.Null(entry.FixedBlockId);
    }

    [Fact]
    public void SequencingBuildsNestedPipeline()
    {
        var dto = new SequencingDto
        {
            FixedBlock = new FixedBlockRefDto { BlockIdentifier = "FB1" },
            SamplePreparation = new SamplePreparationDto
            {
                LibraryPreparationKit = "KitA",
                Sequencing = new SequencingRunDto
                {
                    SequencingPlatform = "Illumina",
                    Analyses = [new AnalysisDto { AnalysisIdentifier = "A1" }],
                },
            },
        };

        var entry = Assert.Single(_mapper.ToSequencing(dto, new SequencingId("PRED1"), new SampleId("S1")).Value.Entries);

        Assert.Equal(new FixedBlockId("FB1"), entry.FixedBlockId);
        Assert.Equal("KitA", entry.SamplePreparation!.LibraryPreparationKit);
        Assert.Equal("Illumina", entry.SamplePreparation.Sequencing!.SequencingPlatform);
        Assert.Equal("A1", entry.SamplePreparation.Sequencing.Analysis!.AnalysisIdentifier);
    }

    [Fact]
    public void WsiReturnsNullFixedBlockWhenNoKeys()
    {
        var wsi = _mapper.ToWsi(new WsiDto(), new WsiId("BIO1"), new SampleId("S1")).Value;

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

        var wsi = _mapper.ToWsi(dto, new WsiId("BIO1"), new SampleId("S1")).Value;

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

        var study = _mapper.ToImagingStudy(dto, new PatientId("P1")).Value;

        Assert.Equal(new AccessionNumber("ACC1"), study.Id);
        Assert.Equal(new PatientId("P1"), study.PatientId);
        Assert.Equal(["CT"], study.ImagingModality);
        Assert.NotNull(study.CtSeries);
        Assert.Equal("CT-1", study.CtSeries!.SeriesIdentifier);
        Assert.Equal(120, study.CtSeries.TubeVoltageKvp);
        Assert.Null(study.MrSeries);
    }
}
