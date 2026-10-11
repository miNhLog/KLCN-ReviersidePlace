"""Cau hinh AI Service."""
import os
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent.parent
CATALOG_PATH = os.getenv("CATALOG_PATH", str(BASE_DIR / "data" / "raw_catalog.json"))
WEIGHTS_PATH = os.getenv("WEIGHTS_PATH", str(BASE_DIR / "app" / "core" / "trained_weights.json"))

SERVICE_TITLE = "Riverside Palace - Intelligent Recommendation & Decision Support Service"
SERVICE_VERSION = "1.0.0"

CORS_ORIGINS = [
    "https://localhost:7101",
    "https://localhost:7102",
    "http://localhost:5102",
]

OVER_BUDGET_TOLERANCE = 1.10
TOP_K = 3
DEFAULT_WEIGHTS = {"w1_budget": 0.25, "w2_capacity": 0.25, "w3_style": 0.25, "w4_service": 0.25}
