"""Hoc trong so Hybrid (alpha, beta, gamma) — KHONG dung mac dinh 0.2/0.2/0.6.

Mo hinh: s_final = alpha*s_cf + beta*s_cbf + gamma*S_wsm, sum=1, bounds 0..1.
- s_wsm: WSM 4 tieu chi (wsm_engine.score_candidate + trained_weights.json).
- s_cbf: TF-IDF cosine (cbf_engine).
- s_cf: MF-SVD fit TREN TRAIN (80%) de tranh leakage; val user moi -> item-mean train.
- target y = Satisfaction/5.0.
- SLSQP min MSE tren train (seed 257) + grid-search simplex step 0.05 chon tren val;
  lay bo co MSE-val thap nhat, luu app/core/hybrid_weights.json {alpha,beta,gamma,method}.

Chay:  python notebooks/learn_hybrid_weights.py
"""
import json
import sys
import tempfile
from pathlib import Path

import numpy as np
import pandas as pd
from scipy.optimize import minimize
from sklearn.model_selection import train_test_split

HERE = Path(__file__).resolve().parent
BASE = HERE.parent
sys.path.insert(0, str(BASE))

from app.core import cbf_engine  # noqa: E402
from app.core import cf_engine  # noqa: E402
from app.core import wsm_engine  # noqa: E402

SEED = 257
DATA_SYNTHETIC = BASE / "data" / "wedding_dataset_5k.csv"
DATA_REAL = BASE / "data" / "wedding_reviews_real.csv"
MIN_REAL_ROWS = 20
# TODO-6: giu tin hieu CF — beta >= 0.05; L2 chong suy bien alpha=1/gamma=1.
BETA_MIN = 0.05
L2_LAMBDA = 1e-3
GRID_STEP = 0.025
OUT_JSON = BASE / "app" / "core" / "hybrid_weights.json"


def pick_dataset() -> tuple:
    """Uu tien du lieu that (>=20 dong) theo TODO-5, fallback synthetic."""
    try:
        if DATA_REAL.exists():
            import pandas as _pd
            _df = _pd.read_csv(DATA_REAL, encoding="utf-8")
            if len(_df) >= MIN_REAL_ROWS:
                return DATA_REAL, f"REAL {DATA_REAL.name} ({len(_df)} dong)"
    except Exception:
        pass
    return DATA_SYNTHETIC, f"SYNTHETIC {DATA_SYNTHETIC.name}"


def data_sha256(path) -> str:
    import hashlib
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def _parse_services(text) -> list:
    if text is None or (isinstance(text, float) and np.isnan(text)) or str(text).strip() == "":
        return []
    return [s for s in str(text).split(";") if s]


def compute_features(df: pd.DataFrame, cf_train_csv: Path, df_train_ref: pd.DataFrame | None = None) -> np.ndarray:
    catalog = wsm_engine.load_catalog()
    groups = catalog["style_groups"]
    weights = wsm_engine.load_weights()
    hall_map = {h["hall_id"]: h for h in catalog["halls"]}
    menu_map = {m["menu_id"]: m for m in catalog["menus"]}
    decor_map = {d["decor_id"]: d for d in catalog["decors"]}
    # CF leakage-free (Koren bias baseline): s_cf = item-mean, trong do
    # train-row dung mean LOAI CHINH NO (leave-one-out), val-row dung mean train.
    # (SVD tren ma tran moi-user-1-rating se nho thuoc rating -> alpha=1 suy bien.)
    ref = df_train_ref if df_train_ref is not None else pd.read_csv(cf_train_csv, encoding="utf-8")
    ref = ref.copy()
    ref["__item"] = ref["HallId"].astype(int).astype(str) + "_" + ref["MenuId"].astype(int).astype(str) + "_" + ref["DecorId"].astype(int).astype(str)
    gmean = float(ref["Satisfaction"].mean())
    sums = ref.groupby("__item")["Satisfaction"].sum().to_dict()
    counts = ref.groupby("__item")["Satisfaction"].count().to_dict()
    is_train = df_train_ref is None  # True khi df chinh la train
    feats = []
    for _, row in df.iterrows():
        hall = hall_map[int(row["HallId"])]
        menu = menu_map[int(row["MenuId"])]
        decor = decor_map[int(row["DecorId"])]
        priority = _parse_services(row["SelectedServices"])
        included = _parse_services(row.get("IncludedServices", row["SelectedServices"]))
        s_wsm, _, _ = wsm_engine.score_candidate(
            float(row["Budget"]), int(row["TableCount"]), str(row["DesiredStyle"]),
            priority, hall, float(menu["price_per_table"]), decor, included, groups, weights)
        try:
            s_cbf = cbf_engine.cbf_score(str(row["DesiredStyle"]), priority, menu, decor, hall)
        except Exception:
            s_cbf = 0.0
        key = f"{int(row['HallId'])}_{int(row['MenuId'])}_{int(row['DecorId'])}"
        own = float(row["Satisfaction"])
        if is_train and key in sums and counts.get(key, 0) > 1:
            base = (float(sums[key]) - own) / (int(counts[key]) - 1)
        else:
            base = float(sums.get(key, gmean)) if not is_train or key not in sums else gmean
            if is_train and key in sums and int(counts[key]) == 1:
                base = gmean  # LOO khi item chi xuat hien 1 lan -> global
        s_cf = max(0.0, min(1.0, (base - 1.0) / 4.0))
        feats.append([s_cf, s_cbf, float(s_wsm)])
    return np.array(feats, dtype=float)


def mse(w, X, y):
    return float(np.mean((X @ w - y) ** 2))


def mse_l2(w, X, y, lam: float = L2_LAMBDA):
    # L2 tren w (giua don gian, tranh tap trung 1 kenh) cho SLSQP train.
    return float(np.mean((X @ w - y) ** 2) + lam * float(np.sum(w ** 2)))


def main() -> None:
    DATA_CSV, data_label = pick_dataset()
    df = pd.read_csv(DATA_CSV, encoding="utf-8")
    print(f"[hybrid] Doc {len(df)} mau tu {DATA_CSV} ({data_label}).")
    df_train, df_val = train_test_split(df, test_size=0.20, random_state=SEED)
    print(f"[hybrid] Train: {len(df_train)} | Val: {len(df_val)} (seed {SEED})")
    with tempfile.TemporaryDirectory() as tmp:
        train_csv = Path(tmp) / "train.csv"
        df_train.to_csv(train_csv, index=False, encoding="utf-8")
        X_train = compute_features(df_train, train_csv)  # LOO trong train
        X_val = compute_features(df_val, train_csv, df_train_ref=df_train)  # mean train
    y_train = df_train["Satisfaction"].to_numpy(dtype=float) / 5.0
    y_val = df_val["Satisfaction"].to_numpy(dtype=float) / 5.0

    cons = {"type": "eq", "fun": lambda w: float(np.sum(w)) - 1.0}
    bounds = [(0.0, 1.0), (BETA_MIN, 1.0), (0.0, 1.0)]  # TODO-6: beta >= 0.05
    res = minimize(mse_l2, x0=np.array([1 / 3, 1 / 3, 1 / 3]), args=(X_train, y_train),
                   method="SLSQP", bounds=bounds, constraints=cons,
                   options={"maxiter": 500, "ftol": 1e-9})
    w_slsqp = np.clip(res.x, 0, 1)
    w_slsqp = w_slsqp / w_slsqp.sum()
    print(f"[hybrid] SLSQP success={res.success} msg={res.message}")
    print(f"[hybrid] SLSQP weights: alpha={w_slsqp[0]:.4f} beta={w_slsqp[1]:.4f} gamma={w_slsqp[2]:.4f} "
          f"| MSE train={mse(w_slsqp, X_train, y_train):.6f} MSE val={mse(w_slsqp, X_val, y_val):.6f}")

    # Grid-search simplex step 0.025 tren VAL (duyet beta>=BETA_MIN), chon MSE-val thap nhat.
    best, best_mse = None, float("inf")
    step = GRID_STEP
    n = int(round(1 / step))
    grid_points = 0
    for i in range(n + 1):
        for j in range(n + 1 - i):
            k = n - i - j
            w = np.array([i * step, j * step, k * step])
            if w[1] < BETA_MIN - 1e-12:
                continue
            grid_points += 1
            m = mse(w, X_val, y_val)
            if m < best_mse:
                best_mse, best = m, w.copy()
    print(f"[hybrid] GRID duyet {grid_points} diem (beta>={BETA_MIN}, step {step})")
    assert best is not None
    print(f"[hybrid] GRID-SEARCH val (step {step}): alpha={best[0]:.4f} beta={best[1]:.4f} gamma={best[2]:.4f} "
          f"| MSE val={best_mse:.6f} MSE train={mse(best, X_train, y_train):.6f}")

    # Chon bo thang tren VAL (khong mac dinh).
    mse_slsqp_val = mse(w_slsqp, X_val, y_val)
    if best_mse <= mse_slsqp_val:
        w_final, method = best, f"SLSQP-init + grid-search simplex step {step} tren val (seed {SEED}), chon MSE-val thap nhat"
        print(f"[hybrid] => Chon GRID (val {best_mse:.6f} <= SLSQP val {mse_slsqp_val:.6f})")
    else:
        w_final, method = w_slsqp, f"SLSQP min MSE train + so sanh grid-search val (seed {SEED}), chon MSE-val thap nhat"
        print(f"[hybrid] => Chon SLSQP (val {mse_slsqp_val:.6f} < grid val {best_mse:.6f})")
    print(f"[hybrid] HOC DUOC: alpha={w_final[0]:.4f} beta={w_final[1]:.4f} gamma={w_final[2]:.4f}")
    print(f"[hybrid] (KHONG dung mac dinh 0.2/0.2/0.6)")

    # Ablation: tat tung kenh (=0, chuan hoa lai 2 kenh con) do MSE-val.
    ablation = {}
    names = ["alpha", "beta", "gamma"]
    for z in range(3):
        w0 = w_final.copy()
        w0[z] = 0.0
        s = float(w0.sum())
        w0 = w0 / s if s > 0 else np.array([1 / 3, 1 / 3, 1 / 3])
        ablation[f"{names[z]}=0"] = {"alpha": float(w0[0]), "beta": float(w0[1]),
                                     "gamma": float(w0[2]),
                                     "mse_train": mse(w0, X_train, y_train),
                                     "mse_val": mse(w0, X_val, y_val)}
    for k, v in ablation.items():
        print(f"[hybrid] ABLATION {k}: MSE train={v['mse_train']:.6f} MSE val={v['mse_val']:.6f}")

    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump({"alpha": float(w_final[0]), "beta": float(w_final[1]), "gamma": float(w_final[2]),
                   "method": method, "seed": SEED,
                   "bounds": {"alpha": [0.0, 1.0], "beta": [BETA_MIN, 1.0], "gamma": [0.0, 1.0]},
                   "l2_lambda": L2_LAMBDA, "grid_step": GRID_STEP,
                   "mse_train": mse(w_final, X_train, y_train),
                   "mse_val": mse(w_final, X_val, y_val),
                   "ablation_zero": ablation,
                   "data_source": data_label, "data_sha256": data_sha256(DATA_CSV),
                   "data_note": "Uu tien wedding_reviews_real.csv (>=20 dong), fallback synthetic (xem export_reviews.py)"},
                  f, ensure_ascii=False, indent=2)
    print(f"[hybrid] Da luu -> {OUT_JSON}")


if __name__ == "__main__":
    main()
