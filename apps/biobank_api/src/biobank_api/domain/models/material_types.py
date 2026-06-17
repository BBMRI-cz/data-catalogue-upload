"""Material-type code lookups for biobank samples.

The ``<materialType>`` element carries a short code whose meaning depends on the
enclosing element (``<tissue>`` / ``<serum>`` / ``<genome>`` / ``<diagnosisMaterial>``).
The tables below reproduce ``docs/patient-data-report.md`` §6.5 (full lookup) so the
meaning of a code such as ``53`` / ``SD`` / ``PK`` is discoverable from the model.

Codes are intentionally *not* hard-validated by the domain models (new codes may appear
in future exports); these maps are reference data plus the :func:`material_type_label`
convenience lookup. The pathological/storage semantics encoded here are what FAIR
Genomes ``Material.pathological_state`` / ``Material.biospecimen_type`` are derived from.
"""

from __future__ import annotations

# <tissue>/<materialType> — surgical/tumour tissue (report §6.3.2)
TISSUE_MATERIAL_TYPES: dict[str, str] = {
    "1": "Malignant tumour",
    "2": "Metastasis",
    "3": "Benign tumour",
    "4": "Healthy/normal tissue",
    "5": "Premalignant tissue",
    "7": "Peripheral blood mononuclear cells (PBMNC)",
    "53": "Malignant tumour (RNAlater)",
    "54": "Healthy tissue (RNAlater)",
    "55": "Metastasis (RNAlater)",
    "56": "Benign tumour (RNAlater)",
}

# <serum>/<materialType> — blood-derived liquid samples (report §6.3.3)
SERUM_MATERIAL_TYPES: dict[str, str] = {
    "K": "K3EDTA plasma, nitrogen-stored",
    "SD": "Serum, nitrogen-stored",
    "S": "Serum (room temperature / short-term)",
    "PD": "Plasma, nitrogen-stored",
    "L": "Li-heparin plasma, nitrogen-stored",
    "T": "CTAD plasma, nitrogen-stored",
    "C": "Plasma with DNA stabiliser",
    "PR": "Primary cell cultures",
}

# <genome>/<materialType> — DNA / nucleic acid samples (report §6.3.4)
GENOME_MATERIAL_TYPES: dict[str, str] = {
    "PK": "Whole blood (for DNA extraction)",
    "PS": "Whole blood with DNA stabiliser",
    "gD": "Extracted genomic DNA",
}

# <diagnosisMaterial>/<materialType> — STS diagnostic specimens (report §6.4.1).
# Always "S" (surgical/serum specimen) in observed data.
DIAGNOSIS_MATERIAL_TYPES: dict[str, str] = {
    "S": "Serum / surgical specimen",
}

# Sample-type discriminators accepted by :func:`material_type_label`.
_LOOKUPS: dict[str, dict[str, str]] = {
    "tissue": TISSUE_MATERIAL_TYPES,
    "serum": SERUM_MATERIAL_TYPES,
    "genome": GENOME_MATERIAL_TYPES,
    "diagnosis_material": DIAGNOSIS_MATERIAL_TYPES,
}


def material_type_label(sample_type: str, code: str | None) -> str | None:
    """Return the English label for a ``<materialType>`` code, or ``None`` if unknown.

    ``sample_type`` is one of ``"tissue"``, ``"serum"``, ``"genome"`` or
    ``"diagnosis_material"``; ``code`` is the raw code (e.g. ``"53"``, ``"SD"``).
    """
    if code is None:
        return None
    return _LOOKUPS.get(sample_type, {}).get(code)
