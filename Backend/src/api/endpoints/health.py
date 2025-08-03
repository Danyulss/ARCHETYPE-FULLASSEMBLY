from fastapi import APIRouter, Request, HTTPException
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

@router.get("/health", response_model=HealthResponse)
async def health_check(request: Request):
    """Health check endpoint for Unity connection verification"""
    try:
        current_time = time.time()
        startup_time = getattr(request.app.state, 'startup_time', current_time)
        uptime = current_time - startup_time
        
        # Get system metrics
        memory_usage = psutil.virtual_memory().percent
        cpu_usage = psutil.cpu_percent(interval=0.1)
        
        # GPU information
        gpu_available = torch.cuda.is_available()
        gpu_count = torch.cuda.device_count() if gpu_available else 0
        
        return HealthResponse(
            status="healthy",
            version="1.0.0",
            uptime=uptime,
            timestamp=time.strftime("%Y-%m-%d %H:%M:%S"),
            gpu_available=gpu_available,
            gpu_count=gpu_count,
            memory_usage_percent=memory_usage,
            cpu_usage_percent=cpu_usage
        )
    except Exception as e:
        # Return a minimal healthy response even if some metrics fail
        return HealthResponse(
            status="healthy",
            version="1.0.0", 
            uptime=0.0,
            timestamp=time.strftime("%Y-%m-%d %H:%M:%S"),
            gpu_available=False,
            gpu_count=0,
            memory_usage_percent=0.0,
            cpu_usage_percent=0.0
        )

@router.get("/health/detailed")
async def detailed_health_check(request: Request):
    """Detailed health check with component status"""
    try:
        gpu_manager = getattr(request.app.state, 'gpu_manager', None)
        model_factory = getattr(request.app.state, 'model_factory', None)
        training_engine = getattr(request.app.state, 'training_engine', None)
        plugin_manager = getattr(request.app.state, 'plugin_manager', None)
        
        return {
            "status": "healthy",
            "components": {
                "gpu_manager": {
                    "initialized": gpu_manager.is_initialized() if gpu_manager else False,
                    "device": str(gpu_manager.device) if gpu_manager and gpu_manager.is_initialized() else None,
                    "memory_allocated": torch.cuda.memory_allocated() if torch.cuda.is_available() else 0
                },
                "model_factory": {
                    "initialized": model_factory.is_initialized() if model_factory else False,
                    "active_models": len(model_factory.get_active_models()) if model_factory else 0
                },
                "training_engine": {
                    "initialized": training_engine.is_initialized() if training_engine else False,
                    "active_trainings": len(training_engine.get_active_trainings()) if training_engine else 0
                },
                "plugin_manager": {
                    "loaded_plugins": len(plugin_manager.get_loaded_plugins()) if plugin_manager else 0,
                    "core_plugins": len(plugin_manager.get_core_plugins()) if plugin_manager else 0
                }
            }
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Health check failed: {str(e)}")