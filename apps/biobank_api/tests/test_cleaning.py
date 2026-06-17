"""Unit tests for the domain cleaning/normalization helpers."""

from __future__ import annotations

from datetime import date, datetime

import pytest

from biobank_api.domain.cleaning import (
    clean_accessions,
    clean_code,
    clean_sample_id,
    clean_text,
    dash_to_none,
    parse_consent,
    parse_int,
    parse_month,
    parse_retrieved,
    parse_sex,
    parse_temporal,
    parse_year,
)
from biobank_api.domain.enums import Retrieved, Sex


@pytest.mark.parametrize(
    ("raw", "expected"),
    [("  MOU ", "MOU"), ("", None), ("   ", None), (None, None)],
)
def test_clean_text(raw: str | None, expected: str | None) -> None:
    assert clean_text(raw) == expected


def test_clean_code_preserves_case() -> None:
    assert clean_code(" C504 ") == "C504"
    assert clean_code("8500/32") == "8500/32"


def test_clean_sample_id_keeps_sts_ampersand_prefix() -> None:
    assert clean_sample_id(" BBM:2023:181:1 ") == "BBM:2023:181:1"
    assert clean_sample_id("&:2022:118485") == "&:2022:118485"


def test_clean_accessions_strips_and_drops_empty() -> None:
    assert clean_accessions([" RDG1 ", "", "RDG2"]) == ["RDG1", "RDG2"]
    assert clean_accessions(None) == []
    assert clean_accessions([]) == []


@pytest.mark.parametrize(
    ("raw", "expected"),
    [("-", None), ("2023/2872-1", "2023/2872-1"), ("", None), (None, None)],
)
def test_dash_to_none(raw: str | None, expected: str | None) -> None:
    assert dash_to_none(raw) == expected


@pytest.mark.parametrize(
    ("raw", "expected"),
    [("true", True), ("TRUE", True), ("false", False), ("", None), (None, None)],
)
def test_parse_consent(raw: str | None, expected: bool | None) -> None:
    assert parse_consent(raw) == expected


def test_parse_consent_rejects_unknown() -> None:
    with pytest.raises(ValueError):
        parse_consent("maybe")


def test_parse_sex() -> None:
    assert parse_sex("female") is Sex.FEMALE
    assert parse_sex("MALE") is Sex.MALE
    assert parse_sex("") is None
    with pytest.raises(ValueError):
        parse_sex("other")


def test_parse_retrieved() -> None:
    assert parse_retrieved("operational") is Retrieved.OPERATIONAL
    assert parse_retrieved("unknown") is Retrieved.UNKNOWN
    assert parse_retrieved(None) is None
    with pytest.raises(ValueError):
        parse_retrieved("nope")


def test_parse_int() -> None:
    assert parse_int(" 524 ") == 524
    assert parse_int("") is None
    with pytest.raises(ValueError):
        parse_int("x")


def test_parse_year() -> None:
    assert parse_year("2023") == 2023


@pytest.mark.parametrize(
    ("raw", "expected"),
    [("--07", 7), ("7", 7), ("--12", 12), ("", None), (None, None)],
)
def test_parse_month(raw: str | None, expected: int | None) -> None:
    assert parse_month(raw) == expected


def test_parse_temporal_datetime() -> None:
    result = parse_temporal("2023-03-24T11:15:00")
    assert result == datetime(2023, 3, 24, 11, 15)
    assert isinstance(result, datetime)


def test_parse_temporal_date_only() -> None:
    result = parse_temporal("2023-03-24")
    assert result == date(2023, 3, 24)
    # date-only must stay a plain date, not a midnight datetime.
    assert type(result) is date


def test_parse_temporal_empty() -> None:
    assert parse_temporal("") is None
