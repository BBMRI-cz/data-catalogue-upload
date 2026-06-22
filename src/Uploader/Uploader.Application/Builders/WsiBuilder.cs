using System.Text.Json.Nodes;
using Uploader.Application.Json;
using Uploader.Domain;

namespace Uploader.Application.Builders;

/// <summary>Builds the WSI pipeline (fixed block -> slide container -> assay -> whole-slide imaging).</summary>
public sealed class WsiBuilder
{
    public WsiData BuildWsi(string biopticNumber, JsonObject payload)
    {
        var fixedBlockPayload = RawJson.FirstDict(payload["fixed_block"]) ?? payload;
        return new WsiData
        {
            BiopticNumber = biopticNumber,
            SourceId = RawJson.ResolveSourceId(payload, biopticNumber),
            FixedBlock = BuildFixedBlock(fixedBlockPayload),
        };
    }

    private static FixedBlock? BuildFixedBlock(JsonObject? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        if (!RawJson.HasAnyKeys(
            payload,
            "block_identifier",
            "source_material",
            "name_of_fixative",
            "embedding_medium",
            "slide_container"))
        {
            return null;
        }

        var slideContainerPayload = RawJson.FirstDict(payload["slide_container"]) ?? payload;
        return new FixedBlock
        {
            BlockIdentifier = payload.GetString("block_identifier"),
            SourceMaterial = payload.GetString("source_material"),
            NameOfFixative = payload.GetString("name_of_fixative"),
            EmbeddingMedium = payload.GetString("embedding_medium"),
            SamplePreparation = null,
            SlideContainer = BuildSlideContainer(slideContainerPayload),
        };
    }

    private static SlideContainer? BuildSlideContainer(JsonObject? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        if (!RawJson.HasAnyKeys(
            payload,
            "slide_container_identifier",
            "source_fixed_block",
            "container_type",
            "section_thickness",
            "slide_preparation_assay"))
        {
            return null;
        }

        var assayPayload = RawJson.FirstDict(payload["slide_preparation_assay"]) ?? payload;
        return new SlideContainer
        {
            SlideContainerIdentifier = payload.GetString("slide_container_identifier"),
            SourceFixedBlock = payload.GetString("source_fixed_block"),
            ContainerType = payload.GetString("container_type"),
            SectionThickness = payload.GetInt("section_thickness"),
            CellType = payload.GetStringList("cell_type"),
            TissueType = payload.GetStringList("tissue_type"),
            SlidePreparationAssay = BuildSlidePreparationAssay(assayPayload),
        };
    }

    private static SlidePreparationAssay? BuildSlidePreparationAssay(JsonObject? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        if (!RawJson.HasAnyKeys(
            payload,
            "assay_identifier",
            "has_input_slide_container",
            "staining_method",
            "assay_type",
            "whole_slide_imaging",
            "wsi"))
        {
            return null;
        }

        var imagingPayload = RawJson.FirstDict(payload["whole_slide_imaging"])
            ?? RawJson.FirstDict(payload["wsi"])
            ?? payload;

        return new SlidePreparationAssay
        {
            AssayIdentifier = payload.GetString("assay_identifier"),
            HasInputSlideContainer = payload.GetString("has_input_slide_container"),
            StainingMethod = payload.GetString("staining_method"),
            AssayType = payload.GetString("assay_type"),
            WholeSlideImaging = BuildWholeSlideImaging(imagingPayload),
        };
    }

    private static WholeSlideImaging? BuildWholeSlideImaging(JsonObject? payload)
    {
        if (payload is null || payload.Count == 0)
        {
            return null;
        }

        if (!RawJson.HasAnyKeys(
            payload,
            "wsi_identifier",
            "belongs_to_imaging_study",
            "dicom_images_count",
            "series_start_date",
            "imaging_device"))
        {
            return null;
        }

        return new WholeSlideImaging
        {
            WsiIdentifier = payload.GetString("wsi_identifier"),
            BelongsToImagingStudy = payload.GetString("belongs_to_imaging_study"),
            DicomImagesCount = payload.GetInt("dicom_images_count"),
            SeriesStartDate = payload.GetString("series_start_date"),
            BodyRegion = payload.GetString("body_region"),
            ImagingDevice = payload.GetString("imaging_device"),
            ManufacturerOfImagingDevice = payload.GetString("manufacturer_of_imaging_device"),
            SoftwareVersion = payload.GetString("software_version"),
            ZStacking = payload.GetString("z_stacking"),
            ObjectiveLensMagnification = payload.GetInt("objective_lens_magnification"),
            IlluminationMethod = payload.GetString("illumination_method"),
            IlluminationWavelength = payload.GetInt("illumination_wavelength"),
            ScanningOperationMode = payload.GetString("scanning_operation_mode"),
            TissueScanArea = payload.GetInt("tissue_scan_area"),
            NumberOfFocalPlanes = payload.GetInt("number_of_focal_planes"),
            DistanceBetweenFocalPlanes = payload.GetInt("distance_between_focal_planes"),
            PyramidLevels = payload.GetInt("pyramid_levels"),
            ColourIccProfile = payload.GetString("colour_icc_profile"),
            PreviewAvailable = payload.GetBool("preview_available"),
            LabelAvailable = payload.GetBool("label_available"),
            SourceAssay = payload.GetString("source_assay"),
            FileFormat = payload.GetString("file_format"),
            FileSize = payload.GetInt("file_size"),
            ImageWidth = payload.GetInt("image_width"),
            ImageHeight = payload.GetInt("image_height"),
            ImageDepth = payload.GetInt("image_depth"),
            NumberOfChannels = payload.GetInt("number_of_channels"),
            ChannelResolution = payload.GetInt("channel_resolution"),
            CompressionMethod = payload.GetString("compression_method"),
            CompressionQualityLabel = payload.GetString("compression_quality_label"),
            AnnotationsAvailable = payload.GetBool("annotations_available"),
        };
    }
}
