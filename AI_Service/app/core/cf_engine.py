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
import logging
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np
import pandas as pd

logger = logging.getLogger("ai_service.cf_engine")

try:
    from sklearn.decomposition import TruncatedSVD
except Exception:  # pragma: no cover
    TruncatedSVD = None  # type: ignore

_BASE = Path(__file__).resolve().parent.parent.parent
_DEFAULT_CSV = _BASE / "data" / "wedding_dataset_5k.csv"
_REAL_CSV = _BASE / "data" / "wedding_reviews_real.csv"
MIN_REAL_ROWS = 20


def resolve_csv(csv_path: Optional[str] = None) -> str:
    """Chon CSV training: explicit path > real (>=20 dong) > synthetic fallback."""
    if csv_path:
        return str(csv_path)
    try:
        if _REAL_CSV.exists():
            with open(_REAL_CSV, "r", encoding="utf-8") as f:
                lines = sum(1 for line in f if line.strip()) - 1  # tru header
            if lines >= MIN_REAL_ROWS:
                logger.info(
                    f"[CF Engine] Đang huấn luyện TruncatedSVD trên dữ liệu thực tế: {_REAL_CSV.name} ({lines} dòng >= {MIN_REAL_ROWS})"
                )
                return str(_REAL_CSV)
            else:
                logger.info(
                    f"[CF Engine] Đang chạy chế độ Synthetic data fallback ({_REAL_CSV.name} chỉ có {lines} dòng < {MIN_REAL_ROWS})"
                )
        else:
            logger.info(f"[CF Engine] Đang chạy chế độ Synthetic data fallback (chưa có file {_REAL_CSV.name})")
    except OSError as ex:
        logger.warning(f"[CF Engine] Đang chạy chế độ Synthetic data fallback (lỗi đọc {_REAL_CSV.name}: {ex})")
    return str(_DEFAULT_CSV)


def active_source() -> str:
    """Ten file CSV dang duoc CF dung (de endpoint/log ghi ro nguon)."""
    return Path(resolve_csv()).name

_state: Dict = {"fitted": False}


def _item_key(hall_id, menu_id, decor_id) -> str:
    return f"{int(hall_id)}_{int(menu_id)}_{int(decor_id)}"


def _get_column_mapping(df: pd.DataFrame) -> Tuple[str, str, str, str, str]:
    user_col = "user_id" if "user_id" in df.columns else ("SampleId" if "SampleId" in df.columns else df.columns[0])
    rating_col = "rating" if "rating" in df.columns else ("Satisfaction" if "Satisfaction" in df.columns else df.columns[-1])
    hall_col = "HallId" if "HallId" in df.columns else ("hall_id" if "hall_id" in df.columns else "Hall")
    menu_col = "MenuId" if "MenuId" in df.columns else ("menu_id" if "menu_id" in df.columns else "Menu")
    decor_col = "DecorId" if "DecorId" in df.columns else ("decor_id" if "decor_id" in df.columns else "Decor")
    return user_col, rating_col, hall_col, menu_col, decor_col


def build_matrix(csv_path: Optional[str] = None) -> Tuple[np.ndarray, List, List, Dict, Dict]:
    resolved = resolve_csv(csv_path)
    df = pd.read_csv(resolved, encoding="utf-8")
    user_col, rating_col, hall_col, menu_col, decor_col = _get_column_mapping(df)

    if "combo_id" in df.columns and df["combo_id"].notna().any():
        df["item"] = df["combo_id"].astype(str)
    else:
        df["item"] = df.apply(lambda r: _item_key(r[hall_col], r[menu_col], r[decor_col]), axis=1)

    users = sorted(df[user_col].unique().tolist())
    items = sorted(df["item"].unique().tolist())
    u_index = {u: i for i, u in enumerate(users)}
    i_index = {it: j for j, it in enumerate(items)}
    mat = np.zeros((len(users), len(items)), dtype=float)

    for _, r in df.iterrows():
        try:
            val = float(r[rating_col])
            mat[u_index[r[user_col]], i_index[r["item"]]] = val
        except (ValueError, TypeError):
            continue

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
    user_col, rating_col, hall_col, menu_col, decor_col = _get_column_mapping(df)

    if "combo_id" in df.columns and df["combo_id"].notna().any():
        df["item"] = df["combo_id"].astype(str)
    else:
        df["item"] = df.apply(lambda r: _item_key(r[hall_col], r[menu_col], r[decor_col]), axis=1)

    mean_val = df[rating_col].mean()
    global_mean = float(mean_val) if not df.empty and not pd.isna(mean_val) else 4.0
    item_mean = df.groupby("item")[rating_col].mean().to_dict()

    # Center: chi tru mean tren o da danh gia de giu thua, tranh ZeroDivisionError
    row_counts = (mat > 0).sum(axis=1, keepdims=True)
    row_sums = mat.sum(axis=1, keepdims=True)
    safe_counts = np.maximum(row_counts, 1)
    user_mean = np.where(row_counts > 0, row_sums / safe_counts, global_mean).ravel()
    centered = np.where(mat > 0, mat - user_mean[:, None], 0.0)

    min_dim = min(mat.shape) if mat.size > 0 else 0
    k = int(min(n_components, max(1, min_dim - 1)))

    if min_dim <= 1 or k < 1:
        # Fallback cho ma tran qua nho/sieu thua: khong du chieu de chay SVD
        logger.warning(f"[CF Engine] Ma tran danh gia {mat.shape} khong du chieu cho SVD (k={k}). Fallback ve bias mean.")
        user_factors = np.zeros((len(users), 1), dtype=float)
        item_factors = np.zeros((len(items), 1), dtype=float)
        svd = None
    else:
        svd = TruncatedSVD(n_components=k, random_state=257)
        user_factors = svd.fit_transform(centered)
        item_factors = svd.components_.T  # (n_items, k)

    _state = {
        "fitted": True, "csv": resolved, "mat": mat,
        "users": users, "items": items, "u_index": u_index, "i_index": i_index,
        "user_mean": user_mean, "global_mean": global_mean, "item_mean": item_mean,
        "svd": svd, "user_factors": user_factors, "item_factors": item_factors
    }
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
