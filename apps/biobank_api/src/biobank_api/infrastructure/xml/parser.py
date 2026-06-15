from __future__ import annotations

from pathlib import Path

from lxml import etree

from biobank_api.domain.models import Patient


class XmlExportParser:
    """Parses biobank XML exports into domain patients using lxml.

    Scaffold: it discovers and validates the export files but the concrete
    element/XPath mapping to ``Patient`` / ``Sample`` lands in #33.
    """

    def __init__(self, export_path: str | Path) -> None:
        self._export_path = Path(export_path)

    def parse_patients(self) -> list[Patient]:
        patients: list[Patient] = []
        for xml_file in sorted(self._export_path.glob("*.xml")):
            etree.parse(str(xml_file))  # validate the export is well-formed
            # TODO(#33): map biobank XML elements to Patient/Sample and append.
        return patients
