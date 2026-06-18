from __future__ import annotations

from datetime import date, datetime

from biobank_api.domain.enums import Retrieved, Sex


def clean_text(raw: str | None) -> str | None:
    if raw is None:
        return None
    stripped = raw.strip()
    return stripped or None


def clean_accessions(values: list[str] | None) -> list[str]:
    if not values:
        return []
    return [cleaned for cleaned in (clean_text(value) for value in values) if cleaned]


def clean_code(raw: str | None) -> str | None:
    # diagnosis/morphology/pTNM codes are kept exactly as exported (e.g. ICD-10 stays dot-less).
    return clean_text(raw)


def clean_sample_id(raw: str | None) -> str | None:
    # lxml already decodes the STS "&amp;" entity, so no entity handling is needed here.
    return clean_text(raw)


def dash_to_none(raw: str | None) -> str | None:
    # "-" is the source's "not applicable" sentinel for biopsy / predictive_number.
    cleaned = clean_text(raw)
    if cleaned == "-":
        return None
    return cleaned


def parse_consent(raw: str | None) -> bool | None:
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    lowered = cleaned.lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False
    raise ValueError(f"invalid consent value: {raw!r}")


def parse_sex(raw: str | None) -> Sex | None:
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    try:
        return Sex(cleaned.lower())
    except ValueError as exc:
        raise ValueError(f"invalid sex value: {raw!r}") from exc


def parse_retrieved(raw: str | None) -> Retrieved | None:
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    try:
        return Retrieved(cleaned.lower())
    except ValueError as exc:
        raise ValueError(f"invalid retrieved value: {raw!r}") from exc


def parse_int(raw: str | None) -> int | None:
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    return int(cleaned)


def parse_year(raw: str | None) -> int | None:
    return parse_int(raw)


def parse_month(raw: str | None) -> int | None:
    # <patient month> is xs:gMonth ("--MM"); strip the leading "--" (a bare "7" also parses).
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    if cleaned.startswith("--"):
        cleaned = cleaned[2:]
    return int(cleaned)


def parse_temporal(raw: str | None) -> date | datetime | None:
    # xs:date | xs:dateTime union: a "T" time component yields a datetime, otherwise a date.
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    if "T" in cleaned:
        return datetime.fromisoformat(cleaned)
    return date.fromisoformat(cleaned)
