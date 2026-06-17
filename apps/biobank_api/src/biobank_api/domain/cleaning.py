"""Pure cleaning/normalization of raw biobank XML values.

Every function here turns a raw string (an XML attribute value or element text, exactly
as extracted from the export) into a typed/cleaned domain value. No I/O, no framework
imports. These are the building blocks the model ``from_raw`` factories use and the
single place the documented value quirks (report §6.x) are handled:

* ``<patient month>`` is ISO 8601 ``gMonth`` (``--MM``)            -> :func:`parse_month`
* dates are an ``xs:date`` | ``xs:dateTime`` union                 -> :func:`parse_temporal`
* ``biopsy`` / ``predictive_number`` use ``"-"`` to mean "n/a"     -> :func:`dash_to_none`
* STS ``sampleId`` carries an ``&amp;`` prefix (decoded to ``&``)  -> :func:`clean_sample_id`

Source of truth: ``docs/patient-data-report.md``.
"""

from __future__ import annotations

from datetime import date, datetime

from biobank_api.domain.enums import Retrieved, Sex


def clean_text(raw: str | None) -> str | None:
    """Strip surrounding whitespace; treat empty/whitespace-only as ``None``.

    ``"  MOU "`` -> ``"MOU"``; ``""`` / ``None`` -> ``None``.
    """
    if raw is None:
        return None
    stripped = raw.strip()
    return stripped or None


def clean_accessions(values: list[str] | None) -> list[str]:
    """Strip and drop empty entries from ``<AccessionNumbers>/<Number>`` values.

    ``[" RDG2005041156 ", ""]`` -> ``["RDG2005041156"]``.
    """
    if not values:
        return []
    return [cleaned for cleaned in (clean_text(value) for value in values) if cleaned]


def clean_code(raw: str | None) -> str | None:
    """Clean a code-like value (``diagnosis`` / ``morphology`` / ``pTNM``).

    Strips whitespace and preserves case; empty -> ``None``. ``" C504 "`` -> ``"C504"``.
    ICD-10 codes are kept dot-less exactly as exported (report §6.6).
    """
    return clean_text(raw)


def clean_sample_id(raw: str | None) -> str | None:
    """Strip a ``sampleId`` value; empty -> ``None``.

    lxml already decodes the STS ``&amp;`` entity, so the input here is already
    ``"&:2022:118485"`` (report §6.3.1 / §7). ``" BBM:2023:181:1 "`` -> ``"BBM:2023:181:1"``.
    """
    return clean_text(raw)


def dash_to_none(raw: str | None) -> str | None:
    """Map the ``"-"`` sentinel (and empty) to ``None``, else the stripped value.

    Used for ``biopsy`` / ``predictive_number`` where ``"-"`` means "not applicable"
    (report §6.3.1 / §7). ``"-"`` -> ``None``; ``"2023/2872-1"`` -> ``"2023/2872-1"``.
    """
    cleaned = clean_text(raw)
    if cleaned == "-":
        return None
    return cleaned


def parse_consent(raw: str | None) -> bool | None:
    """Parse the ``<patient consent>`` boolean (report §6.1).

    ``"true"`` -> ``True``; ``"false"`` -> ``False`` (case-insensitive); empty -> ``None``.
    Raises ``ValueError`` on any other non-empty value.
    """
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
    """Parse the ``<patient sex>`` attribute into :class:`Sex` (report §6.1).

    ``"female"`` -> ``Sex.FEMALE``; empty -> ``None``. Raises ``ValueError`` on an
    unrecognized non-empty value.
    """
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    try:
        return Sex(cleaned.lower())
    except ValueError as exc:
        raise ValueError(f"invalid sex value: {raw!r}") from exc


def parse_retrieved(raw: str | None) -> Retrieved | None:
    """Parse the ``<retrieved>`` element into :class:`Retrieved` (report §6.12).

    ``"operational"`` -> ``Retrieved.OPERATIONAL``; empty -> ``None``. Raises
    ``ValueError`` on an unrecognized non-empty value.
    """
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    try:
        return Retrieved(cleaned.lower())
    except ValueError as exc:
        raise ValueError(f"invalid retrieved value: {raw!r}") from exc


def parse_int(raw: str | None) -> int | None:
    """Parse an integer-valued field (e.g. ``number``, ``<samplesNo>``); empty -> ``None``.

    ``" 524 "`` -> ``524``. Raises ``ValueError`` on a non-integer non-empty value.
    """
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    return int(cleaned)


def parse_year(raw: str | None) -> int | None:
    """Parse an ``xs:gYear`` value (``<patient year>``, sample ``year``); empty -> ``None``.

    ``"2023"`` -> ``2023`` (report §6.1). Raises ``ValueError`` on a non-numeric value.
    """
    return parse_int(raw)


def parse_month(raw: str | None) -> int | None:
    """Parse an ``xs:gMonth`` value (``<patient month>``) into a month number.

    ISO 8601 ``gMonth`` form ``--MM`` (report §6.1): ``"--07"`` -> ``7``; empty -> ``None``.
    The leading ``--`` is optional here so a bare ``"7"`` also parses. Range validity
    (1-12) is enforced by the owning model's invariants.
    """
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    if cleaned.startswith("--"):
        cleaned = cleaned[2:]
    return int(cleaned)


def parse_temporal(raw: str | None) -> date | datetime | None:
    """Parse an ``xs:date`` | ``xs:dateTime`` union value; empty -> ``None``.

    Returns a :class:`datetime.datetime` when a time component is present (``"T"``),
    otherwise a :class:`datetime.date` (report §6.10–§6.11):
    ``"2023-03-24T11:15:00"`` -> ``datetime(2023, 3, 24, 11, 15)``;
    ``"2023-03-24"`` -> ``date(2023, 3, 24)``.
    """
    cleaned = clean_text(raw)
    if cleaned is None:
        return None
    if "T" in cleaned:
        return datetime.fromisoformat(cleaned)
    return date.fromisoformat(cleaned)
