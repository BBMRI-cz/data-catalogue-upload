from __future__ import annotations

from domain.models import CtSeries, DxSeries, ImagingStudy, MgSeries, MrSeries, UsSeries
from domain.utils import first_dict, resolve_source_id


class RadiologyBuilder:
    def build_imaging_study(self, payload: dict) -> ImagingStudy:
        ct_payload = first_dict(payload.get("ct_series"))
        mr_payload = first_dict(payload.get("mr_series"))
        us_payload = first_dict(payload.get("us_series"))
        dx_payload = first_dict(payload.get("dx_series"))
        mg_payload = first_dict(payload.get("mg_series"))
        return ImagingStudy(
            accession_number=str(payload["accession_number"]),
            source_id=resolve_source_id(payload, str(payload["accession_number"])),
            imaging_study_identifier=payload.get("imaging_study_identifier"),
            belongs_to_person=payload.get("belongs_to_person"),
            imaging_modality=payload.get("imaging_modality"),
            body_region=payload.get("body_region"),
            imaging_procedure=payload.get("imaging_procedure"),
            reason_for_imaging_procedure=payload.get("reason_for_imaging_procedure"),
            study_start_date=payload.get("study_start_date"),
            dicom_series_count=payload.get("dicom_series_count"),
            dicom_images_count=payload.get("dicom_images_count"),
            affiliated_institution=payload.get("affiliated_institution"),
            ct_series=self._build_ct_series(ct_payload) if ct_payload else None,
            mr_series=self._build_mr_series(mr_payload) if mr_payload else None,
            us_series=self._build_us_series(us_payload) if us_payload else None,
            dx_series=self._build_dx_series(dx_payload) if dx_payload else None,
            mg_series=self._build_mg_series(mg_payload) if mg_payload else None,
        )

    def _build_ct_series(self, payload: dict) -> CtSeries:
        return CtSeries(
            source_id=resolve_source_id(payload),
            series_identifier=payload.get("series_identifier"),
            imaging_study_identifier=payload.get("imaging_study_identifier"),
            dicom_images_count=payload.get("dicom_images_count"),
            series_start_date=payload.get("series_start_date"),
            body_region=payload.get("body_region"),
            laterality=payload.get("laterality"),
            imaging_device=payload.get("imaging_device"),
            manufacturer_of_imaging_device=payload.get(
                "manufacturer_of_imaging_device"
            ),
            software_version=payload.get("software_version"),
            color_space=payload.get("color_space"),
            pixel_spacing=payload.get("pixel_spacing"),
            image_type=payload.get("image_type"),
            file_format=payload.get("file_format"),
            file_size=payload.get("file_size"),
            image_width=payload.get("image_width"),
            image_height=payload.get("image_height"),
            image_depth=payload.get("image_depth"),
            number_of_channels=payload.get("number_of_channels"),
            channel_resolution=payload.get("channel_resolution"),
            compression_method=payload.get("compression_method"),
            compression_quality_label=payload.get("compression_quality_label"),
            annotations_available=payload.get("annotations_available"),
            tube_voltage_kvp=payload.get("tube_voltage_kvp"),
            x_ray_tube_current_ma=payload.get("x_ray_tube_current_ma"),
            exposure_time_ms=payload.get("exposure_time_ms"),
            spiral_pitch_factor=payload.get("spiral_pitch_factor"),
            filter_type=payload.get("filter_type"),
            convolution_kernel=payload.get("convolution_kernel"),
            field_of_view=payload.get("field_of_view"),
            slice_thickness=payload.get("slice_thickness"),
            imaging_injection=payload.get("imaging_injection"),
            number_of_image_planes=payload.get("number_of_image_planes"),
        )

    def _build_mr_series(self, payload: dict) -> MrSeries:
        return MrSeries(
            source_id=resolve_source_id(payload),
            series_identifier=payload.get("series_identifier"),
            imaging_study_identifier=payload.get("imaging_study_identifier"),
            dicom_images_count=payload.get("dicom_images_count"),
            series_start_date=payload.get("series_start_date"),
            body_region=payload.get("body_region"),
            laterality=payload.get("laterality"),
            imaging_device=payload.get("imaging_device"),
            manufacturer_of_imaging_device=payload.get(
                "manufacturer_of_imaging_device"
            ),
            software_version=payload.get("software_version"),
            color_space=payload.get("color_space"),
            pixel_spacing=payload.get("pixel_spacing"),
            image_type=payload.get("image_type"),
            file_format=payload.get("file_format"),
            file_size=payload.get("file_size"),
            image_width=payload.get("image_width"),
            image_height=payload.get("image_height"),
            image_depth=payload.get("image_depth"),
            number_of_channels=payload.get("number_of_channels"),
            channel_resolution=payload.get("channel_resolution"),
            compression_method=payload.get("compression_method"),
            compression_quality_label=payload.get("compression_quality_label"),
            annotations_available=payload.get("annotations_available"),
            sequence_name=payload.get("sequence_name"),
            magnetic_field_strength=payload.get("magnetic_field_strength"),
            mr_acquisition_type=payload.get("mr_acquisition_type"),
            repetition_time=payload.get("repetition_time"),
            echo_time=payload.get("echo_time"),
            imaging_frequency=payload.get("imaging_frequency"),
            flip_angle=payload.get("flip_angle"),
            inversion_time=payload.get("inversion_time"),
            receive_coil_name=payload.get("receive_coil_name"),
            field_of_view=payload.get("field_of_view"),
            slice_thickness=payload.get("slice_thickness"),
            imaging_injection=payload.get("imaging_injection"),
            number_of_image_planes=payload.get("number_of_image_planes"),
        )

    def _build_us_series(self, payload: dict) -> UsSeries:
        return UsSeries(
            source_id=resolve_source_id(payload),
            series_identifier=payload.get("series_identifier"),
            imaging_study_identifier=payload.get("imaging_study_identifier"),
            dicom_images_count=payload.get("dicom_images_count"),
            series_start_date=payload.get("series_start_date"),
            body_region=payload.get("body_region"),
            laterality=payload.get("laterality"),
            imaging_device=payload.get("imaging_device"),
            manufacturer_of_imaging_device=payload.get(
                "manufacturer_of_imaging_device"
            ),
            software_version=payload.get("software_version"),
            color_space=payload.get("color_space"),
            pixel_spacing=payload.get("pixel_spacing"),
            image_type=payload.get("image_type"),
            file_format=payload.get("file_format"),
            file_size=payload.get("file_size"),
            image_width=payload.get("image_width"),
            image_height=payload.get("image_height"),
            image_depth=payload.get("image_depth"),
            number_of_channels=payload.get("number_of_channels"),
            channel_resolution=payload.get("channel_resolution"),
            compression_method=payload.get("compression_method"),
            compression_quality_label=payload.get("compression_quality_label"),
            annotations_available=payload.get("annotations_available"),
            transducer_frequency_mhz=payload.get("transducer_frequency_mhz"),
            mechanical_index=payload.get("mechanical_index"),
            thermal_index=payload.get("thermal_index"),
        )

    def _build_dx_series(self, payload: dict) -> DxSeries:
        return DxSeries(
            source_id=resolve_source_id(payload),
            series_identifier=payload.get("series_identifier"),
            imaging_study_identifier=payload.get("imaging_study_identifier"),
            dicom_images_count=payload.get("dicom_images_count"),
            series_start_date=payload.get("series_start_date"),
            body_region=payload.get("body_region"),
            laterality=payload.get("laterality"),
            imaging_device=payload.get("imaging_device"),
            manufacturer_of_imaging_device=payload.get(
                "manufacturer_of_imaging_device"
            ),
            software_version=payload.get("software_version"),
            color_space=payload.get("color_space"),
            pixel_spacing=payload.get("pixel_spacing"),
            image_type=payload.get("image_type"),
            file_format=payload.get("file_format"),
            file_size=payload.get("file_size"),
            image_width=payload.get("image_width"),
            image_height=payload.get("image_height"),
            image_depth=payload.get("image_depth"),
            number_of_channels=payload.get("number_of_channels"),
            channel_resolution=payload.get("channel_resolution"),
            compression_method=payload.get("compression_method"),
            compression_quality_label=payload.get("compression_quality_label"),
            annotations_available=payload.get("annotations_available"),
            patient_orientation=payload.get("patient_orientation"),
            tube_voltage_kvp=payload.get("tube_voltage_kvp"),
            exposure_time_ms=payload.get("exposure_time_ms"),
            exposure_mas=payload.get("exposure_mas"),
        )

    def _build_mg_series(self, payload: dict) -> MgSeries:
        return MgSeries(
            source_id=resolve_source_id(payload),
            series_identifier=payload.get("series_identifier"),
            imaging_study_identifier=payload.get("imaging_study_identifier"),
            dicom_images_count=payload.get("dicom_images_count"),
            series_start_date=payload.get("series_start_date"),
            body_region=payload.get("body_region"),
            laterality=payload.get("laterality"),
            imaging_device=payload.get("imaging_device"),
            manufacturer_of_imaging_device=payload.get(
                "manufacturer_of_imaging_device"
            ),
            software_version=payload.get("software_version"),
            color_space=payload.get("color_space"),
            pixel_spacing=payload.get("pixel_spacing"),
            image_type=payload.get("image_type"),
            file_format=payload.get("file_format"),
            file_size=payload.get("file_size"),
            image_width=payload.get("image_width"),
            image_height=payload.get("image_height"),
            image_depth=payload.get("image_depth"),
            number_of_channels=payload.get("number_of_channels"),
            channel_resolution=payload.get("channel_resolution"),
            compression_method=payload.get("compression_method"),
            compression_quality_label=payload.get("compression_quality_label"),
            annotations_available=payload.get("annotations_available"),
            tube_voltage_kvp=payload.get("tube_voltage_kvp"),
            exposure_time_ms=payload.get("exposure_time_ms"),
            exposure_mas=payload.get("exposure_mas"),
            compression_force_n=payload.get("compression_force_n"),
        )
