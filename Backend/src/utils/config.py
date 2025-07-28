import os
from pathlib import Path
from typing import Optional
from pydantic_settings import BaseSettings


class Settings():
    """Application settings"""
    
    # Server configuration
    host: str = "localhost"
    port: int = 8000
    debug: bool = False
    
    # GPU configuration
    gpu_enabled: bool = True
    gpu_memory_fraction: float = 0.8
    mixed_precision: bool = True
    
    # Training configuration
    max_concurrent_trainings: int = 3
    default_batch_size: int = 32
    checkpoint_interval: int = 10  # epochs
    
    # Plugin configuration
    plugin_directory: Path = Path("plugins")
    enable_plugin_hot_reload: bool = True
    plugin_timeout: int = 30  # seconds
    
    # Storage configuration
    model_storage_directory: Path = Path("models")
    dataset_storage_directory: Path = Path("datasets")
    temp_directory: Path = Path("temp")
    
    # Security
    max_file_size_mb: int = 1024
    allowed_file_types: list = [".pt", ".pth", ".pkl", ".json", ".yaml"]
    
    class Config:
        env_file = ".env"
        case_sensitive = False