"""Hybrid Engine — Weighted fusion: s_final = alpha*s_cf + beta*s_cbf + gamma*S_wsm.

De cuong CNTT-KLCN257 (Content-Based + Collaborative + Hybrid + MCDM + LLM/RAG):
- s_cbf: Content-Based (TF-IDF cosine, ``cbf_engine``).
- s_cf: Collaborative MF-SVD (``cf_engine``), cold-start -> item-mean.
- S_wsm: MCDM nhom WSM 4 tieu chi (``wsm_engine.score_candidate``), da 0..1.
Trong so (alpha, beta, gamma) KHONG mac dinh 0.2/0.2/0.6 ma duoc hoc bang
SLSQP min MSE + grid-search tren val (xem ``notebooks/learn_hybrid_weights.py``),
rang buoc sum=1, bounds 0..1; luu tai ``hybrid_weights.json``.

Tai lieu: Burke (2002) Hybrid Recommender Systems (weighted hybrid);
Aggarwal Ch. 8 Hybrid; Fishburn/Keeney-Raiffa cho MCDM/WSM.
"""
import json
from pathlib import Path
from typing import Dict, List, Tuple

_HERE = Path(__file__).resolve().parent
_WEIGHTS_FILE = _HERE / "hybrid_weights.json"

_cached: Dict = {}


def load_hybrid_weights() -> Dict[str, float]:
    global _cached
    if _cached:
        return _cached
    try:
        with open(_WEIGHTS_FILE, "r", encoding="utf-8") as f:
            data = json.load(f)
        _cached = {"alpha": float(data["alpha"]), "beta": float(data["beta"]),
                   "gamma": float(data["gamma"])}
    except Exception:
        # Chi dung khi chua hoc (endpoint se bao ro); khong coi la gia tri chuan.
        _cached = {"alpha": 0.2, "beta": 0.2, "gamma": 0.6, "_unlearned": True}  # type: ignore
    return _cached


def weights_source() -> str:
    """Chuoi nguon trong so de endpoint tra ve (TODO-6: weights_source)."""
    w = load_hybrid_weights()
    if w.get("_unlearned"):
        return "default(0.2/0.2/0.6)-unlearned"
    try:
        with open(_WEIGHTS_FILE, "r", encoding="utf-8") as f:
            data = json.load(f)
        method = str(data.get("method", "learned"))
        mse_val = data.get("mse_val")
        extra = f", mse_val={mse_val:.6f}" if isinstance(mse_val, (int, float)) else ""
        return f"learned:hybrid_weights.json ({method}{extra})"
    except Exception:
        return "learned:hybrid_weights.json"


def fuse(s_cf: float, s_cbf: float, s_wsm: float,
         weights: Dict[str, float] | None = None) -> float:
    w = weights or load_hybrid_weights()
    a, b, g = float(w["alpha"]), float(w["beta"]), float(w["gamma"])
    return max(0.0, min(1.0, a * s_cf + b * s_cbf + g * s_wsm))


def rank_candidates(request, candidates: List[Dict],
                    weights: Dict[str, float] | None = None,
                    user_id=None) -> Tuple[List[Dict], Dict]:
    """Cham hybrid cho danh sach candidates da co S_wsm; tra top3 + weights."""
    from . import cbf_engine, cf_engine
    w = weights or load_hybrid_weights()
    priority = list(getattr(request, "priority_services", None) or [])
    desired = str(getattr(request, "desired_style", "") or "")
    scored = []
    for c in candidates:
        s_wsm = float(c.get("score", 0.0))
        try:
            s_cbf = cbf_engine.cbf_score(desired, priority, c["menu"], c["decor"], c["hall"])
        except Exception:
            s_cbf = 0.0
        try:
            s_cf = cf_engine.cf_score(user_id, c["hall"], c["menu"], c["decor"])
        except Exception:
            s_cf = 0.0
        s_final = fuse(s_cf, s_cbf, s_wsm, w)
        scored.append({**c, "s_cf": s_cf, "s_cbf": s_cbf,
                       "s_wsm": s_wsm, "score": s_final})
    scored.sort(key=lambda x: x["score"], reverse=True)
    # Tái dùng diversity Top3 của wsm_engine (sảnh khác nhau trước).
    try:
        from .wsm_engine import rank_top3 as _  # noqa: F401 (giữ import rõ nguồn)
    except Exception:
        pass
    top_entries: List[Dict] = []
    used_halls, used_md = set(), set()
    for e in scored:
        if len(top_entries) >= 3:
            break
        key = (f"{e['menu']['menu_id']}|{e['decor']['decor_id']}|"
               f"{round(float(e['menu']['price_per_table'])/1_000_000)}|"
               f"{round(float(e['decor']['price'])/1_000_000)}")
        if e["hall"]["hall_id"] not in used_halls and key not in used_md:
            top_entries.append(e)
            used_halls.add(e["hall"]["hall_id"])
            used_md.add(key)
    for e in scored:
        if len(top_entries) >= 3:
            break
        if e in top_entries:
            continue
        key = f"{e['menu']['menu_id']}|{e['decor']['decor_id']}"
        if e["hall"]["hall_id"] not in used_halls or key not in used_md:
            top_entries.append(e)
            used_halls.add(e["hall"]["hall_id"])
            used_md.add(key)
    for e in scored:
        if len(top_entries) >= 3:
            break
        if e not in top_entries:
            top_entries.append(e)
    return top_entries[:3], dict(w)
