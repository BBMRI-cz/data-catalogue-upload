from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Analysis:
    analysis_identifier: str | None = None
    belongs_to_sequencing: str | None = None
    physical_data_location: str | None = None
    abstract_data_location: str | None = None
    data_formats_stored: list[str] | None = None
    algorithms_used: list[str] | None = None
    reference_genome_used: str | None = None
    bioinformatic_protocol_used: str | None = None
    bioinformatic_protocol_deviation: str | None = None
    reason_for_bioinformatic_protocol_deviation: str | None = None
    wgs_guideline_followed: str | None = None


@dataclass(frozen=True)
class Sequencing:
    sequencing_identifier: str | None = None
    belongs_to_sample_preparation: str | None = None
    sequencing_date: str | None = None
    sequencing_platform: str | None = None
    sequencing_instrument_model: str | None = None
    sequencing_method: str | None = None
    median_read_depth: int | None = None
    observed_read_length: int | None = None
    observed_insert_size: int | None = None
    percentage_q30: float | None = None
    percentage_tr20: float | None = None
    other_quality_metrics: str | None = None
    analysis: Analysis | None = None


@dataclass(frozen=True)
class SamplePreparation:
    sampleprep_identifier: str | None = None
    belongs_to_material: str | None = None
    input_amount: str | None = None
    library_preparation_kit: str | None = None
    pcr_free: bool | None = None
    target_enrichment_kit: str | None = None
    full_sequence_genes: list[str] | None = None
    partial_sequence_genes: list[str] | None = None
    umis_present: bool | None = None
    intended_insert_size: int | None = None
    intended_read_length: int | None = None
    sequencing: Sequencing | None = None


@dataclass(frozen=True)
class SequencingEntry:
    predictive_number: str
    source_id: str
    fixed_block_identifier: str | None = None
    sample_preparation: SamplePreparation | None = None


SequencingData = list[SequencingEntry]
