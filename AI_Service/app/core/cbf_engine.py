"""CBF Engine — Content-Based Filtering bang TF-IDF + cosine similarity.

De cuong CNTT-KLCN257: Content-Based (mo ta menu/decor/hall) ket hop voi
Collaborative (MF) va MCDM/WSM thanh Hybrid co trong so hoc duoc.

Tai lieu goc:
- Aggarwal, C. C. (2016). *Recommender Systems: The Textbook*. Springer,
  Ch. 3 Content-Based Recommender Systems (ho so item, TF-IDF, cosine).
- Ricci, F., Rokach, L., & Shapira, B. (2011/2022). *Recommender Systems
  Handbook*. Springer, Ch. Content-Based (ho so nguoi dung/item, do tuong dong).
- scikit-learn: ``sklearn.feature_extraction.text.TfidfVectorizer`` + cosine
  similarity (Pedregosa et al., 2011, JMLR).

Y tuong: moi combo (hall, menu, decor) co 1 van ban mo ta ghep tu ten +
style + mo ta + mon an. Query cua khach (desired_style + priority services)
cung duoc vector hoa trong cung khong gian TF-IDF; ``cbf_score`` la cosine
giua query va combo, chuan hoa 0..1 (cosine TF-IDF khong am).
"""
import json
import re
import unicodedata
from pathlib import Path
from typing import Dict, List, Optional

try:
    from sklearn.feature_extraction.text import TfidfVectorizer
    from sklearn.metrics.pairwise import cosine_similarity
except Exception:  # pragma: no cover
    TfidfVectorizer = None  # type: ignore
    cosine_similarity = None  # type: ignore

_BASE = Path(__file__).resolve().parent.parent.parent
_DEFAULT_CATALOG = _BASE / "data" / "raw_catalog.json"

_vectorizer: Optional[object] = None
_combo_ids: List[str] = []
_combo_matrix = None


def _norm(text: str) -> str:
    t = unicodedata.normalize("NFD", (text or "").lower())
    t = "".join(c for c in t if unicodedata.category(c) != "Mn")
    t = t.replace("đ", "d")
    return re.sub(r"\s+", " ", t).strip()


def _combo_text(hall: Dict, menu: Dict, decor: Dict) -> str:
    dishes = " ".join(menu.get("dishes", []) or [])
    return " ".join([
        str(hall.get("hall_name", "")),
        str(hall.get("description", "")),
        str(menu.get("menu_name", "")),
        str(menu.get("description", "")),
        dishes,
        str(decor.get("decor_name", "")),
        str(decor.get("style", "")),
        str(decor.get("description", "")),
    ])


def _query_text(desired_style: str, priority: List[str], catalog: Dict) -> str:
    svc_map = {s["service_id"]: (s.get("service_name", "") + " " + s.get("description", "")) for s in catalog.get("services", [])}
    svc_txt = " ".join(svc_map.get(s, s) for s in (priority or []))
    return f"{desired_style or ''} {svc_txt}".strip()


def _load_catalog(catalog_path: Optional[str] = None) -> Dict:
    path = Path(catalog_path) if catalog_path else _DEFAULT_CATALOG
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def build_index(catalog_path: Optional[str] = None, force: bool = False):
    """Dung TF-IDF index tren toan bo combo hall x menu x decor (9x24x7=1512)."""
    global _vectorizer, _combo_ids, _combo_matrix
    if _vectorizer is not None and not force:
        return _vectorizer, _combo_matrix, _combo_ids
    if TfidfVectorizer is None:
        raise RuntimeError("scikit-learn chua duoc cai dat (can TfidfVectorizer).")
    catalog = _load_catalog(catalog_path)
    docs: List[str] = []
    ids: List[str] = []
    for hall in catalog["halls"]:
        for menu in catalog["menus"]:
            for decor in catalog["decors"]:
                ids.append(f"{hall['hall_id']}_{menu['menu_id']}_{decor['decor_id']}")
                docs.append(_combo_text(hall, menu, decor))
    vec = TfidfVectorizer(max_features=2048, preprocessor=_norm,
                          token_pattern=r"(?u)\b\w\w+\b")
    mat = vec.fit_transform(docs)
    _vectorizer, _combo_matrix, _combo_ids = vec, mat, ids
    return vec, mat, ids


def cbf_score(desired_style: str, priority: List[str],
              menu: Dict, decor: Dict, hall: Dict,
              catalog_path: Optional[str] = None) -> float:
    """Cosine TF-IDF giua query khach va 1 combo cu the -> s_cbf 0..1.

    Args:
        desired_style: phong cach mong muon (VD "Lang man").
        priority: danh sach service_id uu tien (VD ["DV001"]).
        menu/decor/hall: dict catalog cua combo can cham.
    """
    vec, mat, ids = build_index(catalog_path)
    assert vec is not None and cosine_similarity is not None
    catalog = _load_catalog(catalog_path)
    q = _query_text(desired_style, list(priority or []), catalog)
    q_vec = vec.transform([q])
    key = f"{hall['hall_id']}_{menu['menu_id']}_{decor['decor_id']}"
    try:
        idx = ids.index(key)
    except ValueError:
        # Combo la (hall,menu,decor) rieng le: so sanh truc tiep 1-1.
        d_vec = vec.transform([_combo_text(hall, menu, decor)])
        sim = float(cosine_similarity(q_vec, d_vec)[0, 0])
        return max(0.0, min(1.0, sim))
    sim = float(cosine_similarity(q_vec, mat[idx])[0, 0])
    return max(0.0, min(1.0, sim))
