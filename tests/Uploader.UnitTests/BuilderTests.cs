using System.Text.Json.Nodes;
using Uploader.Application.Builders;
using Xunit;

namespace Uploader.UnitTests;

public sealed class BuilderTests
{
    private static JsonObject Obj(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void ClinicalBuilderMapsPersonalAndClinicalAndMaterial()
    {
        var payload = Obj("""
        {
          "personal_identifier": "P1",
          "year_of_birth": 1980,
          "clinical_diagnosis": ["C50", "C51"],
          "percentage_tumor_cells": 12.5,
          "biospecimen_type": "tissue"
        }
        """);
        var builder = new ClinicalBuilder();

        var personal = builder.BuildPersonal(payload);
        var clinical = builder.BuildClinical(payload);
        var material = builder.BuildMaterial(payload);

        Assert.Equal("P1", personal.PersonalIdentifier);
        Assert.Equal(1980, personal.YearOfBirth);
        Assert.Equal(["C50", "C51"], clinical.ClinicalDiagnosis);
        Assert.Equal(12.5, material.PercentageTumorCells);
        Assert.Equal("tissue", material.BiospecimenType);
    }

    [Fact]
    public void SequencingBuilderReturnsEntryWithoutPrepWhenNoKnownKeys()
    {
        var payload = Obj("""{ "id": "SEQ1" }""");

        var entries = new SequencingBuilder().BuildSequencingData("PRED1", payload);

        var entry = Assert.Single(entries);
        Assert.Equal("PRED1", entry.PredictiveNumber);
        Assert.Equal("SEQ1", entry.SourceId);
        Assert.Null(entry.SamplePreparation);
    }

    [Fact]
    public void SequencingBuilderBuildsNestedPipeline()
    {
        var payload = Obj("""
        {
          "sample_preparation": {
            "library_preparation_kit": "KitA",
            "sequencing": {
              "sequencing_platform": "Illumina",
              "analysis": [{ "analysis_identifier": "A1" }]
            }
          }
        }
        """);

        var entry = Assert.Single(new SequencingBuilder().BuildSequencingData("PRED1", payload));

        Assert.Equal("KitA", entry.SamplePreparation!.LibraryPreparationKit);
        Assert.Equal("Illumina", entry.SamplePreparation.Sequencing!.SequencingPlatform);
        Assert.Equal("A1", entry.SamplePreparation.Sequencing.Analysis!.AnalysisIdentifier);
    }

    [Fact]
    public void WsiBuilderReturnsNullFixedBlockWhenNoKnownKeys()
    {
        var payload = Obj("""{ "id": "WSI1" }""");

        var wsi = new WsiBuilder().BuildWsi("BIO1", payload);

        Assert.Equal("BIO1", wsi.BiopticNumber);
        Assert.Null(wsi.FixedBlock);
    }

    [Fact]
    public void WsiBuilderBuildsNestedPipeline()
    {
        var payload = Obj("""
        {
          "block_identifier": "FB1",
          "slide_container": {
            "container_type": "glass",
            "slide_preparation_assay": {
              "staining_method": "H&E",
              "whole_slide_imaging": { "imaging_device": "Scanner" }
            }
          }
        }
        """);

        var wsi = new WsiBuilder().BuildWsi("BIO1", payload);

        Assert.Equal("FB1", wsi.FixedBlock!.BlockIdentifier);
        Assert.Equal("glass", wsi.FixedBlock.SlideContainer!.ContainerType);
        Assert.Equal("H&E", wsi.FixedBlock.SlideContainer.SlidePreparationAssay!.StainingMethod);
        Assert.Equal("Scanner", wsi.FixedBlock.SlideContainer.SlidePreparationAssay.WholeSlideImaging!.ImagingDevice);
    }

    [Fact]
    public void RadiologyBuilderSelectsCtSeriesOnly()
    {
        var payload = Obj("""
        {
          "accession_number": "ACC1",
          "imaging_modality": ["CT"],
          "ct_series": { "series_identifier": "CT-1", "tube_voltage_kvp": 120 }
        }
        """);

        var study = new RadiologyBuilder().BuildImagingStudy(payload);

        Assert.Equal("ACC1", study.AccessionNumber);
        Assert.Equal(["CT"], study.ImagingModality);
        Assert.NotNull(study.CtSeries);
        Assert.Equal("CT-1", study.CtSeries!.SeriesIdentifier);
        Assert.Equal(120, study.CtSeries.TubeVoltageKvp);
        Assert.Null(study.MrSeries);
    }
}
