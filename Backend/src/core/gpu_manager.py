import torch
import logging
import asyncio
import platform
import subprocess
import psutil
import time
import os
import _wmi
from typing import Dict, List, Optional, Any, Union
from dataclasses import dataclass
from enum import Enum

from src.core.temp_monitor import temp_check

# Try importing additional GPU libraries
try:
    import pynvml
    PYNVML_AVAILABLE = True
except ImportError:
    PYNVML_AVAILABLE = False
    logging.warning("pynvml not available - NVIDIA monitoring limited")

try:
    import pyopencl as cl
    OPENCL_AVAILABLE = True
except ImportError:
    OPENCL_AVAILABLE = False
    logging.warning("PyOpenCL not available - OpenCL support disabled")

# Check for DirectML on Windows
DIRECTML_AVAILABLE = False
if platform.system() == "Windows":
    try:
        import torch_directml
        DIRECTML_AVAILABLE = True
        logging.info("DirectML support available for AMD/Intel GPUs")
    except ImportError:
        logging.warning("torch-directml not available - install for better AMD/Intel support")

# Check for Metal Performance Shaders on macOS
MPS_AVAILABLE = False
if platform.system() == "Darwin":
    try:
        if hasattr(torch.backends, 'mps') and torch.backends.mps.is_available():
            MPS_AVAILABLE = True
            logging.info("Metal Performance Shaders available")
    except:
        logging.warning("MPS not available")

class DeviceType(Enum):
    CUDA = "cuda"           # NVIDIA CUDA
    DIRECTML = "directml"   # AMD/Intel on Windows  
    OPENCL = "opencl"       # Cross-platform OpenCL
    MPS = "mps"            # Apple Metal
    CPU = "cpu"            # CPU fallback

class VendorType(Enum):
    NVIDIA = "NVIDIA"
    AMD = "AMD"
    INTEL = "Intel"
    APPLE = "Apple" 
    UNKNOWN = "Unknown"

@dataclass
class GPUDevice:
    """Represents a GPU device with unified information"""
    device_id: str
    name: str
    vendor: VendorType
    device_type: DeviceType
    compute_units: int
    total_memory_mb: int
    available_memory_mb: int
    memory_usage_percent: float
    temperature_c: Optional[float]
    power_usage_w: Optional[float]
    driver_version: str
    compute_capability: Optional[str]
    performance_score: int  # 0-1000, higher = better
    is_discrete: bool
    supports_fp16: bool
    supports_int8: bool
    max_work_group_size: int
    
class UniversalGPUManager:
    """Universal GPU manager supporting all major GPU vendors"""
    
    def __init__(self):
        self.devices: List[GPUDevice] = []
        self.selected_device: Optional[GPUDevice] = None
        self.torch_device: Optional[torch.device] = None
        self.initialized = False
        self.opencl_context = None
        self.opencl_queue = None
        
    async def initialize(self):
        """Initialize GPU manager with device detection"""
        logging.info("🔧 Initializing Universal GPU Manager...")
        
        try:
            # Detect all available devices
            await self._detect_all_devices()
            
            # Auto-select best device
            await self._auto_select_device()
            
            # Initialize selected device
            if self.selected_device:
                await self._initialize_device()
            
            self.initialized = True
            logging.info(f"✅ GPU Manager initialized - Selected: {self.get_device_info()}")
            return True
            
        except Exception as e:
            logging.error(f"❌ GPU Manager initialization failed: {e}")
            # Fall back to CPU
            await self._fallback_to_cpu()
            self.initialized = True
            return True
    
    async def _detect_all_devices(self):
        """Detect all available GPU devices across all platforms"""
        self.devices = []
        
        # 1. Detect NVIDIA CUDA devices
        await self._detect_cuda_devices()
        
        # 2. Detect AMD/Intel devices via DirectML (Windows)
        if DIRECTML_AVAILABLE:
            await self._detect_directml_devices()
        
        # 3. Detect devices via OpenCL (cross-platform)
        if OPENCL_AVAILABLE:
            await self._detect_opencl_devices()
        
        # 4. Detect Apple Metal devices (macOS)
        if MPS_AVAILABLE:
            await self._detect_mps_devices()
        
        # 5. Always include CPU as fallback
        await self._detect_cpu_device()
        
        logging.info(f"🔍 Detected {len(self.devices)} compute devices")
        for device in self.devices:
            logging.info(f"  - {device.vendor.value} {device.name} ({device.device_type.value})")
    
    async def _detect_cuda_devices(self):
        """Detect NVIDIA CUDA devices"""
        if not torch.cuda.is_available():
            return
        
        # Initialize NVIDIA monitoring if available
        nvidia_info = {}
        if PYNVML_AVAILABLE:
            try:
                pynvml.nvmlInit()
                nvidia_info = await self._get_nvidia_monitoring_info()
            except Exception as e:
                logging.warning(f"NVIDIA monitoring failed: {e}")
        
        device_count = torch.cuda.device_count()
        for i in range(device_count):
            props = torch.cuda.get_device_properties(i)
            
            # Get memory info
            torch.cuda.set_device(i)
            total_memory = props.total_memory
            allocated = torch.cuda.memory_allocated(i)
            available = total_memory - allocated
            usage_percent = (allocated / total_memory) * 100 if total_memory > 0 else 0
            
            # Get additional NVIDIA info
            temp = nvidia_info.get(i, {}).get('temperature')
            power = nvidia_info.get(i, {}).get('power')
            driver = nvidia_info.get(i, {}).get('driver_version')
            
            device = GPUDevice(
                device_id=f"cuda:{i}",
                name=props.name,
                vendor=VendorType.NVIDIA,
                device_type=DeviceType.CUDA,
                compute_units=props.multi_processor_count,
                total_memory_mb=total_memory // (1024 * 1024),
                available_memory_mb=available // (1024 * 1024),
                memory_usage_percent=usage_percent,
                temperature_c=temp,
                power_usage_w=power,
                driver_version=str(driver),
                compute_capability=f"{props.major}.{props.minor}",
                performance_score=self._calculate_cuda_performance_score(props),
                is_discrete=True,  # CUDA devices are typically discrete
                supports_fp16=props.major >= 6,  # Pascal+
                supports_int8=props.major >= 6,
                max_work_group_size=props.max_threads_per_block
            )
            self.devices.append(device)
    
    async def _detect_directml_devices(self):
        """Detect AMD/Intel devices via DirectML on Windows"""
        if not DIRECTML_AVAILABLE:
            return
        
        try:
            # DirectML device enumeration
            device_count = torch_directml.device_count()
            for i in range(device_count):
                device_name = torch_directml.device_name(i)
                
                # Determine vendor from device name
                vendor = VendorType.UNKNOWN
                if "AMD" in device_name.upper() or "RADEON" in device_name.upper():
                    vendor = VendorType.AMD
                elif "INTEL" in device_name.upper() or "UHD" in device_name.upper():
                    vendor = VendorType.INTEL
                elif "NVIDIA" in device_name.upper():
                    vendor = VendorType.NVIDIA  # Fallback for NVIDIA via DirectML
                
                # Get basic device info (DirectML provides limited info)
                device = GPUDevice(
                    device_id=f"directml:{i}",
                    name=device_name,
                    vendor=vendor,
                    device_type=DeviceType.DIRECTML,
                    compute_units=0,  # DirectML doesn't expose this
                    total_memory_mb=0,  # Will try to get from system
                    available_memory_mb=0,
                    memory_usage_percent=0.0,
                    temperature_c=None,
                    power_usage_w=None,
                    driver_version="DirectML",
                    compute_capability=None,
                    performance_score=self._estimate_directml_performance(device_name, vendor),
                    is_discrete=vendor in [VendorType.AMD, VendorType.NVIDIA],
                    supports_fp16=True,  # DirectML supports FP16
                    supports_int8=True,
                    max_work_group_size=1024  # Conservative estimate
                )
                
                # Try to get memory info from system
                await self._get_directml_memory_info(device, i)
                self.devices.append(device)
                
        except Exception as e:
            logging.error(f"DirectML detection failed: {e}")
    
    async def _detect_opencl_devices(self):
        """Detect devices via OpenCL (AMD, Intel, some NVIDIA)"""
        if not OPENCL_AVAILABLE:
            return
        
        try:
            platforms = cl.get_platforms()
            
            for platform in platforms:
                try:
                    devices = platform.get_devices(device_type=cl.device_type.GPU)
                    
                    for i, device in enumerate(devices):
                        vendor_name = device.vendor.strip()
                        device_name = device.name.strip()
                        
                        # Determine vendor
                        vendor = VendorType.UNKNOWN
                        if "AMD" in vendor_name.upper() or "Advanced Micro Devices" in vendor_name:
                            vendor = VendorType.AMD
                        elif "Intel" in vendor_name.upper():
                            vendor = VendorType.INTEL
                        elif "NVIDIA" in vendor_name.upper():
                            vendor = VendorType.NVIDIA
                        
                        # Get device properties
                        compute_units = device.max_compute_units
                        max_work_group = device.max_work_group_size
                        global_mem = device.global_mem_size
                        
                        opencl_device = GPUDevice(
                            device_id=f"opencl:{platform.name}:{i}",
                            name=device_name,
                            vendor=vendor,
                            device_type=DeviceType.OPENCL,
                            compute_units=compute_units,
                            total_memory_mb=global_mem // (1024 * 1024),
                            available_memory_mb=global_mem // (1024 * 1024),  # Estimate
                            memory_usage_percent=0.0,
                            temperature_c=None,
                            power_usage_w=None,
                            driver_version=device.driver_version,
                            compute_capability=device.opencl_c_version,
                            performance_score=self._calculate_opencl_performance_score(device),
                            is_discrete=vendor in [VendorType.AMD, VendorType.NVIDIA],
                            supports_fp16=cl.device_type.GPU in device.extensions,
                            supports_int8=True,
                            max_work_group_size=max_work_group
                        )
                        self.devices.append(opencl_device)
                        
                except Exception as e:
                    logging.warning(f"Failed to get OpenCL devices from platform {platform.name}: {e}")
                    
        except Exception as e:
            logging.error(f"OpenCL detection failed: {e}")
    
    async def _detect_mps_devices(self):
        """Detect Apple Metal Performance Shaders devices"""
        if not MPS_AVAILABLE:
            return
        
        try:
            device = GPUDevice(
                device_id="mps:0",
                name="Apple Metal GPU",
                vendor=VendorType.APPLE,
                device_type=DeviceType.MPS,
                compute_units=0,  # Metal doesn't expose this easily
                total_memory_mb=0,  # Will get from system memory
                available_memory_mb=0,
                memory_usage_percent=0.0,
                temperature_c=None,
                power_usage_w=None,
                driver_version="Metal",
                compute_capability="Metal",
                performance_score=700,  # Apple Silicon is quite powerful
                is_discrete=False,  # Integrated in Apple Silicon
                supports_fp16=True,
                supports_int8=True,
                max_work_group_size=1024
            )
            
            # Estimate memory from system RAM (unified memory)
            total_ram = psutil.virtual_memory().total
            device.total_memory_mb = total_ram // (1024 * 1024)
            device.available_memory_mb = device.total_memory_mb // 2  # Conservative
            
            self.devices.append(device)
            
        except Exception as e:
            logging.error(f"MPS detection failed: {e}")
    
    async def _detect_cpu_device(self):
        """Add CPU as fallback device"""
        cpu_info = platform.processor() or "CPU"
        cpu_cores = psutil.cpu_count(logical=False)
        cpu_threads = psutil.cpu_count(logical=True)
        total_ram = psutil.virtual_memory().total
        
        if cpu_cores is not None and cpu_threads is not None:
            cpu_device = GPUDevice(
                device_id="cpu:0",
                name=f"{cpu_info} ({cpu_cores}C/{cpu_threads}T)",
                vendor=VendorType.UNKNOWN,
                device_type=DeviceType.CPU,
                compute_units=cpu_cores,
                total_memory_mb=total_ram // (1024 * 1024),
                available_memory_mb=psutil.virtual_memory().available // (1024 * 1024),
                memory_usage_percent=psutil.virtual_memory().percent,
                temperature_c=await self._get_cpu_temperature(),
                power_usage_w=None,
                driver_version="N/A",
                compute_capability="CPU",
                performance_score=100,  # Base CPU score
                is_discrete=False,
                supports_fp16=True,  # Modern CPUs support FP16
                supports_int8=True,
                max_work_group_size=cpu_threads
            )
            self.devices.append(cpu_device)
    
    async def _auto_select_device(self):
        """Automatically select the best available device"""
        if not self.devices:
            await self._fallback_to_cpu()
            return
        
        # Sort devices by performance score (highest first)
        sorted_devices = sorted(self.devices, key=lambda d: d.performance_score, reverse=True)
        
        # Prefer discrete GPUs over integrated
        discrete_devices = [d for d in sorted_devices if d.is_discrete and d.device_type != DeviceType.CPU]
        if discrete_devices:
            self.selected_device = discrete_devices[0]
        else:
            # Fall back to best available device
            self.selected_device = sorted_devices[0]
        
        logging.info(f"🎯 Auto-selected: {self.selected_device.vendor.value} {self.selected_device.name}")
    
    async def _initialize_device(self):
        """Initialize the selected device for PyTorch"""
        if not self.selected_device:
            return
        
        try:
            if self.selected_device.device_type == DeviceType.CUDA:
                device_id = int(self.selected_device.device_id.split(':')[1])
                self.torch_device = torch.device(f"cuda:{device_id}")
                torch.cuda.set_device(device_id)
                
            elif self.selected_device.device_type == DeviceType.DIRECTML:
                if DIRECTML_AVAILABLE:
                    device_id = int(self.selected_device.device_id.split(':')[1])
                    self.torch_device = torch.device(torch_directml.device(device_id))
                else:
                    raise Exception("DirectML not available")
                    
            elif self.selected_device.device_type == DeviceType.MPS:
                if MPS_AVAILABLE:
                    self.torch_device = torch.device("mps")
                else:
                    raise Exception("MPS not available")
                    
            elif self.selected_device.device_type == DeviceType.OPENCL:
                # For OpenCL, we'll fall back to CPU for PyTorch but can use OpenCL for custom kernels
                self.torch_device = torch.device("cpu")
                await self._initialize_opencl_context()
                
            else:  # CPU
                self.torch_device = torch.device("cpu")
            
            logging.info(f"✅ Initialized device: {self.torch_device}")
            
        except Exception as e:
            logging.error(f"❌ Device initialization failed: {e}")
            await self._fallback_to_cpu()
    
    async def _fallback_to_cpu(self):
        """Fallback to CPU-only operation"""
        logging.warning("⚠️ Falling back to CPU-only operation")
        
        cpu_device = None
        for device in self.devices:
            if device.device_type == DeviceType.CPU:
                cpu_device = device
                break
        
        if not cpu_device:
            # Create basic CPU device
            await self._detect_cpu_device()
            cpu_device = self.devices[-1]
        
        self.selected_device = cpu_device
        self.torch_device = torch.device("cpu")
    
    # Utility methods for device info and performance scoring
    def _calculate_cuda_performance_score(self, props) -> int:
        """Calculate CUDA device performance score"""
        base_score = 0
        
        # Memory size score (0-300 points)
        memory_gb = props.total_memory / (1024**3)
        memory_score = min(memory_gb * 30, 300)
        
        # Compute capability score (0-200 points)
        cc_score = (props.major * 100) + (props.minor * 10)
        cc_score = min(cc_score, 200)
        
        # Multiprocessor count score (0-300 points)
        mp_score = min(props.multi_processor_count * 3, 300)
        
        # Memory bandwidth approximation (0-200 points)
        memory_bandwidth_score = min(props.total_memory / (1024**2) / 10, 200)
        
        total_score = base_score + memory_score + cc_score + mp_score + memory_bandwidth_score
        return min(int(total_score), 1000)
    
    def _estimate_directml_performance(self, device_name: str, vendor: VendorType) -> int:
        """Estimate DirectML device performance"""
        base_score = 300  # DirectML baseline
        
        # Vendor bonus
        if vendor == VendorType.AMD:
            base_score += 200  # AMD GPUs generally good for compute
        elif vendor == VendorType.INTEL:
            base_score += 100  # Intel integrated
        elif vendor == VendorType.NVIDIA:
            base_score += 250  # NVIDIA via DirectML
        
        # Device name heuristics
        name_upper = device_name.upper()
        if any(x in name_upper for x in ["RTX", "RX 6", "RX 7"]):
            base_score += 300  # High-end cards
        elif any(x in name_upper for x in ["GTX", "RX 5", "ARC"]):
            base_score += 200  # Mid-range cards
        elif "UHD" in name_upper or "INTEGRATED" in name_upper:
            base_score += 50   # Integrated graphics
        
        return min(base_score, 1000)
    
    def _calculate_opencl_performance_score(self, device) -> int:
        """Calculate OpenCL device performance score"""
        base_score = 200  # OpenCL baseline
        
        # Compute units score
        cu_score = min(device.max_compute_units * 5, 300)
        
        # Memory score  
        memory_gb = device.global_mem_size / (1024**3)
        memory_score = min(memory_gb * 50, 300)
        
        # Max work group size score
        wg_score = min(device.max_work_group_size / 10, 100)
        
        return min(int(base_score + cu_score + memory_score + wg_score), 1000)
    
    async def _get_nvidia_monitoring_info(self) -> Dict[int, Dict[str, Any]]:
        """Get NVIDIA monitoring info via NVML"""
        info = {}
        
        try:
            driver_version = pynvml.nvmlSystemGetDriverVersion()
            info['driver_version'] = driver_version.decode() if isinstance(driver_version, bytes) else driver_version
            
            device_count = pynvml.nvmlDeviceGetCount()
            for i in range(device_count):
                handle = pynvml.nvmlDeviceGetHandleByIndex(i)
                
                device_info = {}
                
                # Temperature
                try:
                    temp = pynvml.nvmlDeviceGetTemperature(handle, pynvml.NVML_TEMPERATURE_GPU)
                    device_info['temperature'] = float(temp)
                except:
                    device_info['temperature'] = None
                
                # Power
                try:
                    power = pynvml.nvmlDeviceGetPowerUsage(handle) / 1000.0  # Convert mW to W
                    device_info['power'] = float(power)
                except:
                    device_info['power'] = None
                
                info[i] = device_info
                
        except Exception as e:
            logging.warning(f"NVML monitoring failed: {e}")
        
        return info
    
    async def _get_directml_memory_info(self, device: GPUDevice, device_index: int):
        """Try to get memory info for DirectML device"""
        try:
            # This is a best-effort attempt - DirectML doesn't expose detailed memory info
            # We'll try to use system tools or make educated guesses
            
            if device.vendor == VendorType.AMD:
                # Try to get AMD GPU memory
                memory = await self._get_amd_memory_info()
                if memory:
                    device.total_memory_mb = memory
                    device.available_memory_mb = int(memory * 0.8)  # Conservative estimate
            
            elif device.vendor == VendorType.INTEL:
                # Intel integrated graphics usually share system memory
                total_ram = psutil.virtual_memory().total
                # Intel UHD typically can use up to 50% of system RAM
                device.total_memory_mb = min(total_ram // (1024 * 1024) // 2, 8192)
                device.available_memory_mb = int(device.total_memory_mb * 0.9)
            
        except Exception as e:
            logging.warning(f"DirectML memory detection failed: {e}")
    
    async def _get_amd_memory_info(self) -> Optional[int]:
        """Try to get AMD GPU memory via system tools"""
        try:
            if platform.system() == "Windows":
                # Try WMI or other Windows tools
                pass
            elif platform.system() == "Linux":
                # Try reading from /sys or using rocm-smi
                try:
                    result = subprocess.run(['rocm-smi', '--showmeminfo', 'vram'], 
                                          capture_output=True, text=True, timeout=5)
                    if result.returncode == 0:
                        # Parse rocm-smi output
                        for line in result.stdout.split('\n'):
                            if 'Total VRAM' in line:
                                # Extract memory size (this is vendor-specific parsing)
                                pass
                except (subprocess.TimeoutExpired, FileNotFoundError):
                    pass
            
        except Exception as e:
            logging.debug(f"AMD memory detection failed: {e}")
        
        return None
    
    async def _get_cpu_temperature(self) -> Optional[float]:          
        try:
            temp_check()
        except:
            pass
        return None

    async def _initialize_opencl_context(self):
        """Initialize OpenCL context for custom kernels"""
        if not OPENCL_AVAILABLE:
            return
        
        try:
            # Find the selected OpenCL device
            if self.selected_device is not None:
                device_parts = self.selected_device.device_id.split(':')

            platform_name = device_parts[1]
            device_index = int(device_parts[2])
            
            platforms = cl.get_platforms()
            for platform in platforms:
                if platform.name == platform_name:
                    devices = platform.get_devices(device_type=cl.device_type.GPU)
                    if device_index < len(devices):
                        device = devices[device_index]
                        self.opencl_context = cl.Context([device])
                        self.opencl_queue = cl.CommandQueue(self.opencl_context)
                        logging.info("✅ OpenCL context initialized")
                        break
        except Exception as e:
            logging.warning(f"OpenCL context initialization failed: {e}")
    
    # Public interface methods
    def is_initialized(self) -> bool:
        """Check if GPU manager is initialized"""
        return self.initialized
    
    def get_device(self) -> torch.device:
        """Get current PyTorch device"""
        return self.torch_device or torch.device("cpu")
    
    def get_device_info(self) -> str:
        """Get device info string"""
        if not self.selected_device:
            return "No device selected"
        
        device = self.selected_device
        return f"{device.vendor.value} {device.name} ({device.device_type.value})"
    
    def get_all_devices(self) -> List[Dict[str, Any]]:
        """Get information about all detected devices"""
        devices_info = []
        for device in self.devices:
            info = {
                "id": device.device_id,
                "name": device.name,
                "vendor": device.vendor.value,
                "type": device.device_type.value,
                "memory_mb": device.total_memory_mb,
                "available_memory_mb": device.available_memory_mb,
                "memory_usage_percent": device.memory_usage_percent,
                "performance_score": device.performance_score,
                "is_discrete": device.is_discrete,
                "supports_fp16": device.supports_fp16,
                "compute_capability": device.compute_capability,
                "is_selected": device == self.selected_device
            }
            devices_info.append(info)
        return devices_info
    
    async def select_device(self, device_id: str):
        """Manually select a device"""
        target_device = None
        for device in self.devices:
            if device.device_id == device_id:
                target_device = device
                break
        
        if not target_device:
            raise ValueError(f"Device not found: {device_id}")
        
        self.selected_device = target_device
        await self._initialize_device()
        logging.info(f"✅ Manually selected device: {self.get_device_info()}")
    
    async def set_gpu_preference(self, preference: str):
        """Set GPU preference: 'auto', 'gpu_only', 'cpu_only', 'nvidia_only', 'amd_only', 'intel_only'"""
        valid_preferences = ['auto', 'gpu_only', 'cpu_only', 'nvidia_only', 'amd_only', 'intel_only']
        
        if preference not in valid_preferences:
            raise ValueError(f"Invalid preference. Must be one of: {valid_preferences}")
        
        # Filter devices based on preference
        if preference == 'auto':
            # Use existing auto-selection logic
            await self._auto_select_device()
            
        elif preference == 'cpu_only':
            # Force CPU-only mode
            cpu_device = None
            for device in self.devices:
                if device.device_type == DeviceType.CPU:
                    cpu_device = device
                    break
            
            if cpu_device:
                self.selected_device = cpu_device
                await self._initialize_device()
                logging.info("🔧 Forced CPU-only mode")
            else:
                await self._fallback_to_cpu()
                
        elif preference == 'gpu_only':
            # Only use GPU devices, fail if none available
            gpu_devices = [d for d in self.devices if d.device_type != DeviceType.CPU]
            if gpu_devices:
                # Select best GPU
                best_gpu = max(gpu_devices, key=lambda d: d.performance_score)
                self.selected_device = best_gpu
                await self._initialize_device()
                logging.info(f"🎮 GPU-only mode: Selected {self.get_device_info()}")
            else:
                raise RuntimeError("No GPU devices available, but GPU-only mode requested")
                
        elif preference == 'nvidia_only':
            # Only use NVIDIA GPUs
            nvidia_devices = [d for d in self.devices if d.vendor == VendorType.NVIDIA and d.device_type != DeviceType.CPU]
            if nvidia_devices:
                best_nvidia = max(nvidia_devices, key=lambda d: d.performance_score)
                self.selected_device = best_nvidia
                await self._initialize_device()
                logging.info(f"🟢 NVIDIA-only mode: Selected {self.get_device_info()}")
            else:
                raise RuntimeError("No NVIDIA GPUs available, but NVIDIA-only mode requested")
                
        elif preference == 'amd_only':
            # Only use AMD GPUs
            amd_devices = [d for d in self.devices if d.vendor == VendorType.AMD and d.device_type != DeviceType.CPU]
            if amd_devices:
                best_amd = max(amd_devices, key=lambda d: d.performance_score)
                self.selected_device = best_amd
                await self._initialize_device()
                logging.info(f"🔴 AMD-only mode: Selected {self.get_device_info()}")
            else:
                raise RuntimeError("No AMD GPUs available, but AMD-only mode requested")
                
        elif preference == 'intel_only':
            # Only use Intel GPUs
            intel_devices = [d for d in self.devices if d.vendor == VendorType.INTEL and d.device_type != DeviceType.CPU]
            if intel_devices:
                best_intel = max(intel_devices, key=lambda d: d.performance_score)
                self.selected_device = best_intel
                await self._initialize_device()
                logging.info(f"🔵 Intel-only mode: Selected {self.get_device_info()}")
            else:
                raise RuntimeError("No Intel GPUs available, but Intel-only mode requested")
    
    def get_available_preferences(self) -> List[Dict[str, Any]]:
        """Get list of available GPU preferences based on detected hardware"""
        preferences = [
            {"id": "auto", "name": "Auto (Recommended)", "description": "Automatically select best device", "available": True},
            {"id": "cpu_only", "name": "CPU Only", "description": "Use CPU for all computations", "available": True}
        ]
        
        # Check for GPU vendors
        has_nvidia = any(d.vendor == VendorType.NVIDIA and d.device_type != DeviceType.CPU for d in self.devices)
        has_amd = any(d.vendor == VendorType.AMD and d.device_type != DeviceType.CPU for d in self.devices)
        has_intel = any(d.vendor == VendorType.INTEL and d.device_type != DeviceType.CPU for d in self.devices)
        has_any_gpu = any(d.device_type != DeviceType.CPU for d in self.devices)
        
        if has_any_gpu:
            preferences.append({
                "id": "gpu_only", 
                "name": "GPU Only", 
                "description": "Use any available GPU, fail if none", 
                "available": True
            })
        
        if has_nvidia:
            nvidia_count = len([d for d in self.devices if d.vendor == VendorType.NVIDIA and d.device_type != DeviceType.CPU])
            preferences.append({
                "id": "nvidia_only", 
                "name": f"NVIDIA Only ({nvidia_count} available)", 
                "description": "Use only NVIDIA GPUs with CUDA", 
                "available": True
            })
        
        if has_amd:
            amd_count = len([d for d in self.devices if d.vendor == VendorType.AMD and d.device_type != DeviceType.CPU])
            preferences.append({
                "id": "amd_only", 
                "name": f"AMD Only ({amd_count} available)", 
                "description": "Use only AMD GPUs with DirectML/OpenCL", 
                "available": True
            })
        
        if has_intel:
            intel_count = len([d for d in self.devices if d.vendor == VendorType.INTEL and d.device_type != DeviceType.CPU])
            preferences.append({
                "id": "intel_only", 
                "name": f"Intel Only ({intel_count} available)", 
                "description": "Use only Intel GPUs with DirectML/OpenCL", 
                "available": True
            })
        
        return preferences
    
    def get_current_preference(self) -> str:
        """Get current GPU preference setting"""
        if not self.selected_device:
            return "unknown"
        
        device = self.selected_device
        
        if device.device_type == DeviceType.CPU:
            return "cpu_only"
        elif device.vendor == VendorType.NVIDIA:
            return "nvidia_preferred"  # Could be auto or nvidia_only
        elif device.vendor == VendorType.AMD:
            return "amd_preferred"     # Could be auto or amd_only
        elif device.vendor == VendorType.INTEL:
            return "intel_preferred"   # Could be auto or intel_only
        else:
            return "auto"
    
    async def get_device_status(self) -> Dict[str, Any]:
        """Get current device status and utilization"""
        if not self.selected_device:
            return {"error": "No device selected"}
        
        device = self.selected_device
        status = {
            "device_id": device.device_id,
            "name": device.name,
            "vendor": device.vendor.value,
            "type": device.device_type.value,
            "memory_total_mb": device.total_memory_mb,
            "memory_available_mb": device.available_memory_mb,
            "memory_usage_percent": device.memory_usage_percent,
            "temperature_c": device.temperature_c,
            "power_usage_w": device.power_usage_w
        }
        
        # Update real-time info for CUDA devices
        if device.device_type == DeviceType.CUDA and torch.cuda.is_available():
            device_idx = int(device.device_id.split(':')[1])
            torch.cuda.set_device(device_idx)
            
            total = torch.cuda.get_device_properties(device_idx).total_memory
            allocated = torch.cuda.memory_allocated(device_idx)
            
            status["memory_allocated_mb"] = allocated // (1024 * 1024)
            status["memory_usage_percent"] = (allocated / total) * 100
        
        return status
    
    async def clear_cache(self):
        """Clear device cache"""
        try:
            if self.selected_device and self.selected_device.device_type == DeviceType.CUDA:
                torch.cuda.empty_cache()
                logging.info("🧹 CUDA cache cleared")
            elif self.selected_device and self.selected_device.device_type == DeviceType.DIRECTML:
                # DirectML doesn't have explicit cache clearing
                logging.info("🧹 DirectML memory management handled automatically")
            elif self.selected_device and self.selected_device.device_type == DeviceType.MPS:
                if hasattr(torch.mps, 'empty_cache'):
                    torch.mps.empty_cache()
                    logging.info("🧹 MPS cache cleared")
            else:
                logging.info("🧹 CPU doesn't require cache clearing")
        except Exception as e:
            logging.warning(f"Cache clearing failed: {e}")
    
    async def run_benchmark(self) -> Dict[str, Any]:
        """Run cross-platform benchmark"""
        if not self.selected_device:
            return {"error": "No device selected"}
        
        logging.info("🏃 Running cross-platform GPU benchmark...")
        device = self.get_device()
        
        try:
            # Benchmark parameters
            matrix_sizes = [1024, 2048, 4096]
            iterations = 5
            results = {}
            
            for size in matrix_sizes:
                logging.info(f"  Testing {size}x{size} matrices...")
                
                # Create test matrices
                if self.selected_device.device_type == DeviceType.DIRECTML and DIRECTML_AVAILABLE:
                    # DirectML specific benchmark
                    a = torch.randn(size, size, device=device, dtype=torch.float32)
                    b = torch.randn(size, size, device=device, dtype=torch.float32)
                else:
                    # Standard PyTorch benchmark
                    a = torch.randn(size, size, device=device, dtype=torch.float32)
                    b = torch.randn(size, size, device=device, dtype=torch.float32)
                
                # Warm up
                for _ in range(2):
                    _ = torch.matmul(a, b)
                    if device.type == "cuda":
                        torch.cuda.synchronize()
                    elif device.type == "mps":
                        torch.mps.synchronize() if hasattr(torch.mps, 'synchronize') else None
                
                # Actual benchmark
                start_time = time.time()
                for _ in range(iterations):
                    c = torch.matmul(a, b)
                    if device.type == "cuda":
                        torch.cuda.synchronize()
                    elif device.type == "mps":
                        torch.mps.synchronize() if hasattr(torch.mps, 'synchronize') else None
                
                end_time = time.time()
                
                total_time = end_time - start_time
                avg_time = total_time / iterations
                
                # Calculate GFLOPS
                operations = 2 * size * size * size  # Matrix multiplication operations
                gflops = (operations / avg_time) / 1e9
                
                results[f"size_{size}"] = {
                    "avg_time_ms": avg_time * 1000,
                    "gflops": gflops
                }
            
            # Overall score calculation
            overall_gflops = sum(r["gflops"] for r in results.values()) / len(results)
            
            benchmark_result = {
                "device": self.get_device_info(),
                "device_type": self.selected_device.device_type.value,
                "vendor": self.selected_device.vendor.value,
                "results": results,
                "overall_gflops": overall_gflops,
                "performance_score": self.selected_device.performance_score,
                "supports_fp16": self.selected_device.supports_fp16,
                "memory_mb": self.selected_device.total_memory_mb
            }
            
            logging.info(f"✅ Benchmark completed - Overall GFLOPS: {overall_gflops:.2f}")
            return benchmark_result
            
        except Exception as e:
            logging.error(f"❌ Benchmark failed: {e}")
            return {
                "error": f"Benchmark failed: {str(e)}",
                "device": self.get_device_info()
            }
    
    async def optimize_for_model_size(self, estimated_model_size_mb: int) -> Dict[str, Any]:
        """Optimize device selection and settings for a specific model size"""
        recommendations = {
            "recommended_device": None,
            "memory_sufficient": False,
            "recommended_batch_size": 1,
            "use_mixed_precision": False,
            "use_gradient_checkpointing": False,
            "warnings": []
        }
        
        # Calculate memory requirements (model + optimizer + gradients + activations)
        total_memory_needed = estimated_model_size_mb * 4  # Conservative estimate
        
        # Check if current device can handle the model
        if self.selected_device:
            available_memory = self.selected_device.available_memory_mb
            
            if available_memory >= total_memory_needed:
                recommendations["memory_sufficient"] = True
                recommendations["recommended_device"] = self.selected_device.device_id
                
                # Calculate recommended batch size
                remaining_memory = available_memory - total_memory_needed
                # Rough estimate: each batch item needs ~1MB for activations
                max_batch_size = max(1, remaining_memory // max(1, estimated_model_size_mb // 100))
                recommendations["recommended_batch_size"] = min(max_batch_size, 32)
                
            else:
                recommendations["warnings"].append(
                    f"Current device has insufficient memory. "
                    f"Need {total_memory_needed}MB, have {available_memory}MB"
                )
                
                # Look for a better device
                for device in self.devices:
                    if device.available_memory_mb >= total_memory_needed:
                        recommendations["recommended_device"] = device.device_id
                        recommendations["memory_sufficient"] = True
                        break
                
                if not recommendations["memory_sufficient"]:
                    # Suggest optimizations
                    recommendations["use_mixed_precision"] = True
                    recommendations["use_gradient_checkpointing"] = True
                    recommendations["recommended_batch_size"] = 1
                    recommendations["warnings"].append(
                        "Consider using mixed precision and gradient checkpointing to reduce memory usage"
                    )
        
        # Mixed precision recommendations
        if self.selected_device and self.selected_device.supports_fp16:
            if estimated_model_size_mb > 1000:  # Large models benefit from FP16
                recommendations["use_mixed_precision"] = True
        
        return recommendations
    
    async def cleanup(self):
        """Cleanup GPU resources"""
        try:
            if self.torch_device and self.torch_device.type == "cuda":
                torch.cuda.empty_cache()
            
            if self.opencl_context:
                # OpenCL cleanup would go here
                pass
            
            logging.info("🧹 GPU Manager cleanup complete")
            
        except Exception as e:
            logging.warning(f"Cleanup warning: {e}")