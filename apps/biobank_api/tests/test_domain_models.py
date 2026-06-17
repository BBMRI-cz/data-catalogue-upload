"""Unit tests for the biobank domain models: construction, ``from_raw``, invariants.

The ``from_raw`` cases mirror the example files in ``docs/patient-data-report.md`` §8.
"""

from __future__ import annotations

from datetime import date, datetime

import pytest

from biobank_api.domain.enums import Retrieved, Sex
from biobank_api.domain.models import (
    DiagnosticSpecimen,
    GenomeSample,
    Patient,
    Sample,
    SerumSample,
    TissueSample,
    material_type_label,
)

# --- construction + material-type labels -----------------------------------------


def test_sample_base_constructs_and_labels_unknown_type_as_none() -> None:
    sample = Sample(sample_id="BBM:2023:181:1", material_type="1")
    # the abstract base has no sample-type discriminator, so no label is resolved
    assert sample.material_type_label is None


def test_tissue_sample_material_type_label() -> None:
    sample = TissueSample(sample_id="BBM:2023:181:53", material_type="53")
    assert sample.material_type_label == "Malignant tumour (RNAlater)"


def test_material_type_label_unknown_code_is_none() -> None:
    assert material_type_label("tissue", "999") is None
    assert material_type_label("serum", None) is None


# --- from_raw mirrors report §8 examples ------------------------------------------


def test_tissue_from_raw_example5() -> None:
    sample = TissueSample.from_raw(
        sample_id="BBM:2023:181:1",
        material_type="1",
        number="181",
        year="2023",
        biopsy="2023/2872-1",
        predictive_number="2023/1052",
        samples_no="3",
        available_samples_no="3",
        diagnosis="C56",
        p_tnm="T1N0M",
        morphology="8380/31",
        cut_time="2023-03-24T11:15:00",
        freeze_time="2023-03-24T11:20:00",
        retrieved="operational",
    )
    assert sample.sample_id == "BBM:2023:181:1"
    assert sample.material_type == "1"
    assert sample.event_number == 181
    assert sample.collection_year == 2023
    assert sample.biopsy == "2023/2872-1"
    assert sample.predictive_number == "2023/1052"
    assert sample.samples_no == 3
    assert sample.available_samples_no == 3
    assert sample.diagnosis == "C56"
    assert sample.p_tnm == "T1N0M"
    assert sample.morphology == "8380/31"
    assert sample.cut_time == datetime(2023, 3, 24, 11, 15)
    assert sample.freeze_time == datetime(2023, 3, 24, 11, 20)
    assert sample.retrieved is Retrieved.OPERATIONAL


def test_serum_from_raw_example3_dash_sentinels() -> None:
    sample = SerumSample.from_raw(
        sample_id="BBMs:2022:3249:SD",
        material_type="SD",
        number="3249",
        year="2022",
        biopsy="-",
        predictive_number="-",
        samples_no="1",
        available_samples_no="1",
        taking_date="2022-12-07",
    )
    assert sample.biopsy is None
    assert sample.predictive_number is None
    assert sample.material_type == "SD"
    assert sample.taking_date == date(2022, 12, 7)
    assert sample.material_type_label == "Serum, nitrogen-stored"


def test_genome_from_raw_example4() -> None:
    sample = GenomeSample.from_raw(
        sample_id="BBMd:2025:1075:PS",
        material_type="PS",
        number="1075",
        year="2025",
        biopsy="-",
        predictive_number="-",
        samples_no="1",
        available_samples_no="1",
        accession_numbers=[],
        taking_date="2025-07-29",
    )
    assert sample.sample_id == "BBMd:2025:1075:PS"
    assert sample.material_type == "PS"
    assert sample.taking_date == date(2025, 7, 29)
    assert sample.accession_numbers == []


def test_diagnostic_specimen_from_raw_example2() -> None:
    specimen = DiagnosticSpecimen.from_raw(
        sample_id="&:2022:118485",
        number="118485",
        year="2022",
        material_type="S",
        diagnosis="C504",
        taking_date="2022-09-20T10:44:00",
        retrieved="unknown",
    )
    assert specimen.sample_id == "&:2022:118485"
    assert specimen.specimen_number == 118485
    assert specimen.year == 2022
    assert specimen.material_type == "S"
    assert specimen.diagnosis == "C504"
    assert specimen.taking_date == datetime(2022, 9, 20, 10, 44)
    assert specimen.retrieved is Retrieved.UNKNOWN
    assert specimen.material_type_label == "Serum / surgical specimen"


def test_patient_from_raw_consent_false_stub_example1() -> None:
    patient = Patient.from_raw(
        patient_id="271801",
        biobank="MOU",
        consent="false",
        sex="male",
        year="1948",
        month="--07",
    )
    assert patient.patient_id == "271801"
    assert patient.biobank == "MOU"
    assert patient.consent is False
    assert patient.sex is Sex.MALE
    assert patient.birth_year == 1948
    assert patient.birth_month == 7
    assert patient.samples == []


def test_patient_from_raw_with_children() -> None:
    serum = SerumSample.from_raw(
        sample_id="BBMs:2022:3249:SD", material_type="SD", taking_date="2022-12-07"
    )
    sts = DiagnosticSpecimen.from_raw(
        sample_id="&:2022:155548", diagnosis="Z129", taking_date="2022-12-07T07:35:00"
    )
    patient = Patient.from_raw(
        patient_id="138423",
        consent="true",
        sex="female",
        year="1943",
        accession_numbers=[" RDG2005041156 "],
        samples=[serum],
        diagnostic_specimens=[sts],
    )
    assert patient.accession_numbers == ["RDG2005041156"]
    assert patient.samples == [serum]
    assert patient.diagnostic_specimens == [sts]


# --- backward-compatible minimal construction (scaffold call-sites) ---------------


def test_patient_minimal_construction_stays_valid() -> None:
    patient = Patient(patient_id="P1")
    assert patient.samples == []
    assert patient.diagnostic_specimens == []


# --- invariants -------------------------------------------------------------------


def test_patient_rejects_empty_id() -> None:
    with pytest.raises(ValueError):
        Patient(patient_id="  ")


@pytest.mark.parametrize("birth_month", [0, 13, -1])
def test_patient_rejects_out_of_range_month(birth_month: int) -> None:
    with pytest.raises(ValueError):
        Patient(patient_id="P1", birth_month=birth_month)


@pytest.mark.parametrize("birth_year", [1850, 2200])
def test_patient_rejects_out_of_range_year(birth_year: int) -> None:
    with pytest.raises(ValueError):
        Patient(patient_id="P1", birth_year=birth_year)


def test_patient_without_consent_must_not_carry_data() -> None:
    serum = SerumSample(sample_id="BBMs:2022:3249:SD", material_type="SD")
    with pytest.raises(ValueError):
        Patient(patient_id="P1", consent=False, samples=[serum])
    with pytest.raises(ValueError):
        Patient(patient_id="P1", consent=False, accession_numbers=["RDG1"])


def test_sample_rejects_empty_required_fields() -> None:
    with pytest.raises(ValueError):
        TissueSample(sample_id="", material_type="1")
    with pytest.raises(ValueError):
        TissueSample(sample_id="BBM:2023:181:1", material_type="")


def test_sample_rejects_available_exceeding_total() -> None:
    with pytest.raises(ValueError):
        SerumSample(
            sample_id="BBMs:2022:3249:SD",
            material_type="SD",
            samples_no=1,
            available_samples_no=2,
        )


def test_sample_rejects_negative_counts() -> None:
    with pytest.raises(ValueError):
        SerumSample(sample_id="BBMs:2022:3249:SD", material_type="SD", samples_no=-1)


def test_tissue_rejects_freeze_before_cut() -> None:
    with pytest.raises(ValueError):
        TissueSample(
            sample_id="BBM:2023:181:1",
            material_type="1",
            cut_time=datetime(2023, 3, 24, 11, 20),
            freeze_time=datetime(2023, 3, 24, 11, 15),
        )


def test_tissue_allows_mixed_date_and_datetime_ordering() -> None:
    # freeze (datetime) after cut (date) — comparison must not raise TypeError.
    sample = TissueSample(
        sample_id="BBM:2023:181:1",
        material_type="1",
        cut_time=date(2023, 3, 24),
        freeze_time=datetime(2023, 3, 24, 11, 20),
    )
    assert sample.freeze_time == datetime(2023, 3, 24, 11, 20)


def test_diagnostic_specimen_rejects_empty_id() -> None:
    with pytest.raises(ValueError):
        DiagnosticSpecimen(sample_id="")


def test_diagnostic_specimen_rejects_out_of_range_year() -> None:
    with pytest.raises(ValueError):
        DiagnosticSpecimen(sample_id="&:2022:118485", year=1800)
