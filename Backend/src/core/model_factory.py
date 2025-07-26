import torch
import torch.nn as nn
import logging
import uuid
import json
from typing import Dict, List, Any, Optional
from datetime import datetime
from pathlib import Path

class ModelFactory:
    """Factory for creating and managing neural network models"""
    
    def __init__(self, gpu_manager):
        self.gpu_manager = gpu_manager
        self.models: Dict[str, Dict[str, Any]] = {}
        self.model_storage_path = Path("models")
        self.model_storage_path.mkdir(exist_ok=True)
        self.initialized = False
    
    async def initialize(self):
        """Initialize model factory"""
        logging.info("🏭 Initializing Model Factory...")
        self.initialized = True
        return True
    
    async def create_model(self, name: str, model_type: str, 
                          architecture: Dict[str, Any], 
                          hyperparameters: Dict[str, Any]) -> str:
        """Create a new neural network model"""
        model_id = str(uuid.uuid4())
        
        # Create model based on type
        if model_type == "mlp":
            model = self._create_mlp(architecture, hyperparameters)
        elif model_type == "rnn":
            model = self._create_rnn(architecture, hyperparameters)
        elif model_type == "cnn":
            model = self._create_cnn(architecture, hyperparameters)
        else:
            raise ValueError(f"Unsupported model type: {model_type}")
        
        # Move to device
        model = model.to(self.gpu_manager.get_device())
        
        # Calculate parameter count
        param_count = sum(p.numel() for p in model.parameters())
        
        # Store model info
        model_info = {
            "id": model_id,
            "name": name,
            "model_type": model_type,
            "architecture": architecture,
            "hyperparameters": hyperparameters,
            "parameter_count": param_count,
            "created_at": datetime.now().isoformat(),
            "status": "created",
            "model": model
        }
        
        self.models[model_id] = model_info
        
        # Save model metadata
        await self._save_model_metadata(model_id, model_info)
        
        logging.info(f"🧠 Created {model_type} model '{name}' with {param_count:,} parameters")
        return model_id
    
    def _create_mlp(self, architecture: Dict[str, Any], hyperparameters: Dict[str, Any]) -> nn.Module:
        """Create Multi-Layer Perceptron"""
        layers = architecture.get("layers", [784, 128, 64, 10])
        activation = hyperparameters.get("activation", "relu")
        dropout_rate = hyperparameters.get("dropout", 0.0)
        
        activation_fn = {
            "relu": nn.ReLU(),
            "tanh": nn.Tanh(),
            "sigmoid": nn.Sigmoid(),
            "leaky_relu": nn.LeakyReLU(),
            "gelu": nn.GELU()
        }.get(activation, nn.ReLU())
        
        model_layers = []
        for i in range(len(layers) - 1):
            model_layers.append(nn.Linear(layers[i], layers[i + 1]))
            if i < len(layers) - 2:  # Don't add activation after last layer
                model_layers.append(activation_fn)
                if dropout_rate > 0:
                    model_layers.append(nn.Dropout(dropout_rate))
        
        return nn.Sequential(*model_layers)
    
    def _create_rnn(self, architecture: Dict[str, Any], hyperparameters: Dict[str, Any]) -> nn.Module:
        """Create Recurrent Neural Network"""
        input_size = architecture.get("input_size", 100)
        hidden_size = architecture.get("hidden_size", 128)
        num_layers = architecture.get("num_layers", 2)
        output_size = architecture.get("output_size", 10)
        rnn_type = architecture.get("rnn_type", "LSTM")
        bidirectional = architecture.get("bidirectional", False)
        dropout = hyperparameters.get("dropout", 0.0)
        
        class RNNModel(nn.Module):
            def __init__(self):
                super().__init__()
                if rnn_type == "LSTM":
                    self.rnn = nn.LSTM(input_size, hidden_size, num_layers, 
                                     batch_first=True, dropout=dropout, 
                                     bidirectional=bidirectional)
                elif rnn_type == "GRU":
                    self.rnn = nn.GRU(input_size, hidden_size, num_layers,
                                    batch_first=True, dropout=dropout,
                                    bidirectional=bidirectional)
                else:
                    self.rnn = nn.RNN(input_size, hidden_size, num_layers,
                                    batch_first=True, dropout=dropout,
                                    bidirectional=bidirectional)
                
                fc_input_size = hidden_size * (2 if bidirectional else 1)
                self.fc = nn.Linear(fc_input_size, output_size)
            
            def forward(self, x):
                output, _ = self.rnn(x)
                return self.fc(output[:, -1, :])  # Use last output
        
        return RNNModel()
    
    def _create_cnn(self, architecture: Dict[str, Any], hyperparameters: Dict[str, Any]) -> nn.Module:
        """Create Convolutional Neural Network"""
        input_channels = architecture.get("input_channels", 3)
        num_classes = architecture.get("num_classes", 10)
        conv_layers = architecture.get("conv_layers", [32, 64, 128])
        kernel_sizes = architecture.get("kernel_sizes", [3, 3, 3])
        fc_layers = architecture.get("fc_layers", [512, 256])
        
        class CNNModel(nn.Module):
            def __init__(self):
                super().__init__()
                
                # Convolutional layers
                conv_blocks = []
                in_channels = input_channels
                
                for i, (out_channels, kernel_size) in enumerate(zip(conv_layers, kernel_sizes)):
                    conv_blocks.extend([
                        nn.Conv2d(in_channels, out_channels, kernel_size, padding=1),
                        nn.BatchNorm2d(out_channels),
                        nn.ReLU(inplace=True),
                        nn.MaxPool2d(2)
                    ])
                    in_channels = out_channels
                
                self.conv_layers = nn.Sequential(*conv_blocks)
                
                # Calculate flattened size (assuming 32x32 input)
                dummy_input = torch.zeros(1, input_channels, 32, 32)
                with torch.no_grad():
                    conv_output = self.conv_layers(dummy_input)
                    flattened_size = conv_output.view(1, -1).size(1)
                
                # Fully connected layers
                fc_blocks = []
                in_features = flattened_size
                
                for fc_size in fc_layers:
                    fc_blocks.extend([
                        nn.Linear(in_features, fc_size),
                        nn.ReLU(inplace=True),
                        nn.Dropout(0.5)
                    ])
                    in_features = fc_size
                
                fc_blocks.append(nn.Linear(in_features, num_classes))
                self.fc_layers = nn.Sequential(*fc_blocks)
            
            def forward(self, x):
                x = self.conv_layers(x)
                x = x.view(x.size(0), -1)
                return self.fc_layers(x)
        
        return CNNModel()
    
    async def get_model_info(self, model_id: str) -> Dict[str, Any]:
        """Get model information"""
        if model_id not in self.models:
            raise ValueError(f"Model {model_id} not found")
        
        info = self.models[model_id].copy()
        # Remove the actual model object for serialization
        info.pop("model", None)
        return info
    
    async def list_models(self, skip: int = 0, limit: int = 100) -> List[Dict[str, Any]]:
        """List all models"""
        models = []
        for model_info in list(self.models.values())[skip:skip + limit]:
            info = model_info.copy()
            info.pop("model", None)  # Remove model object
            models.append(info)
        return models
    
    async def count_models(self) -> int:
        """Count total models"""
        return len(self.models)
    
    async def update_model(self, model_id: str, updates: Dict[str, Any]):
        """Update model parameters"""
        if model_id not in self.models:
            raise ValueError(f"Model {model_id} not found")
        
        self.models[model_id].update(updates)
        await self._save_model_metadata(model_id, self.models[model_id])
    
    async def delete_model(self, model_id: str):
        """Delete a model"""
        if model_id not in self.models:
            raise ValueError(f"Model {model_id} not found")
        
        # Remove from memory
        del self.models[model_id]
        
        # Remove from disk
        model_path = self.model_storage_path / f"{model_id}.json"
        if model_path.exists():
            model_path.unlink()
        
        logging.info(f"🗑️ Deleted model {model_id}")
    
    async def export_model(self, model_id: str, format: str) -> str:
        """Export model in specified format"""
        if model_id not in self.models:
            raise ValueError(f"Model {model_id} not found")
        
        model_info = self.models[model_id]
        model = model_info["model"]
        
        export_path = self.model_storage_path / f"{model_id}.{format}"
        
        if format == "pt" or format == "pth":
            torch.save(model.state_dict(), export_path)
        elif format == "onnx":
            # Export to ONNX (requires example input)
            dummy_input = self._get_dummy_input(model_info)
            torch.onnx.export(model, tuple(dummy_input), export_path)
        else:
            raise ValueError(f"Unsupported export format: {format}")
        
        logging.info(f"📤 Exported model {model_id} to {export_path}")
        return str(export_path)
    
    def _get_dummy_input(self, model_info: Dict[str, Any]) -> torch.Tensor:
        """Get dummy input for model export"""
        model_type = model_info["model_type"]
        architecture = model_info["architecture"]
        
        if model_type == "mlp":
            input_size = architecture["layers"][0]
            return torch.randn(1, input_size)
        elif model_type == "rnn":
            input_size = architecture["input_size"]
            seq_length = 10  # Default sequence length
            return torch.randn(1, seq_length, input_size)
        elif model_type == "cnn":
            channels = architecture["input_channels"]
            return torch.randn(1, channels, 32, 32)  # Default 32x32 image
        
        return torch.randn(1, 100)  # Fallback
    
    async def _save_model_metadata(self, model_id: str, model_info: Dict[str, Any]):
        """Save model metadata to disk"""
        metadata = model_info.copy()
        metadata.pop("model", None)  # Remove model object
        
        metadata_path = self.model_storage_path / f"{model_id}.json"
        with open(metadata_path, 'w') as f:
            json.dump(metadata, f, indent=2)
    
    def get_model(self, model_id: str) -> nn.Module:
        """Get actual model object"""
        if model_id not in self.models:
            raise ValueError(f"Model {model_id} not found")
        return self.models[model_id]["model"]
    
    def get_active_models(self) -> List[str]:
        """Get list of active model IDs"""
        return list(self.models.keys())
    
    def is_initialized(self) -> bool:
        """Check if model factory is initialized"""
        return self.initialized