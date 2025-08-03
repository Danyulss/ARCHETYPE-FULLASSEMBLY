from pydantic import BaseModel
from typing import List, Dict, Any, Optional

class GPUDeviceModel(BaseModel):
    """Pydantic model for GPU device information"""
    id: str
    name: str
    vendor: str
    type: str
    memory_mb: int
    available_memory_mb: int
    memory_usage_percent: float
    performance_score: int
    is_discrete: bool
    supports_fp16: bool
    compute_capability: Optional[str]
    is_selected: bool
    temperature_c: Optional[float] = None
    power_usage_w: Optional[float] = None

class GPUPreferenceModel(BaseModel):
    """Pydantic model for GPU preference options"""
    id: str
    name: str
    description: str
    available: bool

class GPUSettingsModel(BaseModel):
    """Pydantic model for complete GPU settings"""
    current_device: Optional[GPUDeviceModel]
    available_devices: List[GPUDeviceModel]
    available_preferences: List[GPUPreferenceModel]
    current_preference: str

class GPUPreferenceRequest(BaseModel):
    """Request model for setting GPU preference"""
    preference: str

class GPUDeviceSelection(BaseModel):
    """Request model for device selection"""
    device_id: str

class GPUBenchmarkResult(BaseModel):
    """Model for benchmark results"""
    device: str
    device_type: str
    vendor: str
    overall_gflops: float
    results: Dict[str, Any]
    performance_score: int
    supports_fp16: bool
    memory_mb: int
    error: Optional[str] = None