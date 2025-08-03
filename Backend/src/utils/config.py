from pydantic_settings import BaseSettings
from pathlib import Path

class Settings(BaseSettings):
    """Application settings"""
    
    # Server settings
    host: str = "localhost"
    port: int = 8000
    reload: bool = False
    
    # Plugin settings
    plugin_directory: Path = Path("src/plugins")
    
    # GPU settings
    preferred_device: str = "DirectML"
    enable_mixed_precision: bool = True
    
    # Logging settings
    log_level: str = "INFO"
    log_file: str = "archetype.log"
    
    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"