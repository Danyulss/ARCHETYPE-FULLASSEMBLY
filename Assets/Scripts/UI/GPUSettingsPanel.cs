using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Archetype.Backend;
using System;
using Mono.Cecil.Cil;

namespace Archetype.UI
{
    public class GPUSettingsPanel : MonoBehaviour
    {
        [Header("UI Components")]
        public TMP_Dropdown preferenceDropdown;
        public TMP_Dropdown deviceDropdown;
        public Button benchmarkButton;
        public TMP_Text currentDeviceText;
        public TMP_Text memoryText;
        public TMP_Text performanceText;
        public TMP_Text benchmarkResultText;
        public Image vendorColorIndicator;
        //public GameObject loadingIndicator;

        [Header("Device List")]
        public Transform deviceListParent;
        public GameObject deviceItemPrefab;

        private bool isUpdating = false;

        private void Start()
        {
            InitializeUI();
            StartCoroutine(WaitForManagerAndSubscribe<GPUSettingsResponse>());
        }

        private IEnumerator<T> WaitForManagerAndSubscribe<T>()
        {
            // Wait until GPUSettingsManager is initialized
            while (GPUSettingsManager.Instance == null)
            {
                yield return default(T);
            }
            
            // Now safely subscribe to events
            GPUSettingsManager.Instance.OnSettingsLoaded += OnSettingsLoaded;
            GPUSettingsManager.Instance.OnPreferenceChanged += OnPreferenceChanged;
            GPUSettingsManager.Instance.OnDeviceSelected += OnDeviceSelected;
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (GPUSettingsManager.Instance != null)
            {
                GPUSettingsManager.Instance.OnSettingsLoaded -= OnSettingsLoaded;
                GPUSettingsManager.Instance.OnPreferenceChanged -= OnPreferenceChanged;
                GPUSettingsManager.Instance.OnDeviceSelected -= OnDeviceSelected;
            }
        }

        private void InitializeUI()
        {
            // Setup dropdown listeners
            preferenceDropdown.onValueChanged.AddListener(OnPreferenceDropdownChanged);
            deviceDropdown.onValueChanged.AddListener(OnDeviceDropdownChanged);
            benchmarkButton.onClick.AddListener(OnBenchmarkClicked);

            // Show loading initially
            //ShowLoading(true);
        }

        private void OnSettingsLoaded(GPUSettingsResponse settings)
        {
            UpdatePreferenceDropdown(settings.available_preferences);
            UpdateDeviceDropdown(settings.available_devices);
            UpdateCurrentDeviceInfo(settings.current_device);
            UpdateDeviceList(settings.available_devices);
            //ShowLoading(false);
        }

        private void UpdatePreferenceDropdown(List<GPUPreference> preferences)
        {
            preferenceDropdown.ClearOptions();
            
            var options = preferences.Where(p => p.available)
                                   .Select(p => p.name)
                                   .ToList();
            
            preferenceDropdown.AddOptions(options);
            
            // Set current selection
            var currentPref = preferences.FirstOrDefault(p => p.id == GPUSettingsManager.Instance.currentPreference);
            if (currentPref != null)
            {
                var index = preferences.Where(p => p.available).ToList().FindIndex(p => p.id == currentPref.id);
                if (index >= 0)
                {
                    preferenceDropdown.SetValueWithoutNotify(index);
                }
            }
        }

        private void UpdateDeviceDropdown(List<GPUDevice> devices)
        {
            deviceDropdown.ClearOptions();
            
            var options = devices.Select(d => $"{d.vendor} {d.name} ({d.type})")
                                .ToList();
            
            deviceDropdown.AddOptions(options);
            
            // Set current selection
            var selectedDevice = GPUSettingsManager.Instance.selectedDevice;
            if (selectedDevice != null)
            {
                var index = devices.FindIndex(d => d.id == selectedDevice.id);
                if (index >= 0)
                {
                    deviceDropdown.SetValueWithoutNotify(index);
                }
            }
        }

        private void UpdateCurrentDeviceInfo(GPUDevice device)
        {
            if (device != null)
            {
                currentDeviceText.text = $"{device.vendor} {device.name}";
                memoryText.text = $"{device.memory_mb:N0} MB";
                performanceText.text = $"Score: {device.performance_score}";
                
                // Update vendor color indicator
                vendorColorIndicator.color = GetVendorColor(device.vendor);
            }
            else
            {
                currentDeviceText.text = "No device selected";
                memoryText.text = "N/A";
                performanceText.text = "N/A";
                vendorColorIndicator.color = Color.gray;
            }
        }

        private void UpdateDeviceList(List<GPUDevice> devices)
        {
            // Clear existing items
            foreach (Transform child in deviceListParent)
            {
                Destroy(child.gameObject);
            }

            // Create device items
            foreach (var device in devices)
            {
                var item = Instantiate(deviceItemPrefab, deviceListParent);
                var deviceItem = item.GetComponent<GPUDeviceItem>();
                
                if (deviceItem != null)
                {
                    deviceItem.SetupDevice(device);
                    deviceItem.OnDeviceSelected += (selectedDevice) => {
                        StartCoroutine(GPUSettingsManager.Instance.SelectGPUDevice<System.Threading.Tasks.Task<GPUSettingsResponse>>(selectedDevice.id));
                    };
                }
            }
        }

        public void OnPreferenceDropdownChanged(int index)
        {
            if (isUpdating) return;

            var preferences = GPUSettingsManager.Instance.availablePreferences
                                                        .Where(p => p.available)
                                                        .ToList();
            
            if (index >= 0 && index < preferences.Count)
            {
                var selectedPreference = preferences[index];
                StartCoroutine(GPUSettingsManager.Instance.SetGPUPreference<System.Threading.Tasks.Task<GPUSettingsResponse>>(selectedPreference.id));
            }
        }

        public void OnDeviceDropdownChanged(int index)
        {
            if (isUpdating) return;

            var devices = GPUSettingsManager.Instance.availableDevices;
            
            if (index >= 0 && index < devices.Count)
            {
                var selectedDevice = devices[index];
                StartCoroutine(GPUSettingsManager.Instance.SelectGPUDevice<System.Threading.Tasks.Task<GPUSettingsResponse>>(selectedDevice.id));
            }
        }

        public void OnBenchmarkClicked()
        {
            benchmarkButton.interactable = false;
            benchmarkButton.GetComponentInChildren<TMP_Text>().text = "Running...";
            benchmarkResultText.text = "Benchmark in progress...";
            
            StartCoroutine(RunBenchmarkCoroutine<System.Threading.Tasks.Task<object>>());
        }

        private IEnumerator<T> RunBenchmarkCoroutine<T>()
        {
            var result = StartCoroutine(GPUSettingsManager.Instance.RunBenchmark<System.Threading.Tasks.Task<object>>());
            yield return (T) Convert.ChangeType(result, typeof(T));
            
            benchmarkButton.interactable = true;
            benchmarkButton.GetComponentInChildren<TMP_Text>().text = "Run Benchmark";
            
            if (result != null)
            {
                // Parse benchmark results and display
                benchmarkResultText.text = "Benchmark completed! Check console for details.";
            }
            else
            {
                benchmarkResultText.text = "Benchmark failed.";
            }
        }

        private void OnPreferenceChanged(string preference)
        {
            isUpdating = true;
            // Update UI to reflect preference change
            Debug.Log($"GPU preference changed to: {preference}");
            isUpdating = false;
        }

        private void OnDeviceSelected(GPUDevice device)
        {
            isUpdating = true;
            UpdateCurrentDeviceInfo(device);
            Debug.Log($"GPU device selected: {device.name}");
            isUpdating = false;
        }

        /* private void ShowLoading(bool show)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(show);
            }
            
            // Disable interactive elements while loading
            preferenceDropdown.interactable = !show;
            deviceDropdown.interactable = !show;
            benchmarkButton.interactable = !show;
        } */

        public void Show()
        {
            this.gameObject.SetActive(!this.gameObject.activeSelf);
        }

        private Color GetVendorColor(string vendor)
        {
            switch (vendor.ToLower())
            {
                case "nvidia": return new Color(0.3f, 0.8f, 0.3f); // Green
                case "amd": return new Color(0.8f, 0.3f, 0.3f);   // Red
                case "intel": return new Color(0.3f, 0.3f, 0.8f); // Blue
                default: return Color.gray;
            }
        }
    }

    // Helper class for individual device items in the list
    public class GPUDeviceItem : MonoBehaviour
    {
        [Header("UI Components")]
        public TMP_Text deviceNameText;
        public TMP_Text deviceStatsText;
        public Image vendorIndicator;
        public Button selectButton;
        public GameObject selectedIndicator;

        public System.Action<GPUDevice> OnDeviceSelected;
        private GPUDevice device;

        public void SetupDevice(GPUDevice deviceData)
        {
            device = deviceData;
            
            deviceNameText.text = $"{device.vendor} {device.name}";
            deviceStatsText.text = $"{device.type.ToUpper()} • {device.memory_mb:N0}MB • Score: {device.performance_score}";
            
            vendorIndicator.color = GetVendorColor(device.vendor);
            selectedIndicator.SetActive(device.is_selected);
            
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => OnDeviceSelected?.Invoke(device));
        }

        private Color GetVendorColor(string vendor)
        {
            switch (vendor.ToLower())
            {
                case "nvidia": return new Color(0.3f, 0.8f, 0.3f);
                case "amd": return new Color(0.8f, 0.3f, 0.3f);
                case "intel": return new Color(0.3f, 0.3f, 0.8f);
                default: return Color.gray;
            }
        }
    }
}