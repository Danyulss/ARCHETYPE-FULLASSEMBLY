from fastapi import APIRouter, Request, HTTPException
from pydantic import BaseModel
from typing import Dict, Any, List, Optional

router = APIRouter()

class TrainingRequest(BaseModel):
    model_id: str
    dataset_config: Dict[str, Any]
    training_config: Dict[str, Any]
    validation_config: Optional[Dict[str, Any]] = None

class TrainingResponse(BaseModel):
    training_id: str
    model_id: str
    status: str
    current_epoch: int
    total_epochs: int
    metrics: Dict[str, float]
    estimated_time_remaining: float

class TrainingListResponse(BaseModel):
    trainings: List[TrainingResponse]
    total: int

@router.post("/training/start", response_model=TrainingResponse)
async def start_training(training_request: TrainingRequest, request: Request):
    """Start model training"""
    training_engine = request.app.state.training_engine
    
    try:
        training_id = await training_engine.start_training(
            model_id=training_request.model_id,
            dataset_config=training_request.dataset_config,
            training_config=training_request.training_config,
            validation_config=training_request.validation_config
        )
        
        training_info = await training_engine.get_training_status(training_id)
        return TrainingResponse(**training_info)
        
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.post("/training/{training_id}/stop")
async def stop_training(training_id: str, request: Request):
    """Stop training"""
    training_engine = request.app.state.training_engine
    
    try:
        await training_engine.stop_training(training_id)
        return {"status": "stopped", "training_id": training_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.post("/training/{training_id}/pause")
async def pause_training(training_id: str, request: Request):
    """Pause training"""
    training_engine = request.app.state.training_engine
    
    try:
        await training_engine.pause_training(training_id)
        return {"status": "paused", "training_id": training_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.post("/training/{training_id}/resume")
async def resume_training(training_id: str, request: Request):
    """Resume training"""
    training_engine = request.app.state.training_engine
    
    try:
        await training_engine.resume_training(training_id)
        return {"status": "resumed", "training_id": training_id}
    except Exception as e:
        raise HTTPException(status_code=400, detail=str(e))

@router.get("/training/{training_id}", response_model=TrainingResponse)
async def get_training_status(training_id: str, request: Request):
    """Get training status"""
    training_engine = request.app.state.training_engine
    
    try:
        training_info = await training_engine.get_training_status(training_id)
        return TrainingResponse(**training_info)
    except Exception as e:
        raise HTTPException(status_code=404, detail=str(e))

@router.get("/training", response_model=TrainingListResponse)
async def list_trainings(request: Request, status: Optional[str] = None):
    """List all trainings"""
    training_engine = request.app.state.training_engine
    
    trainings = await training_engine.list_trainings(status_filter=status)
    return TrainingListResponse(trainings=trainings, total=len(trainings))

@router.get("/training/{training_id}/metrics")
async def get_training_metrics(training_id: str, request: Request):
    """Get detailed training metrics"""
    training_engine = request.app.state.training_engine
    
    try:
        metrics = await training_engine.get_training_metrics(training_id)
        return metrics
    except Exception as e:
        raise HTTPException(status_code=404, detail=str(e))