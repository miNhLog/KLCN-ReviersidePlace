"""Endpoint Dong bo Danh muc: POST /api/v1/ai/catalog/reload

Ho tro 2 che do:
- Mode A (Reload tu file): Body trong hoac rong -> doc lai raw_catalog.json tu dia va refresh in-memory cache (WSM, CBF).
- Mode B (Sync truc tiep): Nhan payload JSON danh muc moi tu Backend, atomic write vao file dia va reload in-memory cache.
"""
import json
import logging
import os
import tempfile
from pathlib import Path
from typing import Any, Dict, Optional

from fastapi import APIRouter, Body
from pydantic import BaseModel

from ....core import config as _cfg
from ....core import cbf_engine, wsm_engine

router = APIRouter(prefix="/api/v1/ai/catalog", tags=["catalog"])
logger = logging.getLogger("uvicorn.error")


class CatalogSyncResponse(BaseModel):
    success: bool
    mode: str
    halls_count: int
    menus_count: int
    decors_count: int
    services_count: int
    message: str


def _atomic_write_json(file_path: Path, data: Dict[str, Any]) -> None:
    """Ghi file an toan (atomic write) bang tempfile + os.replace de tranh file hong khi crash."""
    file_path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", dir=str(file_path.parent), delete=False, encoding="utf-8", suffix=".tmp") as tf:
        json.dump(data, tf, ensure_ascii=False, indent=2)
        temp_name = tf.name
    os.replace(temp_name, str(file_path))


@router.post("/reload", response_model=CatalogSyncResponse)
def reload_catalog(payload: Optional[Dict[str, Any]] = Body(default=None)):
    cat_path = Path(_cfg.CATALOG_PATH)

    # Mode B: Tiep nhan truc tiep payload day du tu Backend
    if payload and isinstance(payload, dict) and payload.get("halls"):
        mode = "direct_sync"
        # 1. Ghi de an toan vao dia
        try:
            _atomic_write_json(cat_path, payload)
        except Exception as ex:
            logger.error("[Catalog Sync] Loi ghi file catalog: %s", ex)
        
        # 2. Cap nhat in-memory cache cua WSM
        cat = wsm_engine.reload_catalog(payload)
        
        # 3. Build lai TF-IDF index cua CBF
        try:
            cbf_engine.build_index(force=True)
        except Exception as ex:
            logger.warning("[Catalog Sync] Canh bao rebuild CBF index: %s", ex)

        n_halls = len(cat.get("halls", []))
        n_menus = len(cat.get("menus", []))
        n_decors = len(cat.get("decors", []))
        n_svcs = len(cat.get("services", []))
        msg = f"[Catalog Sync] Đã cập nhật {n_halls} sảnh, {n_menus} menu, {n_decors} decor thành công (từ Backend API)."
        logger.info(msg)
        return CatalogSyncResponse(
            success=True,
            mode=mode,
            halls_count=n_halls,
            menus_count=n_menus,
            decors_count=n_decors,
            services_count=n_svcs,
            message=msg
        )

    # Mode A: Doc lai tu file dia
    mode = "file_reload"
    cat = wsm_engine.reload_catalog(None)
    try:
        cbf_engine.build_index(force=True)
    except Exception as ex:
        logger.warning("[Catalog Sync] Canh bao rebuild CBF index: %s", ex)

    n_halls = len(cat.get("halls", []))
    n_menus = len(cat.get("menus", []))
    n_decors = len(cat.get("decors", []))
    n_svcs = len(cat.get("services", []))
    msg = f"[Catalog Sync] Đã cập nhật {n_halls} sảnh, {n_menus} menu, {n_decors} decor thành công (từ đĩa)."
    logger.info(msg)
    return CatalogSyncResponse(
        success=True,
        mode=mode,
        halls_count=n_halls,
        menus_count=n_menus,
        decors_count=n_decors,
        services_count=n_svcs,
        message=msg
    )
