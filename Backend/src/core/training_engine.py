import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import DataLoader, TensorDataset
import logging
import asyncio
import uuid
import time
from typing import Dict, List, Any, Optional, Callable
from datetime import datetime
import numpy as np

class TrainingEngine:
    """Manages neural network training processes"""
    
    def __init__(self, gpu_manager, model_factory):
        self.gpu_manager = gpu_manager
        self.model_factory = model_factory
        self.active_trainings: Dict[str, Dict[str, Any]] = {}
        self.training_tasks: Dict[str, asyncio.Task] = {}
        self.subscribers: Dict[str, List[Any]] = {}  # WebSocket subscribers
        self.initialized = False
    
    async def initialize(self):
        """Initialize training engine"""
        logging.info("🎯 Initializing Training Engine...")
        self.initialized = True
        return True
    
    async def start_training(self, model_id: str, dataset_config: Dict[str, Any],
                           training_config: Dict[str, Any], 
                           validation_config: Optional[Dict[str, Any]] = None) -> str:
        """Start model training"""
        training_id = str(uuid.uuid4())
        
        # Get model
        model = self.model_factory.get_model(model_id)
        
        # Prepare training info
        training_info = {
            "training_id": training_id,
            "model_id": model_id,
            "status": "initializing",
            "current_epoch": 0,
            "total_epochs": training_config.get("epochs", 100),
            "metrics": {
                "loss": 0.0,
                "accuracy": 0.0,
                "val_loss": 0.0,
                "val_accuracy": 0.0
            },
            "start_time": time.time(),
            "estimated_time_remaining": 0.0,
            "config": training_config,
            "dataset_config": dataset_config,
            "validation_config": validation_config
        }
        
        self.active_trainings[training_id] = training_info
        
        # Start training task
        task = asyncio.create_task(self._training_loop(training_id, model, training_config, dataset_config))
        self.training_tasks[training_id] = task
        
        logging.info(f"🚀 Started training {training_id} for model {model_id}")
        return training_id
    
    async def _training_loop(self, training_id: str, model: nn.Module,
                           training_config: Dict[str, Any], dataset_config: Dict[str, Any]):
        """Main training loop"""
        try:
            training_info = self.active_trainings[training_id]
            training_info["status"] = "running"
            
            # Setup training components
            optimizer = self._create_optimizer(model, training_config)
            criterion = self._create_criterion(training_config)
            train_loader = await self._create_dataloader(dataset_config, training_config)
            
            epochs = training_config.get("epochs", 100)
            device = self.gpu_manager.get_device()
            
            # Training loop
            for epoch in range(epochs):
                if training_info["status"] != "running":
                    break
                
                model.train()
                total_loss = 0.0
                correct = 0
                total = 0
                
                for batch_idx, (data, target) in enumerate(train_loader):
                    data, target = data.to(device), target.to(device)
                    
                    optimizer.zero_grad()
                    output = model(data)
                    loss = criterion(output, target)
                    loss.backward()
                    optimizer.step()
                    
                    total_loss += loss.item()
                    
                    # Calculate accuracy
                    pred = output.argmax(dim=1, keepdim=True)
                    correct += pred.eq(target.view_as(pred)).sum().item()
                    total += target.size(0)
                    
                    # Check for stop signal
                    if training_info["status"] != "running":
                        break
                
                # Update metrics
                avg_loss = total_loss / len(train_loader)
                accuracy = 100.0 * correct / total
                
                training_info["current_epoch"] = epoch + 1
                training_info["metrics"]["loss"] = avg_loss
                training_info["metrics"]["accuracy"] = accuracy
                
                # Estimate remaining time
                elapsed_time = time.time() - training_info["start_time"]
                time_per_epoch = elapsed_time / (epoch + 1)
                remaining_epochs = epochs - (epoch + 1)
                training_info["estimated_time_remaining"] = time_per_epoch * remaining_epochs
                
                # Broadcast progress
                await self._broadcast_training_progress(training_id)
                
                # Log progress
                if (epoch + 1) % 10 == 0:
                    logging.info(f"Training {training_id} - Epoch {epoch+1}/{epochs}, "
                               f"Loss: {avg_loss:.4f}, Accuracy: {accuracy:.2f}%")
                
                # Small delay to allow other operations
                await asyncio.sleep(0.01)
            
            # Training completed
            training_info["status"] = "completed"
            await self._broadcast_training_progress(training_id)
            logging.info(f"✅ Training {training_id} completed")
            
        except asyncio.CancelledError:
            training_info["status"] = "cancelled"
            logging.info(f"⏹️ Training {training_id} cancelled")
        except Exception as e:
            training_info["status"] = "failed"
            training_info["error"] = str(e)
            logging.error(f"❌ Training {training_id} failed: {e}")
        finally:
            # Cleanup
            if training_id in self.training_tasks:
                del self.training_tasks[training_id]
    
    def _create_optimizer(self, model: nn.Module, config: Dict[str, Any]) -> optim.Optimizer:
        """Create optimizer based on configuration"""
        optimizer_type = config.get("optimizer", "adam").lower()
        learning_rate = config.get("learning_rate", 0.001)
        weight_decay = config.get("weight_decay", 0.0)
        
        if optimizer_type == "adam":
            return optim.Adam(model.parameters(), lr=learning_rate, weight_decay=weight_decay)
        elif optimizer_type == "sgd":
            momentum = config.get("momentum", 0.9)
            return optim.SGD(model.parameters(), lr=learning_rate, momentum=momentum, weight_decay=weight_decay)
        elif optimizer_type == "rmsprop":
            return optim.RMSprop(model.parameters(), lr=learning_rate, weight_decay=weight_decay)
        elif optimizer_type == "adamw":
            return optim.AdamW(model.parameters(), lr=learning_rate, weight_decay=weight_decay)
        else:
            return optim.Adam(model.parameters(), lr=learning_rate, weight_decay=weight_decay)
    
    def _create_criterion(self, config: Dict[str, Any]) -> nn.Module:
        """Create loss criterion based on configuration"""
        loss_type = config.get("loss_function", "cross_entropy").lower()
        
        if loss_type == "cross_entropy":
            return nn.CrossEntropyLoss()
        elif loss_type == "mse":
            return nn.MSELoss()
        elif loss_type == "mae":
            return nn.L1Loss()
        elif loss_type == "bce":
            return nn.BCELoss()
        elif loss_type == "bce_with_logits":
            return nn.BCEWithLogitsLoss()
        else:
            return nn.CrossEntropyLoss()
    
    async def _create_dataloader(self, dataset_config: Dict[str, Any], 
                               training_config: Dict[str, Any]) -> DataLoader:
        """Create data loader from dataset configuration"""
        batch_size = training_config.get("batch_size", 32)
        
        # For now, create dummy data - extend this to load real datasets
        data_type = dataset_config.get("type", "dummy")
        
        if data_type == "dummy":
            # Create dummy classification data
            num_samples = dataset_config.get("num_samples", 1000)
            input_size = dataset_config.get("input_size", 784)
            num_classes = dataset_config.get("num_classes", 10)
            
            data = torch.randn(num_samples, input_size)
            targets = torch.randint(0, num_classes, (num_samples,))
            
            dataset = TensorDataset(data, targets)
            return DataLoader(dataset, batch_size=batch_size, shuffle=True)
        
        # Add support for real datasets here (CIFAR-10, MNIST, etc.)
        else:
            raise ValueError(f"Unsupported dataset type: {data_type}")
    
    async def _broadcast_training_progress(self, training_id: str):
        """Broadcast training progress to subscribers"""
        if training_id in self.subscribers:
            progress_data = {
                "type": "training_progress",
                "training_id": training_id,
                "data": self.active_trainings[training_id]
            }
            
            # Remove non-serializable data
            broadcast_data = progress_data["data"].copy()
            broadcast_data.pop("config", None)
            broadcast_data.pop("dataset_config", None)
            broadcast_data.pop("validation_config", None)
            
            # Send to all subscribers
            for websocket in self.subscribers[training_id]:
                try:
                    await websocket.send_json(progress_data)
                except:
                    # Remove failed connections
                    pass
    
    async def stop_training(self, training_id: str):
        """Stop training"""
        if training_id in self.active_trainings:
            self.active_trainings[training_id]["status"] = "stopping"
            
            if training_id in self.training_tasks:
                self.training_tasks[training_id].cancel()
                
        logging.info(f"⏹️ Stopped training {training_id}")
    
    async def pause_training(self, training_id: str):
        """Pause training"""
        if training_id in self.active_trainings:
            self.active_trainings[training_id]["status"] = "paused"
        logging.info(f"⏸️ Paused training {training_id}")
    
    async def resume_training(self, training_id: str):
        """Resume training"""
        if training_id in self.active_trainings:
            self.active_trainings[training_id]["status"] = "running"
        logging.info(f"▶️ Resumed training {training_id}")
    
    async def get_training_status(self, training_id: str) -> Dict[str, Any]:
        """Get training status"""
        if training_id not in self.active_trainings:
            raise ValueError(f"Training {training_id} not found")
        
        return self.active_trainings[training_id].copy()
    
    async def list_trainings(self, status_filter: Optional[str] = None) -> List[Dict[str, Any]]:
        """List all trainings"""
        trainings = []
        for training_info in self.active_trainings.values():
            if status_filter is None or training_info["status"] == status_filter:
                info = training_info.copy()
                # Remove large config objects
                info.pop("config", None)
                info.pop("dataset_config", None)
                info.pop("validation_config", None)
                trainings.append(info)
        return trainings
    
    async def get_training_metrics(self, training_id: str) -> Dict[str, Any]:
        """Get detailed training metrics"""
        if training_id not in self.active_trainings:
            raise ValueError(f"Training {training_id} not found")
        
        training_info = self.active_trainings[training_id]
        return {
            "training_id": training_id,
            "current_metrics": training_info["metrics"],
            "progress": training_info["current_epoch"] / training_info["total_epochs"],
            "elapsed_time": time.time() - training_info["start_time"],
            "estimated_remaining": training_info["estimated_time_remaining"],
            "status": training_info["status"]
        }
    
    async def subscribe_to_training(self, training_id: str, websocket):
        """Subscribe to training updates"""
        if training_id not in self.subscribers:
            self.subscribers[training_id] = []
        self.subscribers[training_id].append(websocket)
    
    async def unsubscribe_from_training(self, training_id: str, websocket):
        """Unsubscribe from training updates"""
        if training_id in self.subscribers:
            if websocket in self.subscribers[training_id]:
                self.subscribers[training_id].remove(websocket)
    
    def get_active_trainings(self) -> List[str]:
        """Get list of active training IDs"""
        return list(self.active_trainings.keys())
    
    def is_initialized(self) -> bool:
        """Check if training engine is initialized"""
        return self.initialized
    
    async def shutdown(self):
        """Shutdown training engine"""
        # Cancel all running trainings
        for training_id in list(self.training_tasks.keys()):
            await self.stop_training(training_id)
        
        # Wait for tasks to complete
        if self.training_tasks:
            await asyncio.gather(*self.training_tasks.values(), return_exceptions=True)
        
        logging.info("🛑 Training Engine shutdown complete")