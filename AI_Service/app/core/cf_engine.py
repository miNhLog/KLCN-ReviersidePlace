"""CF Engine — Collaborative Filtering bang Matrix Factorization (TruncatedSVD).

De cuong CNTT-KLCN257: CF bu sung cho CBF va MCDM/WSM trong mo hinh Hybrid.

Tai lieu goc:
- Koren, Y., Bell, R., & Volinsky, C. (2009). Matrix Factorization Techniques
  for Recommender Systems. *Computer*, 42(8), 30-37. Phan ra rating
  R ~= U * Sigma * V^T voi latent factors; SVD cat ngan (TruncatedSVD) la
  xap xi tuyen tinh kinh dien truoc khi dung FunkSVD/ALS.
- Aggarwal (2016) Ch. 3-4; Ricci et al. Handbook Ch. CF.

Du lieu: ``AI_Service/data/wedding_dataset_5k.csv`` — moi dong la 1 danh gia:
user = SampleId, item = "HallId_MenuId_DecorId", rating = Satisfaction (1..5).
Ma tran user-item thua (moi user 1 rating) nen SVD chu yeu hoc bias item
(mean item) + thanh phan latent; du cho lanh (cold-start user/item moi) thi
fallback ve item-mean -> global-mean, chuan hoa (r-1)/4 ve 0..1.

Sau khi he thong thu du lieu that (WeddingReview/Booking), chay
``app/core/export_reviews.py`` de xuat CSV that thay the synthetic.
"""
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np
import pandas as pd

try:
    from sklearn.decomposition import TruncatedSVD
except Exception:  # pragma: no cover
    TruncatedSVD = None  # type: ignore

_BASE = Path(__file__).resolve().parent.parent.parent
_DEFAULT_CSV = _BASE / "data" / "wedding_dataset_5k.csv"
# TODO-5: uu tien du lieu that khi du dong; guard min-rows 20 de tranh fit tren mau qua nho.
_REAL_CSV = _BASE / "data" / "wedding_reviews_real.csv"
MIN_REAL_ROWS = 20


def resolve_csv(csv_path: Optional[str] = None) -> str:
    """Chon CSV training: explicit path > real (>=20 dong) > synthetic fallback."""
    if csv_path:
        return str(csv_path)
    try:
        if _REAL_CSV.exists():
            with open(_REAL_CSV, "r", encoding="utf-8") as f:
                lines = sum(1 for _ in f) - 1  # tru header
            if lines >= MIN_REAL_ROWS:
                return str(_REAL_CSV)
    except OSError:
        pass
    return str(_DEFAULT_CSV)


def active_source() -> str:
    """Ten file CSV dang duoc CF dung (de endpoint/log ghi ro nguon)."""
    return Path(resolve_csv()).name

_state: Dict = {"fitted": False}


def _item_key(hall_id, menu_id, decor_id) -> str:
    return f"{int(hall_id)}_{int(menu_id)}_{int(decor_id)}"


def build_matrix(csv_path: Optional[str] = None) -> Tuple[np.ndarray, List, List, Dict, Dict]:
    resolved = resolve_csv(csv_path)
    df = pd.read_csv(resolved, encoding="utf-8")
    df["item"] = df.apply(lambda r: _item_key(r["HallId"], r["MenuId"], r["DecorId"]), axis=1)
    users = sorted(df["SampleId"].unique().tolist())
    items = sorted(df["item"].unique().tolist())
    u_index = {u: i for i, u in enumerate(users)}
    i_index = {it: j for j, it in enumerate(items)}
    mat = np.zeros((len(users), len(items)), dtype=float)
    for _, r in df.iterrows():
        mat[u_index[r["SampleId"]], i_index[r["item"]]] = float(r["Satisfaction"])
    return mat, users, items, u_index, i_index


def fit(csv_path: Optional[str] = None, n_components: int = 20, force: bool = False) -> Dict:
    """Fit TruncatedSVD tren user-item matrix (mean-centered theo user)."""
    global _state
    resolved = resolve_csv(csv_path)
    if _state.get("fitted") and not force and _state.get("csv") == resolved:
        return _state
    if TruncatedSVD is None:
        raise RuntimeError("scikit-learn chua duoc cai dat (can TruncatedSVD).")
    mat, users, items, u_index, i_index = build_matrix(resolved)
    df = pd.read_csv(resolved, encoding="utf-8")
    df["item"] = df.apply(lambda r: _item_key(r["HallId"], r["MenuId"], r["DecorId"]), axis=1)
    global_mean = float(df["Satisfaction"].mean())
    item_mean = df.groupby("item")["Satisfaction"].mean().to_dict()
    # Center: chi tru mean tren o da danh gia de giu thua.
    row_counts = (mat > 0).sum(axis=1, keepdims=True)
    row_sums = mat.sum(axis=1, keepdims=True)
    user_mean = np.where(row_counts > 0, row_sums / np.maximum(row_counts, 1), global_mean).ravel()
    centered = np.where(mat > 0, mat - user_mean[:, None], 0.0)
    k = int(max(1, min(n_components, min(mat.shape) - 1)))
    svd = TruncatedSVD(n_components=k, random_state=257)
    user_factors = svd.fit_transform(centered)
    item_factors = svd.components_.T  # (n_items, k)
    _state = {"fitted": True, "csv": resolved, "mat": mat,
              "users": users, "items": items, "u_index": u_index, "i_index": i_index,
              "user_mean": user_mean, "global_mean": global_mean, "item_mean": item_mean,
              "svd": svd, "user_factors": user_factors, "item_factors": item_factors}
    return _state


def predict_rating(user_id, hall_id, menu_id, decor_id) -> float:
    """Du doan rating 1..5 cho cap (user, item); user/item moi -> fallback mean."""
    st = fit()
    key = _item_key(hall_id, menu_id, decor_id)
    base = float(st["item_mean"].get(key, st["global_mean"]))
    if user_id not in st["u_index"] or key not in st["i_index"]:
        return max(1.0, min(5.0, base))
    i = st["u_index"][user_id]
    j = st["i_index"][key]
    pred = float(st["user_mean"][i] + st["user_factors"][i] @ st["item_factors"][j])
    # Tron voi item-mean de on dinh khi moi user chi 1 rating (shrinkage).
    pred = 0.5 * pred + 0.5 * base
    return max(1.0, min(5.0, pred))


def cf_score(user_id, hall: Dict, menu: Dict, decor: Dict) -> float:
    """Chuan hoa predict 1..5 ve s_cf 0..1: (r-1)/4. user_id=None -> dung item-mean."""
    if user_id is None:
        st = fit()
        key = _item_key(hall["hall_id"], menu["menu_id"], decor["decor_id"])
        base = float(st["item_mean"].get(key, st["global_mean"]))
        return max(0.0, min(1.0, (base - 1.0) / 4.0))
    r = predict_rating(user_id, hall["hall_id"], menu["menu_id"], decor["decor_id"])
    return max(0.0, min(1.0, (r - 1.0) / 4.0))
