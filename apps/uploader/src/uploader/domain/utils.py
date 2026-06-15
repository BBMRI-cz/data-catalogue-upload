from __future__ import annotations

from typing import Any


def as_list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]


def first_dict(value: Any) -> dict[str, Any] | None:
    if isinstance(value, dict):
        return value
    if isinstance(value, list):
        for item in value:
            if isinstance(item, dict):
                return item
    return None


def has_any_keys(payload: dict[str, Any] | None, keys: list[str]) -> bool:
    if not isinstance(payload, dict):
        return False
    for key in keys:
        value = payload.get(key)
        if value is None:
            continue
        if isinstance(value, str) and value == "":
            continue
        if isinstance(value, list) and len(value) == 0:
            continue
        return True
    return False


def resolve_source_id(
    payload: dict[str, Any],
    stable_fallback: str | None = None,
) -> str:
    return str(
        payload.get("source_id", payload.get("id", stable_fallback or "unknown"))
    )
