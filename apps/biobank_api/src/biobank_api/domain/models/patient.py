"""Biobank patient domain model — the root ``<patient>`` element of one XML export file.

Source of truth: ``docs/patient-data-report.md`` §5 (demographics) and §6.1 (the
``<patient>`` element, its attributes and child blocks). One file = one patient, holding
patient-level radiology ``<AccessionNumbers>``, an ``<LTS>`` block of archived research
:class:`~biobank_api.domain.models.sample.Sample` rows, and an ``<STS>`` block of
:class:`~biobank_api.domain.models.sample.DiagnosticSpecimen` records.

Maps to FAIR Genomes: the patient attributes -> ``Personal`` (and ``consent`` ->
``Individual consent``); the projection itself is left to the serialization boundary
(this stays an XML-faithful source model). Pure: a frozen dataclass, no I/O, no
framework imports. ``from_raw`` applies :mod:`biobank_api.domain.cleaning`;
``__post_init__`` enforces invariants.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from biobank_api.domain.cleaning import (
    clean_accessions,
    clean_text,
    parse_consent,
    parse_month,
    parse_sex,
    parse_year,
)
from biobank_api.domain.enums import Sex
from biobank_api.domain.models.sample import DiagnosticSpecimen, Sample

# Sanity bounds for a patient birth year (report observed 1928–2023, paediatric cases
# into the 2000s); kept wide and clock-independent so validation stays deterministic.
MIN_BIRTH_YEAR = 1900
MAX_BIRTH_YEAR = 2100


@dataclass(frozen=True)
class Patient:
    """A biobank patient — the root ``<patient>`` element (report §6.1).

    A ``consent="false"`` patient is exported as a self-closing stub with no clinical
    data (report §4 / §7); that invariant is enforced below.

    Example::

        <patient biobank="MOU" consent="true" id="138423" month="--05" sex="female"
                 year="1943"> ... <LTS>...</LTS> <STS>...</STS> </patient>
    """

    patient_id: str  # <patient id> attr  -> Personal.personal_identifier
    biobank: str | None = None  # <patient biobank> attr (always "MOU" in this dataset)
    consent: bool | None = None  # <patient consent> attr  -> Individual consent
    sex: Sex | None = None  # <patient sex> attr  -> Personal.gender_at_birth
    birth_year: int | None = None  # <patient year> gYear  -> Personal.year_of_birth
    birth_month: int | None = (
        None  # <patient month> gMonth (1-12; optional in the source)
    )
    accession_numbers: list[str] = field(
        default_factory=list
    )  # patient-level <AccessionNumbers>/<Number> (radiology linkage)
    samples: list[Sample] = field(
        default_factory=list
    )  # <LTS> archived research samples
    diagnostic_specimens: list[DiagnosticSpecimen] = field(
        default_factory=list
    )  # <STS> diagnostic specimens

    def __post_init__(self) -> None:
        if not self.patient_id.strip():
            raise ValueError("patient_id must not be empty")
        if self.birth_year is not None and not (
            MIN_BIRTH_YEAR <= self.birth_year <= MAX_BIRTH_YEAR
        ):
            raise ValueError(f"birth_year out of range: {self.birth_year}")
        if self.birth_month is not None and not (1 <= self.birth_month <= 12):
            raise ValueError(f"birth_month must be 1-12, got {self.birth_month}")
        # consent="false" => self-closing stub with no exported clinical data (report §4/§7).
        if self.consent is False and (
            self.samples or self.diagnostic_specimens or self.accession_numbers
        ):
            raise ValueError("a patient without consent must not carry clinical data")

    @classmethod
    def from_raw(
        cls,
        *,
        patient_id: str | None,
        biobank: str | None = None,
        consent: str | None = None,
        sex: str | None = None,
        year: str | None = None,
        month: str | None = None,
        accession_numbers: list[str] | None = None,
        samples: list[Sample] | None = None,
        diagnostic_specimens: list[DiagnosticSpecimen] | None = None,
    ) -> Patient:
        """Build a :class:`Patient` from raw ``<patient>`` attribute strings.

        ``samples`` / ``diagnostic_specimens`` are already-constructed child models (each
        built via its own ``from_raw``); the scalar attributes are cleaned here.
        """
        return cls(
            patient_id=clean_text(patient_id) or "",
            biobank=clean_text(biobank),
            consent=parse_consent(consent),
            sex=parse_sex(sex),
            birth_year=parse_year(year),
            birth_month=parse_month(month),
            accession_numbers=clean_accessions(accession_numbers),
            samples=list(samples) if samples else [],
            diagnostic_specimens=(
                list(diagnostic_specimens) if diagnostic_specimens else []
            ),
        )
