from fastapi import APIRouter

from .endpoints.hybrid import router as hybrid_router
from .endpoints.rag import router as rag_router

api_router = APIRouter()
api_router.include_router(hybrid_router)
api_router.include_router(rag_router)
