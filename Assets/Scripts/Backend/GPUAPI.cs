using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Archetype.Backend.API
{
    /// <summary>
    /// API wrapper for GPU operations
    /// </summary>
    public class GPUAPI : MonoBehaviour
    {
        /// <summary>
        /// Get GPU information
        /// </summary>
        public static async Task<GPUListResponse> GetGPUInfo()
        {
            return await BackendInterface.Instance.GetAsync<GPUListResponse>("gpu");
        }

        /// <summary>
        /// Get specific GPU information
        /// </summary>
        public static async Task<GPUInfo> GetSpecificGPU(int deviceId)
        {
            return await BackendInterface.Instance.GetAsync<GPUInfo>($"gpu/{deviceId}");
        }

        /// <summary>
        /// Select GPU for training
        /// </summary>
        public static async Task<ApiResponse<object>> SelectGPU(int deviceId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"gpu/{deviceId}/select");
        }

        /// <summary>
        /// Clear GPU memory cache
        /// </summary>
        public static async Task<ApiResponse<object>> ClearGPUMemory()
        {
            return await BackendInterface.Instance.GetAsync<ApiResponse<object>>("gpu/memory/clear");
        }

        /// <summary>
        /// Run GPU benchmark
        /// </summary>
        public static async Task<BenchmarkResult> RunBenchmark()
        {
            return await BackendInterface.Instance.GetAsync<BenchmarkResult>("gpu/benchmark");
        }
    }

    #region GPU Data Classes

    [Serializable]
    public class GPUInfo
    {
        public int device_id;
        public string name;
        public string compute_capability;
        public long total_memory;
        public long available_memory;
        public float utilization;
        public float temperature;
        public float power_usage;
    }

    [Serializable]
    public class GPUListResponse
    {
        public List<GPUInfo> gpus;
        public int selected_gpu;
        public bool cuda_available;
        public int total_devices;
    }

    [Serializable]
    public class BenchmarkResult
    {
        public string device;
        public int matrix_size;
        public int iterations;
        public float total_time;
        public float average_time;
        public float gflops;
        public long memory_allocated;
        public long memory_cached;
    }

    #endregion
}