from __future__ import annotations

from datetime import datetime

from sqlalchemy import select
from sqlalchemy.orm import Session

from biobank_api.domain.enums import Retrieved, Sex
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
    SerumSampleORM,
    TissueSampleORM,
)
from biobank_api.infrastructure.db.repositories import SqlBiobankRepository


def _full_patient() -> Patient:
    return Patient(
        patient_id="138423",
        biobank="MOU",
        consent=True,
        sex=Sex.FEMALE,
        birth_year=1943,
        birth_month=5,
        accession_numbers=["RAD-1", "RAD-2"],
        samples=[
            TissueSample(
                sample_id="BBM:2023:181:1",
                material_type="1",
                event_number=181,
                collection_year=2023,
                biopsy="2023/2872-1",
                predictive_number="2023/1052",
                samples_no=3,
                available_samples_no=3,
                accession_numbers=["ACC-1"],
                diagnosis="C56",
                p_tnm="T1N0M",
                morphology="8380/31",
                cut_time=datetime(2023, 3, 24, 11, 15),
                freeze_time=datetime(2023, 3, 24, 11, 20),
                retrieved=Retrieved.OPERATIONAL,
            ),
            SerumSample(
                sample_id="BBMs:2022:3249:SD",
                material_type="SD",
                event_number=3249,
                collection_year=2022,
                samples_no=1,
                available_samples_no=1,
                taking_date=datetime(2022, 12, 7),
                retrieved=Retrieved.UNKNOWN,
            ),
            GenomeSample(
                sample_id="BBMd:2023:249:PK",
                material_type="PK",
                event_number=249,
                collection_year=2023,
                samples_no=1,
                available_samples_no=1,
                taking_date=datetime(2023, 3, 24),
                retrieved=Retrieved.UNKNOWN,
            ),
        ],
        diagnostic_specimens=[
            DiagnosticSpecimen(
                sample_id="&:2022:118485",
                specimen_number=118485,
                year=2022,
                material_type="S",
                diagnosis="C504",
                taking_date=datetime(2022, 9, 20, 10, 44),
                retrieved=Retrieved.UNKNOWN,
            )
        ],
    )


def _by_id(samples: list[Sample]) -> dict[str, Sample]:
    return {sample.sample_id: sample for sample in samples}


def _count(session: Session, orm_class: type) -> int:
    return len(session.scalars(select(orm_class)).all())


def test_save_and_list_round_trips_full_patient(session: Session) -> None:
    patient = _full_patient()
    SqlBiobankRepository(session).save_patients([patient])

    (loaded,) = SqlBiobankRepository(session).list_patients()

    assert loaded.patient_id == patient.patient_id
    assert loaded.biobank == patient.biobank
    assert loaded.consent is True
    assert loaded.sex is Sex.FEMALE
    assert loaded.birth_year == 1943
    assert loaded.birth_month == 5
    assert loaded.accession_numbers == ["RAD-1", "RAD-2"]
    # Sample types live in separate tables, so order across types is not guaranteed.
    assert _by_id(loaded.samples) == _by_id(patient.samples)
    assert loaded.diagnostic_specimens == patient.diagnostic_specimens


def test_round_trips_consent_false_stub(session: Session) -> None:
    stub = Patient(patient_id="P-STUB", biobank="MOU", consent=False)
    SqlBiobankRepository(session).save_patients([stub])

    (loaded,) = SqlBiobankRepository(session).list_patients()
    assert loaded == stub


def test_resaving_same_patient_is_idempotent(session: Session) -> None:
    repo = SqlBiobankRepository(session)
    repo.save_patients([_full_patient()])
    repo.save_patients([_full_patient()])

    (loaded,) = repo.list_patients()
    assert len(loaded.samples) == 3
    assert len(loaded.diagnostic_specimens) == 1
    assert _count(session, TissueSampleORM) == 1
    assert _count(session, SerumSampleORM) == 1
    assert _count(session, GenomeSampleORM) == 1
    assert _count(session, DiagnosticSpecimenORM) == 1


def test_resaving_replaces_children(session: Session) -> None:
    repo = SqlBiobankRepository(session)
    repo.save_patients([_full_patient()])

    updated = Patient(
        patient_id="138423",
        biobank="MOU",
        consent=True,
        samples=[TissueSample(sample_id="NEW:1", material_type="1")],
    )
    repo.save_patients([updated])

    (loaded,) = repo.list_patients()
    assert {sample.sample_id for sample in loaded.samples} == {"NEW:1"}
    assert loaded.diagnostic_specimens == []
    # stale rows from the previous save are gone from every child table
    assert _count(session, TissueSampleORM) == 1
    assert _count(session, SerumSampleORM) == 0
    assert _count(session, GenomeSampleORM) == 0
    assert _count(session, DiagnosticSpecimenORM) == 0


def test_list_patients_empty(session: Session) -> None:
    assert SqlBiobankRepository(session).list_patients() == []
