using UnityEngine;
using UnityEngine.UIElements;
using Archetype.Backend;
using Archetype.GPU;
using Archetype.Plugins;
using Archetype.Visualization;

namespace Archetype.UI
{
    public class MainHUDController : MonoBehaviour
    {
        [Header("UI Document")]
        public UIDocument uiDocument;
        
        // UI Elements
        private VisualElement root;
        private Label backendStatus;
        private Label gpuStatus;
        private Label fpsCounter;
        private Label gpuName;
        private Label gpuMemory;
        private Label gpuUtilization;
        private Button startTrainingBtn;
        private Button stopTrainingBtn;
        private Button pauseTrainingBtn;
        private ProgressBar trainingProgress;
        private Label trainingStats;
        private ScrollView pluginList;
        
        // Layer buttons
        private Button denseLayerBtn;
        private Button convLayerBtn;
        private Button lstmLayerBtn;
        private Button dropoutLayerBtn;
        
        private bool isInitialized = false;
        
        private void Start()
        {
            InitializeUI();
            SubscribeToEvents();
        }
        
        private void InitializeUI()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
            
            root = uiDocument.rootVisualElement;
            
            // Get status elements
            backendStatus = root.Q<Label>("backend-status");
            gpuStatus = root.Q<Label>("gpu-status");
            fpsCounter = root.Q<Label>("fps-counter");
            gpuName = root.Q<Label>("gpu-name");
            gpuMemory = root.Q<Label>("gpu-memory");
            gpuUtilization = root.Q<Label>("gpu-utilization");
            
            // Get training controls
            startTrainingBtn = root.Q<Button>("start-training-btn");
            stopTrainingBtn = root.Q<Button>("stop-training-btn");
            pauseTrainingBtn = root.Q<Button>("pause-training-btn");
            trainingProgress = root.Q<ProgressBar>("training-progress");
            trainingStats = root.Q<Label>("training-stats");
            
            // Get layer buttons
            denseLayerBtn = root.Q<Button>("dense-layer-btn");
            convLayerBtn = root.Q<Button>("conv-layer-btn");
            lstmLayerBtn = root.Q<Button>("lstm-layer-btn");
            dropoutLayerBtn = root.Q<Button>("dropout-layer-btn");
            
            // Get plugin list
            pluginList = root.Q<ScrollView>("plugin-list");
            
            // Setup button callbacks
            SetupButtonCallbacks();
            
            isInitialized = true;
            Debug.Log("✅ UI initialized successfully");
        }
        
        private void SetupButtonCallbacks()
        {
            // Training controls
            startTrainingBtn?.RegisterCallback<ClickEvent>(OnStartTraining);
            stopTrainingBtn?.RegisterCallback<ClickEvent>(OnStopTraining);
            pauseTrainingBtn?.RegisterCallback<ClickEvent>(OnPauseTraining);
            
            // Layer creation buttons
            denseLayerBtn?.RegisterCallback<ClickEvent>(evt => OnCreateLayer("Dense"));
            convLayerBtn?.RegisterCallback<ClickEvent>(evt => OnCreateLayer("Convolutional"));
            lstmLayerBtn?.RegisterCallback<ClickEvent>(evt => OnCreateLayer("LSTM"));
            dropoutLayerBtn?.RegisterCallback<ClickEvent>(evt => OnCreateLayer("Dropout"));
        }
        
        private void SubscribeToEvents()
        {
            // Backend events
            if (RustInterface.Instance != null)
            {
                RustInterface.Instance.OnBackendConnected += OnBackendConnected;
                RustInterface.Instance.OnBackendError += OnBackendError;
            }
            
            // GPU events
            if (GPUManager.Instance != null)
            {
                GPUManager.Instance.OnGPUsDetected += OnGPUsDetected;
                GPUManager.Instance.OnPerformanceUpdated += OnGPUPerformanceUpdated;
                GPUManager.Instance.OnGPUError += OnGPUError;
            }
            
            // Plugin events
            if (PluginManager.Instance != null)
            {
                PluginManager.Instance.OnPluginsLoaded += OnPluginsLoaded;
                PluginManager.Instance.OnPluginStatusChanged += OnPluginStatusChanged;
            }
        }
        
        // Event Handlers
        private void OnBackendConnected()
        {
            if (backendStatus != null)
            {
                backendStatus.text = "Backend: ✅ Connected";
                backendStatus.style.color = Color.green;
            }
        }
        
        private void OnBackendError(string error)
        {
            if (backendStatus != null)
            {
                backendStatus.text = "Backend: ❌ Error";
                backendStatus.style.color = Color.red;
            }
        }
        
        private void OnGPUsDetected(System.Collections.Generic.List<GPUInfo> gpus)
        {
            if (gpuStatus != null)
            {
                gpuStatus.text = $"GPU: ✅ {gpus.Count} detected";
                gpuStatus.style.color = Color.green;
            }
            
            if (gpus.Count > 0 && gpuName != null && gpuMemory != null)
            {
                var gpu = gpus[0];
                gpuName.text = $"Name: {gpu.name}";
                gpuMemory.text = $"Memory: {gpu.memory_mb}MB";
            }
        }
        
        private void OnGPUPerformanceUpdated(GPUPerformanceMetrics metrics)
        {
            if (gpuUtilization != null)
            {
                gpuUtilization.text = $"Utilization: {metrics.gpu_utilization_percent:F1}%";
            }
        }
        
        private void OnGPUError(string error)
        {
            if (gpuStatus != null)
            {
                gpuStatus.text = "GPU: ⚠️ Error";
                gpuStatus.style.color = Color.yellow;
            }
        }
        
        private void OnPluginsLoaded(System.Collections.Generic.List<PluginInfo> plugins)
        {
            UpdatePluginList(plugins);
        }
        
        private void OnPluginStatusChanged(PluginInfo plugin)
        {
            // Update specific plugin status in the list
            UpdatePluginList(PluginManager.Instance.AvailablePlugins);
        }
        
        private void UpdatePluginList(System.Collections.Generic.List<PluginInfo> plugins)
        {
            if (pluginList == null) return;
            
            pluginList.Clear();
            
            foreach (var plugin in plugins)
            {
                var pluginItem = new VisualElement();
                pluginItem.AddToClassList("plugin-item");
                
                var pluginLabel = new Label($"{plugin.display_name} v{plugin.version}");
                pluginLabel.AddToClassList("plugin-label");
                
                var statusIcon = new Label(plugin.is_loaded ? "✅" : "⭕");
                statusIcon.AddToClassList("plugin-status");
                
                pluginItem.Add(pluginLabel);
                pluginItem.Add(statusIcon);
                pluginList.Add(pluginItem);
            }
        }
        
        // Training Control Handlers
        private async void OnStartTraining(ClickEvent evt)
        {
            try
            {
                await RustInterface.Instance.CallRustCommand<object>("start_training", new
                {
                    epochs = 100,
                    learning_rate = 0.001f,
                    batch_size = 32
                });
                
                Debug.Log("🚀 Training started");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to start training: {e.Message}");
            }
        }
        
        private async void OnStopTraining(ClickEvent evt)
        {
            try
            {
                await RustInterface.Instance.CallRustCommand<object>("stop_training", null);
                Debug.Log("⏹️ Training stopped");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to stop training: {e.Message}");
            }
        }
        
        private async void OnPauseTraining(ClickEvent evt)
        {
            try
            {
                await RustInterface.Instance.CallRustCommand<object>("pause_training", null);
                Debug.Log("⏸️ Training paused");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to pause training: {e.Message}");
            }
        }
        
        // Layer Creation Handler
        private void OnCreateLayer(string layerType)
        {
            Debug.Log($"🔧 Creating {layerType} layer");
            
            // This would integrate with NetworkRenderer to add visual layer
            var networkRenderer = FindFirstObjectByType<NetworkRenderer>();
            if (networkRenderer != null)
            {
                networkRenderer.CreateLayer(layerType);
            }
        }
        
        // Update Loop
        private void Update()
        {
            if (!isInitialized) return;
            
            // Update FPS counter
            if (fpsCounter != null)
            {
                float fps = 1.0f / Time.unscaledDeltaTime;
                fpsCounter.text = $"FPS: {fps:F0}";
                
                // Color code FPS
                if (fps >= 55)
                    fpsCounter.style.color = Color.green;
                else if (fps >= 30)
                    fpsCounter.style.color = Color.yellow;
                else
                    fpsCounter.style.color = Color.red;
            }
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (RustInterface.Instance != null)
            {
                RustInterface.Instance.OnBackendConnected -= OnBackendConnected;
                RustInterface.Instance.OnBackendError -= OnBackendError;
            }
            
            if (GPUManager.Instance != null)
            {
                GPUManager.Instance.OnGPUsDetected -= OnGPUsDetected;
                GPUManager.Instance.OnPerformanceUpdated -= OnGPUPerformanceUpdated;
                GPUManager.Instance.OnGPUError -= OnGPUError;
            }
            
            if (PluginManager.Instance != null)
            {
                PluginManager.Instance.OnPluginsLoaded -= OnPluginsLoaded;
                PluginManager.Instance.OnPluginStatusChanged -= OnPluginStatusChanged;
            }
        }
    }
}