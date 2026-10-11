"""Pydantic schemas cho API goi y WSM."""
from typing import List, Optional

from pydantic import BaseModel, Field


class RecommendationRequest(BaseModel):
    guest_count: int = Field(ge=1, le=2000)
    table_count: int = Field(ge=1, le=200)
    expected_budget: float = Field(gt=0)
    event_date: str = Field(pattern=r"^\d{4}-\d{2}-\d{2}$")
    shift: str = Field(pattern=r"^(Ca trua|Ca toi)$")
    desired_style: str
    priority_services: List[str] = Field(default_factory=list)
    occupied_hall_ids: List[int] = Field(default_factory=list)


class RecommendationItem(BaseModel):
    hall_id: int
    hall_name: str
    hall_price: float
    menu_id: int
    menu_name: str
    price_per_table: float
    total_menu_price: float
    decor_id: int
    decor_name: str
    decor_price: float
    decor_style: str
    included_services: List[str] = Field(default_factory=list)
    total_estimated_cost: float
    matching_score: float = Field(ge=0, le=100)
    explanation_arguments: List[str] = Field(default_factory=list)


class RecommendationResponse(BaseModel):
    success: bool = True
    total_candidates_evaluated: int = 0
    top_3: List[RecommendationItem] = Field(default_factory=list)
    algorithm_notice: Optional[str] = None
