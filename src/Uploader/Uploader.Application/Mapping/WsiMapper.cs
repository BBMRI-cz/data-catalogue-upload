using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>Maps the raw WSI DTO onto the <see cref="WsiAggregate"/> (fixed block + slide chain).</summary>
public static class WsiMapper
{
    public static ErrorOr<WsiAggregate> ToWsi(WsiDto dto, WsiId id, SampleId sampleId) =>
        WsiAggregate.Create(id.Value, sampleId, ToFixedBlock(dto));

    private static FixedBlock? ToFixedBlock(WsiDto dto)
    {
        if (dto.BlockIdentifier is null
            && dto.SourceMaterial is null
            && dto.NameOfFixative is null
            && dto.EmbeddingMedium is null
            && dto.SlideContainer is null)
        {
            return null;
        }

        return new FixedBlock
        {
            Id = OptionalFixedBlockId(dto.BlockIdentifier),
            SourceMaterial = dto.SourceMaterial,
            NameOfFixative = dto.NameOfFixative,
            EmbeddingMedium = dto.EmbeddingMedium,
            SlideContainer = ToSlideContainer(dto.SlideContainer),
        };
    }

    private static SlideContainer? ToSlideContainer(SlideContainerDto? dto) =>
        dto is null
            ? null
            : new SlideContainer
            {
                SlideContainerIdentifier = dto.SlideContainerIdentifier,
                SourceFixedBlock = dto.SourceFixedBlock,
                ContainerType = dto.ContainerType,
                SectionThickness = dto.SectionThickness,
                CellType = dto.CellType,
                TissueType = dto.TissueType,
                SlidePreparationAssay = ToSlidePreparationAssay(dto.SlidePreparationAssay),
            };

    private static SlidePreparationAssay? ToSlidePreparationAssay(SlidePreparationAssayDto? dto) =>
        dto is null
            ? null
            : new SlidePreparationAssay
            {
                AssayIdentifier = dto.AssayIdentifier,
                HasInputSlideContainer = dto.HasInputSlideContainer,
                StainingMethod = dto.StainingMethod,
                AssayType = dto.AssayType,
                WholeSlideImaging = ToWholeSlideImaging(dto.WholeSlideImaging),
            };

    private static WholeSlideImaging? ToWholeSlideImaging(WholeSlideImagingDto? dto) =>
        dto is null
            ? null
            : new WholeSlideImaging
            {
                WsiIdentifier = dto.WsiIdentifier,
                BelongsToImagingStudy = dto.BelongsToImagingStudy,
                DicomImagesCount = dto.DicomImagesCount,
                SeriesStartDate = dto.SeriesStartDate,
                BodyRegion = dto.BodyRegion,
                ImagingDevice = dto.ImagingDevice,
                ManufacturerOfImagingDevice = dto.ManufacturerOfImagingDevice,
                SoftwareVersion = dto.SoftwareVersion,
                ZStacking = dto.ZStacking,
                ObjectiveLensMagnification = dto.ObjectiveLensMagnification,
                IlluminationMethod = dto.IlluminationMethod,
                IlluminationWavelength = dto.IlluminationWavelength,
                ScanningOperationMode = dto.ScanningOperationMode,
                TissueScanArea = dto.TissueScanArea,
                NumberOfFocalPlanes = dto.NumberOfFocalPlanes,
                DistanceBetweenFocalPlanes = dto.DistanceBetweenFocalPlanes,
                PyramidLevels = dto.PyramidLevels,
                ColourIccProfile = dto.ColourIccProfile,
                PreviewAvailable = dto.PreviewAvailable,
                LabelAvailable = dto.LabelAvailable,
                SourceAssay = dto.SourceAssay,
                FileFormat = dto.FileFormat,
                FileSize = dto.FileSize,
                ImageWidth = dto.ImageWidth,
                ImageHeight = dto.ImageHeight,
                ImageDepth = dto.ImageDepth,
                NumberOfChannels = dto.NumberOfChannels,
                ChannelResolution = dto.ChannelResolution,
                CompressionMethod = dto.CompressionMethod,
                CompressionQualityLabel = dto.CompressionQualityLabel,
                AnnotationsAvailable = dto.AnnotationsAvailable,
            };

    private static FixedBlockId? OptionalFixedBlockId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new FixedBlockId(value);
}
