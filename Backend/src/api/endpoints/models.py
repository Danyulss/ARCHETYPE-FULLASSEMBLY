from fastapi import APIRouter, Request, HTTPException
from pydantic import BaseModel
from typing import Dict, Any, List, Optional
import uuid

router = APIRouter()

class ModelCreateRequest(BaseModel):
    name: str
    model_type: str  # "mlp", "rnn", "cnn", etc.
    architecture: Dict[str, Any]
    hyperparameters: Dict[str, Any]

class ModelResponse(BaseModel):
    id: str
    name: str
    model_type: str
    architecture: Dict[str, Any]
    hyperparameters: Dict[str, Any]
    parameter_count: int
    created_at: str
    status: str

class ModelListResponse(BaseModel):
    models: List[ModelResponse]
    total: int

@router.post("/models", response_model=ModelResponse)
async def create_model(model_request: ModelCreateRequest, request: Request):
    """Create a new neural network model"""
    model_factory = request.app.state.model_factory
    
    try:
        model_id = await model_factory.create_model(
            name=model_request.name,
            model_type=model_request.model_type,
            architecture=model_request.architecture,
            hyperparameters=model_request.hyperparameters
        )
        
        model_info = await model_factory.get_model_info(model_id)
        return ModelResponse(**model_info)
        
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.get("/models", response_model=ModelListResponse)
async def list_models(request: Request, skip: int = 0, limit: int = 100):
    """List all models"""
    model_factory = request.app.state.model_factory
    
    models = await model_factory.list_models(skip=skip, limit=limit)
    total = await model_factory.count_models()
    
    return ModelListResponse(models=models, total=total)

@router.get("/models/{model_id}", response_model=ModelResponse)
async def get_model(model_id: str, request: Request):
    """Get specific model information"""
    model_factory = request.app.state.model_factory
    
    try:
        model_info = await model_factory.get_model_info(model_id)
        return ModelResponse(**model_info)
    except Exception as e:
        raise HTTPException(status_code=404, detail=str(e))

@router.put("/models/{model_id}")
async def update_model(model_id: str, updates: Dict[str, Any], request: Request):
    """Update model parameters"""
    model_factory = request.app.state.model_factory
    
    try:
        await model_factory.update_model(model_id, updates)
        return {"status": "updated", "model_id": model_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.delete("/models/{model_id}")
async def delete_model(model_id: str, request: Request):
    """Delete a model"""
    model_factory = request.app.state.model_factory
    
    try:
        await model_factory.delete_model(model_id)
        return {"status": "deleted", "model_id": model_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.post("/models/{model_id}/export")
async def export_model(model_id: str, format: str, request: Request):
    """Export model in specified format"""
    model_factory = request.app.state.model_factory
    
    try:
        export_path = await model_factory.export_model(model_id, format)
        return {"export_path": export_path, "format": format}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))