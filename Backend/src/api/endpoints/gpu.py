from fastapi import APIRouter, Request, HTTPException
from pydantic import BaseModel
from typing import List, Dict, Any
import torch as torch
import time as time
import psutil

router = APIRouter()

class GPUInfo(BaseModel):
    device_id: int
    name: str
    compute_capability: str
    total_memory: int
    available_memory: int
    utilization: float
    temperature: float
    power_usage: float

class GPUListResponse(BaseModel):
    gpus: List[GPUInfo]
    selected_gpu: int
    cuda_available: bool
    total_devices: int

@router.get("/gpu", response_model=GPUListResponse)
async def get_gpu_info(request: Request):
    """Get GPU information"""
    gpu_manager = request.app.state.gpu_manager
    
    try:
        gpu_info = await gpu_manager.get_gpu_info()
        return GPUListResponse(**gpu_info)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@router.get("/gpu/{device_id}", response_model=GPUInfo)
async def get_specific_gpu(device_id: int, request: Request):
    """Get specific GPU information"""
    gpu_manager = request.app.state.gpu_manager
    
    try:
        gpu_info = await gpu_manager.get_device_info(device_id)
        return GPUInfo(**gpu_info)
    except Exception as e:
        raise HTTPException(status_code=404, detail=str(e))

@router.post("/gpu/{device_id}/select")
async def select_gpu(device_id: int, request: Request):
    """Select GPU for training"""
    gpu_manager = request.app.state.gpu_manager
    
    try:
        await gpu_manager.set_device(device_id)
        return {"status": "selected", "device_id": device_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.get("/gpu/memory/clear")
async def clear_gpu_memory(request: Request):
    """Clear GPU memory cache"""
    gpu_manager = request.app.state.gpu_manager
    
    try:
        await gpu_manager.clear_cache()
        return {"status": "cleared"}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@router.get("/gpu/benchmark")
async def benchmark_gpu(request: Request):
    """Run GPU benchmark"""
    gpu_manager = request.app.state.gpu_manager
    
    try:
        benchmark_results = await gpu_manager.run_benchmark()
        return benchmark_results
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
        router.get("/health", response_model=HealthResponse) 
      
async def health_check(request: Request):
    """Health check endpoint for Unity connection verification"""
    current_time = time.time()
    startup_time = request.app.state.startup_time
    uptime = current_time - startup_time
    
    # Get system metrics
    memory_usage = psutil.virtual_memory().percent
    cpu_usage = psutil.cpu_percent(interval=0.1)
    
    # GPU information
    gpu_available = torch.cuda.is_available()
    gpu_count = torch.cuda.device_count() if gpu_available else 0
    
    return {
        "status": "healthy",
        "version": "1.0.0",
        "uptime": uptime,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
        "gpu_available": gpu_available,
        "gpu_count": gpu_count,
        "memory_usage_percent": memory_usage,
        "cpu_usage_percent": cpu_usage
    }

@router.get("/health/detailed")
async def detailed_health_check(request: Request):
    """Detailed health check with component status"""
    gpu_manager = request.app.state.gpu_manager
    model_factory = request.app.state.model_factory
    training_engine = request.app.state.training_engine
    plugin_manager = request.app.state.plugin_manager
    
    return {
        "status": "healthy",
        "components": {
            "gpu_manager": {
                "initialized": gpu_manager.is_initialized(),
                "device": str(gpu_manager.device) if gpu_manager.is_initialized() else None,
                "memory_allocated": torch.cuda.memory_allocated() if torch.cuda.is_available() else 0
            },
            "model_factory": {
                "initialized": model_factory.is_initialized(),
                "active_models": len(model_factory.get_active_models())
            },
            "training_engine": {
                "initialized": training_engine.is_initialized(),
                "active_trainings": len(training_engine.get_active_trainings())
            },
            "plugin_manager": {
                "loaded_plugins": len(plugin_manager.get_loaded_plugins()),
                "core_plugins": len(plugin_manager.get_core_plugins())
            }
        }
    }

router = APIRouter()

class GPUPreferenceRequest(BaseModel):
    preference: str

class GPUDeviceSelection(BaseModel):
    device_id: str

class GPUPreference(BaseModel):
    id: str
    name: str
    description: str
    available: bool

class GPUSettings(BaseModel):
    current_device: Dict[str, Any]
    available_devices: List[Dict[str, Any]]
    available_preferences: List[GPUPreference]
    current_preference: str

@router.get("/gpu/settings", response_model=GPUSettings)
async def get_gpu_settings(request: Request):
    """Get current GPU settings and available options"""
    gpu_manager = getattr(request.app.state, 'gpu_manager', None)
    if not gpu_manager:
        raise HTTPException(status_code=503, detail="GPU manager not initialized")
    
    try:
        # Get current device info
        current_device = {}
        if gpu_manager.selected_device:
            device = gpu_manager.selected_device
            current_device = {
                "id": device.device_id,
                "name": device.name,
                "vendor": device.vendor.value,
                "type": device.device_type.value,
                "memory_mb": device.total_memory_mb,
                "performance_score": device.performance_score,
                "is_discrete": device.is_discrete
            }
        
        # Get all available devices
        available_devices = gpu_manager.get_all_devices()
        
        # Get available preferences
        available_preferences = [
            GPUPreference(**pref) for pref in gpu_manager.get_available_preferences()
        ]
        
        # Get current preference
        current_preference = gpu_manager.get_current_preference()
        
        return GPUSettings(
            current_device=current_device,
            available_devices=available_devices,
            available_preferences=available_preferences,
            current_preference=current_preference
        )
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get GPU settings: {str(e)}")

@router.post("/gpu/preference")
async def set_gpu_preference(request: Request, preference_request: GPUPreferenceRequest):
    """Set GPU preference (auto, gpu_only, cpu_only, nvidia_only, amd_only, intel_only)"""
    gpu_manager = getattr(request.app.state, 'gpu_manager', None)
    if not gpu_manager:
        raise HTTPException(status_code=503, detail="GPU manager not initialized")
    
    try:
        await gpu_manager.set_gpu_preference(preference_request.preference)
        
        # Return updated settings
        return {
            "status": "success",
            "preference": preference_request.preference,
            "selected_device": gpu_manager.get_device_info(),
            "pytorch_device": str(gpu_manager.get_device())
        }
        
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.post("/gpu/select-device")
async def select_gpu_device(request: Request, device_selection: GPUDeviceSelection):
    """Manually select a specific GPU device"""
    gpu_manager = getattr(request.app.state, 'gpu_manager', None)
    if not gpu_manager:
        raise HTTPException(status_code=503, detail="GPU manager not initialized")
    
    try:
        await gpu_manager.select_device(device_selection.device_id)
        
        return {
            "status": "success",
            "selected_device": gpu_manager.get_device_info(),
            "pytorch_device": str(gpu_manager.get_device())
        }
        
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.get("/gpu/benchmark")
async def run_gpu_benchmark(request: Request):
    """Run GPU benchmark on current device"""
    gpu_manager = getattr(request.app.state, 'gpu_manager', None)
    if not gpu_manager:
        raise HTTPException(status_code=503, detail="GPU manager not initialized")
    
    try:
        benchmark_results = await gpu_manager.run_benchmark()
        return benchmark_results
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Benchmark failed: {str(e)}")

@router.post("/gpu/optimize-for-model")
async def optimize_for_model(request: Request, model_size_mb: int):
    """Get optimization recommendations for a specific model size"""
    gpu_manager = getattr(request.app.state, 'gpu_manager', None)
    if not gpu_manager:
        raise HTTPException(status_code=503, detail="GPU manager not initialized")
    
    try:
        optimization = await gpu_manager.optimize_for_model_size(model_size_mb)
        return optimization
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Optimization failed: {str(e)}")