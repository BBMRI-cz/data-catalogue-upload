# Kept at the domain root (not under domain/models) so domain.cleaning can import it without
# creating a models-package import cycle.
from __future__ import annotations

from enum import Enum


class Sex(Enum):
    MALE = "male"
    FEMALE = "female"


class Retrieved(Enum):
    # operational = taken during a surgical/clinical procedure; unknown = context not recorded.
    OPERATIONAL = "operational"
    UNKNOWN = "unknown"
