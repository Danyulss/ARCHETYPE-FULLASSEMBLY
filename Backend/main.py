import uvicorn
import asyncio
import logging
import argparse
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from fastapi.responses import JSONResponse

# Import our modules
from src.api.endpoints import health, models, training, plugins, gpu
from src.core.gpu_manager import GPUManager
from src.core.model_factory import ModelFactory
from src.core.training_engine import TrainingEngine
from src.plugins.plugin_manager import PluginManager
from src.utils.config import Settings
from src.utils.logging_config import setup_logging
from src.utils.websocket_manager import WebSocketManager

# Global managers
gpu_manager: GPUManager = GPUManager()
model_factory: ModelFactory = ModelFactory(gpu_manager=gpu_manager)
training_engine: TrainingEngine = TrainingEngine(gpu_manager, model_factory)
plugin_manager: PluginManager = PluginManager(plugin_directory = Path(__file__).parent / "src" / "plugins")
websocket_manager: WebSocketManager = WebSocketManager()
settings: Settings = Settings()

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager - startup and shutdown"""
    global gpu_manager, model_factory, training_engine, plugin_manager, websocket_manager, settings
    
    # Startup
    logging.info("🚀 Starting Archetype Neural Network Backend...")
    
    try:
        # Load configuration
        settings = Settings()
        
        # Initialize GPU manager first
        gpu_manager = GPUManager()
        await gpu_manager.initialize()
        logging.info(f"✅ GPU Manager initialized - {gpu_manager.get_device_info()}")
        
        # Initialize model factory
        model_factory = ModelFactory(gpu_manager)
        await model_factory.initialize()
        logging.info("✅ Model Factory initialized")
        
        # Initialize training engine
        training_engine = TrainingEngine(gpu_manager, model_factory)
        await training_engine.initialize()
        logging.info("✅ Training Engine initialized")
        
        # Initialize plugin manager
        plugin_manager = PluginManager(settings.plugin_directory)
        await plugin_manager.load_core_plugins()
        logging.info(f"✅ Plugin Manager initialized - {len(plugin_manager.get_loaded_plugins())} plugins loaded")
        
        # Initialize WebSocket manager
        websocket_manager = WebSocketManager()
        logging.info("✅ WebSocket Manager initialized")
        
        # Store managers in app state for access by endpoints
        app.state.gpu_manager = gpu_manager
        app.state.model_factory = model_factory
        app.state.training_engine = training_engine
        app.state.plugin_manager = plugin_manager
        app.state.websocket_manager = websocket_manager
        app.state.settings = settings
        
        logging.info("🎉 Archetype Backend startup complete!")
        
    except Exception as e:
        logging.error(f"❌ Failed to initialize backend: {e}")
        raise
    
    yield  # Server runs here
    
    # Shutdown
    logging.info("🛑 Shutting down Archetype Backend...")
    
    if training_engine:
        await training_engine.shutdown()
        logging.info("✅ Training Engine shutdown")
    
    if plugin_manager:
        await plugin_manager.unload_all_plugins()
        logging.info("✅ Plugins unloaded")
    
    if gpu_manager:
        await gpu_manager.cleanup()
        logging.info("✅ GPU Manager cleanup")
    
    logging.info("👋 Archetype Backend shutdown complete")

# Create FastAPI application
app = FastAPI(
    title="Archetype Neural Network Backend",
    description="Python backend for Archetype Unity neural network builder",
    version="1.0.0",
    docs_url="/docs",
    redoc_url="/redoc",
    lifespan=lifespan
)

# CORS middleware for Unity requests
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Unity localhost requests
    allow_credentials=True,
    allow_methods=["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    allow_headers=["*"],
)

# Include API routers
app.include_router(health.router, prefix="/api/v1", tags=["health"])
app.include_router(models.router, prefix="/api/v1", tags=["models"])
app.include_router(training.router, prefix="/api/v1", tags=["training"])
app.include_router(plugins.router, prefix="/api/v1", tags=["plugins"])
app.include_router(gpu.router, prefix="/api/v1", tags=["gpu"])

# WebSocket endpoint for real-time updates
@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket):
    """WebSocket endpoint for real-time communication with Unity"""
    await websocket_manager.connect(websocket)
    try:
        while True:
            # Keep connection alive and handle any incoming messages
            data = await websocket.receive_text()
            # Echo back for now - extend as needed
            await websocket_manager.send_personal_message(f"Echo: {data}", websocket)
    except WebSocketDisconnect:
        websocket_manager.disconnect(websocket)

# Training progress WebSocket
@app.websocket("/ws/training/{training_id}")
async def training_websocket(websocket: WebSocket, training_id: str):
    """WebSocket for real-time training progress updates"""
    await websocket_manager.connect(websocket)
    try:
        # Subscribe to training updates
        await training_engine.subscribe_to_training(training_id, websocket)
        while True:
            await asyncio.sleep(0.1)  # Keep connection alive
    except WebSocketDisconnect:
        websocket_manager.disconnect(websocket)
        await training_engine.unsubscribe_from_training(training_id, websocket)

# Static file serving for plugin assets
app.mount("/static", StaticFiles(directory="static"), name="static")

# Root endpoint
@app.get("/")
async def root():
    """Root endpoint with server information"""
    return {
        "name": "Archetype Neural Network Backend",
        "version": "1.0.0",
        "status": "running",
        "docs": "/docs",
        "health": "/api/v1/health",
        "websocket": "/ws"
    }

# Global exception handler
@app.exception_handler(Exception)
async def global_exception_handler(request, exc):
    logging.error(f"Global exception: {exc}")
    return JSONResponse(
        status_code=500,
        content={
            "error": "Internal server error",
            "message": str(exc),
            "type": type(exc).__name__
        }
    )

def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description="Archetype Neural Network Backend")
    parser.add_argument("--host", default="localhost", help="Server host")
    parser.add_argument("--port", type=int, default=8000, help="Server port")
    parser.add_argument("--reload", action="store_true", help="Enable auto-reload")
    parser.add_argument("--log-level", default="info", help="Log level")
    parser.add_argument("--headless", action="store_true", help="Run in headless mode")
    
    args = parser.parse_args()
    
    # Setup logging
    setup_logging(level=args.log_level.upper())
    
    logging.info(f"🐍 Starting Archetype Backend on {args.host}:{args.port}")
    
    # Run server
    uvicorn.run(
        "main:app",
        host=args.host,
        port=args.port,
        reload=args.reload,
        log_level=args.log_level,
        access_log=not args.headless
    )

if __name__ == "__main__":
    main()