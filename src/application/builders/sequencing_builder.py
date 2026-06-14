from __future__ import annotations

from domain.models import (
    Analysis,
    SamplePreparation,
    Sequencing,
    SequencingData,
    SequencingEntry,
)
from domain.utils import as_list, has_any_keys, resolve_source_id


class SequencingBuilder:
    def build_sequencing_data(
        self, predictive_number: str, payload: dict
    ) -> SequencingData:
        entries_payload = as_list(payload.get("sequencing_entries"))
        if not entries_payload:
            entries_payload = [payload]
        entries: SequencingData = []
        for entry_payload in entries_payload:
            if not isinstance(entry_payload, dict):
                continue
            source_id = resolve_source_id(entry_payload, predictive_number)
            entries.append(
                SequencingEntry(
                    predictive_number=predictive_number,
                    source_id=source_id,
                    fixed_block_identifier=self._build_fixed_block_identifier(
                        entry_payload.get("fixed_block", entry_payload)
                    ),
                    sample_preparation=self._build_sample_preparation(
                        entry_payload.get("sample_preparation", entry_payload)
                    ),
                )
            )
        return entries

    def _build_fixed_block_identifier(self, payload: object) -> str | None:
        if not isinstance(payload, dict) or not payload:
            return None
        return payload.get("block_identifier")

    def _build_sample_preparation(self, payload: object) -> SamplePreparation | None:
        if not isinstance(payload, dict) or not payload:
            return None
        if not has_any_keys(
            payload,
            [
                "sampleprep_identifier",
                "belongs_to_material",
                "input_amount",
                "library_preparation_kit",
                "target_enrichment_kit",
                "sequencing_run",
                "sequencing",
            ],
        ):
            return None
        sequencing_run = None
        direct_run = payload.get("sequencing_run", payload.get("sequencing"))
        if isinstance(direct_run, dict):
            sequencing_run = self._build_sequencing_run(direct_run)
        return SamplePreparation(
            sampleprep_identifier=payload.get("sampleprep_identifier"),
            belongs_to_material=payload.get("belongs_to_material"),
            input_amount=payload.get("input_amount"),
            library_preparation_kit=payload.get("library_preparation_kit"),
            pcr_free=payload.get("pcr_free"),
            target_enrichment_kit=payload.get("target_enrichment_kit"),
            full_sequence_genes=payload.get("full_sequence_genes"),
            partial_sequence_genes=payload.get("partial_sequence_genes"),
            umis_present=payload.get("umis_present"),
            intended_insert_size=payload.get("intended_insert_size"),
            intended_read_length=payload.get("intended_read_length"),
            sequencing=sequencing_run,
        )

    def _build_sequencing_run(self, payload: object) -> Sequencing | None:
        if not isinstance(payload, dict) or not payload:
            return None
        if not has_any_keys(
            payload,
            [
                "sequencing_identifier",
                "belongs_to_sample_preparation",
                "sequencing_date",
                "sequencing_platform",
                "sequencing_instrument_model",
                "sequencing_method",
                "analysis",
            ],
        ):
            return None
        analysis = [
            self._build_analysis(item)
            for item in as_list(payload.get("analysis"))
            if isinstance(item, dict)
        ]
        return Sequencing(
            sequencing_identifier=payload.get("sequencing_identifier"),
            belongs_to_sample_preparation=payload.get("belongs_to_sample_preparation"),
            sequencing_date=payload.get("sequencing_date"),
            sequencing_platform=payload.get("sequencing_platform"),
            sequencing_instrument_model=payload.get("sequencing_instrument_model"),
            sequencing_method=payload.get("sequencing_method"),
            median_read_depth=payload.get("median_read_depth"),
            observed_read_length=payload.get("observed_read_length"),
            observed_insert_size=payload.get("observed_insert_size"),
            percentage_q30=payload.get("percentage_q30"),
            percentage_tr20=payload.get("percentage_tr20"),
            other_quality_metrics=payload.get("other_quality_metrics"),
            analysis=analysis[0] if analysis else None,
        )

    def _build_analysis(self, payload: dict) -> Analysis:
        return Analysis(
            analysis_identifier=payload.get("analysis_identifier"),
            belongs_to_sequencing=payload.get("belongs_to_sequencing"),
            physical_data_location=payload.get("physical_data_location"),
            abstract_data_location=payload.get("abstract_data_location"),
            data_formats_stored=payload.get("data_formats_stored"),
            algorithms_used=payload.get("algorithms_used"),
            reference_genome_used=payload.get("reference_genome_used"),
            bioinformatic_protocol_used=payload.get("bioinformatic_protocol_used"),
            bioinformatic_protocol_deviation=payload.get(
                "bioinformatic_protocol_deviation"
            ),
            reason_for_bioinformatic_protocol_deviation=payload.get(
                "reason_for_bioinformatic_protocol_deviation"
            ),
            wgs_guideline_followed=payload.get("wgs_guideline_followed"),
        )
