"""Enumerations for closed-vocabulary biobank XML values.

Source of truth: ``docs/patient-data-report.md`` (the MOU biobank XML export, namespace
``http://www.bbmri.cz/schemas/biobank/data``, schema ``exportNIS.xsd``). See report
§5/§6.1 for ``sex`` and §6.12 for ``retrieved``.

Kept at the ``domain`` root (not under ``domain/models``) so the low-level
:mod:`biobank_api.domain.cleaning` helpers can depend on it without importing the models
package — which would otherwise create an import cycle.
"""

from __future__ import annotations

from enum import Enum


class Sex(Enum):
    """Patient sex (``<patient sex>`` attribute; report §6.1).

    Maps to FAIR Genomes ``Personal.gender_at_birth`` / ``genotypic sex``.
    """

    MALE = "male"
    FEMALE = "female"


class Retrieved(Enum):
    """Collection context of a sample (``<retrieved>`` element; report §6.12).

    ``operational`` = taken during an active surgical/clinical procedure;
    ``unknown`` = collection context not recorded (always ``unknown`` for STS
    diagnostic specimens).
    """

    OPERATIONAL = "operational"
    UNKNOWN = "unknown"
