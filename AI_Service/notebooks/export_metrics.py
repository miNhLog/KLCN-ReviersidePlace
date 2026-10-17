"""Xuat chi so danh gia (synthetic) ra output_plots/metrics_summary.json.

Chay: python -X utf8 notebooks/export_metrics.py  (workdir: AI_Service)
"""
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = HERE.parent
for p in (str(BASE), str(HERE)):
    if p not in sys.path:
        sys.path.insert(0, p)

import evaluate_hybrid  # noqa: E402
import train_and_evaluate  # noqa: E402

PLOT_DIR = HERE / "output_plots"


def main():
    print("=== EXPORT METRICS (SYNTHETIC - mo phong, khong phai du lieu that) ===")
    print("--- Phan 1: WSM tuning (Baseline vs SLSQP) ---")
    wsm = train_and_evaluate.run(write_weights=False)
    print("--- Phan 2: So sanh WSM / CBF / CF / Hybrid ---")
    hyb = evaluate_hybrid.main()
    meta = dict(hyb["meta"])
    meta["generated_at"] = datetime.now(timezone.utc).isoformat()
    deployed = json.loads((BASE / "app" / "core" / "trained_weights.json").read_text(encoding="utf-8"))
    data = {
        "meta": meta,
        "data_source": "SYNTHETIC wedding_dataset_5k.csv — MÔ PHỎNG, không phải dữ liệu thật",
        "deployed_weights": {
            "w1_budget": float(deployed.get("w1_budget", 0.25)),
            "w2_capacity": float(deployed.get("w2_capacity", 0.25)),
            "w3_style": float(deployed.get("w3_style", 0.25)),
            "w4_service": float(deployed.get("w4_service", 0.25)),
            "note": "Trọng số WSM đang dùng bởi wsm_engine (deployed); bảng so sánh 4 mô hình dùng bộ này.",
        },
        "recommender_comparison": hyb["recommender_comparison"],
        "hybrid_weights": hyb["hybrid_weights"],
        "wsm_tuning": wsm,
        "plots": {
            "hybrid_comparison": "output_plots/hybrid_comparison.png",
            "metrics_comparison": "output_plots/metrics_comparison.png",
            "weight_distribution": "output_plots/weight_distribution.png",
            "score_distribution": "output_plots/score_distribution.png",
        },
    }
    PLOT_DIR.mkdir(parents=True, exist_ok=True)
    out = PLOT_DIR / "metrics_summary.json"
    out.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Da luu -> {out}")


if __name__ == "__main__":
    main()
