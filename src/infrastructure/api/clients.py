from __future__ import annotations

from dataclasses import asdict
import os
from typing import Any, cast

import requests

from domain.models import PatientAggregate


class ApiClient:
    def __init__(self, base_url: str, timeout_seconds: int = 30) -> None:
        self.base_url = base_url.rstrip("/")
        self.timeout_seconds = timeout_seconds

    def get(self, path: str, params: dict | None = None) -> Any:
        response = requests.get(
            f"{self.base_url}/{path.lstrip('/')}",
            params=params,
            timeout=self.timeout_seconds,
        )
        response.raise_for_status()
        return response.json()

    def post(self, path: str, payload: dict) -> Any:
        response = requests.post(
            f"{self.base_url}/{path.lstrip('/')}",
            json=payload,
            timeout=self.timeout_seconds,
        )
        response.raise_for_status()
        return response.json() if response.content else {}

    def delete(self, path: str) -> None:
        response = requests.delete(
            f"{self.base_url}/{path.lstrip('/')}",
            timeout=self.timeout_seconds,
        )
        response.raise_for_status()


class HttpSourceDataGateway:
    def __init__(
        self,
        biobank_client: ApiClient,
        radiology_client: ApiClient,
        sequencing_client: ApiClient,
        wsi_client: ApiClient,
    ) -> None:
        self.biobank_client = biobank_client
        self.radiology_client = radiology_client
        self.sequencing_client = sequencing_client
        self.wsi_client = wsi_client

    def fetch_patients(self) -> list[dict[str, Any]]:
        return cast(list[dict[str, Any]], self.biobank_client.get("/patients"))

    def fetch_radiology(self, accession_numbers: list[str]) -> list[dict[str, Any]]:
        if not accession_numbers:
            return []
        return cast(
            list[dict[str, Any]],
            self.radiology_client.get(
                "/radiology",
                params={"accession_numbers": ",".join(accession_numbers)},
            ),
        )

    def fetch_sequencing(self, predictive_number: str) -> dict[str, Any] | None:
        return cast(
            dict[str, Any] | None,
            self.sequencing_client.get(
                "/sequencing",
                params={"predictive_number": predictive_number},
            ),
        )

    def fetch_wsi(self, bioptic_number: str) -> dict[str, Any] | None:
        return cast(
            dict[str, Any] | None,
            self.wsi_client.get("/slides", params={"bioptic_number": bioptic_number}),
        )


class HttpCatalogueGateway:
    def __init__(self, catalogue_client: ApiClient) -> None:
        self.catalogue_client = catalogue_client

    def upsert_patient(self, patient: PatientAggregate) -> str:
        response = self.catalogue_client.post(
            "/patients/upsert",
            payload={
                "external_id": patient.patient_id,
                "accession_numbers": patient.accession_numbers,
                "samples": [
                    {
                        "sample_id": sample.sample_id,
                        "predictive_number": sample.predictive_number,
                        "bioptic_number": sample.bioptic_number,
                        "material": asdict(sample.material)
                        if sample.material
                        else None,
                        "sequencing": [asdict(entry) for entry in sample.sequencing]
                        if sample.sequencing
                        else None,
                        "wsi": asdict(sample.wsi) if sample.wsi else None,
                    }
                    for sample in patient.samples
                ],
                "radiology": [asdict(entry) for entry in patient.radiology],
                "personal": asdict(patient.personal) if patient.personal else None,
                "clinical": asdict(patient.clinical) if patient.clinical else None,
                "payload": patient.payload,
            },
        )
        remote_id = response.get("id") or response.get("patient_id")
        if not remote_id:
            raise RuntimeError("Catalogue upsert response does not include id")
        return str(remote_id)

    def delete_patient(self, entity_key: str, remote_id: str | None) -> None:
        target_id = remote_id or entity_key
        self.catalogue_client.delete(f"/patients/{target_id}")


def build_source_gateway_from_env() -> HttpSourceDataGateway:
    return HttpSourceDataGateway(
        biobank_client=ApiClient(os.environ["BIOBANK_API_URL"]),
        radiology_client=ApiClient(os.environ["RADIOLOGY_API_URL"]),
        sequencing_client=ApiClient(os.environ["SEQUENCING_API_URL"]),
        wsi_client=ApiClient(os.environ["WSI_API_URL"]),
    )


def build_catalogue_gateway_from_env() -> HttpCatalogueGateway:
    return HttpCatalogueGateway(
        catalogue_client=ApiClient(os.environ["CATALOGUE_API_URL"])
    )
