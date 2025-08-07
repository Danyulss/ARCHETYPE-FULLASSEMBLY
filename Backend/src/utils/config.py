from pydantic_settings import BaseSettings
from pathlib import Path
from typing import Optional

class Settings(BaseSettings):
    # Existing settings...
    host: str = "localhost"
    port: int = 8000
    plugin_directory: Path = Path("src/plugins")
    
    # GPU Configuration
    gpu_preference: str = "auto"  # auto, gpu_only, cpu_only, nvidia_only, amd_only, intel_only
    preferred_device_id: Optional[str] = None
    enable_gpu_monitoring: bool = True
    benchmark_on_startup: bool = False
    gpu_memory_fraction: float = 0.8  # Use 80% of GPU memory max
    enable_mixed_precision: bool = True
    
    # Performance settings
    cpu_threads: Optional[int] = None  # Auto-detect if None
    enable_cpu_optimizations: bool = True
    memory_pool_size_mb: int = 1024
    
    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"