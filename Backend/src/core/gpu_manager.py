import torch
import logging
import asyncio
from typing import Dict, List, Optional, Any
import psutil
import time

class GPUManager:
    """Manages GPU resources and CUDA operations"""
    
    def __init__(self):
        self.device = None
        self.cuda_available = torch.cuda.is_available()
        self.device_count = torch.cuda.device_count() if self.cuda_available else 0
        self.current_device_id = 0
        self.initialized = False
        
    async def initialize(self):
        """Initialize GPU manager"""
        logging.info("🔧 Initializing GPU Manager...")
        
        if not self.cuda_available:
            logging.warning("⚠️ CUDA not available - using CPU")
            self.device = torch.device("cpu")
        else:
            # Select best GPU
            best_gpu = await self._select_best_gpu()
            self.current_device_id = best_gpu
            self.device = torch.device(f"cuda:{best_gpu}")
            torch.cuda.set_device(self.device)
            
            # Clear cache and set memory allocation
            torch.cuda.empty_cache()
            if hasattr(torch.cuda, 'set_per_process_memory_fraction'):
                torch.cuda.set_per_process_memory_fraction(0.8)
            
            logging.info(f"🎯 Selected GPU {best_gpu}: {torch.cuda.get_device_name(best_gpu)}")
        
        self.initialized = True
        return True
    
    async def _select_best_gpu(self) -> int:
        """Select the best available GPU"""
        if self.device_count == 0:
            return 0
        
        best_gpu = 0
        best_memory = 0
        
        for i in range(self.device_count):
            props = torch.cuda.get_device_properties(i)
            total_memory = props.total_memory
            
            # Get current memory usage
            torch.cuda.set_device(i)
            allocated = torch.cuda.memory_allocated(i)
            available = total_memory - allocated
            
            if available > best_memory:
                best_memory = available
                best_gpu = i
                
        return best_gpu
    
    async def get_gpu_info(self) -> Dict[str, Any]:
        """Get comprehensive GPU information"""
        if not self.cuda_available:
            return {
                "cuda_available": False,
                "gpus": [],
                "selected_gpu": -1,
                "total_devices": 0
            }
        
        gpus = []
        for i in range(self.device_count):
            props = torch.cuda.get_device_properties(i)
            
            # Get memory info
            torch.cuda.set_device(i)
            total_memory = props.total_memory
            allocated = torch.cuda.memory_allocated(i)
            cached = torch.cuda.memory_reserved(i)
            
            # Get utilization (simplified)
            utilization = (allocated / total_memory) * 100 if total_memory > 0 else 0
            
            gpu_info = {
                "device_id": i,
                "name": props.name,
                "compute_capability": f"{props.major}.{props.minor}",
                "total_memory": total_memory,
                "allocated_memory": allocated,
                "cached_memory": cached,
                "available_memory": total_memory - allocated,
                "utilization": utilization,
                "multiprocessor_count": props.multi_processor_count,
                "max_threads_per_multiprocessor": props.max_threads_per_multi_processor,
                "temperature": await self._get_gpu_temperature(i),
                "power_usage": await self._get_gpu_power(i)
            }
            gpus.append(gpu_info)
        
        return {
            "cuda_available": True,
            "gpus": gpus,
            "selected_gpu": self.current_device_id,
            "total_devices": self.device_count
        }
    
    async def _get_gpu_temperature(self, device_id: int) -> Optional[float]:
        """Get GPU temperature (requires nvidia-ml-py or similar)"""
        try:
            # This is a placeholder - implement with nvidia-ml-py if needed
            return None
        except:
            return None
    
    async def _get_gpu_power(self, device_id: int) -> Optional[float]:
        """Get GPU power usage (requires nvidia-ml-py or similar)"""
        try:
            # This is a placeholder - implement with nvidia-ml-py if needed
            return None
        except:
            return None
    
    async def set_device(self, device_id: int):
        """Set active GPU device"""
        if not self.cuda_available:
            raise ValueError("CUDA not available")
        
        if device_id >= self.device_count:
            raise ValueError(f"Device {device_id} not available")
        
        self.current_device_id = device_id
        self.device = torch.device(f"cuda:{device_id}")
        torch.cuda.set_device(self.device)
        
        logging.info(f"🎯 Switched to GPU {device_id}: {torch.cuda.get_device_name(device_id)}")
    
    async def clear_cache(self):
        """Clear GPU memory cache"""
        if self.cuda_available:
            torch.cuda.empty_cache()
            logging.info("🧹 GPU cache cleared")
    
    async def run_benchmark(self) -> Dict[str, Any]:
        """Run GPU benchmark"""
        if not self.cuda_available:
            return {"error": "CUDA not available"}
        
        logging.info("🏃 Running GPU benchmark...")
        
        # Simple matrix multiplication benchmark
        size = 4096
        iterations = 10
        
        start_time = time.time()
        
        for _ in range(iterations):
            a = torch.randn(size, size, device=self.device)
            b = torch.randn(size, size, device=self.device)
            c = torch.matmul(a, b)
            torch.cuda.synchronize()
        
        end_time = time.time()
        total_time = end_time - start_time
        avg_time = total_time / iterations
        
        # Calculate GFLOPS
        operations = 2 * size * size * size  # Matrix multiplication operations
        gflops = (operations / avg_time) / 1e9
        
        return {
            "device": str(self.device),
            "matrix_size": size,
            "iterations": iterations,
            "total_time": total_time,
            "average_time": avg_time,
            "gflops": gflops,
            "memory_allocated": torch.cuda.memory_allocated() if self.cuda_available else 0,
            "memory_cached": torch.cuda.memory_reserved() if self.cuda_available else 0
        }
    
    def get_device(self) -> torch.device | None:
        """Get current device"""
        return self.device
    
    def get_device_info(self) -> str:
        """Get device info string"""
        if not self.cuda_available:
            return "CPU"
        return f"GPU {self.current_device_id}: {torch.cuda.get_device_name(self.current_device_id)}"
    
    def is_initialized(self) -> bool:
        """Check if GPU manager is initialized"""
        return self.initialized
    
    async def cleanup(self):
        """Cleanup GPU resources"""
        if self.cuda_available:
            torch.cuda.empty_cache()
        logging.info("🧹 GPU Manager cleanup complete")