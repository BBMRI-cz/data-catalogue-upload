from __future__ import annotations

from datetime import date, datetime

from biobank_api.domain.models import (
    DiagnosticSpecimen,
    GenomeSample,
    Patient,
    Sample,
    SerumSample,
    TissueSample,
)
from biobank_api.infrastructure.db.models import (
    DiagnosticSpecimenORM,
    GenomeSampleORM,
    PatientORM,
    SerumSampleORM,
    TissueSampleORM,
)


def _as_datetime(value: date | datetime) -> datetime:
    # DateTime columns need a datetime; a date-only value becomes midnight, so a date
    # round-trips back as a midnight datetime (lossy).
    if isinstance(value, datetime):
        return value
    return datetime(value.year, value.month, value.day)


def _opt_datetime(value: date | datetime | None) -> datetime | None:
    return None if value is None else _as_datetime(value)


# --- domain -> ORM ---------------------------------------------------------------------


def tissue_to_orm(sample: TissueSample, patient_id: str) -> TissueSampleORM:
    return TissueSampleORM(
        sample_id=sample.sample_id,
        patient_id=patient_id,
        material_type=sample.material_type,
        event_number=sample.event_number,
        collection_year=sample.collection_year,
        biopsy=sample.biopsy,
        predictive_number=sample.predictive_number,
        samples_no=sample.samples_no,
        available_samples_no=sample.available_samples_no,
        accession_numbers=list(sample.accession_numbers),
        diagnosis=sample.diagnosis,
        p_tnm=sample.p_tnm,
        morphology=sample.morphology,
        cut_time=_opt_datetime(sample.cut_time),
        freeze_time=_opt_datetime(sample.freeze_time),
        retrieved=sample.retrieved,
    )


def serum_to_orm(sample: SerumSample, patient_id: str) -> SerumSampleORM:
    return SerumSampleORM(
        sample_id=sample.sample_id,
        patient_id=patient_id,
        material_type=sample.material_type,
        event_number=sample.event_number,
        collection_year=sample.collection_year,
        biopsy=sample.biopsy,
        predictive_number=sample.predictive_number,
        samples_no=sample.samples_no,
        available_samples_no=sample.available_samples_no,
        accession_numbers=list(sample.accession_numbers),
        diagnosis=sample.diagnosis,
        taking_date=_opt_datetime(sample.taking_date),
        retrieved=sample.retrieved,
    )


def genome_to_orm(sample: GenomeSample, patient_id: str) -> GenomeSampleORM:
    return GenomeSampleORM(
        sample_id=sample.sample_id,
        patient_id=patient_id,
        material_type=sample.material_type,
        event_number=sample.event_number,
        collection_year=sample.collection_year,
        biopsy=sample.biopsy,
        predictive_number=sample.predictive_number,
        samples_no=sample.samples_no,
        available_samples_no=sample.available_samples_no,
        accession_numbers=list(sample.accession_numbers),
        taking_date=_opt_datetime(sample.taking_date),
        retrieved=sample.retrieved,
    )


def specimen_to_orm(
    specimen: DiagnosticSpecimen, patient_id: str
) -> DiagnosticSpecimenORM:
    return DiagnosticSpecimenORM(
        sample_id=specimen.sample_id,
        patient_id=patient_id,
        specimen_number=specimen.specimen_number,
        year=specimen.year,
        material_type=specimen.material_type,
        diagnosis=specimen.diagnosis,
        taking_date=_opt_datetime(specimen.taking_date),
        retrieved=specimen.retrieved,
    )


def patient_to_orm(patient: Patient) -> PatientORM:
    orm = PatientORM(
        patient_id=patient.patient_id,
        biobank=patient.biobank,
        consent=patient.consent,
        sex=patient.sex,
        birth_year=patient.birth_year,
        birth_month=patient.birth_month,
        accession_numbers=list(patient.accession_numbers),
    )
    for sample in patient.samples:
        if isinstance(sample, TissueSample):
            orm.tissue_samples.append(tissue_to_orm(sample, patient.patient_id))
        elif isinstance(sample, SerumSample):
            orm.serum_samples.append(serum_to_orm(sample, patient.patient_id))
        elif isinstance(sample, GenomeSample):
            orm.genome_samples.append(genome_to_orm(sample, patient.patient_id))
        else:  # pragma: no cover - guards against a new, unmapped Sample subclass
            raise TypeError(f"unsupported sample type: {type(sample).__name__}")
    for specimen in patient.diagnostic_specimens:
        orm.diagnostic_specimens.append(specimen_to_orm(specimen, patient.patient_id))
    return orm


# --- ORM -> domain ---------------------------------------------------------------------


def orm_to_tissue(row: TissueSampleORM) -> TissueSample:
    return TissueSample(
        sample_id=row.sample_id,
        material_type=row.material_type,
        event_number=row.event_number,
        collection_year=row.collection_year,
        biopsy=row.biopsy,
        predictive_number=row.predictive_number,
        samples_no=row.samples_no,
        available_samples_no=row.available_samples_no,
        accession_numbers=list(row.accession_numbers),
        diagnosis=row.diagnosis,
        p_tnm=row.p_tnm,
        morphology=row.morphology,
        cut_time=row.cut_time,
        freeze_time=row.freeze_time,
        retrieved=row.retrieved,
    )


def orm_to_serum(row: SerumSampleORM) -> SerumSample:
    return SerumSample(
        sample_id=row.sample_id,
        material_type=row.material_type,
        event_number=row.event_number,
        collection_year=row.collection_year,
        biopsy=row.biopsy,
        predictive_number=row.predictive_number,
        samples_no=row.samples_no,
        available_samples_no=row.available_samples_no,
        accession_numbers=list(row.accession_numbers),
        diagnosis=row.diagnosis,
        taking_date=row.taking_date,
        retrieved=row.retrieved,
    )


def orm_to_genome(row: GenomeSampleORM) -> GenomeSample:
    return GenomeSample(
        sample_id=row.sample_id,
        material_type=row.material_type,
        event_number=row.event_number,
        collection_year=row.collection_year,
        biopsy=row.biopsy,
        predictive_number=row.predictive_number,
        samples_no=row.samples_no,
        available_samples_no=row.available_samples_no,
        accession_numbers=list(row.accession_numbers),
        taking_date=row.taking_date,
        retrieved=row.retrieved,
    )


def orm_to_specimen(row: DiagnosticSpecimenORM) -> DiagnosticSpecimen:
    return DiagnosticSpecimen(
        sample_id=row.sample_id,
        specimen_number=row.specimen_number,
        year=row.year,
        material_type=row.material_type,
        diagnosis=row.diagnosis,
        taking_date=row.taking_date,
        retrieved=row.retrieved,
    )


def orm_to_patient(orm: PatientORM) -> Patient:
    samples: list[Sample] = []
    samples.extend(orm_to_tissue(row) for row in orm.tissue_samples)
    samples.extend(orm_to_serum(row) for row in orm.serum_samples)
    samples.extend(orm_to_genome(row) for row in orm.genome_samples)
    return Patient(
        patient_id=orm.patient_id,
        biobank=orm.biobank,
        consent=orm.consent,
        sex=orm.sex,
        birth_year=orm.birth_year,
        birth_month=orm.birth_month,
        accession_numbers=list(orm.accession_numbers),
        samples=samples,
        diagnostic_specimens=[orm_to_specimen(row) for row in orm.diagnostic_specimens],
    )
