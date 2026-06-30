using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>Maps the raw imaging-study DTO onto the <see cref="ImagingStudyAggregate"/> + its series.</summary>
public static class ImagingStudyMapper
{
    public static ErrorOr<ImagingStudyAggregate> ToImagingStudy(ImagingStudyDto dto, PatientId patientId) =>
        ImagingStudyAggregate.Create(dto.AccessionNumber, patientId, new ImagingStudyAggregate.Details
        {
            ImagingStudyIdentifier = dto.ImagingStudyIdentifier,
            BelongsToPerson = dto.BelongsToPerson,
            ImagingModality = dto.ImagingModality,
            BodyRegion = dto.BodyRegion,
            ImagingProcedure = dto.ImagingProcedure,
            ReasonForImagingProcedure = dto.ReasonForImagingProcedure,
            StudyStartDate = dto.StudyStartDate,
            DicomSeriesCount = dto.DicomSeriesCount,
            DicomImagesCount = dto.DicomImagesCount,
            AffiliatedInstitution = dto.AffiliatedInstitution,
            CtSeries = ToCtSeries(dto.CtSeries),
            MrSeries = ToMrSeries(dto.MrSeries),
            UsSeries = ToUsSeries(dto.UsSeries),
            DxSeries = ToDxSeries(dto.DxSeries),
            MgSeries = ToMgSeries(dto.MgSeries),
        });

    private static CtSeries? ToCtSeries(CtSeriesDto? dto) =>
        dto is null
            ? null
            : new CtSeries
            {
                SeriesIdentifier = dto.SeriesIdentifier,
                ImagingStudyIdentifier = dto.ImagingStudyIdentifier,
                DicomImagesCount = dto.DicomImagesCount,
                SeriesStartDate = dto.SeriesStartDate,
                BodyRegion = dto.BodyRegion,
                Laterality = dto.Laterality,
                ImagingDevice = dto.ImagingDevice,
                ManufacturerOfImagingDevice = dto.ManufacturerOfImagingDevice,
                SoftwareVersion = dto.SoftwareVersion,
                ColorSpace = dto.ColorSpace,
                PixelSpacing = dto.PixelSpacing,
                ImageType = dto.ImageType,
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
                TubeVoltageKvp = dto.TubeVoltageKvp,
                XRayTubeCurrentMa = dto.XRayTubeCurrentMa,
                ExposureTimeMs = dto.ExposureTimeMs,
                SpiralPitchFactor = dto.SpiralPitchFactor,
                FilterType = dto.FilterType,
                ConvolutionKernel = dto.ConvolutionKernel,
                FieldOfView = dto.FieldOfView,
                SliceThickness = dto.SliceThickness,
                ImagingInjection = dto.ImagingInjection,
                NumberOfImagePlanes = dto.NumberOfImagePlanes,
            };

    private static MrSeries? ToMrSeries(MrSeriesDto? dto) =>
        dto is null
            ? null
            : new MrSeries
            {
                SeriesIdentifier = dto.SeriesIdentifier,
                ImagingStudyIdentifier = dto.ImagingStudyIdentifier,
                DicomImagesCount = dto.DicomImagesCount,
                SeriesStartDate = dto.SeriesStartDate,
                BodyRegion = dto.BodyRegion,
                Laterality = dto.Laterality,
                ImagingDevice = dto.ImagingDevice,
                ManufacturerOfImagingDevice = dto.ManufacturerOfImagingDevice,
                SoftwareVersion = dto.SoftwareVersion,
                ColorSpace = dto.ColorSpace,
                PixelSpacing = dto.PixelSpacing,
                ImageType = dto.ImageType,
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
                SequenceName = dto.SequenceName,
                MagneticFieldStrength = dto.MagneticFieldStrength,
                MrAcquisitionType = dto.MrAcquisitionType,
                RepetitionTime = dto.RepetitionTime,
                EchoTime = dto.EchoTime,
                ImagingFrequency = dto.ImagingFrequency,
                FlipAngle = dto.FlipAngle,
                InversionTime = dto.InversionTime,
                ReceiveCoilName = dto.ReceiveCoilName,
                FieldOfView = dto.FieldOfView,
                SliceThickness = dto.SliceThickness,
                ImagingInjection = dto.ImagingInjection,
                NumberOfImagePlanes = dto.NumberOfImagePlanes,
            };

    private static UsSeries? ToUsSeries(UsSeriesDto? dto) =>
        dto is null
            ? null
            : new UsSeries
            {
                SeriesIdentifier = dto.SeriesIdentifier,
                ImagingStudyIdentifier = dto.ImagingStudyIdentifier,
                DicomImagesCount = dto.DicomImagesCount,
                SeriesStartDate = dto.SeriesStartDate,
                BodyRegion = dto.BodyRegion,
                Laterality = dto.Laterality,
                ImagingDevice = dto.ImagingDevice,
                ManufacturerOfImagingDevice = dto.ManufacturerOfImagingDevice,
                SoftwareVersion = dto.SoftwareVersion,
                ColorSpace = dto.ColorSpace,
                PixelSpacing = dto.PixelSpacing,
                ImageType = dto.ImageType,
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
                TransducerFrequencyMhz = dto.TransducerFrequencyMhz,
                MechanicalIndex = dto.MechanicalIndex,
                ThermalIndex = dto.ThermalIndex,
            };

    private static DxSeries? ToDxSeries(DxSeriesDto? dto) =>
        dto is null
            ? null
            : new DxSeries
            {
                SeriesIdentifier = dto.SeriesIdentifier,
                ImagingStudyIdentifier = dto.ImagingStudyIdentifier,
                DicomImagesCount = dto.DicomImagesCount,
                SeriesStartDate = dto.SeriesStartDate,
                BodyRegion = dto.BodyRegion,
                Laterality = dto.Laterality,
                ImagingDevice = dto.ImagingDevice,
                ManufacturerOfImagingDevice = dto.ManufacturerOfImagingDevice,
                SoftwareVersion = dto.SoftwareVersion,
                ColorSpace = dto.ColorSpace,
                PixelSpacing = dto.PixelSpacing,
                ImageType = dto.ImageType,
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
                PatientOrientation = dto.PatientOrientation,
                TubeVoltageKvp = dto.TubeVoltageKvp,
                ExposureTimeMs = dto.ExposureTimeMs,
                ExposureMas = dto.ExposureMas,
            };

    private static MgSeries? ToMgSeries(MgSeriesDto? dto) =>
        dto is null
            ? null
            : new MgSeries
            {
                SeriesIdentifier = dto.SeriesIdentifier,
                ImagingStudyIdentifier = dto.ImagingStudyIdentifier,
                DicomImagesCount = dto.DicomImagesCount,
                SeriesStartDate = dto.SeriesStartDate,
                BodyRegion = dto.BodyRegion,
                Laterality = dto.Laterality,
                ImagingDevice = dto.ImagingDevice,
                ManufacturerOfImagingDevice = dto.ManufacturerOfImagingDevice,
                SoftwareVersion = dto.SoftwareVersion,
                ColorSpace = dto.ColorSpace,
                PixelSpacing = dto.PixelSpacing,
                ImageType = dto.ImageType,
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
                TubeVoltageKvp = dto.TubeVoltageKvp,
                ExposureTimeMs = dto.ExposureTimeMs,
                ExposureMas = dto.ExposureMas,
                CompressionForceN = dto.CompressionForceN,
            };
}
