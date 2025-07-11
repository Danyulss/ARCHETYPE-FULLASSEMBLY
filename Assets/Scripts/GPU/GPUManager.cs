using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Archetype.Backend;

namespace Archetype.GPU
{
    public class GPUManager : MonoBehaviour
    {
        public static GPUManager Instance { get; private set; }
        
        [Header("GPU Information")]
        public List<GPUInfo> detectedGPUs = new List<GPUInfo>();
        public GPUInfo selectedGPU;
        
        [Header("Performance Settings")]
        public bool enablePerformanceMonitoring = true;
        public float performanceUpdateInterval = 1.0f;
        
        public event Action<List<GPUInfo>> OnGPUsDetected;
        public event Action<GPUPerformanceMetrics> OnPerformanceUpdated;
        public event Action<string> OnGPUError;
        
        private bool isMonitoring = false;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // Wait for Rust backend to initialize
            if (RustInterface.Instance != null)
            {
                RustInterface.Instance.OnBackendConnected += OnBackendReady;
                RustInterface.Instance.OnBackendError += OnBackendError;
            }
        }
        
        private async void OnBackendReady()
        {
            await DetectGPUs();
            
            if (enablePerformanceMonitoring)
            {
                StartPerformanceMonitoring();
            }
        }
        
        public async Task DetectGPUs()
        {
            try
            {
                Debug.Log("🔍 Detecting GPUs via Rust backend...");
                
                // Call Rust backend for GPU detection
                var response = await RustInterface.Instance.CallRustCommand<GPUDetectionResponse>("get_gpu_info");
                
                detectedGPUs.Clear();
                detectedGPUs.AddRange(response.gpus);
                
                if (detectedGPUs.Count > 0)
                {
                    selectedGPU = detectedGPUs[0]; // Auto-select first GPU
                    await SelectGPU(0);
                }
                
                OnGPUsDetected?.Invoke(detectedGPUs);
                Debug.Log($"✅ Detected {detectedGPUs.Count} GPU(s)");
                
                foreach (var gpu in detectedGPUs)
                {
                    Debug.Log($"   GPU: {gpu.name} ({gpu.memory_mb}MB VRAM)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ GPU detection failed: {e.Message}");
                OnGPUError?.Invoke($"GPU detection failed: {e.Message}");
                
                // Fallback to Unity's built-in GPU detection
                FallbackGPUDetection();
            }
        }
        
        public async Task SelectGPU(int index)
        {
            if (index >= 0 && index < detectedGPUs.Count)
            {
                try
                {
                    selectedGPU = detectedGPUs[index];
                    
                    // Tell Rust backend to select this GPU
                    await RustInterface.Instance.CallRustCommand<object>("select_gpu_device", 
                        new { adapter_index = index });
                    
                    Debug.Log($"✅ Selected GPU: {selectedGPU.name}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"❌ Failed to select GPU: {e.Message}");
                    OnGPUError?.Invoke($"Failed to select GPU: {e.Message}");
                }
            }
        }
        
        private void FallbackGPUDetection()
        {
            Debug.Log("🔄 Using Unity fallback GPU detection...");
            
            // Use Unity's SystemInfo as fallback
            var unityGPU = new GPUInfo
            {
                id = "unity-fallback",
                name = SystemInfo.graphicsDeviceName,
                vendor = SystemInfo.graphicsDeviceVendor,
                memory_mb = (uint)SystemInfo.graphicsMemorySize,
                device_type = MapUnityGPUType(SystemInfo.graphicsDeviceType),
                performance_tier = DeterminePerformanceTier(SystemInfo.graphicsMemorySize)
            };
            
            detectedGPUs.Clear();
            detectedGPUs.Add(unityGPU);
            selectedGPU = unityGPU;
            OnGPUsDetected?.Invoke(detectedGPUs);
        }
        
        private void StartPerformanceMonitoring()
        {
            if (!isMonitoring)
            {
                isMonitoring = true;
                InvokeRepeating(nameof(UpdatePerformanceMetrics), 
                    performanceUpdateInterval, performanceUpdateInterval);
            }
        }
        
        private async void UpdatePerformanceMetrics()
        {
            try
            {
                var metrics = await RustInterface.Instance.CallRustCommand<GPUPerformanceMetrics>(
                    "get_performance_metrics");
                OnPerformanceUpdated?.Invoke(metrics);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"⚠️ Performance update failed: {e.Message}");
            }
        }
        
        private GPUDeviceType MapUnityGPUType(UnityEngine.Rendering.GraphicsDeviceType unityType)
        {
            return unityType switch
            {
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 => GPUDeviceType.DirectX,
                UnityEngine.Rendering.GraphicsDeviceType.Direct3D12 => GPUDeviceType.DirectX,
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan => GPUDeviceType.Vulkan,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore => GPUDeviceType.OpenGL,
                UnityEngine.Rendering.GraphicsDeviceType.Metal => GPUDeviceType.Metal,
                _ => GPUDeviceType.Other
            };
        }
        
        private GPUPerformanceTier DeterminePerformanceTier(int memoryMB)
        {
            return memoryMB switch
            {
                >= 16384 => GPUPerformanceTier.Enthusiast,
                >= 8192 => GPUPerformanceTier.HighEnd,
                >= 4096 => GPUPerformanceTier.Mainstream,
                >= 2048 => GPUPerformanceTier.Budget,
                _ => GPUPerformanceTier.Integrated
            };
        }
        
        private void OnBackendError(string error)
        {
            OnGPUError?.Invoke($"Backend error: {error}");
        }
        
        private void OnDestroy()
        {
            isMonitoring = false;
            
            if (RustInterface.Instance != null)
            {
                RustInterface.Instance.OnBackendConnected -= OnBackendReady;
                RustInterface.Instance.OnBackendError -= OnBackendError;
            }
        }
    }
    
    // Data structures that match Rust backend
    [Serializable]
    public class GPUDetectionResponse
    {
        public GPUInfo[] gpus;
    }
    
    [Serializable]
    public class GPUInfo
    {
        public string id;
        public string name;
        public string vendor;
        public uint memory_mb;
        public GPUDeviceType device_type;
        public GPUPerformanceTier performance_tier;
    }
    
    [Serializable]
    public class GPUPerformanceMetrics
    {
        public float gpu_utilization_percent;
        public uint gpu_memory_used_mb;
        public uint gpu_memory_total_mb;
        public uint system_memory_used_mb;
        public uint system_memory_total_mb;
        public float cpu_usage_percent;
        public float[] temperatures;
    }
    
    public enum GPUDeviceType
    {
        DirectX,
        Vulkan,
        OpenGL,
        Metal,
        Other
    }
    
    public enum GPUPerformanceTier
    {
        Integrated,
        Budget,
        Mainstream,
        HighEnd,
        Enthusiast
    }
}
