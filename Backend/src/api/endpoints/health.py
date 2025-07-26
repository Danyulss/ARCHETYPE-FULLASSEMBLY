from fastapi import APIRouter, Request
from pydantic import BaseModel
import time
import psutil
import torch

router = APIRouter()

class HealthResponse(BaseModel):
    status: str
    version: str
    uptime: float
    timestamp: str
    gpu_available: bool
    gpu_count: int
    memory_usage_percent: float
    cpu_usage_percent: float

class HTTPException(Exception):
    def __init__(self, status_code: int, detail: str):
        self.status_code = status_code
        self.detail = detail

startup_time = time.time()

@router.post("/plugins/{plugin_id}/enable")
async def enable_plugin(plugin_id: str, request: Request):
    """Enable a plugin"""
    plugin_manager = request.app.state.plugin_manager
    
    try:
        await plugin_manager.enable_plugin(plugin_id)
        return {"status": "enabled", "plugin_id": plugin_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.post("/plugins/{plugin_id}/disable")
async def disable_plugin(plugin_id: str, request: Request):
    """Disable a plugin"""
    plugin_manager = request.app.state.plugin_manager
    
    try:
        await plugin_manager.disable_plugin(plugin_id)
        return {"status": "disabled", "plugin_id": plugin_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.get("/plugins/categories")
async def get_plugin_categories(request: Request):
    """Get available plugin categories"""
    plugin_manager = request.app.state.plugin_manager
    
    categories = await plugin_manager.get_plugin_categories()
    return {"categories": categories}