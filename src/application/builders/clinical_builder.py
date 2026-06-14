from __future__ import annotations

from domain.models import Clinical, Material, Personal


class ClinicalBuilder:
    def build_personal(self, payload: dict) -> Personal:
        return Personal(
            personal_identifier=payload.get("personal_identifier"),
            year_of_birth=payload.get("year_of_birth"),
            gender_at_birth=payload.get("gender_at_birth"),
            gender_identity=payload.get("gender_identity"),
        )

    def build_clinical(self, payload: dict) -> Clinical:
        return Clinical(
            clinical_identifier=payload.get("clinical_identifier"),
            belongs_to_person=payload.get("belongs_to_person"),
            clinical_diagnosis=payload.get("clinical_diagnosis"),
            age_at_diagnosis=payload.get("age_at_diagnosis"),
            age_of_onset=payload.get("age_of_onset"),
        )

    def build_material(self, payload: dict) -> Material:
        return Material(
            material_identifier=payload.get("material_identifier"),
            collected_from_person=payload.get("collected_from_person"),
            belongs_to_diagnosis=payload.get("belongs_to_diagnosis"),
            sampling_timestamp=payload.get("sampling_timestamp"),
            registration_timestamp=payload.get("registration_timestamp"),
            sampling_protocol=payload.get("sampling_protocol"),
            sampling_protocol_deviation=payload.get("sampling_protocol_deviation"),
            reason_for_sampling_protocol_deviation=payload.get(
                "reason_for_sampling_protocol_deviation"
            ),
            biospecimen_type=payload.get("biospecimen_type"),
            anatomical_source=payload.get("anatomical_source"),
            pathological_state=payload.get("pathological_state"),
            storage_conditions=payload.get("storage_conditions"),
            expiration_date=payload.get("expiration_date"),
            percentage_tumor_cells=payload.get("percentage_tumor_cells"),
            physical_location=payload.get("physical_location"),
            analyses_performed=payload.get("analyses_performed"),
            derived_from=payload.get("derived_from"),
        )
