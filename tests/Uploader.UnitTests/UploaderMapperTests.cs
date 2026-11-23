using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

public sealed class UploaderMapperTests
{
    // Patient and sample rows live in CataloguePatientMapperTests and CatalogueSampleMapperTests, and
    // sequencing in CatalogueSequencingMapperTests: its source serves a nested list rather than one
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
