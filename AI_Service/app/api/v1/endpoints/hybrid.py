"""Endpoint Hybrid rank: POST /api/v1/ai/hybrid/rank (giu nguyen /wsm/rank cu)."""
from typing import List, Optional

from fastapi import APIRouter
from pydantic import Field

from ....core import config as _pricing_config
from ....core import hybrid_engine, wsm_engine
from ....schemas.recommendation import (
    RecommendationItem,
    RecommendationRequest,
    RecommendationResponse,
)

router = APIRouter(prefix="/api/v1/ai/hybrid", tags=["hybrid"])


class HybridRankRequest(RecommendationRequest):
    user_id: Optional[int] = Field(default=None)


def _enumerate_candidates(payload: RecommendationRequest):
    """Hard-filtering giong hets wsm_engine.rank_top3 nhung tra candidates tho."""
    catalog = wsm_engine.load_catalog()
    weights = wsm_engine.load_weights()
    groups = catalog["style_groups"]
    halls, menus, decors = catalog["halls"], catalog["menus"], catalog["decors"]
    occupied = set(payload.occupied_hall_ids or [])
    priority = list(payload.priority_services or [])
    budget = float(payload.expected_budget)
    table_count = int(payload.table_count)
    svc_map = {s["service_id"]: s["price"] for s in catalog["services"]}
    out = []
    for hall in halls:
        if hall["hall_id"] in occupied:
            continue
        if table_count < hall["min_tables"] or table_count > hall["max_tables"]:
            continue
        for menu in menus:
            for decor in decors:
                est_services = sum(svc_map.get(s, 0) for s in priority)
                est_total = (menu["price_per_table"] * table_count + hall["base_price"]
                             + decor["price"] + est_services)
                if est_total > budget + _pricing_config.budget_tolerance(budget):
                    continue
                included = list(priority)
                s, parts, total = wsm_engine.score_candidate(
                    budget, table_count, payload.desired_style, priority,
                    hall, float(menu["price_per_table"]), decor, included, groups, weights)
                out.append({"hall": hall, "menu": menu, "decor": decor,
                            "included": included, "score": s, "parts": parts, "total": total})
    return out


@router.post("/rank", response_model=RecommendationResponse)
def rank_hybrid(payload: HybridRankRequest):
    candidates = _enumerate_candidates(payload)
    total = len(candidates)
    if total == 0:
        return RecommendationResponse(success=True, total_candidates_evaluated=0,
                                      top_3=[],
                                      algorithm_notice=("Khong co phuong an nao thoa Hard Filtering "
                                                        "(sanh trong, suc chua, tran ngan sach Δ=min(10tr,5%))."),
                                      weights_source=hybrid_engine.weights_source())
    top, w = hybrid_engine.rank_candidates(payload, candidates, user_id=payload.user_id)
    combo_meta = [
        ("BALANCED", "Combo Hoàn Hảo (Top 1)", "LỰA CHỌN TỐI ƯU"),
        ("STANDARD", "Combo Tiêu Chuẩn (Top 2)", "CÂN BẰNG CHI PHÍ"),
        ("LUXURY", "Combo Sang Trọng (Top 3)", "TRẢI NGHIỆM ĐẲNG CẤP"),
    ]
    items: List[RecommendationItem] = []
    for idx, entry in enumerate(top):
        hall, menu, decor = entry["hall"], entry["menu"], entry["decor"]
        total_cost = entry["total"]
        menu_total = float(menu["price_per_table"]) * int(payload.table_count)
        ctype, ctitle, cbadge = combo_meta[idx] if idx < len(combo_meta) else combo_meta[-1]
        args = wsm_engine.build_explanation(hall, decor, payload.desired_style,
                                            float(payload.expected_budget), total_cost,
                                            int(payload.table_count), entry["parts"],
                                            list(payload.priority_services or []), entry["included"])
        # VĐ2: null-safe s_cf/s_cbf/S_wsm (None -> 0.0, không vỡ format :.2f).
        s_cf = entry.get('s_cf') or 0.0
        s_cbf = entry.get('s_cbf') or 0.0
        s_wsm = entry.get('s_wsm') or 0.0
        try:
            alpha_f, beta_f, gamma_f = float(w.get('alpha', 0)), float(w.get('beta', 0)), float(w.get('gamma', 0))
        except (TypeError, ValueError):
            alpha_f, beta_f, gamma_f = 0.0, 0.0, 0.0
        args = list(args) + [f"Hybrid: s_cf={float(s_cf):.2f}, s_cbf={float(s_cbf):.2f}, "
                             f"S_wsm={float(s_wsm):.2f} (alpha={alpha_f:.2f}, beta={beta_f:.2f}, gamma={gamma_f:.2f})."]
        parts = entry.get("parts") or {}
        r1 = float(parts.get("r1", 0.95))
        r2 = float(parts.get("r2", 0.92))
        r3 = float(parts.get("r3", 0.90))
        b_score = round(max(50.0, min(100.0, r1 * 100)), 1)
        sp_score = round(max(50.0, min(100.0, r2 * 100)), 1)
        st_score = round(max(50.0, min(100.0, (1.0 if r3 >= 1.0 else 0.85 if r3 >= 0.5 else 0.75) * 100)), 1)
        rt_score = round(max(50.0, min(100.0, ((sp_score + st_score) / 2))), 1)
        items.append(RecommendationItem(
            hall_id=hall["hall_id"], hall_name=hall["hall_name"], hall_price=float(hall["base_price"]),
            hall_min_guests=int(hall.get("min_guests", hall["min_tables"] * 10)),
            hall_max_guests=int(hall.get("max_guests", hall["max_tables"] * 10)),
            hall_min_tables=int(hall["min_tables"]), hall_max_tables=int(hall["max_tables"]),
            capacity_min_guests=int(hall.get("min_guests", hall["min_tables"] * 10)),
            capacity_max_guests=int(hall.get("max_guests", hall["max_tables"] * 10)),
            capacity_min_tables=int(hall["min_tables"]), capacity_max_tables=int(hall["max_tables"]),
            menu_id=menu["menu_id"], menu_name=menu["menu_name"],
            price_per_table=float(menu["price_per_table"]), total_menu_price=float(menu_total),
            decor_id=decor["decor_id"], decor_name=decor["decor_name"],
            decor_price=float(decor["price"]), decor_style=decor["style"],
            included_services=entry["included"], total_estimated_cost=float(total_cost),
            matching_score=round(float(entry["score"]) * 100, 1),
            budget_match_score=b_score, space_fit_score=sp_score,
            style_theme_score=st_score, rating_credibility_score=rt_score,
            explanation_arguments=args,
            combo_name=ctitle, combo_type=ctype, combo_title=ctitle, combo_badge=cbadge))
    # VĐ2: null-safe weights_source (None -> chuoi mac dinh, khong vo khi thieu file).
    try:
        ws = hybrid_engine.weights_source() or "unknown"
    except Exception:
        ws = "unknown"
    try:
        wa, wb, wg = float(w.get('alpha', 0)), float(w.get('beta', 0)), float(w.get('gamma', 0))
    except (TypeError, ValueError, AttributeError):
        wa, wb, wg = 0.0, 0.0, 0.0
    notice = (f"Da danh gia {total} phuong an bang Hybrid "
              f"(alpha={wa:.3f}, beta={wb:.3f}, gamma={wg:.3f} hoc tu du lieu) "
              f"va tra ve toi da 3 ket qua.")
    return RecommendationResponse(success=True, total_candidates_evaluated=total,
                                   top_3=[i.model_dump() for i in items], algorithm_notice=notice,  # type: ignore
                                   weights_source=ws)
