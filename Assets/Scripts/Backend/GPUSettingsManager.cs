using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Archetype.Backend
{
    [Serializable]
    public class GPUPreference
    {
        public string id;
        public string name;
        public string description;
        public bool available;
    }

    [Serializable]
    public class GPUDevice
    {
        public string id;
        public string name;
        public string vendor;
        public string type;
        public int memory_mb;
        public int performance_score;
        public bool is_discrete;
        public bool is_selected;
    }

    [Serializable]
    public class GPUSettingsResponse
    {
        public GPUDevice current_device;
        public List<GPUDevice> available_devices;
        public List<GPUPreference> available_preferences;
        public string current_preference;
    }

    public class GPUSettingsManager : MonoBehaviour
    {
        public static GPUSettingsManager Instance { get; private set; }

        [Header("GPU Settings")]
        public string currentPreference = "auto";
        public GPUDevice selectedDevice;
        public List<GPUDevice> availableDevices = new List<GPUDevice>();
        public List<GPUPreference> availablePreferences = new List<GPUPreference>();

        // Events
        public event Action<string> OnPreferenceChanged;
        public event Action<GPUDevice> OnDeviceSelected;
        public event Action<GPUSettingsResponse> OnSettingsLoaded;

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
            Debug.Log("🎮 GPUSettingsManager Start() called");
            
            if (BackendInterface.Instance == null)
            {
                Debug.LogError("❌ BackendInterface.Instance is null - retrying in 1 second");
                StartCoroutine(RetryLoadSettings<UnityEngine.WaitForSeconds>());
                return;
            }
            
            StartCoroutine(LoadGPUSettings());
        }

        private IEnumerator<UnityEngine.WaitForSeconds> RetryLoadSettings<T>()
        {
            yield return new WaitForSeconds(1f);
            
            if (BackendInterface.Instance != null)
            {
                Debug.Log("🔄 Retrying GPU settings load");
                StartCoroutine(LoadGPUSettings());
            }
            else
            {
                Debug.LogError("❌ BackendInterface still not available");
            }
        }

        public IEnumerator<System.Threading.Tasks.Task<GPUSettingsResponse>> LoadGPUSettings()
        {
            var request = BackendInterface.Instance.GetAsync<GPUSettingsResponse>("gpu/settings");
            yield return request;

            if (request.IsCompleted && !request.IsFaulted)
            {
                var settings = request.Result;
                selectedDevice = settings.current_device;
                availableDevices = settings.available_devices;
                availablePreferences = settings.available_preferences;
                currentPreference = settings.current_preference;

                OnSettingsLoaded?.Invoke(settings);
                Debug.Log($"🎮 GPU Settings loaded - Current: {selectedDevice?.name}");
            }
            else
            {
                Debug.LogError("Failed to load GPU settings");
            }
        }

        public IEnumerator<T> SetGPUPreference<T>(string preference)
        {
            var requestData = new { preference = preference };
            var request = BackendInterface.Instance.PostAsync<object>("gpu/preference", requestData);
            yield return (T)Convert.ChangeType(request, typeof(T));

            if (request.IsCompleted && !request.IsFaulted)
            {
                currentPreference = preference;
                OnPreferenceChanged?.Invoke(preference);
                Debug.Log($"🎮 GPU preference set to: {preference}");

                // Reload settings to get updated device selection
                yield return (T)Convert.ChangeType(StartCoroutine(LoadGPUSettings()), typeof(T));
            }
            else
            {
                Debug.LogError($"Failed to set GPU preference: {preference}");
            }
        }

        public IEnumerator<T> SelectGPUDevice<T>(string deviceId)
        {
            var requestData = new { device_id = deviceId };
            var request = BackendInterface.Instance.PostAsync<object>("gpu/select-device", requestData);
            yield return (T)Convert.ChangeType(request, typeof(T));

            if (request.IsCompleted && !request.IsFaulted)
            {
                // Find the selected device in our list
                selectedDevice = availableDevices.Find(d => d.id == deviceId);
                OnDeviceSelected?.Invoke(selectedDevice);
                Debug.Log($"🎮 GPU device selected: {selectedDevice?.name}");

                // Reload settings to confirm selection
                yield return (T)Convert.ChangeType(StartCoroutine(LoadGPUSettings()), typeof(T));
            }
            else
            {
                Debug.LogError($"Failed to select GPU device: {deviceId}");
            }
        }

        public IEnumerator<T> RunBenchmark<T>()
        {
            Debug.Log("🏃 Starting GPU benchmark...");
            var request = BackendInterface.Instance.GetAsync<object>("gpu/benchmark");
            yield return (T)Convert.ChangeType(request, typeof(T));

            if (request.IsCompleted && !request.IsFaulted)
            {
                Debug.Log($"✅ Benchmark completed: {JsonConvert.SerializeObject(request.Result)}");
                yield return (T)Convert.ChangeType(request.Result, typeof(T));
            }
            else
            {
                Debug.LogError("❌ GPU benchmark failed");
                yield return default(T);
            }
        }

        // Helper methods for UI
        public bool IsGPUAvailable()
        {
            return availableDevices.Exists(d => d.type != "cpu");
        }

        public bool IsNVIDIAAvailable()
        {
            return availableDevices.Exists(d => d.vendor.ToLower() == "nvidia" && d.type != "cpu");
        }

        public bool IsAMDAvailable()
        {
            return availableDevices.Exists(d => d.vendor.ToLower() == "amd" && d.type != "cpu");
        }

        public bool IsIntelAvailable()
        {
            return availableDevices.Exists(d => d.vendor.ToLower() == "intel" && d.type != "cpu");
        }

        public string GetCurrentDeviceInfo()
        {
            if (selectedDevice != null)
            {
                return $"{selectedDevice.vendor} {selectedDevice.name} ({selectedDevice.type})";
            }
            return "No device selected";
        }

        public string GetMemoryInfo()
        {
            if (selectedDevice != null)
            {
                return $"{selectedDevice.memory_mb}MB";
            }
            return "N/A";
        }

        public Color GetVendorColor()
        {
            if (selectedDevice == null) return Color.gray;

            switch (selectedDevice.vendor.ToLower())
            {
                case "nvidia": return Color.green;
                case "amd": return Color.red;
                case "intel": return Color.blue;
                default: return Color.gray;
            }
        }
    }
}
