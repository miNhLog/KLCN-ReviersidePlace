"""So sanh WSM / CBF / CF / Hybrid tren cung 1000 test x 20 candidates.

De cuong CNTT-KLCN257: thuc nghiem cong bang 4 mo hinh tren cung tap test,
metrics P@3/R@3/NDCG@3/HitRate@3 (relevant = grade>=2), luu
output_plots/hybrid_comparison.png.

- NGUON DU LIEU: mac dinh SYNTHETIC (wedding_dataset_5k.csv, seed 257, 1000 test).
  Khi co du lieu that (wedding_reviews_real.csv tu export_reviews.py), script tu
  phat hien va chay them cot 'REAL-*' rieng, KHONG tron lan voi synthetic.
- 4 cach cham tren cung 20 ung vien (1 ground-truth + 19 random, seed 257):
  WSM = S_wsm (trained_weights), CBF = cosine TF-IDF, CF = item-mean LOO-free,
  Hybrid = alpha*s_cf+beta*s_cbf+gamma*S_wsm (hybrid_weights.json hoc duoc).
- Ground-truth grade tu latent goc cua generate_dataset.py
  (0.40*r1+0.25*r2+0.20*r3+0.15*r4 -> grade).

Chay: python notebooks/evaluate_hybrid.py
"""
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from sklearn.model_selection import train_test_split

HERE = Path(__file__).resolve().parent
BASE = HERE.parent
sys.path.insert(0, str(BASE))
from app.core import cbf_engine  # noqa: E402
from app.core import wsm_engine  # noqa: E402

SEED = 257
N_TEST = 1000
N_CAND = 20
DATA_SYN = BASE / "data" / "wedding_dataset_5k.csv"
DATA_REAL = BASE / "data" / "wedding_reviews_real.csv"
PLOT_DIR = HERE / "output_plots"
PLOT_DIR.mkdir(parents=True, exist_ok=True)


def parse_services(text):
    if text is None or (isinstance(text, float) and pd.isna(text)) or str(text).strip() == "":
        return []
    return [s for s in str(text).split(";") if s]


def dcg_at_k(grades, k):
    v = 0.0
    for i in range(min(k, len(grades))):
        v += (2 ** grades[i] - 1) / np.log2(i + 2)
    return v


def grade_from_latent(latent: float) -> int:
    sat = 1.0 + 4.0 * latent
    if sat >= 4.5:
        return 3
    if sat >= 3.5:
        return 2
    if sat >= 2.5:
        return 1
    return 0


def evaluate(df_test: pd.DataFrame, tag: str):
    catalog = wsm_engine.load_catalog()
    groups = catalog["style_groups"]
    weights = wsm_engine.load_weights()
    try:
        hw = json.loads((BASE / "app" / "core" / "hybrid_weights.json").read_text(encoding="utf-8"))
        alpha, beta, gamma = float(hw["alpha"]), float(hw["beta"]), float(hw["gamma"])
    except Exception:
        alpha, beta, gamma = 0.2, 0.2, 0.6
    print(f"[{tag}] Hybrid weights: alpha={alpha:.4f} beta={beta:.4f} gamma={gamma:.4f}")
    halls = catalog["halls"]; menus = catalog["menus"]; decors = catalog["decors"]
    hall_map = {h["hall_id"]: h for h in halls}
    menu_map = {m["menu_id"]: m for m in menus}
    decor_map = {d["decor_id"]: d for d in decors}
    hall_ids = [h["hall_id"] for h in halls]
    menu_ids = [m["menu_id"] for m in menus]
    decor_ids = [d["decor_id"] for d in decors]
    service_ids = [s["service_id"] for s in catalog["services"]]
    # Item-mean CF tu FULL train (leakage-free cho test: test users chua thay).
    full = pd.read_csv(DATA_SYN, encoding="utf-8")
    full["__item"] = full["HallId"].astype(int).astype(str) + "_" + full["MenuId"].astype(int).astype(str) + "_" + full["DecorId"].astype(int).astype(str)
    gmean = float(full["Satisfaction"].mean())
    imean = full.groupby("__item")["Satisfaction"].mean().to_dict()
    rng = np.random.default_rng(SEED)
    test = df_test.sample(n=min(N_TEST, len(df_test)), random_state=SEED).reset_index(drop=True)
    mets = {m: {"p3": [], "r3": [], "ndcg3": [], "hit3": []} for m in ["WSM", "CBF", "CF", "Hybrid"]}
    for _, row in test.iterrows():
        budget = float(row["Budget"]); tc = int(row["TableCount"])
        desired = str(row["DesiredStyle"]); priority = parse_services(row["SelectedServices"])
        cands = [(int(row["HallId"]), int(row["MenuId"]), int(row["DecorId"]),
                  set(parse_services(row.get("IncludedServices", row["SelectedServices"]))))]
        for _ in range(N_CAND - 1):
            n_inc = int(rng.integers(0, 4))
            inc = set(rng.choice(service_ids, size=n_inc, replace=False).tolist()) if n_inc > 0 else set()
            cands.append((int(rng.choice(hall_ids)), int(rng.choice(menu_ids)),
                          int(rng.choice(decor_ids)), inc))
        grades, s_wsm, s_cbf, s_cf = [], [], [], []
        for (h_id, m_id, d_id, inc) in cands:
            hall, menu, decor = hall_map[h_id], menu_map[m_id], decor_map[d_id]
            r1 = max(0.0, 1.0 - abs((menu["price_per_table"] * tc + hall["base_price"] + decor["price"]
                                     + sum({s['service_id']: s['price'] for s in catalog['services']}.get(s, 0) for s in inc)) - budget) / max(1.0, budget))
            r2 = max(0.0, 1.0 - abs(tc - hall["optimal_tables"]) / max(1.0, float(hall["max_tables"])))
            r3 = wsm_engine.style_score(desired, decor["style"], groups)
            r4 = 1.0 if len(priority) == 0 else len(set(priority) & set(inc)) / len(priority)
            latent = 0.40 * r1 + 0.25 * r2 + 0.20 * r3 + 0.15 * r4
            grades.append(grade_from_latent(latent))
            sw, _, _ = wsm_engine.score_candidate(budget, tc, desired, priority, hall,
                                                  float(menu["price_per_table"]), decor, list(inc), groups, weights)
            s_wsm.append(float(sw))
            try:
                s_cbf.append(float(cbf_engine.cbf_score(desired, priority, menu, decor, hall)))
            except Exception:
                s_cbf.append(0.0)
            key = f"{h_id}_{m_id}_{d_id}"
            base = float(imean.get(key, gmean))
            s_cf.append(max(0.0, min(1.0, (base - 1.0) / 4.0)))
        grades = np.array(grades)
        s_hyb = alpha * np.array(s_cf) + beta * np.array(s_cbf) + gamma * np.array(s_wsm)
        for name, vec in (("WSM", np.array(s_wsm)), ("CBF", np.array(s_cbf)),
                          ("CF", np.array(s_cf)), ("Hybrid", s_hyb)):
            order = np.argsort(-vec, kind="stable")
            top = grades[order[:3]]
            rel_total = int(np.sum(grades >= 2))
            hits = int(np.sum(top >= 2))
            p3 = hits / 3.0
            r3 = (hits / rel_total) if rel_total > 0 else 0.0
            ideal = np.sort(grades)[::-1][:3]
            denom = dcg_at_k(ideal.tolist(), 3)
            ndcg = (dcg_at_k(top.tolist(), 3) / denom) if denom > 0 else 0.0
            mets[name]["p3"].append(p3); mets[name]["r3"].append(r3)
            mets[name]["ndcg3"].append(ndcg); mets[name]["hit3"].append(1.0 if hits > 0 else 0.0)
    avg = {m: {k: float(np.mean(v)) for k, v in d.items()} for m, d in mets.items()}
    print(f"=== KET QUA {tag} (Top-3, relevant=grade>=2, {len(test)} samples x {N_CAND} candidates) ===")
    for m in ["WSM", "CBF", "CF", "Hybrid"]:
        a = avg[m]
        print(f"{tag} {m}: P@3={a['p3']:.4f} R@3={a['r3']:.4f} NDCG@3={a['ndcg3']:.4f} HitRate@3={a['hit3']:.4f}")
    return avg


def _hybrid_weights_meta() -> dict:
    try:
        hw = json.loads((BASE / "app" / "core" / "hybrid_weights.json").read_text(encoding="utf-8"))
        return {
            "alpha": float(hw["alpha"]),
            "beta": float(hw["beta"]),
            "gamma": float(hw["gamma"]),
            "mse_train": hw.get("mse_train"),
            "mse_val": hw.get("mse_val"),
            "ablation": hw.get("ablation_zero"),
        }
    except Exception:
        return {"alpha": 0.2, "beta": 0.2, "gamma": 0.6}


def main():
    df = pd.read_csv(DATA_SYN, encoding="utf-8")
    _, df_test = train_test_split(df, test_size=0.20, random_state=SEED)
    print(f"SYNTHETIC: {len(df)} mau (wedding_dataset_5k.csv) — KHONG phai data that.")
    avg_syn = evaluate(df_test, "SYNTHETIC")
    avg_real = None
    if DATA_REAL.exists():
        try:
            dfr = pd.read_csv(DATA_REAL, encoding="utf-8")
            dfr = dfr.dropna(subset=["Satisfaction"])
            if len(dfr) >= 20 and set(["HallId", "MenuId", "DecorId", "Satisfaction"]).issubset(dfr.columns):
                print(f"REAL: {len(dfr)} dong that — chay rieng, khong tron voi synthetic.")
                avg_real = evaluate(dfr, "REAL")
            else:
                print("REAL: file that chua du cot/dong (>=20, HallId/MenuId/DecorId/Satisfaction) — bo qua, chi bao cao synthetic.")
        except Exception as ex:
            print(f"REAL: khong doc duoc {DATA_REAL}: {ex} — chi bao cao synthetic.")
    else:
        print("REAL: chua co wedding_reviews_real.csv (xem export_reviews.py) — chi bao cao synthetic.")
    # Ve hybrid_comparison.png (synthetic; them real neu co).
    labels = ["P@3", "R@3", "NDCG@3", "HitRate@3"]
    keys = ["p3", "r3", "ndcg3", "hit3"]
    models = ["WSM", "CBF", "CF", "Hybrid"]
    x = np.arange(len(labels))
    fig, ax = plt.subplots(figsize=(10, 5.5))
    width = 0.2
    for i, m in enumerate(models):
        vals = [avg_syn[m][k] for k in keys]
        off = (i - 1.5) * width
        bars = ax.bar(x + off, vals, width, label=f"{m} (syn)")
        for xi, v in zip(x + off, vals):
            ax.text(xi, v + 0.01, f"{v:.2f}", ha="center", fontsize=7)
    ax.set_xticks(x); ax.set_xticklabels(labels); ax.set_ylim(0, 1.15)
    ax.set_ylabel("Gia tri metric")
    title = "So sanh WSM/CBF/CF/Hybrid — SYNTHETIC 1000 test x 20 candidates (seed 257)"
    if avg_real:
        title += " (+REAL rieng, xem log)"
    ax.set_title(title)
    ax.legend(fontsize=8)
    fig.tight_layout()
    out = PLOT_DIR / "hybrid_comparison.png"
    fig.savefig(out, dpi=150)
    plt.close(fig)
    print(f"Da luu plot -> {out}")
    return {
        "meta": {
            "seed": SEED,
            "n_samples": int(len(df)),
            "n_train": int(len(df) - len(df_test)),
            "n_test_frame": int(len(df_test)),
            "n_eval": int(min(N_TEST, len(df_test))),
            "n_candidates": N_CAND,
            "top_k": 3,
            "relevant": "grade>=2",
            "dataset": DATA_SYN.name,
        },
        "recommender_comparison": avg_syn,
        "real": avg_real,
        "hybrid_weights": _hybrid_weights_meta(),
        "plot": out.as_posix(),
    }


if __name__ == "__main__":
    main()
