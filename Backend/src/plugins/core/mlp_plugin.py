import torch
import torch.nn as nn
from typing import Dict, List, Any
from ..plugin_manager import PluginBase

class MLPPlugin(PluginBase):
    """Plugin for Multi-Layer Perceptron neural networks"""
    
    def __init__(self):
        self.name = "MLP Core Plugin"
        self.version = "1.0.0"
        self.initialized = False
    
    def get_manifest(self) -> Dict[str, Any]:
        """Return plugin manifest"""
        return {
            "id": "mlp_core",
            "name": self.name,
            "version": self.version,
            "description": "Multi-Layer Perceptron neural network implementation",
            "author": "Archetype Core Team",
            "type": "neural_component",
            "category": "core",
            "dependencies": ["torch"],
            "neural_components": ["mlp_layer", "dense_layer", "linear_layer"],
            "ui_components": ["MLPBuilder", "LayerConfig"],
            "parameters": {
                "input_size": {"type": "int", "min": 1, "max": 10000, "default": 784},
                "output_size": {"type": "int", "min": 1, "max": 10000, "default": 10},
                "hidden_layers": {"type": "list", "default": [128, 64]},
                "activation": {"type": "enum", "values": ["relu", "tanh", "sigmoid", "leaky_relu", "gelu"], "default": "relu"},
                "dropout": {"type": "float", "min": 0.0, "max": 0.9, "default": 0.0},
                "bias": {"type": "bool", "default": True}
            }
        }
    
    async def initialize(self) -> bool:
        """Initialize plugin"""
        try:
            # Verify torch is available
            torch.zeros(1)
            self.initialized = True
            return True
        except Exception as e:
            print(f"Failed to initialize MLP plugin: {e}")
            return False
    
    async def cleanup(self):
        """Cleanup plugin resources"""
        self.initialized = False
    
    def get_neural_component_types(self) -> List[str]:
        """Return list of neural component types this plugin provides"""
        return ["mlp_layer", "dense_layer", "linear_layer"]
    
    async def create_component(self, component_type: str, config: Dict[str, Any]) -> Any:
        """Create a neural component of specified type"""
        if not self.initialized:
            raise RuntimeError("Plugin not initialized")
        
        if component_type in ["mlp_layer", "dense_layer", "linear_layer"]:
            return self._create_mlp_layer(config)
        else:
            raise ValueError(f"Unknown component type: {component_type}")
    
    def _create_mlp_layer(self, config: Dict[str, Any]) -> nn.Module:
        """Create MLP layer"""
        input_size = config.get("input_size", 784)
        output_size = config.get("output_size", 10)
        hidden_layers = config.get("hidden_layers", [128, 64])
        activation = config.get("activation", "relu")
        dropout = config.get("dropout", 0.0)
        bias = config.get("bias", True)
        
        # Create activation function
        activation_fn = {
            "relu": nn.ReLU(),
            "tanh": nn.Tanh(),
            "sigmoid": nn.Sigmoid(),
            "leaky_relu": nn.LeakyReLU(),
            "gelu": nn.GELU()
        }.get(activation, nn.ReLU())
        
        # Build layers
        layers = []
        layer_sizes = [input_size] + hidden_layers + [output_size]
        
        for i in range(len(layer_sizes) - 1):
            layers.append(nn.Linear(layer_sizes[i], layer_sizes[i + 1], bias=bias))
            
            # Add activation and dropout for hidden layers
            if i < len(layer_sizes) - 2:
                layers.append(activation_fn)
                if dropout > 0:
                    layers.append(nn.Dropout(dropout))
        
        return nn.Sequential(*layers)