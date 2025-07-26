from fastapi import APIRouter

# Import all endpoint routers for easy access
from .endpoints import health, models, training, plugins, gpu

# Create main API router
api_router = APIRouter()

# Include all endpoint routers
api_router.include_router(health.router, prefix="/health", tags=["health"])
api_router.include_router(models.router, prefix="/models", tags=["models"])
api_router.include_router(training.router, prefix="/training", tags=["training"])
api_router.include_router(plugins.router, prefix="/plugins", tags=["plugins"])
api_router.include_router(gpu.router, prefix="/gpu", tags=["gpu"])

__all__ = [
    "api_router",
    "health",
    "models", 
    "training",
    "plugins",
    "gpu"
]