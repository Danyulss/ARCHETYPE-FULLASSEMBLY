import torch
import torch.nn as nn
from typing import Dict, List, Any
from ..plugin_manager import PluginBase

class CNNPlugin(PluginBase):
    """Plugin for Convolutional Neural Networks"""
    
    def __init__(self):
        self.name = "CNN Core Plugin"
        self.version = "1.0.0"
        self.initialized = False
    
    def get_manifest(self) -> Dict[str, Any]:
        """Return plugin manifest"""
        return {
            "id": "cnn_core",
            "name": self.name,
            "version": self.version,
            "description": "Convolutional Neural Network implementations",
            "author": "Archetype Core Team",
            "type": "neural_component",
            "category": "core",
            "dependencies": ["torch"],
            "neural_components": ["conv2d_layer", "cnn_block", "resnet_block"],
            "ui_components": ["CNNBuilder", "ConvConfig"],
            "parameters": {
                "input_channels": {"type": "int", "min": 1, "max": 512, "default": 3},
                "num_classes": {"type": "int", "min": 1, "max": 10000, "default": 10},
                "conv_layers": {"type": "list", "default": [32, 64, 128]},
                "kernel_sizes": {"type": "list", "default": [3, 3, 3]},
                "strides": {"type": "list", "default": [1, 1, 1]},
                "fc_layers": {"type": "list", "default": [512, 256]},
                "dropout": {"type": "float", "min": 0.0, "max": 0.9, "default": 0.5},
                "batch_norm": {"type": "bool", "default": True},
                "pooling": {"type": "enum", "values": ["max", "avg", "adaptive"], "default": "max"}
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
            print(f"Failed to initialize CNN plugin: {e}")
            return False
    
    async def cleanup(self):
        """Cleanup plugin resources"""
        self.initialized = False
    
    def get_neural_component_types(self) -> List[str]:
        """Return list of neural component types this plugin provides"""
        return ["conv2d_layer", "cnn_block", "resnet_block"]
    
    def _create_cnn_layer(self, config: Dict[str, Any]) -> nn.Module:
        """Create a convolutional neural network layer"""
        return nn.Conv2d(
            in_channels=config["input_channels"],
            out_channels=config["output_channels"],
            kernel_size=config["kernel_size"],
            stride=config["stride"],
            padding=config["padding"]
        )
    
    def _create_resnet_block(self, config: Dict[str, Any]) -> nn.Module:
        """Create a ResNet block"""
        return nn.Sequential(
            nn.Conv2d(
                in_channels=config["input_channels"],
                out_channels=config["output_channels"],
                kernel_size=config["kernel_size"],
                stride=config["stride"],
                padding=config["padding"]
            ),
            nn.BatchNorm2d(config["output_channels"]),
            nn.ReLU(),
            nn.Conv2d(
                in_channels=config["output_channels"],
                out_channels=config["output_channels"],
                kernel_size=config["kernel_size"],
                stride=config["stride"],
                padding=config["padding"]
            ),
            nn.BatchNorm2d(config["output_channels"]),
            nn.ReLU()
        )
    
    async def create_component(self, component_type: str, config: Dict[str, Any]) -> Any:
        """Create a neural component of specified type"""
        if not self.initialized:
            raise RuntimeError("Plugin not initialized")
        
        if component_type in ["conv2d_layer", "cnn_block"]:
            return self._create_cnn_layer(config)
        elif component_type == "resnet_block":
            return self._create_resnet_block(config)
        else:
            raise