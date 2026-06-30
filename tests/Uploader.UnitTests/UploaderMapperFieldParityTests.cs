using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// Full-field parity guards for the hand-written per-aggregate Uploader mappers (Patient/Sample/
/// Sequencing/Wsi/ImagingStudy). A dropped or mis-sourced field is no longer a compile error, so
/// every value-object field is asserted here.
/// The all-scalar imaging-series records (no collection members) are compared by record value
/// equality, which checks every field in one assertion; the list-bearing value objects are checked
/// field by field (xUnit compares lists structurally).
/// </summary>
public sealed class UploaderMapperFieldParityTests
{
    [Fact]
    public void PersonalAndClinicalMapEveryField()
    {
        var patient = PatientMapper.ToPatient(new PatientDto
        {
            PatientId = "P1",
            PersonalIdentifier = "PI",
            YearOfBirth = 1980,
            GenderAtBirth = "male",
            GenderIdentity = "male",
            ClinicalIdentifier = "CI",
            BelongsToPerson = "P1",
            ClinicalDiagnosis = ["C50", "C51"],
            AgeAtDiagnosis = 40,
            AgeOfOnset = 38,
        }).Value;

        var personal = patient.Personal!;
        Assert.Equal("PI", personal.PersonalIdentifier);
        Assert.Equal(1980, personal.YearOfBirth);
        Assert.Equal("male", personal.GenderAtBirth);
        Assert.Equal("male", personal.GenderIdentity);

        var clinical = patient.Clinical!;
        Assert.Equal("CI", clinical.ClinicalIdentifier);
        Assert.Equal("P1", clinical.BelongsToPerson);
        Assert.Equal(["C50", "C51"], clinical.ClinicalDiagnosis);
        Assert.Equal(40, clinical.AgeAtDiagnosis);
        Assert.Equal(38, clinical.AgeOfOnset);
    }

    [Fact]
    public void MaterialMapsEveryField()
    {
        var sample = SampleMapper.ToSample(
            new SampleDto
            {
                SampleId = "S1",
                MaterialIdentifier = "MI",
                CollectedFromPerson = "P1",
                BelongsToDiagnosis = ["C50"],
                SamplingTimestamp = "2020-01-01",
                RegistrationTimestamp = "2020-01-02",
                SamplingProtocol = "SP",
                SamplingProtocolDeviation = "dev",
                ReasonForSamplingProtocolDeviation = "reason",
                BiospecimenType = "tissue",
                AnatomicalSource = "liver",
                PathologicalState = "tumour",
                StorageConditions = "-80",
                ExpirationDate = "2030-01-01",
                PercentageTumorCells = 12.5,
                PhysicalLocation = "freezer-1",
                AnalysesPerformed = ["wgs", "rna"],
                DerivedFrom = "BLOCK1",
            },
            new PatientId("P1")).Value;

        var m = sample.Material!;
        Assert.Equal("MI", m.MaterialIdentifier);
        Assert.Equal("P1", m.CollectedFromPerson);
        Assert.Equal(["C50"], m.BelongsToDiagnosis);
        Assert.Equal("2020-01-01", m.SamplingTimestamp);
        Assert.Equal("2020-01-02", m.RegistrationTimestamp);
        Assert.Equal("SP", m.SamplingProtocol);
        Assert.Equal("dev", m.SamplingProtocolDeviation);
        Assert.Equal("reason", m.ReasonForSamplingProtocolDeviation);
        Assert.Equal("tissue", m.BiospecimenType);
        Assert.Equal("liver", m.AnatomicalSource);
        Assert.Equal("tumour", m.PathologicalState);
        Assert.Equal("-80", m.StorageConditions);
        Assert.Equal("2030-01-01", m.ExpirationDate);
        Assert.Equal(12.5, m.PercentageTumorCells);
        Assert.Equal("freezer-1", m.PhysicalLocation);
        Assert.Equal(["wgs", "rna"], m.AnalysesPerformed);
        Assert.Equal("BLOCK1", m.DerivedFrom);
    }

    [Fact]
    public void SamplePreparationSequencingRunAndAnalysisMapEveryField()
    {
        var dto = new SequencingDto
        {
            SamplePreparation = new SamplePreparationDto
            {
                SampleprepIdentifier = "PREP",
                BelongsToMaterial = "MAT",
                InputAmount = 100,
                LibraryPreparationKit = "KitA",
                PcrFree = true,
                TargetEnrichmentKit = "Enrich",
                FullSequenceGenes = ["BRCA1", "BRCA2"],
                PartialSequenceGenes = ["TP53"],
                UmisPresent = false,
                IntendedInsertSize = 350,
                IntendedReadLength = 150,
                Sequencing = new SequencingRunDto
                {
                    SequencingIdentifier = "SEQ",
                    BelongsToSamplePreparation = "PREP",
                    SequencingDate = "2021-05-05",
                    SequencingPlatform = "Illumina",
                    SequencingInstrumentModel = "NovaSeq",
                    SequencingMethod = "WGS",
                    MedianReadDepth = 30,
                    ObservedReadLength = 149,
                    ObservedInsertSize = 348,
                    PercentageQ30 = 95.5,
                    PercentageTr20 = 98.1,
                    OtherQualityMetrics = "ok",
                    Analyses =
                    [
                        new AnalysisDto
                        {
                            AnalysisIdentifier = "AN",
                            BelongsToSequencing = "SEQ",
                            PhysicalDataLocation = "/data",
                            AbstractDataLocation = "s3://data",
                            DataFormatsStored = ["bam", "vcf"],
                            AlgorithmsUsed = ["bwa", "gatk"],
                            ReferenceGenomeUsed = "GRCh38",
                            BioinformaticProtocolUsed = "proto",
                            BioinformaticProtocolDeviation = "dev",
                            ReasonForBioinformaticProtocolDeviation = "reason",
                            WgsGuidelineFollowed = "yes",
                        },
                    ],
                },
            },
        };

        var entry = Assert.Single(SequencingMapper.ToSequencing(dto, new SequencingId("PRED1"), new SampleId("S1")).Value.Entries);
        var prep = entry.SamplePreparation!;
        Assert.Equal("PREP", prep.SampleprepIdentifier);
        Assert.Equal("MAT", prep.BelongsToMaterial);
        Assert.Equal(100, prep.InputAmount);
        Assert.Equal("KitA", prep.LibraryPreparationKit);
        Assert.True(prep.PcrFree);
        Assert.Equal("Enrich", prep.TargetEnrichmentKit);
        Assert.Equal(["BRCA1", "BRCA2"], prep.FullSequenceGenes);
        Assert.Equal(["TP53"], prep.PartialSequenceGenes);
        Assert.False(prep.UmisPresent);
        Assert.Equal(350, prep.IntendedInsertSize);
        Assert.Equal(150, prep.IntendedReadLength);

        var run = prep.Sequencing!;
        Assert.Equal("SEQ", run.SequencingIdentifier);
        Assert.Equal("PREP", run.BelongsToSamplePreparation);
        Assert.Equal("2021-05-05", run.SequencingDate);
        Assert.Equal("Illumina", run.SequencingPlatform);
        Assert.Equal("NovaSeq", run.SequencingInstrumentModel);
        Assert.Equal("WGS", run.SequencingMethod);
        Assert.Equal(30, run.MedianReadDepth);
        Assert.Equal(149, run.ObservedReadLength);
        Assert.Equal(348, run.ObservedInsertSize);
        Assert.Equal(95.5, run.PercentageQ30);
        Assert.Equal(98.1, run.PercentageTr20);
        Assert.Equal("ok", run.OtherQualityMetrics);

        var analysis = run.Analysis!;
        Assert.Equal("AN", analysis.AnalysisIdentifier);
        Assert.Equal("SEQ", analysis.BelongsToSequencing);
        Assert.Equal("/data", analysis.PhysicalDataLocation);
        Assert.Equal("s3://data", analysis.AbstractDataLocation);
        Assert.Equal(["bam", "vcf"], analysis.DataFormatsStored);
        Assert.Equal(["bwa", "gatk"], analysis.AlgorithmsUsed);
        Assert.Equal("GRCh38", analysis.ReferenceGenomeUsed);
        Assert.Equal("proto", analysis.BioinformaticProtocolUsed);
        Assert.Equal("dev", analysis.BioinformaticProtocolDeviation);
        Assert.Equal("reason", analysis.ReasonForBioinformaticProtocolDeviation);
        Assert.Equal("yes", analysis.WgsGuidelineFollowed);
    }

    [Fact]
    public void WsiSlideChainMapsEveryField()
    {
        var wsi = new WholeSlideImagingDto
        {
            WsiIdentifier = "WSI",
            BelongsToImagingStudy = "STUDY",
            DicomImagesCount = 3,
            SeriesStartDate = "2022-01-01",
            BodyRegion = "breast",
            ImagingDevice = "Scanner",
            ManufacturerOfImagingDevice = "Acme",
            SoftwareVersion = "1.2",
            ZStacking = "yes",
            ObjectiveLensMagnification = 40,
            IlluminationMethod = "brightfield",
            IlluminationWavelength = 550,
            ScanningOperationMode = "mode",
            TissueScanArea = 1000,
            NumberOfFocalPlanes = 5,
            DistanceBetweenFocalPlanes = 2,
            PyramidLevels = 6,
            ColourIccProfile = "sRGB",
            PreviewAvailable = true,
            LabelAvailable = false,
            SourceAssay = "ASSAY",
            FileFormat = "svs",
            FileSize = 999,
            ImageWidth = 1024,
            ImageHeight = 768,
            ImageDepth = 8,
            NumberOfChannels = 3,
            ChannelResolution = 8,
            CompressionMethod = "jpeg",
            CompressionQualityLabel = "high",
            AnnotationsAvailable = true,
        };
        var dto = new WsiDto
        {
            BlockIdentifier = "FB1",
            SourceMaterial = "tissue",
            NameOfFixative = "formalin",
            EmbeddingMedium = "paraffin",
            SlideContainer = new SlideContainerDto
            {
                SlideContainerIdentifier = "SC",
                SourceFixedBlock = "FB1",
                ContainerType = "glass",
                SectionThickness = 4,
                CellType = ["epithelial"],
                TissueType = ["breast"],
                SlidePreparationAssay = new SlidePreparationAssayDto
                {
                    AssayIdentifier = "AS",
                    HasInputSlideContainer = "SC",
                    StainingMethod = "H&E",
                    AssayType = "IHC",
                    WholeSlideImaging = wsi,
                },
            },
        };

        var block = WsiMapper.ToWsi(dto, new WsiId("BIO1"), new SampleId("S1")).Value.FixedBlock!;
        Assert.Equal(new FixedBlockId("FB1"), block.Id);
        Assert.Equal("tissue", block.SourceMaterial);
        Assert.Equal("formalin", block.NameOfFixative);
        Assert.Equal("paraffin", block.EmbeddingMedium);

        var container = block.SlideContainer!;
        Assert.Equal("SC", container.SlideContainerIdentifier);
        Assert.Equal("FB1", container.SourceFixedBlock);
        Assert.Equal("glass", container.ContainerType);
        Assert.Equal(4, container.SectionThickness);
        Assert.Equal(["epithelial"], container.CellType);
        Assert.Equal(["breast"], container.TissueType);

        var assay = container.SlidePreparationAssay!;
        Assert.Equal("AS", assay.AssayIdentifier);
        Assert.Equal("SC", assay.HasInputSlideContainer);
        Assert.Equal("H&E", assay.StainingMethod);
        Assert.Equal("IHC", assay.AssayType);

        // WholeSlideImaging is all-scalar: record equality checks every one of its fields at once.
        var expectedWsi = new WholeSlideImaging
        {
            WsiIdentifier = "WSI",
            BelongsToImagingStudy = "STUDY",
            DicomImagesCount = 3,
            SeriesStartDate = "2022-01-01",
            BodyRegion = "breast",
            ImagingDevice = "Scanner",
            ManufacturerOfImagingDevice = "Acme",
            SoftwareVersion = "1.2",
            ZStacking = "yes",
            ObjectiveLensMagnification = 40,
            IlluminationMethod = "brightfield",
            IlluminationWavelength = 550,
            ScanningOperationMode = "mode",
            TissueScanArea = 1000,
            NumberOfFocalPlanes = 5,
            DistanceBetweenFocalPlanes = 2,
            PyramidLevels = 6,
            ColourIccProfile = "sRGB",
            PreviewAvailable = true,
            LabelAvailable = false,
            SourceAssay = "ASSAY",
            FileFormat = "svs",
            FileSize = 999,
            ImageWidth = 1024,
            ImageHeight = 768,
            ImageDepth = 8,
            NumberOfChannels = 3,
            ChannelResolution = 8,
            CompressionMethod = "jpeg",
            CompressionQualityLabel = "high",
            AnnotationsAvailable = true,
        };
        Assert.Equal(expectedWsi, assay.WholeSlideImaging);
    }

    [Fact]
    public void EveryImagingSeriesMapsEveryField()
    {
        var dto = new ImagingStudyDto
        {
            AccessionNumber = "ACC1",
            ImagingStudyIdentifier = "STUDY",
            BelongsToPerson = "P1",
            ImagingModality = ["CT", "MR"],
            BodyRegion = ["chest"],
            ImagingProcedure = ["scan"],
            ReasonForImagingProcedure = ["screening"],
            StudyStartDate = "2022-03-03",
            DicomSeriesCount = 5,
            DicomImagesCount = 500,
            AffiliatedInstitution = "Hospital",
            CtSeries = new CtSeriesDto
            {
                SeriesIdentifier = "CT",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 10,
                SeriesStartDate = "2022-03-03",
                BodyRegion = "chest",
                Laterality = "L",
                ImagingDevice = "CTdev",
                ManufacturerOfImagingDevice = "Acme",
                SoftwareVersion = "1.0",
                ColorSpace = "gray",
                PixelSpacing = 1,
                ImageType = "axial",
                FileFormat = "dcm",
                FileSize = 100,
                ImageWidth = 512,
                ImageHeight = 512,
                ImageDepth = 16,
                NumberOfChannels = 1,
                ChannelResolution = 16,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = false,
                TubeVoltageKvp = 120,
                XRayTubeCurrentMa = 200,
                ExposureTimeMs = 50,
                SpiralPitchFactor = 1.5,
                FilterType = "soft",
                ConvolutionKernel = "B30",
                FieldOfView = 350.0,
                SliceThickness = 1.25,
                ImagingInjection = "contrast",
                NumberOfImagePlanes = 64,
            },
            MrSeries = new MrSeriesDto
            {
                SeriesIdentifier = "MR",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 20,
                SeriesStartDate = "2022-03-04",
                BodyRegion = "brain",
                Laterality = "R",
                ImagingDevice = "MRdev",
                ManufacturerOfImagingDevice = "Beta",
                SoftwareVersion = "2.0",
                ColorSpace = "gray",
                PixelSpacing = 2,
                ImageType = "sag",
                FileFormat = "dcm",
                FileSize = 200,
                ImageWidth = 256,
                ImageHeight = 256,
                ImageDepth = 16,
                NumberOfChannels = 1,
                ChannelResolution = 16,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = true,
                SequenceName = "T1",
                MagneticFieldStrength = 3.0,
                MrAcquisitionType = "3D",
                RepetitionTime = 500.0,
                EchoTime = 10.0,
                ImagingFrequency = 128.0,
                FlipAngle = 90,
                InversionTime = 100,
                ReceiveCoilName = "head",
                FieldOfView = 240.0,
                SliceThickness = 3.0,
                ImagingInjection = "gad",
                NumberOfImagePlanes = 32,
            },
            UsSeries = new UsSeriesDto
            {
                SeriesIdentifier = "US",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 5,
                SeriesStartDate = "2022-03-05",
                BodyRegion = "abdomen",
                Laterality = "L",
                ImagingDevice = "USdev",
                ManufacturerOfImagingDevice = "Gamma",
                SoftwareVersion = "3.0",
                ColorSpace = "rgb",
                PixelSpacing = 1,
                ImageType = "bmode",
                FileFormat = "dcm",
                FileSize = 50,
                ImageWidth = 640,
                ImageHeight = 480,
                ImageDepth = 8,
                NumberOfChannels = 3,
                ChannelResolution = 8,
                CompressionMethod = "jpeg",
                CompressionQualityLabel = "high",
                AnnotationsAvailable = false,
                TransducerFrequencyMhz = 7.5,
                MechanicalIndex = 0.8,
                ThermalIndex = 0.3,
            },
            DxSeries = new DxSeriesDto
            {
                SeriesIdentifier = "DX",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 2,
                SeriesStartDate = "2022-03-06",
                BodyRegion = "hand",
                Laterality = "R",
                ImagingDevice = "DXdev",
                ManufacturerOfImagingDevice = "Delta",
                SoftwareVersion = "4.0",
                ColorSpace = "gray",
                PixelSpacing = 1,
                ImageType = "ap",
                FileFormat = "dcm",
                FileSize = 30,
                ImageWidth = 2000,
                ImageHeight = 2500,
                ImageDepth = 12,
                NumberOfChannels = 1,
                ChannelResolution = 12,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = false,
                PatientOrientation = "AP",
                TubeVoltageKvp = 70,
                ExposureTimeMs = 20,
                ExposureMas = 5,
            },
            MgSeries = new MgSeriesDto
            {
                SeriesIdentifier = "MG",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 4,
                SeriesStartDate = "2022-03-07",
                BodyRegion = "breast",
                Laterality = "L",
                ImagingDevice = "MGdev",
                ManufacturerOfImagingDevice = "Epsilon",
                SoftwareVersion = "5.0",
                ColorSpace = "gray",
                PixelSpacing = 1,
                ImageType = "cc",
                FileFormat = "dcm",
                FileSize = 40,
                ImageWidth = 3000,
                ImageHeight = 4000,
                ImageDepth = 12,
                NumberOfChannels = 1,
                ChannelResolution = 12,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = true,
                TubeVoltageKvp = 28,
                ExposureTimeMs = 100,
                ExposureMas = 80,
                CompressionForceN = 120.0,
            },
        };

        var study = ImagingStudyMapper.ToImagingStudy(dto, new PatientId("P1")).Value;

        Assert.Equal("STUDY", study.ImagingStudyIdentifier);
        Assert.Equal("P1", study.BelongsToPerson);
        Assert.Equal(["CT", "MR"], study.ImagingModality);
        Assert.Equal(["chest"], study.BodyRegion);
        Assert.Equal(["scan"], study.ImagingProcedure);
        Assert.Equal(["screening"], study.ReasonForImagingProcedure);
        Assert.Equal("2022-03-03", study.StudyStartDate);
        Assert.Equal(5, study.DicomSeriesCount);
        Assert.Equal(500, study.DicomImagesCount);
        Assert.Equal("Hospital", study.AffiliatedInstitution);

        // Imaging series are all-scalar records: record equality verifies every base + per-type field.
        Assert.Equal(
            new CtSeries
            {
                SeriesIdentifier = "CT",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 10,
                SeriesStartDate = "2022-03-03",
                BodyRegion = "chest",
                Laterality = "L",
                ImagingDevice = "CTdev",
                ManufacturerOfImagingDevice = "Acme",
                SoftwareVersion = "1.0",
                ColorSpace = "gray",
                PixelSpacing = 1,
                ImageType = "axial",
                FileFormat = "dcm",
                FileSize = 100,
                ImageWidth = 512,
                ImageHeight = 512,
                ImageDepth = 16,
                NumberOfChannels = 1,
                ChannelResolution = 16,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = false,
                TubeVoltageKvp = 120,
                XRayTubeCurrentMa = 200,
                ExposureTimeMs = 50,
                SpiralPitchFactor = 1.5,
                FilterType = "soft",
                ConvolutionKernel = "B30",
                FieldOfView = 350.0,
                SliceThickness = 1.25,
                ImagingInjection = "contrast",
                NumberOfImagePlanes = 64,
            },
            study.CtSeries);

        Assert.Equal(
            new MrSeries
            {
                SeriesIdentifier = "MR",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 20,
                SeriesStartDate = "2022-03-04",
                BodyRegion = "brain",
                Laterality = "R",
                ImagingDevice = "MRdev",
                ManufacturerOfImagingDevice = "Beta",
                SoftwareVersion = "2.0",
                ColorSpace = "gray",
                PixelSpacing = 2,
                ImageType = "sag",
                FileFormat = "dcm",
                FileSize = 200,
                ImageWidth = 256,
                ImageHeight = 256,
                ImageDepth = 16,
                NumberOfChannels = 1,
                ChannelResolution = 16,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = true,
                SequenceName = "T1",
                MagneticFieldStrength = 3.0,
                MrAcquisitionType = "3D",
                RepetitionTime = 500.0,
                EchoTime = 10.0,
                ImagingFrequency = 128.0,
                FlipAngle = 90,
                InversionTime = 100,
                ReceiveCoilName = "head",
                FieldOfView = 240.0,
                SliceThickness = 3.0,
                ImagingInjection = "gad",
                NumberOfImagePlanes = 32,
            },
            study.MrSeries);

        Assert.Equal(
            new UsSeries
            {
                SeriesIdentifier = "US",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 5,
                SeriesStartDate = "2022-03-05",
                BodyRegion = "abdomen",
                Laterality = "L",
                ImagingDevice = "USdev",
                ManufacturerOfImagingDevice = "Gamma",
                SoftwareVersion = "3.0",
                ColorSpace = "rgb",
                PixelSpacing = 1,
                ImageType = "bmode",
                FileFormat = "dcm",
                FileSize = 50,
                ImageWidth = 640,
                ImageHeight = 480,
                ImageDepth = 8,
                NumberOfChannels = 3,
                ChannelResolution = 8,
                CompressionMethod = "jpeg",
                CompressionQualityLabel = "high",
                AnnotationsAvailable = false,
                TransducerFrequencyMhz = 7.5,
                MechanicalIndex = 0.8,
                ThermalIndex = 0.3,
            },
            study.UsSeries);

        Assert.Equal(
            new DxSeries
            {
                SeriesIdentifier = "DX",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 2,
                SeriesStartDate = "2022-03-06",
                BodyRegion = "hand",
                Laterality = "R",
                ImagingDevice = "DXdev",
                ManufacturerOfImagingDevice = "Delta",
                SoftwareVersion = "4.0",
                ColorSpace = "gray",
                PixelSpacing = 1,
                ImageType = "ap",
                FileFormat = "dcm",
                FileSize = 30,
                ImageWidth = 2000,
                ImageHeight = 2500,
                ImageDepth = 12,
                NumberOfChannels = 1,
                ChannelResolution = 12,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = false,
                PatientOrientation = "AP",
                TubeVoltageKvp = 70,
                ExposureTimeMs = 20,
                ExposureMas = 5,
            },
            study.DxSeries);

        Assert.Equal(
            new MgSeries
            {
                SeriesIdentifier = "MG",
                ImagingStudyIdentifier = "STUDY",
                DicomImagesCount = 4,
                SeriesStartDate = "2022-03-07",
                BodyRegion = "breast",
                Laterality = "L",
                ImagingDevice = "MGdev",
                ManufacturerOfImagingDevice = "Epsilon",
                SoftwareVersion = "5.0",
                ColorSpace = "gray",
                PixelSpacing = 1,
                ImageType = "cc",
                FileFormat = "dcm",
                FileSize = 40,
                ImageWidth = 3000,
                ImageHeight = 4000,
                ImageDepth = 12,
                NumberOfChannels = 1,
                ChannelResolution = 12,
                CompressionMethod = "none",
                CompressionQualityLabel = "lossless",
                AnnotationsAvailable = true,
                TubeVoltageKvp = 28,
                ExposureTimeMs = 100,
                ExposureMas = 80,
                CompressionForceN = 120.0,
            },
            study.MgSeries);
    }
}
