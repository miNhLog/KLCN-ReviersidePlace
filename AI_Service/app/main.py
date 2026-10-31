"""Riverside Palace - Intelligent Recommendation & Decision Support Service (FastAPI, port 8000)."""
# UC10: nap bien moi truong tu .env TRUOC khi import config/core, vi config.py doc
# os.getenv luc import (CATALOG_PATH, budget tolerance...). rag_engine doc key luc goi
# nen cung huong loi do os.environ da duoc set. Thieu python-dotenv -> bo qua (offline).
try:
    from dotenv import load_dotenv

    load_dotenv()
except Exception:  # pragma: no cover - khong bat buoc de chay offline
    pass

import logging
import time
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from .core.config import CORS_ORIGINS, SERVICE_TITLE, SERVICE_VERSION
from .core.wsm_engine import rank_top3
from .schemas.recommendation import RecommendationRequest, RecommendationResponse

logger = logging.getLogger("uvicorn.error")


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Warm-up luc khoi dong: khoi tao catalog/weights/index/CF/RAG de request dau tien khong bi cham.
    # Truoc day /api/v1/ai/hybrid/rank ~8s va /api/v1/ai/rag/chat ~7-10s o lan goi dau (lazy load)
    # gay timeout tren Backend; warm-up dua chi phi do ve thoi diem khoi dong.
    t0 = time.perf_counter()
    done = []
    try:
        from .core import wsm_engine
        wsm_engine.load_catalog()
        wsm_engine.load_weights()
        done.append("wsm")
    except Exception as ex:  # pragma: no cover
        logger.warning("Warm-up wsm loi: %s", ex)
    try:
        from .core import cbf_engine
        cbf_engine.build_index()
        done.append("cbf")
    except Exception as ex:  # pragma: no cover
        logger.warning("Warm-up cbf loi: %s", ex)
    try:
        from .core import hybrid_engine
        hybrid_engine.load_hybrid_weights()
        done.append("hybrid")
    except Exception as ex:  # pragma: no cover
        logger.warning("Warm-up hybrid loi: %s", ex)
    try:
        from .core import cf_engine
        cf_engine.fit()
        done.append("cf")
    except Exception as ex:  # pragma: no cover
        logger.warning("Warm-up cf loi: %s", ex)
    try:
        from .core import rag_engine
        rag_engine.ensure_ingested()
        rag_engine.search_context("Riverside Palace", top_k=1)
        done.append("rag")
    except Exception as ex:  # pragma: no cover
        logger.warning("Warm-up rag loi: %s", ex)
    logger.info("AI warm-up xong [%s] trong %.2fs", ",".join(done), time.perf_counter() - t0)
    yield


app = FastAPI(title=SERVICE_TITLE, version=SERVICE_VERSION, lifespan=lifespan)

# UC10 RAG router (import lazy để main vẫn chạy khi thiếu dep option).
try:
    from .api.v1 import api_router as _rag_api_router
    app.include_router(_rag_api_router)
except Exception:  # pragma: no cover - fallback khi thiếu package option
    pass

app.add_middleware(
    CORSMiddleware,
    allow_origins=CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
def health():
    # TODO-7: healthcheck sau (catalog + weights) de Docker/compose biet AI that su san sang.
    payload = {"status": "ok", "service": SERVICE_TITLE, "version": SERVICE_VERSION,
               "catalog_loaded": False, "weights_source": "unknown"}
    try:
        from .core import wsm_engine
        catalog = wsm_engine.load_catalog()
        payload["catalog_loaded"] = bool(catalog.get("halls"))
        payload["catalog_halls"] = len(catalog.get("halls", []))
    except Exception as ex:  # pragma: no cover
        payload["catalog_error"] = str(ex)[:200]
    try:
        from .core import hybrid_engine
        payload["weights_source"] = hybrid_engine.weights_source()
    except Exception as ex:  # pragma: no cover
        payload["weights_error"] = str(ex)[:200]
    if not payload["catalog_loaded"]:
        payload["status"] = "degraded"
    return payload


@app.post("/api/v1/ai/wsm/rank", response_model=RecommendationResponse)
def rank_packages(payload: RecommendationRequest):
    items, total = rank_top3(payload)
    notice = (f"Da danh gia {total} phuong an bang WSM (Hard Filtering + Soft Scoring) "
              f"va tra ve toi da 3 ket qua.")
    if total == 0:
        notice = ("Khong co phuong an nao thoa Hard Filtering "
                  "(sanh trong, suc chua, tran ngan sach toi da 10 trieu VND).")
    return RecommendationResponse(success=True, total_candidates_evaluated=total,
                                  top_3=items, algorithm_notice=notice)
