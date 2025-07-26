from fastapi import APIRouter, Request, HTTPException
from pydantic import BaseModel
from typing import List, Dict, Any

router = APIRouter()

class PluginInfo(BaseModel):
    id: str
    name: str
    version: str
    description: str
    author: str
    plugin_type: str
    loaded: bool
    enabled: bool
    dependencies: List[str]
    manifest: Dict[str, Any]

class PluginListResponse(BaseModel):
    plugins: List[PluginInfo]
    total: int

@router.get("/plugins", response_model=PluginListResponse)
async def list_plugins(request: Request):
    """List all available plugins"""
    plugin_manager = request.app.state.plugin_manager
    
    plugins = await plugin_manager.list_plugins()
    return PluginListResponse(plugins=plugins, total=len(plugins))

@router.get("/plugins/{plugin_id}", response_model=PluginInfo)
async def get_plugin(plugin_id: str, request: Request):
    """Get specific plugin information"""
    plugin_manager = request.app.state.plugin_manager
    
    try:
        plugin_info = await plugin_manager.get_plugin_info(plugin_id)
        return PluginInfo(**plugin_info)
    except Exception as e:
        raise HTTPException(status_code=404, detail=str(e))

@router.post("/plugins/{plugin_id}/load")
async def load_plugin(plugin_id: str, request: Request):
    """Load a plugin"""
    plugin_manager = request.app.state.plugin_manager
    
    try:
        await plugin_manager.load_plugin(plugin_id)
        return {"status": "loaded", "plugin_id": plugin_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.post("/plugins/{plugin_id}/unload")
async def unload_plugin(plugin_id: str, request: Request):
    """Unload a plugin"""
    plugin_manager = request.app.state.plugin_manager
    
    try:
        await plugin_manager.unload_plugin(plugin_id)
        return {"status": "unloaded", "plugin_id": plugin_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))