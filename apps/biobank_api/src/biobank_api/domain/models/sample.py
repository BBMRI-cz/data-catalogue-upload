"""Biobank sample domain models (the ``<LTS>`` and ``<STS>`` blocks of a ``<patient>``).

Source of truth: ``docs/patient-data-report.md`` — §6.3 Long-Term Storage (the archived
research samples ``<tissue>`` / ``<serum>`` / ``<genome>``), §6.3.1 the shared sample
attribute group, §6.4 Short-Term Storage (``<diagnosisMaterial>`` diagnostic specimens),
and §6.5 the material-type code tables.

The three LTS sample types share an attribute group (modelled on :class:`Sample`) and
add a few type-specific children (the subclasses). All map to FAIR Genomes
``Material``; the carried ICD-10 ``diagnosis`` links to ``Clinical``. STS
:class:`DiagnosticSpecimen` records are *not* archived — the specimen is consumed during
diagnosis — so they are modelled separately and only carry the diagnosis linkage.

Models are pure: frozen dataclasses, no I/O, no framework imports. ``from_raw`` factories
apply :mod:`biobank_api.domain.cleaning` to raw XML strings; ``__post_init__`` enforces
invariants. Required fields are validated as non-empty; optional fields are validated
only when present (the XML parser, issue #33, guarantees presence of schema-required
values).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date, datetime
from typing import ClassVar

from biobank_api.domain.cleaning import (
    clean_accessions,
    clean_code,
    clean_sample_id,
    clean_text,
    dash_to_none,
    parse_int,
    parse_retrieved,
    parse_temporal,
    parse_year,
)
from biobank_api.domain.enums import Retrieved
from biobank_api.domain.models.material_types import material_type_label

# Sanity bounds for a sample collection year (report observed 2022–2026); kept wide so
# the domain tolerates historical/future exports without hard-coding the current date.
MIN_COLLECTION_YEAR = 1900
MAX_COLLECTION_YEAR = 2100


def _to_datetime(value: date | datetime) -> datetime:
    """Normalise a ``date``/``datetime`` to a ``datetime`` for safe ordering comparison."""
    if isinstance(value, datetime):
        return value
    return datetime(value.year, value.month, value.day)


@dataclass(frozen=True)
class Sample:
    """An archived Long-Term Storage sample — shared ``<tissue>``/``<serum>``/``<genome>``
    attribute group (report §6.3.1). Maps to FAIR Genomes ``Material``.

    Subclassed by :class:`TissueSample`, :class:`SerumSample` and :class:`GenomeSample`
    for the type-specific child elements. A single collection event (same ``number``)
    commonly yields several rows, one per ``materialType`` (report §6.3.2 / §7).
    """

    # Discriminator used to resolve ``material_type`` against the right lookup table;
    # overridden by each subclass. Not a dataclass field.
    _SAMPLE_TYPE: ClassVar[str] = ""

    sample_id: str  # sampleId attr  -> Material.material_identifier
    material_type: (
        str  # <materialType> code  -> Material.biospecimen_type / pathological_state
    )
    event_number: int | None = None  # number attr (shared by all aliquots of one event)
    collection_year: int | None = (
        None  # year attr (sample collection year, not birth year)
    )
    biopsy: str | None = None  # biopsy attr ("-" -> None): pathology lab reference
    predictive_number: str | None = (
        None  # predictive_number attr ("-" -> None): sequencing reference
    )
    samples_no: int | None = None  # <samplesNo>: total aliquots created
    available_samples_no: int | None = (
        None  # <availableSamplesNo>: aliquots still available
    )
    accession_numbers: list[str] = field(
        default_factory=list
    )  # sample-level <AccessionNumbers>/<Number> (post-2024 files)

    def __post_init__(self) -> None:
        if not self.sample_id.strip():
            raise ValueError("sample_id must not be empty")
        if not self.material_type.strip():
            raise ValueError("material_type must not be empty")
        for name in ("event_number", "samples_no", "available_samples_no"):
            value = getattr(self, name)
            if value is not None and value < 0:
                raise ValueError(f"{name} must not be negative, got {value}")
        if self.collection_year is not None and not (
            MIN_COLLECTION_YEAR <= self.collection_year <= MAX_COLLECTION_YEAR
        ):
            raise ValueError(f"collection_year out of range: {self.collection_year}")
        if (
            self.samples_no is not None
            and self.available_samples_no is not None
            and self.available_samples_no > self.samples_no
        ):
            raise ValueError(
                "available_samples_no cannot exceed samples_no "
                f"({self.available_samples_no} > {self.samples_no})"
            )

    @property
    def material_type_label(self) -> str | None:
        """English meaning of ``material_type`` for this sample type (report §6.5)."""
        return material_type_label(self._SAMPLE_TYPE, self.material_type)


@dataclass(frozen=True)
class TissueSample(Sample):
    """Frozen surgical/tumour ``<tissue>`` sample (report §6.3.2).

    Example::

        <tissue biopsy="2023/2872-1" number="181" predictive_number="2023/1052"
                sampleId="BBM:2023:181:1" year="2023">
          <samplesNo>3</samplesNo><availableSamplesNo>3</availableSamplesNo>
          <materialType>1</materialType><pTNM>T1N0M</pTNM><morphology>8380/31</morphology>
          <diagnosis>C56</diagnosis>
          <cutTime>2023-03-24T11:15:00</cutTime><freezeTime>2023-03-24T11:20:00</freezeTime>
          <retrieved>operational</retrieved>
        </tissue>
    """

    _SAMPLE_TYPE: ClassVar[str] = "tissue"

    diagnosis: str | None = None  # <diagnosis> ICD-10  -> Material.belongs_to_diagnosis
    p_tnm: str | None = (
        None  # <pTNM> pathological TNM staging (opaque free text, report §6.7)
    )
    morphology: str | None = None  # <morphology> ICD-O-3 code (report §6.8)
    cut_time: date | datetime | None = None  # <cutTime>  -> Material.sampling_timestamp
    freeze_time: date | datetime | None = (
        None  # <freezeTime> (a few minutes after cutTime)
    )
    retrieved: Retrieved | None = None  # <retrieved> (usually "operational" for tissue)

    def __post_init__(self) -> None:
        super().__post_init__()
        if self.cut_time is not None and self.freeze_time is not None:
            if _to_datetime(self.freeze_time) < _to_datetime(self.cut_time):
                raise ValueError("freeze_time cannot precede cut_time")

    @classmethod
    def from_raw(
        cls,
        *,
        sample_id: str | None,
        material_type: str | None,
        number: str | None = None,
        year: str | None = None,
        biopsy: str | None = None,
        predictive_number: str | None = None,
        samples_no: str | None = None,
        available_samples_no: str | None = None,
        accession_numbers: list[str] | None = None,
        diagnosis: str | None = None,
        p_tnm: str | None = None,
        morphology: str | None = None,
        cut_time: str | None = None,
        freeze_time: str | None = None,
        retrieved: str | None = None,
    ) -> TissueSample:
        """Build a :class:`TissueSample` from raw ``<tissue>`` XML strings."""
        return cls(
            sample_id=clean_sample_id(sample_id) or "",
            material_type=clean_text(material_type) or "",
            event_number=parse_int(number),
            collection_year=parse_year(year),
            biopsy=dash_to_none(biopsy),
            predictive_number=dash_to_none(predictive_number),
            samples_no=parse_int(samples_no),
            available_samples_no=parse_int(available_samples_no),
            accession_numbers=clean_accessions(accession_numbers),
            diagnosis=clean_code(diagnosis),
            p_tnm=clean_code(p_tnm),
            morphology=clean_code(morphology),
            cut_time=parse_temporal(cut_time),
            freeze_time=parse_temporal(freeze_time),
            retrieved=parse_retrieved(retrieved),
        )


@dataclass(frozen=True)
class SerumSample(Sample):
    """Frozen blood-derived liquid ``<serum>`` sample (report §6.3.3).

    Example::

        <serum biopsy="-" number="3249" predictive_number="-" sampleId="BBMs:2022:3249:SD"
               year="2022">
          <samplesNo>1</samplesNo><availableSamplesNo>1</availableSamplesNo>
          <materialType>SD</materialType><takingDate>2022-12-07</takingDate>
        </serum>
    """

    _SAMPLE_TYPE: ClassVar[str] = "serum"

    diagnosis: str | None = (
        None  # <diagnosis> ICD-10 (when linked to a diagnosis event)
    )
    taking_date: date | datetime | None = (
        None  # <takingDate>  -> Material.sampling_timestamp
    )
    retrieved: Retrieved | None = None  # <retrieved> (present ~60% of serum rows)

    @classmethod
    def from_raw(
        cls,
        *,
        sample_id: str | None,
        material_type: str | None,
        number: str | None = None,
        year: str | None = None,
        biopsy: str | None = None,
        predictive_number: str | None = None,
        samples_no: str | None = None,
        available_samples_no: str | None = None,
        accession_numbers: list[str] | None = None,
        diagnosis: str | None = None,
        taking_date: str | None = None,
        retrieved: str | None = None,
    ) -> SerumSample:
        """Build a :class:`SerumSample` from raw ``<serum>`` XML strings."""
        return cls(
            sample_id=clean_sample_id(sample_id) or "",
            material_type=clean_text(material_type) or "",
            event_number=parse_int(number),
            collection_year=parse_year(year),
            biopsy=dash_to_none(biopsy),
            predictive_number=dash_to_none(predictive_number),
            samples_no=parse_int(samples_no),
            available_samples_no=parse_int(available_samples_no),
            accession_numbers=clean_accessions(accession_numbers),
            diagnosis=clean_code(diagnosis),
            taking_date=parse_temporal(taking_date),
            retrieved=parse_retrieved(retrieved),
        )


@dataclass(frozen=True)
class GenomeSample(Sample):
    """Frozen DNA / nucleic-acid ``<genome>`` sample (report §6.3.4).

    Example::

        <genome biopsy="-" number="249" predictive_number="-" sampleId="BBMd:2023:249:PK"
                year="2023">
          <samplesNo>1</samplesNo><availableSamplesNo>1</availableSamplesNo>
          <materialType>PK</materialType><takingDate>2023-03-24</takingDate>
          <retrieved>unknown</retrieved>
        </genome>
    """

    _SAMPLE_TYPE: ClassVar[str] = "genome"

    taking_date: date | datetime | None = (
        None  # <takingDate>  -> Material.sampling_timestamp
    )
    retrieved: Retrieved | None = None  # <retrieved>

    @classmethod
    def from_raw(
        cls,
        *,
        sample_id: str | None,
        material_type: str | None,
        number: str | None = None,
        year: str | None = None,
        biopsy: str | None = None,
        predictive_number: str | None = None,
        samples_no: str | None = None,
        available_samples_no: str | None = None,
        accession_numbers: list[str] | None = None,
        taking_date: str | None = None,
        retrieved: str | None = None,
    ) -> GenomeSample:
        """Build a :class:`GenomeSample` from raw ``<genome>`` XML strings."""
        return cls(
            sample_id=clean_sample_id(sample_id) or "",
            material_type=clean_text(material_type) or "",
            event_number=parse_int(number),
            collection_year=parse_year(year),
            biopsy=dash_to_none(biopsy),
            predictive_number=dash_to_none(predictive_number),
            samples_no=parse_int(samples_no),
            available_samples_no=parse_int(available_samples_no),
            accession_numbers=clean_accessions(accession_numbers),
            taking_date=parse_temporal(taking_date),
            retrieved=parse_retrieved(retrieved),
        )


@dataclass(frozen=True)
class DiagnosticSpecimen:
    """A Short-Term Storage ``<diagnosisMaterial>`` record (report §6.4.1).

    Diagnostic specimen processed for diagnosis and consumed in the process — not
    archived for research. It exists purely to link the patient's ICD-10 ``diagnosis``
    to a collection event, so it maps to FAIR Genomes ``Clinical.clinical_diagnosis``
    rather than ``Material``.

    Example::

        <diagnosisMaterial number="118485" sampleId="&:2022:118485" year="2022">
          <materialType>S</materialType><diagnosis>C504</diagnosis>
          <takingDate>2022-09-20T10:44:00</takingDate><retrieved>unknown</retrieved>
        </diagnosisMaterial>
    """

    sample_id: str  # sampleId attr ("&:{year}:{number}", report §7 on the "&" prefix)
    specimen_number: int | None = None  # number attr
    year: int | None = None  # year attr
    material_type: str | None = None  # <materialType> (always "S" in observed data)
    diagnosis: str | None = None  # <diagnosis> ICD-10  -> Clinical.clinical_diagnosis
    taking_date: date | datetime | None = None  # <takingDate>
    retrieved: Retrieved | None = None  # <retrieved> (always "unknown" for STS)

    def __post_init__(self) -> None:
        if not self.sample_id.strip():
            raise ValueError("sample_id must not be empty")
        if self.specimen_number is not None and self.specimen_number < 0:
            raise ValueError(
                f"specimen_number must not be negative, got {self.specimen_number}"
            )
        if self.year is not None and not (
            MIN_COLLECTION_YEAR <= self.year <= MAX_COLLECTION_YEAR
        ):
            raise ValueError(f"year out of range: {self.year}")

    @property
    def material_type_label(self) -> str | None:
        """English meaning of ``material_type`` (report §6.5)."""
        return material_type_label("diagnosis_material", self.material_type)

    @classmethod
    def from_raw(
        cls,
        *,
        sample_id: str | None,
        number: str | None = None,
        year: str | None = None,
        material_type: str | None = None,
        diagnosis: str | None = None,
        taking_date: str | None = None,
        retrieved: str | None = None,
    ) -> DiagnosticSpecimen:
        """Build a :class:`DiagnosticSpecimen` from raw ``<diagnosisMaterial>`` XML strings."""
        return cls(
            sample_id=clean_sample_id(sample_id) or "",
            specimen_number=parse_int(number),
            year=parse_year(year),
            material_type=clean_text(material_type),
            diagnosis=clean_code(diagnosis),
            taking_date=parse_temporal(taking_date),
            retrieved=parse_retrieved(retrieved),
        )
