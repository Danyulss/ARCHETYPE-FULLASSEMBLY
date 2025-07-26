import importlib
import importlib.util
import logging
import json
import asyncio
import torch
import torch.nn as nn
from typing import Dict, List, Any, Optional, Type
from pathlib import Path
from abc import ABC, abstractmethod

class PluginBase(ABC):
    """Base class for all plugins"""
    
    @abstractmethod
    def get_manifest(self) -> Dict[str, Any]:
        """Return plugin manifest"""
        pass
    
    @abstractmethod
    async def initialize(self) -> bool:
        """Initialize plugin"""
        pass
    
    @abstractmethod
    async def cleanup(self):
        """Cleanup plugin resources"""
        pass
    
    @abstractmethod
    def get_neural_component_types(self) -> List[str]:
        """Return list of neural component types this plugin provides"""
        pass
    
    @abstractmethod
    async def create_component(self, component_type: str, config: Dict[str, Any]) -> Any:
        """Create a neural component of specified type"""
        pass

class PluginManager:
    """Manages plugin loading, unloading, and lifecycle"""
    
    def __init__(self, plugin_directory: Path):
        self.plugin_directory = Path(plugin_directory)
        self.plugin_directory.mkdir(exist_ok=True)
        
        self.loaded_plugins: Dict[str, PluginBase] = {}
        self.plugin_manifests: Dict[str, Dict[str, Any]] = {}
        self.enabled_plugins: set = set()
        
        # Core plugin directory
        self.core_plugin_directory = self.plugin_directory / "core"
        self.core_plugin_directory.mkdir(exist_ok=True)
        
        # Extension plugin directory
        self.extension_plugin_directory = self.plugin_directory / "extensions"
        self.extension_plugin_directory.mkdir(exist_ok=True)
    
    async def load_core_plugins(self):
        """Load core plugins that ship with Archetype"""
        logging.info("🔌 Loading core plugins...")
        
        # Load MLP plugin
        await self._load_plugin_from_module("mlp_core", "src.plugins.core.mlp_plugin", "MLPPlugin")
        
        # Load RNN plugin  
        await self._load_plugin_from_module("rnn_core", "src.plugins.core.rnn_plugin", "RNNPlugin")
        
        # Load CNN plugin
        await self._load_plugin_from_module("cnn_core", "src.plugins.core.cnn_plugin", "CNNPlugin")
        
        logging.info(f"✅ Loaded {len(self.loaded_plugins)} core plugins")
    
    async def _load_plugin_from_module(self, plugin_id: str, module_path: str, class_name: str):
        """Load plugin from Python module"""
        try:
            # Import module
            module = importlib.import_module(module_path)
            
            # Get plugin class
            plugin_class = getattr(module, class_name)
            
            # Create plugin instance
            plugin_instance = plugin_class()
            
            # Initialize plugin
            if await plugin_instance.initialize():
                self.loaded_plugins[plugin_id] = plugin_instance
                self.plugin_manifests[plugin_id] = plugin_instance.get_manifest()
                self.enabled_plugins.add(plugin_id)
                
                logging.info(f"✅ Loaded plugin: {plugin_id}")
            else:
                logging.error(f"❌ Failed to initialize plugin: {plugin_id}")
                
        except Exception as e:
            logging.error(f"❌ Failed to load plugin {plugin_id}: {e}")
    
    async def load_plugin_from_file(self, plugin_path: Path) -> str:
        """Load plugin from file"""
        if not plugin_path.exists():
            raise ValueError(f"Plugin file not found: {plugin_path}")
        
        # Load manifest
        manifest_path = plugin_path / "manifest.json"
        if not manifest_path.exists():
            raise ValueError(f"Plugin manifest not found: {manifest_path}")
        
        with open(manifest_path, 'r') as f:
            manifest = json.load(f)
        
        plugin_id = manifest["id"]
        
        # Load Python module
        main_module = manifest.get("main_module", "plugin.py")
        module_path = plugin_path / main_module
        
        if not module_path.exists():
            raise ValueError(f"Plugin main module not found: {module_path}")
        
        # Import module dynamically
        spec = importlib.util.spec_from_file_location(plugin_id, module_path)

        if spec is not None:
            module = importlib.util.module_from_spec(spec)

            if spec.loader is not None:
                spec.loader.exec_module(module)
        
        # Get plugin class
        plugin_class_name = manifest.get("plugin_class", "Plugin")
        plugin_class = getattr(module, plugin_class_name)
        
        # Create and initialize plugin
        plugin_instance = plugin_class()
        if await plugin_instance.initialize():
            self.loaded_plugins[plugin_id] = plugin_instance
            self.plugin_manifests[plugin_id] = manifest
            
            logging.info(f"✅ Loaded external plugin: {plugin_id}")
            return plugin_id
        else:
            raise ValueError(f"Unknown component type") # TODO: {component_type}) work on fix later
    
    def _create_cnn_layer(self, config: Dict[str, Any]) -> nn.Module:
        """Create CNN layer"""
        input_channels = config.get("input_channels", 3)
        num_classes = config.get("num_classes", 10)
        conv_layers = config.get("conv_layers", [32, 64, 128])
        kernel_sizes = config.get("kernel_sizes", [3, 3, 3])
        strides = config.get("strides", [1, 1, 1])
        fc_layers = config.get("fc_layers", [512, 256])
        dropout = config.get("dropout", 0.5)
        batch_norm = config.get("batch_norm", True)
        pooling = config.get("pooling", "max")
        
        class CNNModel(nn.Module):
            def __init__(self):
                super().__init__()
                
                # Convolutional layers
                conv_blocks = []
                in_channels = input_channels
                
                for i, (out_channels, kernel_size, stride) in enumerate(zip(conv_layers, kernel_sizes, strides)):
                    conv_blocks.append(nn.Conv2d(in_channels, out_channels, kernel_size, stride, padding=1))
                    
                    if batch_norm:
                        conv_blocks.append(nn.BatchNorm2d(out_channels))
                    
                    conv_blocks.append(nn.ReLU(inplace=True))
                    
                    if pooling == "max":
                        conv_blocks.append(nn.MaxPool2d(2))
                    elif pooling == "avg":
                        conv_blocks.append(nn.AvgPool2d(2))
                    
                    in_channels = out_channels
                
                self.conv_layers = nn.Sequential(*conv_blocks)
                
                # Calculate flattened size
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
                        nn.Dropout(dropout)
                    ])
                    in_features = fc_size
                
                fc_blocks.append(nn.Linear(in_features, num_classes))
                self.fc_layers = nn.Sequential(*fc_blocks)
            
            def forward(self, x):
                x = self.conv_layers(x)
                x = x.view(x.size(0), -1)
                return self.fc_layers(x)
        
        return CNNModel()
    
    def _create_resnet_block(self, config: Dict[str, Any]) -> nn.Module:
        """Create ResNet block"""
        in_channels = config.get("in_channels", 64)
        out_channels = config.get("out_channels", 64)
        stride = config.get("stride", 1)
        
        class ResNetBlock(nn.Module):
            def __init__(self):
                super().__init__()
                self.conv1 = nn.Conv2d(in_channels, out_channels, 3, stride, padding=1, bias=False)
                self.bn1 = nn.BatchNorm2d(out_channels)
                self.conv2 = nn.Conv2d(out_channels, out_channels, 3, 1, padding=1, bias=False)
                self.bn2 = nn.BatchNorm2d(out_channels)
                
                self.shortcut = nn.Sequential()
                if stride != 1 or in_channels != out_channels:
                    self.shortcut = nn.Sequential(
                        nn.Conv2d(in_channels, out_channels, 1, stride, bias=False),
                        nn.BatchNorm2d(out_channels)
                    )
            
            def forward(self, x):
                out = torch.relu(self.bn1(self.conv1(x)))
                out = self.bn2(self.conv2(out))
                out += self.shortcut(x)
                return torch.relu(out)
        
        return ResNetBlock() 
        RuntimeError(f"Failed to initialize plugin: {plugin_id}")
    
    async def unload_plugin(self, plugin_id: str):
        """Unload a plugin"""
        if plugin_id in self.loaded_plugins:
            await self.loaded_plugins[plugin_id].cleanup()
            del self.loaded_plugins[plugin_id]
            del self.plugin_manifests[plugin_id]
            self.enabled_plugins.discard(plugin_id)
            
            logging.info(f"🔌 Unloaded plugin: {plugin_id}")
    
    async def enable_plugin(self, plugin_id: str):
        """Enable a plugin"""
        if plugin_id in self.loaded_plugins:
            self.enabled_plugins.add(plugin_id)
            logging.info(f"✅ Enabled plugin: {plugin_id}")
    
    async def disable_plugin(self, plugin_id: str):
        """Disable a plugin"""
        if plugin_id in self.loaded_plugins:
            self.enabled_plugins.discard(plugin_id)
            logging.info(f"⏸️ Disabled plugin: {plugin_id}")
    
    async def list_plugins(self) -> List[Dict[str, Any]]:
        """List all plugins"""
        plugins = []
        
        for plugin_id, manifest in self.plugin_manifests.items():
            plugin_info = {
                "id": plugin_id,
                "name": manifest.get("name", plugin_id),
                "version": manifest.get("version", "1.0.0"),
                "description": manifest.get("description", ""),
                "author": manifest.get("author", "Unknown"),
                "plugin_type": manifest.get("type", "neural_component"),
                "loaded": plugin_id in self.loaded_plugins,
                "enabled": plugin_id in self.enabled_plugins,
                "dependencies": manifest.get("dependencies", []),
                "manifest": manifest
            }
            plugins.append(plugin_info)
        
        return plugins
    
    async def get_plugin_info(self, plugin_id: str) -> Dict[str, Any]:
        """Get specific plugin information"""
        if plugin_id not in self.plugin_manifests:
            raise ValueError(f"Plugin {plugin_id} not found")
        
        manifest = self.plugin_manifests[plugin_id]
        return {
            "id": plugin_id,
            "name": manifest.get("name", plugin_id),
            "version": manifest.get("version", "1.0.0"),
            "description": manifest.get("description", ""),
            "author": manifest.get("author", "Unknown"),
            "plugin_type": manifest.get("type", "neural_component"),
            "loaded": plugin_id in self.loaded_plugins,
            "enabled": plugin_id in self.enabled_plugins,
            "dependencies": manifest.get("dependencies", []),
            "neural_components": self.loaded_plugins[plugin_id].get_neural_component_types() if plugin_id in self.loaded_plugins else [],
            "manifest": manifest
        }
    
    async def get_plugin_categories(self) -> List[str]:
        """Get available plugin categories"""
        categories = set()
        for manifest in self.plugin_manifests.values():
            category = manifest.get("category", "general")
            categories.add(category)
        return list(categories)
    
    async def create_neural_component(self, plugin_id: str, component_type: str, config: Dict[str, Any]) -> Any:
        """Create neural component using plugin"""
        if plugin_id not in self.loaded_plugins:
            raise ValueError(f"Plugin {plugin_id} not loaded")
        
        if plugin_id not in self.enabled_plugins:
            raise ValueError(f"Plugin {plugin_id} not enabled")
        
        plugin = self.loaded_plugins[plugin_id]
        return await plugin.create_component(component_type, config)
    
    def get_loaded_plugins(self) -> List[str]:
        """Get list of loaded plugin IDs"""
        return list(self.loaded_plugins.keys())
    
    def get_core_plugins(self) -> List[str]:
        """Get list of core plugin IDs"""
        return [pid for pid in self.loaded_plugins.keys() if pid.endswith("_core")]
    
    async def unload_all_plugins(self):
        """Unload all plugins"""
        for plugin_id in list(self.loaded_plugins.keys()):
            await self.unload_plugin(plugin_id)
        
        logging.info("🔌 All plugins unloaded")