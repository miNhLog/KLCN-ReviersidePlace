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
# TODO-4: Dung sai vuot ngan sach theo prompt.txt: TongChiPhi <= NganSachTran + D,
# voi D = 10.000.000 VND (hoac <= 5% ngan sach tran). Quyet: D = min(10tr, 5% budget)
# de tranh vuot 20-30tr voi budget lon; bind qua env de dong bo BE (PricingPolicyOptions).
# Min thuc te ~103,3tr cho 150 khach (sanh Elbe 20tr + menu 4,35tr/ban x 15 + decor 18tr).
OVER_BUDGET_TOLERANCE_VND = int(os.getenv("OVER_BUDGET_TOLERANCE_VND", "10000000"))
OVER_BUDGET_TOLERANCE_PERCENT = float(os.getenv("OVER_BUDGET_TOLERANCE_PERCENT", "0.05"))
USE_MIN_TOLERANCE = os.getenv("USE_MIN_TOLERANCE", "true").strip().lower() in ("1", "true", "yes")
OVER_BUDGET_SOFT_VND = OVER_BUDGET_TOLERANCE_VND // 2


def budget_tolerance(budget: float) -> float:
    try:
        b = float(budget)
    except (TypeError, ValueError):
        b = 0.0
    rel = b * OVER_BUDGET_TOLERANCE_PERCENT if b > 0 else 0.0
    if USE_MIN_TOLERANCE:
        return float(min(float(OVER_BUDGET_TOLERANCE_VND), rel))
    return float(max(float(OVER_BUDGET_TOLERANCE_VND), rel))
TOP_K = 3
# Trong so chuan W* (tong 1.0) — dong bo 4 chieu C# <-> Python (P0).
DEFAULT_WEIGHTS = {"w1_budget": 0.3004, "w2_capacity": 0.3198, "w3_style": 0.1799, "w4_service": 0.1999}
