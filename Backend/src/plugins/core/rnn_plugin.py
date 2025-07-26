import torch
import torch.nn as nn
from typing import Dict, List, Any
from ..plugin_manager import PluginBase

class RNNPlugin(PluginBase):
    """Plugin for Recurrent Neural Networks"""
    
    def __init__(self):
        self.name = "RNN Core Plugin"
        self.version = "1.0.0"
        self.initialized = False
    
    def get_manifest(self) -> Dict[str, Any]:
        """Return plugin manifest"""
        return {
            "id": "rnn_core",
            "name": self.name,
            "version": self.version,
            "description": "Recurrent Neural Network implementations (LSTM, GRU, RNN)",
            "author": "Archetype Core Team",
            "type": "neural_component",
            "category": "core",
            "dependencies": ["torch"],
            "neural_components": ["lstm_layer", "gru_layer", "rnn_layer"],
            "ui_components": ["RNNBuilder", "TemporalConfig"],
            "parameters": {
                "input_size": {"type": "int", "min": 1, "max": 10000, "default": 100},
                "hidden_size": {"type": "int", "min": 1, "max": 2048, "default": 128},
                "num_layers": {"type": "int", "min": 1, "max": 10, "default": 2},
                "output_size": {"type": "int", "min": 1, "max": 10000, "default": 10},
                "rnn_type": {"type": "enum", "values": ["LSTM", "GRU", "RNN"], "default": "LSTM"},
                "bidirectional": {"type": "bool", "default": False},
                "dropout": {"type": "float", "min": 0.0, "max": 0.9, "default": 0.0},
                "batch_first": {"type": "bool", "default": True}
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
            print(f"Failed to initialize RNN plugin: {e}")
            return False
    
    async def cleanup(self):
        """Cleanup plugin resources"""
        self.initialized = False
    
    def get_neural_component_types(self) -> List[str]:
        """Return list of neural component types this plugin provides"""
        return ["lstm_layer", "gru_layer", "rnn_layer"]
    
    async def create_component(self, component_type: str, config: Dict[str, Any]) -> Any:
        """Create a neural component of specified type"""
        if not self.initialized:
            raise RuntimeError("Plugin not initialized")
        
        if component_type in ["lstm_layer", "gru_layer", "rnn_layer"]:
            return self._create_rnn_layer(config)
        else:
            raise ValueError(f"Unknown component type: {component_type}")
    
    def _create_rnn_layer(self, config: Dict[str, Any]) -> nn.Module:
        """Create RNN layer"""
        input_size = config.get("input_size", 100)
        hidden_size = config.get("hidden_size", 128)
        num_layers = config.get("num_layers", 2)
        output_size = config.get("output_size", 10)
        rnn_type = config.get("rnn_type", "LSTM")
        bidirectional = config.get("bidirectional", False)
        dropout = config.get("dropout", 0.0)
        batch_first = config.get("batch_first", True)
        
        class RNNModel(nn.Module):
            def __init__(self):
                super().__init__()
                
                if rnn_type == "LSTM":
                    self.rnn = nn.LSTM(
                        input_size, hidden_size, num_layers,
                        batch_first=batch_first, dropout=dropout,
                        bidirectional=bidirectional
                    )
                elif rnn_type == "GRU":
                    self.rnn = nn.GRU(
                        input_size, hidden_size, num_layers,
                        batch_first=batch_first, dropout=dropout,
                        bidirectional=bidirectional
                    )
                else:  # RNN
                    self.rnn = nn.RNN(
                        input_size, hidden_size, num_layers,
                        batch_first=batch_first, dropout=dropout,
                        bidirectional=bidirectional
                    )
                
                # Output layer
                fc_input_size = hidden_size * (2 if bidirectional else 1)
                self.fc = nn.Linear(fc_input_size, output_size)
            
            def forward(self, x):
                output, _ = self.rnn(x)
                # Use last output for classification
                last_output = output[:, -1, :] if batch_first else output[-1, :, :]
                return self.fc(last_output)
        
        return RNNModel()