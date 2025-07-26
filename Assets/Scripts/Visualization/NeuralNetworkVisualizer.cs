using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace Archetype.Visualization
{
    /// <summary>
    /// Main neural network visualization manager
    /// </summary>
    public class NeuralNetworkVisualizer : MonoBehaviour
    {
        public static NeuralNetworkVisualizer Instance { get; private set; }

        [Header("Visualization Settings")]
        public Camera mainCamera;
        public Transform networkContainer;
        public float defaultScale = 1.0f;
        public bool enableAnimations = true;
        public float animationSpeed = 1.0f;

        [Header("Performance")]
        public int maxVisibleNodes = 1000;
        public int maxVisibleConnections = 5000;
        public bool useLOD = true;
        public float lodDistance = 50.0f;

        [Header("Materials")]
        public Material nodeMaterial;
        public Material connectionMaterial;
        public Material highlightMaterial;
        public Material errorMaterial;

        // Internal state
        private Dictionary<string, NetworkVisualization> activeNetworks = new Dictionary<string, NetworkVisualization>();
        private PerformanceMonitor performanceMonitor;
        private VisualizationSettings settings;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeVisualizer();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetupCamera();
            InitializePerformanceMonitoring();
        }

        private void Update()
        {
            UpdateActiveNetworks();
            performanceMonitor?.Update();

            if (useLOD)
                UpdateLOD();
        }

        #endregion

        #region Initialization

        private void InitializeVisualizer()
        {
            settings = new VisualizationSettings();
            performanceMonitor = gameObject.AddComponent<PerformanceMonitor>();

            if (networkContainer == null)
            {
                var container = new GameObject("NetworkContainer");
                networkContainer = container.transform;
                networkContainer.SetParent(transform);
            }

            Debug.Log("🎨 Neural Network Visualizer initialized");
        }

        private void SetupCamera()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null)
            {
                // Add camera controller if not present
                if (mainCamera.GetComponent<CameraController>() == null)
                {
                    mainCamera.gameObject.AddComponent<CameraController>();
                }
            }
        }

        private void InitializePerformanceMonitoring()
        {
            performanceMonitor.OnPerformanceWarning += HandlePerformanceWarning;
            performanceMonitor.SetTargetFrameRate(60);
        }

        #endregion

        #region Network Visualization

        /// <summary>
        /// Create visualization for a neural network model
        /// </summary>
        public async Task<string> VisualizeNetwork(string modelId, Vector3 position = default)
        {
            try
            {
                // Get model information from backend
                var modelInfo = await Backend.API.ModelAPI.GetModel(modelId);

                // Create network visualization
                var networkViz = CreateNetworkVisualization(modelInfo, position);
                activeNetworks[modelId] = networkViz;

                Debug.Log($"✅ Created visualization for model: {modelId}");
                return modelId;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to visualize network {modelId}: {e.Message}");
                return null;
            }
        }

        private NetworkVisualization CreateNetworkVisualization(Backend.API.ModelResponse modelInfo, Vector3 position)
        {
            var vizGO = new GameObject($"Network_{modelInfo.name}");
            vizGO.transform.SetParent(networkContainer);
            vizGO.transform.position = position;

            var networkViz = vizGO.AddComponent<NetworkVisualization>();
            networkViz.Initialize(modelInfo, settings);

            return networkViz;
        }

        /// <summary>
        /// Update visualization with training data
        /// </summary>
        public void UpdateNetworkVisualization(string modelId, Dictionary<string, float> metrics)
        {
            if (activeNetworks.TryGetValue(modelId, out var networkViz))
            {
                networkViz.UpdateMetrics(metrics);
            }
        }

        /// <summary>
        /// Remove network visualization
        /// </summary>
        public void RemoveNetworkVisualization(string modelId)
        {
            if (activeNetworks.TryGetValue(modelId, out var networkViz))
            {
                if (networkViz != null)
                    DestroyImmediate(networkViz.gameObject);

                activeNetworks.Remove(modelId);
                Debug.Log($"🗑️ Removed visualization for model: {modelId}");
            }
        }

        #endregion

        #region Performance Management

        private void UpdateActiveNetworks()
        {
            foreach (var networkViz in activeNetworks.Values)
            {
                if (networkViz != null && networkViz.enabled)
                {
                    networkViz.UpdateVisualization();
                }
            }
        }

        private void UpdateLOD()
        {
            if (mainCamera == null) return;

            Vector3 cameraPos = mainCamera.transform.position;

            foreach (var networkViz in activeNetworks.Values)
            {
                if (networkViz != null)
                {
                    float distance = Vector3.Distance(cameraPos, networkViz.transform.position);
                    networkViz.SetLODLevel(distance > lodDistance ? 1 : 0);
                }
            }
        }

        private void HandlePerformanceWarning(string warning)
        {
            Debug.LogWarning($"⚠️ Performance Warning: {warning}");

            // Automatically reduce quality if performance is poor
            if (performanceMonitor.AverageFrameRate < 30f)
            {
                ReduceVisualizationQuality();
            }
        }

        private void ReduceVisualizationQuality()
        {
            // Reduce LOD distance
            lodDistance *= 0.8f;

            // Reduce max visible elements
            maxVisibleNodes = Mathf.Max(100, maxVisibleNodes / 2);
            maxVisibleConnections = Mathf.Max(200, maxVisibleConnections / 2);

            // Disable some animations
            if (enableAnimations)
            {
                animationSpeed *= 0.5f;
            }

            Debug.Log("📉 Reduced visualization quality to maintain performance");
        }

        #endregion

        #region Public Interface

        public void SetVisualizationSettings(VisualizationSettings newSettings)
        {
            settings = newSettings;

            // Apply to all active networks
            foreach (var networkViz in activeNetworks.Values)
            {
                networkViz?.ApplySettings(settings);
            }
        }

        public void HighlightNetwork(string modelId, bool highlight = true)
        {
            if (activeNetworks.TryGetValue(modelId, out var networkViz))
            {
                networkViz.SetHighlighted(highlight);
            }
        }

        public void FocusOnNetwork(string modelId)
        {
            if (activeNetworks.TryGetValue(modelId, out var networkViz))
            {
                if (mainCamera != null)
                {
                    var cameraController = mainCamera.GetComponent<Camera>();
                    cameraController?.FocusOn(networkViz.transform.position);
                }
            }
        }

        public List<string> GetActiveNetworks()
        {
            return new List<string>(activeNetworks.Keys);
        }

        #endregion
    }

    #region Settings and Configuration

    [Serializable]
    public class VisualizationSettings
    {
        [Header("Node Settings")]
        public float nodeScale = 1.0f;
        public float nodeSpacing = 2.0f;
        public bool showNodeLabels = false;
        public bool showActivationValues = true;

        [Header("Connection Settings")]
        public float connectionThickness = 0.1f;
        public bool showWeights = false;
        public bool colorByWeight = true;
        public float connectionAlpha = 0.7f;

        [Header("Layer Settings")]
        public float layerSpacing = 5.0f;
        public bool show3DLayers = true;
        public bool showLayerLabels = true;

        [Header("Animation Settings")]
        public bool enableDataFlow = true;
        public float dataFlowSpeed = 2.0f;
        public bool enableTrainingAnimation = true;
        public float trainingAnimationSpeed = 1.0f;

        [Header("Colors")]
        public Color inputNodeColor = Color.green;
        public Color hiddenNodeColor = Color.blue;
        public Color outputNodeColor = Color.red;
        public Color positiveWeightColor = Color.cyan;
        public Color negativeWeightColor = Color.magenta;
        public Color highlightColor = Color.yellow;
    }

    #endregion
}