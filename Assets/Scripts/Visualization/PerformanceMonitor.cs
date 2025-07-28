using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Archetype.Visualization
{
    /// <summary>
    /// Performance monitoring component - MUST inherit from MonoBehaviour
    /// Tracks FPS, memory usage, GPU performance, and visualization complexity
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        [Header("Performance Targets")]
        [SerializeField] private float targetFrameRate = 60f;
        [SerializeField] private float warningFrameRate = 30f;
        [SerializeField] private float criticalFrameRate = 15f;
        [SerializeField] private long maxMemoryUsage = 1024 * 1024 * 1024; // 1GB
        [SerializeField] private long warningMemoryUsage = 512 * 1024 * 1024; // 512MB

        [Header("Monitoring Settings")]
        [SerializeField] private bool enableProfiling = true;
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private int frameHistorySize = 60;
        [SerializeField] private bool showDebugUI = true;
        [SerializeField] private bool autoOptimize = true;

        [Header("Debug Display")]
        [SerializeField] private Vector2 debugUIPosition = new Vector2(10, 10);
        [SerializeField] private Vector2 debugUISize = new Vector2(300, 200);
        [SerializeField] private Color debugUIColor = new Color(0, 0, 0, 0.8f);

        // Performance metrics
        public float AverageFrameRate { get; private set; }
        public float CurrentFrameRate { get; private set; }
        public long CurrentMemoryUsage { get; private set; }
        public long TotalAllocatedMemory { get; private set; }
        public int ActiveNetworkCount { get; private set; }
        public int TotalNodeCount { get; private set; }
        public int TotalConnectionCount { get; private set; }
        public float GPUMemoryUsage { get; private set; }

        // Events
        public event Action<string> OnPerformanceWarning;
        public event Action<PerformanceMetrics> OnMetricsUpdated;
        public event Action OnCriticalPerformance;

        // Internal state
        private List<float> frameTimeHistory = new List<float>();
        private float lastUpdateTime = 0f;
        private long lastMemoryCheck = 0L;
        private int frameCount = 0;
        private float totalFrameTime = 0f;
        private bool isMonitoring = false;
        private GUIStyle debugStyle;

        // Performance thresholds
        private PerformanceLevel currentPerformanceLevel = PerformanceLevel.Good;

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeMonitoring();
        }

        private void Start()
        {
            StartMonitoring();
        }

        public void Update()
        {
            if (!isMonitoring) return;

            UpdateFrameRate();
            
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdatePerformanceMetrics();
                CheckPerformanceThresholds();
                lastUpdateTime = Time.time;
            }
        }

        private void OnGUI()
        {
            if (showDebugUI && debugStyle != null)
            {
                DrawDebugUI();
            }
        }

        #endregion

        #region Initialization

        private void InitializeMonitoring()
        {
            // Set target frame rate
            Application.targetFrameRate = Mathf.RoundToInt(targetFrameRate);
            QualitySettings.vSyncCount = 0;

            // Initialize frame history
            frameTimeHistory.Capacity = frameHistorySize;

            // Setup debug UI style
            debugStyle = new GUIStyle();
            debugStyle.normal.textColor = Color.white;
            debugStyle.fontSize = 12;
            debugStyle.normal.background = Texture2D.whiteTexture;

            Debug.Log("🔍 Performance Monitor initialized");
        }

        public void StartMonitoring()
        {
            isMonitoring = true;
            lastUpdateTime = Time.time;
            
            if (enableProfiling)
            {
                Profiler.enabled = true;
                Debug.Log("📊 Performance profiling enabled");
            }
        }

        public void StopMonitoring()
        {
            isMonitoring = false;
            
            if (enableProfiling)
            {
                Profiler.enabled = false;
            }
        }

        #endregion

        #region Performance Tracking

        private void UpdateFrameRate()
        {
            frameCount++;
            totalFrameTime += Time.unscaledDeltaTime;
            
            // Update frame time history
            frameTimeHistory.Add(Time.unscaledDeltaTime);
            if (frameTimeHistory.Count > frameHistorySize)
            {
                frameTimeHistory.RemoveAt(0);
            }
            
            // Calculate current FPS
            CurrentFrameRate = 1f / Time.unscaledDeltaTime;
            
            // Calculate average FPS
            if (frameTimeHistory.Count > 0)
            {
                float averageFrameTime = 0f;
                foreach (float frameTime in frameTimeHistory)
                {
                    averageFrameTime += frameTime;
                }
                averageFrameTime /= frameTimeHistory.Count;
                AverageFrameRate = 1f / averageFrameTime;
            }
        }

        private void UpdatePerformanceMetrics()
        {
            // Memory usage
            CurrentMemoryUsage = Profiler.GetTotalAllocatedMemoryLong();
            TotalAllocatedMemory = Profiler.GetTotalReservedMemoryLong();
            
            // GPU memory (approximation)
            GPUMemoryUsage = Profiler.GetAllocatedMemoryForGraphicsDriver();
            
            // Count visualization elements
            UpdateVisualizationCounts();
            
            // Create metrics object
            var metrics = new PerformanceMetrics
            {
                frameRate = AverageFrameRate,
                memoryUsage = CurrentMemoryUsage,
                gpuMemoryUsage = GPUMemoryUsage,
                nodeCount = TotalNodeCount,
                connectionCount = TotalConnectionCount,
                networkCount = ActiveNetworkCount,
                performanceLevel = currentPerformanceLevel
            };
            
            OnMetricsUpdated?.Invoke(metrics);
        }

        private void UpdateVisualizationCounts()
        {
            // Count active networks
            var visualizers = FindObjectsOfType<NeuralNetworkVisualizer>();
            ActiveNetworkCount = visualizers.Length;
            
            // Count total nodes and connections
            TotalNodeCount = 0;
            TotalConnectionCount = 0;
            
            var nodeVisualizations = FindObjectsOfType<NodeVisualization>();
            TotalNodeCount = nodeVisualizations.Length;
            
            var connectionRenderers = FindObjectsOfType<ConnectionRenderer>();
            TotalConnectionCount = connectionRenderers.Length;
        }

        #endregion

        #region Performance Analysis

        private void CheckPerformanceThresholds()
        {
            PerformanceLevel newLevel = EvaluatePerformanceLevel();
            
            if (newLevel != currentPerformanceLevel)
            {
                currentPerformanceLevel = newLevel;
                HandlePerformanceLevelChange(newLevel);
            }
            
            // Check for specific warnings
            CheckFrameRateWarnings();
            CheckMemoryWarnings();
        }

        private PerformanceLevel EvaluatePerformanceLevel()
        {
            // Evaluate based on frame rate
            if (AverageFrameRate < criticalFrameRate)
                return PerformanceLevel.Critical;
            
            if (AverageFrameRate < warningFrameRate)
                return PerformanceLevel.Poor;
            
            if (AverageFrameRate < targetFrameRate * 0.9f)
                return PerformanceLevel.Fair;
            
            // Evaluate based on memory usage
            if (CurrentMemoryUsage > maxMemoryUsage)
                return PerformanceLevel.Critical;
            
            if (CurrentMemoryUsage > warningMemoryUsage)
                return PerformanceLevel.Poor;
            
            return PerformanceLevel.Good;
        }

        private void HandlePerformanceLevelChange(PerformanceLevel newLevel)
        {
            string message = $"Performance level changed to: {newLevel}";
            Debug.Log($"⚡ {message}");
            
            switch (newLevel)
            {
                case PerformanceLevel.Critical:
                    OnCriticalPerformance?.Invoke();
                    if (autoOptimize)
                    {
                        ApplyCriticalOptimizations();
                    }
                    break;
                    
                case PerformanceLevel.Poor:
                    OnPerformanceWarning?.Invoke("Poor performance detected");
                    if (autoOptimize)
                    {
                        ApplyPerformanceOptimizations();
                    }
                    break;
                    
                case PerformanceLevel.Fair:
                    OnPerformanceWarning?.Invoke("Performance below target");
                    break;
                    
                case PerformanceLevel.Good:
                    Debug.Log("✅ Performance is good");
                    break;
            }
        }

        private void CheckFrameRateWarnings()
        {
            if (AverageFrameRate < criticalFrameRate)
            {
                OnPerformanceWarning?.Invoke($"Critical frame rate: {AverageFrameRate:F1} FPS");
            }
            else if (AverageFrameRate < warningFrameRate)
            {
                OnPerformanceWarning?.Invoke($"Low frame rate: {AverageFrameRate:F1} FPS");
            }
        }

        private void CheckMemoryWarnings()
        {
            if (CurrentMemoryUsage > maxMemoryUsage)
            {
                OnPerformanceWarning?.Invoke($"Memory usage critical: {CurrentMemoryUsage / (1024 * 1024)} MB");
            }
            else if (CurrentMemoryUsage > warningMemoryUsage)
            {
                OnPerformanceWarning?.Invoke($"High memory usage: {CurrentMemoryUsage / (1024 * 1024)} MB");
            }
        }

        #endregion

        #region Auto-Optimization

        private void ApplyPerformanceOptimizations()
        {
            Debug.Log("🔧 Applying performance optimizations...");
            
            // Reduce visualization quality
            var visualizers = FindObjectsOfType<NeuralNetworkVisualizer>();
            foreach (var visualizer in visualizers)
            {
                // Reduce max visible elements
                visualizer.maxVisibleNodes = Mathf.Max(100, visualizer.maxVisibleNodes / 2);
                visualizer.maxVisibleConnections = Mathf.Max(200, visualizer.maxVisibleConnections / 2);
                
                // Enable LOD
                visualizer.useLOD = true;
                visualizer.lodDistance *= 0.8f;
            }
            
            // Reduce connection detail
            var connectionRenderers = FindObjectsOfType<ConnectionRenderer>();
            foreach (var renderer in connectionRenderers)
            {
                renderer.SetLODLevel(1); // Force low detail
            }
            
            // Disable some animations
            var nodeVisualizations = FindObjectsOfType<NodeVisualization>();
            foreach (var node in nodeVisualizations)
            {
                // Reduce animation frequency (implementation depends on NodeVisualization)
            }
        }

        private void ApplyCriticalOptimizations()
        {
            Debug.Log("🚨 Applying critical performance optimizations...");
            
            // Drastically reduce visualization
            var visualizers = FindObjectsOfType<NeuralNetworkVisualizer>();
            foreach (var visualizer in visualizers)
            {
                visualizer.maxVisibleNodes = 50;
                visualizer.maxVisibleConnections = 100;
                visualizer.enableAnimations = false;
                visualizer.useLOD = true;
                visualizer.lodDistance = 10f;
            }
            
            // Disable data flow animations
            var connectionRenderers = FindObjectsOfType<ConnectionRenderer>();
            foreach (var renderer in connectionRenderers)
            {
                renderer.SetDataFlowEnabled(false);
                renderer.SetLODLevel(1);
            }
            
            // Force garbage collection
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Public Interface

        public void SetTargetFrameRate(float frameRate)
        {
            targetFrameRate = frameRate;
            Application.targetFrameRate = Mathf.RoundToInt(frameRate);
        }

        public void SetAutoOptimize(bool enabled)
        {
            autoOptimize = enabled;
        }

        public void ForceOptimization()
        {
            ApplyPerformanceOptimizations();
        }

        public void ResetMetrics()
        {
            frameTimeHistory.Clear();
            frameCount = 0;
            totalFrameTime = 0f;
            AverageFrameRate = 0f;
            CurrentFrameRate = 0f;
        }

        public PerformanceMetrics GetCurrentMetrics()
        {
            return new PerformanceMetrics
            {
                frameRate = AverageFrameRate,
                memoryUsage = CurrentMemoryUsage,
                gpuMemoryUsage = GPUMemoryUsage,
                nodeCount = TotalNodeCount,
                connectionCount = TotalConnectionCount,
                networkCount = ActiveNetworkCount,
                performanceLevel = currentPerformanceLevel
            };
        }

        #endregion

        #region Debug UI

        private void DrawDebugUI()
        {
            Rect debugRect = new Rect(debugUIPosition.x, debugUIPosition.y, debugUISize.x, debugUISize.y);
            
            // Background
            GUI.color = debugUIColor;
            GUI.DrawTexture(debugRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            // Performance info
            GUILayout.BeginArea(debugRect);
            GUILayout.BeginVertical();
            
            GUILayout.Label("PERFORMANCE MONITOR", debugStyle);
            GUILayout.Space(5);
            
            GUILayout.Label($"FPS: {CurrentFrameRate:F1} (Avg: {AverageFrameRate:F1})", debugStyle);
            GUILayout.Label($"Memory: {CurrentMemoryUsage / (1024 * 1024):F1} MB", debugStyle);
            GUILayout.Label($"GPU Memory: {GPUMemoryUsage / (1024 * 1024):F1} MB", debugStyle);
            GUILayout.Label($"Networks: {ActiveNetworkCount}", debugStyle);
            GUILayout.Label($"Nodes: {TotalNodeCount}", debugStyle);
            GUILayout.Label($"Connections: {TotalConnectionCount}", debugStyle);
            GUILayout.Label($"Performance: {currentPerformanceLevel}", debugStyle);
            
            // Performance indicator
            Color indicatorColor = GetPerformanceLevelColor(currentPerformanceLevel);
            GUI.color = indicatorColor;
            GUILayout.Label("●", debugStyle);
            GUI.color = Color.white;
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private Color GetPerformanceLevelColor(PerformanceLevel level)
        {
            switch (level)
            {
                case PerformanceLevel.Good: return Color.green;
                case PerformanceLevel.Fair: return Color.yellow;
                case PerformanceLevel.Poor: return Color.red;
                case PerformanceLevel.Critical: return Color.red;
                default: return Color.white;
            }
        }

        #endregion

        #region Data Structures

        [System.Serializable]
        public class PerformanceMetrics
        {
            public float frameRate;
            public long memoryUsage;
            public float gpuMemoryUsage;
            public int nodeCount;
            public int connectionCount;
            public int networkCount;
            public PerformanceLevel performanceLevel;
        }

        public enum PerformanceLevel
        {
            Good,
            Fair,
            Poor,
            Critical
        }

        #endregion
    }
}