"""WSM Engine: Hard Filtering + Soft Scoring WSM + XAI tieng Viet."""
import json
from pathlib import Path
from typing import Dict, List, Tuple

from .config import DEFAULT_WEIGHTS, OVER_BUDGET_TOLERANCE

_catalog_cache: Dict = {}
_weights_cache: Dict = {}


def _catalog_path() -> Path:
    from . import config as cfg
    return Path(cfg.CATALOG_PATH)


def _weights_path() -> Path:
    from . import config as cfg
    return Path(cfg.WEIGHTS_PATH)


def load_catalog() -> Dict:
    global _catalog_cache
    if _catalog_cache:
        return _catalog_cache
    path = _catalog_path()
    with open(path, "r", encoding="utf-8") as f:
        _catalog_cache = json.load(f)
    return _catalog_cache


def load_weights() -> Dict[str, float]:
    global _weights_cache
    if _weights_cache:
        return _weights_cache
    path = _weights_path()
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        _weights_cache = {
            "w1_budget": float(data.get("w1_budget", 0.25)),
            "w2_capacity": float(data.get("w2_capacity", 0.25)),
            "w3_style": float(data.get("w3_style", 0.25)),
            "w4_service": float(data.get("w4_service", 0.25)),
        }
    except (FileNotFoundError, ValueError, KeyError):
        _weights_cache = dict(DEFAULT_WEIGHTS)
    total = sum(_weights_cache.values())
    if total > 0:
        for key in _weights_cache:
            _weights_cache[key] = _weights_cache[key] / total
    return _weights_cache


def style_score(desired: str, decor_style: str, groups: Dict) -> float:
    desired_norm = (desired or "").strip().lower()
    decor_norm = (decor_style or "").strip().lower()
    if desired_norm == decor_norm or desired_norm in decor_norm or decor_norm in desired_norm:
        return 1.0
    for _group, members in groups.items():
        lowered = [str(m).lower() for m in members]
        if desired_norm in lowered and decor_norm in lowered:
            return 0.5
        # Khop tuong doi theo tu khoa chung
        for member in lowered:
            if desired_norm in member or member in desired_norm:
                if decor_norm in member or member in decor_norm:
                    return 0.5
    return 0.0


def score_candidate(budget: float, table_count: int, desired_style: str,
                    priority: List[str], hall: Dict, menu_price: float,
                    decor: Dict, included: List[str], groups: Dict,
                    weights: Dict[str, float]) -> Tuple[float, Dict[str, float], float]:
    svc_price_map = {s["service_id"]: s["price"] for s in load_catalog()["services"]}
    menu_total = float(menu_price) * int(table_count)
    services_price = float(sum(svc_price_map.get(s, 0) for s in included))
    total = float(menu_total + hall["base_price"] + decor["price"] + services_price)
    r1 = max(0.0, 1.0 - abs(total - budget) / max(1.0, float(budget)))
    r2 = max(0.0, 1.0 - abs(int(table_count) - hall["optimal_tables"]) / max(1.0, float(hall["max_tables"])))
    r3 = style_score(desired_style, decor["style"], groups)
    if len(priority) == 0:
        r4 = 1.0
    else:
        r4 = len(set(priority) & set(included)) / len(priority)
    score = (weights["w1_budget"] * r1 + weights["w2_capacity"] * r2
             + weights["w3_style"] * r3 + weights["w4_service"] * r4)
    return score, {"r1": r1, "r2": r2, "r3": r3, "r4": r4}, total


def build_explanation(hall: Dict, decor: Dict, desired_style: str, budget: float,
                      total: float, table_count: int, r_parts: Dict[str, float],
                      priority: List[str], included: List[str]) -> List[str]:
    args: List[str] = []
    diff = budget - total
    if diff >= 0:
        pct = diff / budget * 100 if budget > 0 else 0.0
        args.append(f"Tiet kiem {pct:.1f}% ngan sach ({diff:,.0f} VND) so voi du kien.")
    else:
        pct = abs(diff) / budget * 100 if budget > 0 else 0.0
        args.append(f"Vuot {pct:.1f}% ngan sach ({abs(diff):,.0f} VND), can can nhac dieu chinh.")
    span = hall["max_tables"] - hall["min_tables"]
    use_ratio = (table_count - hall["min_tables"]) / span if span > 0 else 1.0
    use_ratio = max(0.0, min(1.0, use_ratio))
    args.append(f"Ty le khai thac sanh {hall['hall_name']}: {use_ratio*100:.0f}% "
                f"({table_count}/{hall['max_tables']} ban, toi uu {hall['optimal_tables']} ban).")
    if r_parts["r3"] >= 1.0:
        args.append(f"Khop concept hoan hao: phong cach '{decor['style']}' dung voi mong muon '{desired_style}'.")
    elif r_parts["r3"] >= 0.5:
        args.append(f"Khop concept tuong doi: '{decor['decor_name']}' ({decor['style']}) cung nhom tham my voi '{desired_style}'.")
    else:
        args.append(f"Concept '{decor['decor_name']}' ({decor['style']}) khac voi '{desired_style}', bu lai bang chi phi va suc chua.")
    if len(priority) == 0:
        args.append("Khach khong yeu cau dich vu uu tien, goi dap ung linh hoat dich vu di kem.")
    else:
        hit = len(set(priority) & set(included))
        args.append(f"Dap ung {hit}/{len(priority)} dich vu uu tien ({hit/len(priority)*100:.0f}%).")
    return args


def rank_top3(request) -> Tuple[List[Dict], int]:
    catalog = load_catalog()
    weights = load_weights()
    groups = catalog["style_groups"]
    halls = catalog["halls"]
    menus = catalog["menus"]
    decors = catalog["decors"]
    occupied = set(request.occupied_hall_ids or [])
    priority = list(request.priority_services or [])
    budget = float(request.expected_budget)
    table_count = int(request.table_count)

    scored: List[Dict] = []
    for hall in halls:
        if hall["hall_id"] in occupied:
            continue
        if table_count < hall["min_tables"] or table_count > hall["max_tables"]:
            continue
        for menu in menus:
            for decor in decors:
                # Uoc tinh nhanh de loc cung: dung included = priority (day du nhat)
                svc_map = {s["service_id"]: s["price"] for s in catalog["services"]}
                est_services = sum(svc_map.get(s, 0) for s in priority)
                est_total = menu["price_per_table"] * table_count + hall["base_price"] + decor["price"] + est_services
                if est_total > budget * OVER_BUDGET_TOLERANCE:
                    continue
                included = list(priority)
                s, parts, total = score_candidate(budget, table_count, request.desired_style,
                                                  priority, hall, float(menu["price_per_table"]),
                                                  decor, included, groups, weights)
                scored.append({
                    "hall": hall, "menu": menu, "decor": decor,
                    "included": included, "score": s, "parts": parts, "total": total,
                })
    total_eval = len(scored)
    scored.sort(key=lambda x: x["score"], reverse=True)
    top = scored[:3]
    items: List[Dict] = []
    for entry in top:
        hall, menu, decor = entry["hall"], entry["menu"], entry["decor"]
        total = entry["total"]
        menu_total = float(menu["price_per_table"]) * table_count
        items.append({
            "hall_id": hall["hall_id"],
            "hall_name": hall["hall_name"],
            "hall_price": float(hall["base_price"]),
            "menu_id": menu["menu_id"],
            "menu_name": menu["menu_name"],
            "price_per_table": float(menu["price_per_table"]),
            "total_menu_price": float(menu_total),
            "decor_id": decor["decor_id"],
            "decor_name": decor["decor_name"],
            "decor_price": float(decor["price"]),
            "decor_style": decor["style"],
            "included_services": entry["included"],
            "total_estimated_cost": float(total),
            "matching_score": round(float(entry["score"]) * 100, 1),
            "explanation_arguments": build_explanation(
                hall, decor, request.desired_style, budget, total,
                table_count, entry["parts"], priority, entry["included"]),
        })
    return items, total_eval
