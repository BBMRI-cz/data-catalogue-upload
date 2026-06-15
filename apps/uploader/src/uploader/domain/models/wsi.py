from __future__ import annotations

from dataclasses import dataclass

from uploader.domain.models.sequencing import SamplePreparation


@dataclass(frozen=True)
class WholeSlideImaging:
    wsi_identifier: str | None = None
    belongs_to_imaging_study: str | None = None
    dicom_images_count: int | None = None
    series_start_date: str | None = None
    body_region: str | None = None
    imaging_device: str | None = None
    manufacturer_of_imaging_device: str | None = None
    software_version: str | None = None
    z_stacking: str | None = None
    objective_lens_magnification: int | None = None
    illumination_method: str | None = None
    illumination_wavelength: int | None = None
    scanning_operation_mode: str | None = None
    tissue_scan_area: int | None = None
    number_of_focal_planes: int | None = None
    distance_between_focal_planes: int | None = None
    pyramid_levels: int | None = None
    colour_icc_profile: str | None = None
    preview_available: bool | None = None
    label_available: bool | None = None
    source_assay: str | None = None
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
class SlidePreparationAssay:
    assay_identifier: str | None = None
    has_input_slide_container: str | None = None
    staining_method: str | None = None
    assay_type: str | None = None
    whole_slide_imaging: WholeSlideImaging | None = None


@dataclass(frozen=True)
class SlideContainer:
    slide_container_identifier: str | None = None
    source_fixed_block: str | None = None
    container_type: str | None = None
    section_thickness: int | None = None
    cell_type: list[str] | None = None
    tissue_type: list[str] | None = None
    slide_preparation_assay: SlidePreparationAssay | None = None


@dataclass(frozen=True)
class FixedBlock:
    block_identifier: str | None = None
    source_material: str | None = None
    name_of_fixative: str | None = None
    embedding_medium: str | None = None
    sample_preparation: SamplePreparation | None = None
    slide_container: SlideContainer | None = None


@dataclass(frozen=True)
class WsiData:
    bioptic_number: str
    source_id: str
    fixed_block: FixedBlock | None = None
