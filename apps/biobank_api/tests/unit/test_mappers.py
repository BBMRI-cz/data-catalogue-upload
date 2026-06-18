from __future__ import annotations

from datetime import date, datetime

from biobank_api.domain.enums import Retrieved, Sex
from biobank_api.domain.models import (
    DiagnosticSpecimen,
    GenomeSample,
    Patient,
    SerumSample,
    TissueSample,
)
from biobank_api.infrastructure.db.mappers import (
    orm_to_patient,
    patient_to_orm,
    serum_to_orm,
)
from biobank_api.infrastructure.db.models import (
    GenomeSampleORM,
    SerumSampleORM,
    TissueSampleORM,
)


def test_patient_to_orm_routes_samples_to_their_tables() -> None:
    patient = Patient(
        patient_id="P1",
        consent=True,
        samples=[
            TissueSample(sample_id="T1", material_type="1"),
            SerumSample(sample_id="S1", material_type="SD"),
            GenomeSample(sample_id="G1", material_type="PK"),
        ],
        diagnostic_specimens=[DiagnosticSpecimen(sample_id="&:2022:1")],
    )

    orm = patient_to_orm(patient)

    assert [s.sample_id for s in orm.tissue_samples] == ["T1"]
    assert [s.sample_id for s in orm.serum_samples] == ["S1"]
    assert [s.sample_id for s in orm.genome_samples] == ["G1"]
    assert [d.sample_id for d in orm.diagnostic_specimens] == ["&:2022:1"]
    assert isinstance(orm.tissue_samples[0], TissueSampleORM)
    assert isinstance(orm.serum_samples[0], SerumSampleORM)
    assert isinstance(orm.genome_samples[0], GenomeSampleORM)
    # the FK is set without needing a session
    assert orm.tissue_samples[0].patient_id == "P1"


def test_full_patient_round_trips_through_mappers() -> None:
    patient = Patient(
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
                accession_numbers=["ACC-1"],
                diagnosis="C56",
                cut_time=datetime(2023, 3, 24, 11, 15),
                freeze_time=datetime(2023, 3, 24, 11, 20),
                retrieved=Retrieved.OPERATIONAL,
            )
        ],
        diagnostic_specimens=[
            DiagnosticSpecimen(
                sample_id="&:2022:118485",
                diagnosis="C504",
                taking_date=datetime(2022, 9, 20, 10, 44),
                retrieved=Retrieved.UNKNOWN,
            )
        ],
    )

    restored = orm_to_patient(patient_to_orm(patient))

    # exercises Sex/Retrieved enums, accession_numbers lists, and datetime fields
    assert restored == patient


def test_date_only_temporal_normalises_to_midnight_datetime() -> None:
    serum = SerumSample(
        sample_id="S1", material_type="SD", taking_date=date(2022, 12, 7)
    )

    orm = serum_to_orm(serum, "P1")

    assert orm.taking_date == datetime(2022, 12, 7, 0, 0)
