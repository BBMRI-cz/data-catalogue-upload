from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class ImagingSeriesBase:
    source_id: str
    series_identifier: str | None
    imaging_study_identifier: str | None
    dicom_images_count: int | None = None
    series_start_date: str | None = None
    body_region: str | None = None
    laterality: str | None = None
    imaging_device: str | None = None
    manufacturer_of_imaging_device: str | None = None
    software_version: str | None = None
    color_space: str | None = None
    pixel_spacing: int | None = None
    image_type: str | None = None
    file_format: str | None = None
    file_size: int | None = None
    image_width: int | None = None
    image_height: int | None = None
    image_depth: int | None = None
    number_of_channels: int | None = None
    channel_resolution: int | None = None
    compression_method: str | None = None
    compression_quality_label: str | None = None
    annotations_available: bool | None = None


@dataclass(frozen=True)
class CtSeries(ImagingSeriesBase):
    tube_voltage_kvp: int | None = None
    x_ray_tube_current_ma: int | None = None
    exposure_time_ms: int | None = None
    spiral_pitch_factor: float | None = None
    filter_type: str | None = None
    convolution_kernel: str | None = None
    field_of_view: float | None = None
    slice_thickness: float | None = None
    imaging_injection: str | None = None
    number_of_image_planes: int | None = None


@dataclass(frozen=True)
class MrSeries(ImagingSeriesBase):
    sequence_name: str | None = None
    magnetic_field_strength: float | None = None
    mr_acquisition_type: str | None = None
    repetition_time: float | None = None
    echo_time: float | None = None
    imaging_frequency: float | None = None
    flip_angle: int | None = None
    inversion_time: int | None = None
    receive_coil_name: str | None = None
    field_of_view: float | None = None
    slice_thickness: float | None = None
    imaging_injection: str | None = None
    number_of_image_planes: int | None = None


@dataclass(frozen=True)
class DxSeries(ImagingSeriesBase):
    patient_orientation: str | None = None
    tube_voltage_kvp: int | None = None
    exposure_time_ms: int | None = None
    exposure_mas: int | None = None


@dataclass(frozen=True)
class MgSeries(ImagingSeriesBase):
    tube_voltage_kvp: int | None = None
    exposure_time_ms: int | None = None
    exposure_mas: int | None = None
    compression_force_n: float | None = None


@dataclass(frozen=True)
class UsSeries(ImagingSeriesBase):
    transducer_frequency_mhz: float | None = None
    mechanical_index: float | None = None
    thermal_index: float | None = None


@dataclass(frozen=True)
class ImagingStudy:
    accession_number: str
    source_id: str
    imaging_study_identifier: str | None = None
    belongs_to_person: str | None = None
    imaging_modality: list[str] | None = None
    body_region: list[str] | None = None
    imaging_procedure: list[str] | None = None
    reason_for_imaging_procedure: list[str] | None = None
    study_start_date: str | None = None
    dicom_series_count: int | None = None
    dicom_images_count: int | None = None
    affiliated_institution: str | None = None
    ct_series: CtSeries | None = None
    mr_series: MrSeries | None = None
    us_series: UsSeries | None = None
    dx_series: DxSeries | None = None
    mg_series: MgSeries | None = None


RadiologyData = list[ImagingStudy]
