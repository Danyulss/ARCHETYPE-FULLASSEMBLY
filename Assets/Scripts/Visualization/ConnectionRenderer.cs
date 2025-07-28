using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Archetype.Visualization
{
    /// <summary>
    /// Connection renderer for neural network weights - MUST inherit from MonoBehaviour
    /// for Unity component attachment. Renders connections between neural nodes with
    /// real-time weight visualization and data flow animation.
    /// </summary>
    public class ConnectionRenderer : MonoBehaviour
    {
        [Header("Connection Properties")]
        [SerializeField] private LayerVisualization fromLayer;
        [SerializeField] private LayerVisualization toLayer;
        [SerializeField] private float[,] weights;
        [SerializeField] private bool showAllConnections = true;
        [SerializeField] private float weightThreshold = 0.1f;

        [Header("Visual Settings")]
        [SerializeField] private Material connectionMaterial;
        [SerializeField] private Material positiveWeightMaterial;
        [SerializeField] private Material negativeWeightMaterial;
        [SerializeField] private float baseLineWidth = 0.02f;
        [SerializeField] private float maxLineWidth = 0.1f;
        [SerializeField] private int lineResolution = 20;
        [SerializeField] private AnimationCurve connectionCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Animation")]
        [SerializeField] private bool enableDataFlow = true;
        [SerializeField] private float dataFlowSpeed = 2f;
        [SerializeField] private Material dataFlowMaterial;
        [SerializeField] private float particleSize = 0.05f;
        [SerializeField] private int maxParticlesPerConnection = 3;

        [Header("Performance")]
        [SerializeField] private bool useLOD = true;
        [SerializeField] private float lodDistance = 50f;
        [SerializeField] private int maxVisibleConnections = 1000;
        [SerializeField] private bool enableBatching = true;

        // Internal state
        private List<ConnectionLine> connections = new List<ConnectionLine>();
        private List<DataFlowParticle> dataParticles = new List<DataFlowParticle>();
        private Camera mainCamera;
        private bool isInitialized = false;
        private int currentLODLevel = 0;
        private MaterialPropertyBlock propertyBlock;
        private Mesh connectionMesh;
        private Renderer connectionRenderer;

        // Performance optimization
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int AlphaProperty = Shader.PropertyToID("_Alpha");
        private static readonly int ThicknessProperty = Shader.PropertyToID("_Thickness");

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindFirstObjectByType<Camera>();
            }
        }

        private void Update()
        {
            if (!isInitialized) return;

            UpdateLOD();
            UpdateConnections();
            UpdateDataFlow();
        }

        private void LateUpdate()
        {
            if (!isInitialized) return;

            UpdateConnectionVisibility();
        }

        #endregion

        #region Initialization

        public void Initialize(LayerVisualization from, LayerVisualization to, VisualizationSettings settings)
        {
            fromLayer = from;
            toLayer = to;

            if (settings != null)
            {
                enableDataFlow = settings.enableTrainingAnimation;
                dataFlowSpeed = settings.trainingAnimationSpeed;
                baseLineWidth = settings.connectionThickness;
                useLOD = settings.useLOD;
            }

            InitializeComponents();
            CreateConnections();
            
            isInitialized = true;
            Debug.Log($"✅ ConnectionRenderer initialized: {from.name} → {to.name}");
        }

        private void InitializeComponents()
        {
            propertyBlock = new MaterialPropertyBlock();
            
            // Create connection mesh for efficient rendering
            connectionMesh = CreateConnectionMesh();
            
            // Set default materials if not assigned
            if (connectionMaterial == null)
            {
                connectionMaterial = CreateDefaultConnectionMaterial();
            }
            
            if (positiveWeightMaterial == null)
            {
                positiveWeightMaterial = CreatePositiveWeightMaterial();
            }
            
            if (negativeWeightMaterial == null)
            {
                negativeWeightMaterial = CreateNegativeWeightMaterial();
            }
        }

        #endregion

        #region Connection Creation

        private void CreateConnections()
        {
            if (fromLayer == null || toLayer == null)
            {
                Debug.LogError("❌ ConnectionRenderer: Source or target layer is null");
                return;
            }

            connections.Clear();

            // Get nodes from layers
            var fromNodes = GetLayerNodes(fromLayer);
            var toNodes = GetLayerNodes(toLayer);

            if (fromNodes.Count == 0 || toNodes.Count == 0)
            {
                Debug.LogWarning("⚠️ ConnectionRenderer: One or both layers have no nodes");
                return;
            }

            // Create connections between all nodes
            for (int i = 0; i < fromNodes.Count; i++)
            {
                for (int j = 0; j < toNodes.Count; j++)
                {
                    if (ShouldCreateConnection(i, j))
                    {
                        var connection = CreateConnection(fromNodes[i], toNodes[j], i, j);
                        connections.Add(connection);
                    }
                }
            }

            Debug.Log($"📊 Created {connections.Count} connections between layers");
        }

        private ConnectionLine CreateConnection(Transform fromNode, Transform toNode, int fromIndex, int toIndex)
        {
            var connectionObj = new GameObject($"Connection_{fromIndex}_{toIndex}");
            connectionObj.transform.SetParent(transform);

            var lineRenderer = connectionObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer);

            var connection = new ConnectionLine
            {
                gameObject = connectionObj,
                lineRenderer = lineRenderer,
                fromNode = fromNode,
                toNode = toNode,
                fromIndex = fromIndex,
                toIndex = toIndex,
                weight = GetWeight(fromIndex, toIndex),
                isVisible = true
            };

            UpdateConnectionVisuals(connection);
            return connection;
        }

        private void ConfigureLineRenderer(LineRenderer lineRenderer)
        {
            lineRenderer.material = connectionMaterial;
            lineRenderer.startWidth = baseLineWidth;
            lineRenderer.endWidth = baseLineWidth;
            lineRenderer.positionCount = lineResolution;
            lineRenderer.useWorldSpace = true;
            lineRenderer.sortingOrder = -1; // Behind nodes
            
            // Enable GPU instancing for better performance
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
        }

        private bool ShouldCreateConnection(int fromIndex, int toIndex)
        {
            if (!showAllConnections)
            {
                float weight = GetWeight(fromIndex, toIndex);
                return Mathf.Abs(weight) >= weightThreshold;
            }
            
            return connections.Count < maxVisibleConnections;
        }

        #endregion

        #region Connection Updates

        private void UpdateConnections()
        {
            for (int i = 0; i < connections.Count; i++)
            {
                var connection = connections[i];
                if (connection.isVisible && connection.lineRenderer != null)
                {
                    UpdateConnectionPath(connection);
                    UpdateConnectionVisuals(connection);
                }
            }
        }

        private void UpdateConnectionPath(ConnectionLine connection)
        {
            if (connection.fromNode == null || connection.toNode == null) return;

            Vector3 startPos = connection.fromNode.position;
            Vector3 endPos = connection.toNode.position;
            
            // Create curved path
            Vector3[] positions = new Vector3[lineResolution];
            
            for (int i = 0; i < lineResolution; i++)
            {
                float t = (float)i / (lineResolution - 1);
                
                // Apply curve to the interpolation
                float curveT = connectionCurve.Evaluate(t);
                Vector3 basePos = Vector3.Lerp(startPos, endPos, curveT);
                
                // Add curve offset
                Vector3 direction = (endPos - startPos).normalized;
                Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
                
                float curveHeight = Mathf.Sin(t * Mathf.PI) * 0.5f;
                basePos += perpendicular * curveHeight;
                
                positions[i] = basePos;
            }
            
            connection.lineRenderer.SetPositions(positions);
        }

        private void UpdateConnectionVisuals(ConnectionLine connection)
        {
            float weight = connection.weight;
            
            // Update material based on weight sign
            Material targetMaterial = weight >= 0 ? positiveWeightMaterial : negativeWeightMaterial;
            connection.lineRenderer.material = targetMaterial;
            
            // Update line width based on weight magnitude
            float thickness = Mathf.Lerp(baseLineWidth, maxLineWidth, Mathf.Abs(weight));
            connection.lineRenderer.startWidth = thickness;
            connection.lineRenderer.endWidth = thickness;
            
            // Update color intensity based on weight
            Color baseColor = weight >= 0 ? Color.green : Color.red;
            Color finalColor = Color.Lerp(Color.gray, baseColor, Mathf.Abs(weight));
            finalColor.a = Mathf.Clamp01(Mathf.Abs(weight) * 2f); // Transparency based on strength
            
            // Apply color using material property block
            propertyBlock.SetColor(ColorProperty, finalColor);
            connection.lineRenderer.SetPropertyBlock(propertyBlock);
        } 

        public void ApplySettings(VisualizationSettings settings)
        {
            if (settings == null) return;

            // Apply scale settings
            baseLineWidth = settings.connectionThickness;
            transform.localScale = Vector3.one * baseLineWidth;

            // Apply animation settings
            enableDataFlow = settings.enableDataFlow;
            dataFlowSpeed = settings.dataFlowSpeed;

            // Apply visual settings
            if (settings.connectionMaterial != null && connectionRenderer != null)
            {
                connectionRenderer.material = settings.connectionMaterial;
            }

            Debug.Log($"📐 Applied settings to connection");
        }

        #endregion

        #region Data Flow Animation

        private void UpdateDataFlow()
        {
            if (!enableDataFlow) return;

            // Update existing particles
            for (int i = dataParticles.Count - 1; i >= 0; i--)
            {
                var particle = dataParticles[i];
                UpdateParticle(particle);

                if (particle.progress >= 1f)
                {
                    DestroyParticle(particle);
                    dataParticles.RemoveAt(i);
                }
            }

            // Spawn new particles periodically
            if (Time.time - lastParticleSpawn > 1f / dataFlowSpeed)
            {
                SpawnDataFlowParticles();
                lastParticleSpawn = Time.time;
            }
        }

        private float lastParticleSpawn = 0f;

        private void SpawnDataFlowParticles()
        {
            foreach (var connection in connections)
            {
                if (!connection.isVisible || Mathf.Abs(connection.weight) < weightThreshold) continue;
                
                int particleCount = Mathf.Min(
                    Mathf.RoundToInt(Mathf.Abs(connection.weight) * maxParticlesPerConnection),
                    maxParticlesPerConnection
                );
                
                for (int i = 0; i < particleCount; i++)
                {
                    SpawnParticle(connection);
                }
            }
        }

        private void SpawnParticle(ConnectionLine connection)
        {
            var particleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            particleObj.transform.SetParent(transform);
            particleObj.transform.localScale = Vector3.one * particleSize;
            
            // Configure particle
            var renderer = particleObj.GetComponent<Renderer>();
            if (dataFlowMaterial != null)
            {
                renderer.material = dataFlowMaterial;
            }
            
            // Remove collider for performance
            DestroyImmediate(particleObj.GetComponent<Collider>());
            
            var particle = new DataFlowParticle
            {
                gameObject = particleObj,
                connection = connection,
                progress = 0f,
                speed = dataFlowSpeed * (0.8f + UnityEngine.Random.Range(0f, 0.4f)) // Slight randomization
            };
            
            dataParticles.Add(particle);
        }

        private void UpdateParticle(DataFlowParticle particle)
        {
            particle.progress += Time.deltaTime * particle.speed;
            particle.progress = Mathf.Clamp01(particle.progress);
            
            // Get position along connection path
            Vector3 position = GetPositionAlongConnection(particle.connection, particle.progress);
            particle.gameObject.transform.position = position;
            
            // Update particle appearance based on progress
            float alpha = 1f - particle.progress; // Fade out as it approaches end
            var renderer = particle.gameObject.GetComponent<Renderer>();
            Color color = renderer.material.color;
            color.a = alpha;
            
            propertyBlock.SetColor(ColorProperty, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private Vector3 GetPositionAlongConnection(ConnectionLine connection, float t)
        {
            if (connection.lineRenderer == null) return Vector3.zero;
            
            Vector3[] positions = new Vector3[connection.lineRenderer.positionCount];
            connection.lineRenderer.GetPositions(positions);
            
            float exactIndex = t * (positions.Length - 1);
            int index = Mathf.FloorToInt(exactIndex);
            float fraction = exactIndex - index;
            
            if (index >= positions.Length - 1) return positions[positions.Length - 1];
            if (index < 0) return positions[0];
            
            return Vector3.Lerp(positions[index], positions[index + 1], fraction);
        }

        private void DestroyParticle(DataFlowParticle particle)
        {
            if (particle.gameObject != null)
            {
                DestroyImmediate(particle.gameObject);
            }
        }

        #endregion

        #region Performance Optimization

        private void UpdateLOD()
        {
            if (!useLOD || mainCamera == null) return;

            float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
            int newLODLevel = distance > lodDistance ? 1 : 0;
            
            if (newLODLevel != currentLODLevel)
            {
                currentLODLevel = newLODLevel;
                ApplyLOD();
            }
        }

        private void ApplyLOD()
        {
            switch (currentLODLevel)
            {
                case 0: // High detail
                    SetConnectionVisibility(true);
                    enableDataFlow = true;
                    break;
                    
                case 1: // Low detail
                    SetConnectionVisibility(false);
                    enableDataFlow = false;
                    ClearDataParticles();
                    break;
            }
        }

        private void UpdateConnectionVisibility()
        {
            foreach (var connection in connections)
            {
                bool shouldBeVisible = ShouldConnectionBeVisible(connection);
                
                if (connection.isVisible != shouldBeVisible)
                {
                    connection.isVisible = shouldBeVisible;
                    connection.gameObject.SetActive(shouldBeVisible);
                }
            }
        }

        private bool ShouldConnectionBeVisible(ConnectionLine connection)
        {
            if (currentLODLevel > 0) return false;
            if (!showAllConnections && Mathf.Abs(connection.weight) < weightThreshold) return false;
            
            return true;
        }

        private void SetConnectionVisibility(bool visible)
        {
            foreach (var connection in connections)
            {
                connection.gameObject.SetActive(visible);
            }
        }

        private void ClearDataParticles()
        {
            foreach (var particle in dataParticles)
            {
                DestroyParticle(particle);
            }
            dataParticles.Clear();
        }

        #endregion

        #region Public Interface

        public void UpdateWeights(float[,] newWeights)
        {
            weights = newWeights;
            
            // Update existing connections with new weights
            foreach (var connection in connections)
            {
                connection.weight = GetWeight(connection.fromIndex, connection.toIndex);
            }
        }

        public void SetDataFlowEnabled(bool enabled)
        {
            enableDataFlow = enabled;
            
            if (!enabled)
            {
                ClearDataParticles();
            }
        }

        public void SetWeightThreshold(float threshold)
        {
            weightThreshold = threshold;
            
            // Update connection visibility based on new threshold
            foreach (var connection in connections)
            {
                bool shouldBeVisible = ShouldConnectionBeVisible(connection);
                connection.isVisible = shouldBeVisible;
                connection.gameObject.SetActive(shouldBeVisible);
            }
        }

        public void SetLODLevel(int level)
        {
            currentLODLevel = level;
            ApplyLOD();
        }

        public void HighlightConnection(int fromIndex, int toIndex, bool highlight)
        {
            var connection = connections.Find(c => c.fromIndex == fromIndex && c.toIndex == toIndex);
            if (connection != null)
            {
                Color highlightColor = highlight ? Color.yellow : GetWeightColor(connection.weight);
                propertyBlock.SetColor(ColorProperty, highlightColor);
                connection.lineRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        #endregion

        #region Utility Methods

        private float GetWeight(int fromIndex, int toIndex)
        {
            if (weights == null) return UnityEngine.Random.Range(-1f, 1f); // Random weight for demo
            
            if (fromIndex >= 0 && fromIndex < weights.GetLength(0) && 
                toIndex >= 0 && toIndex < weights.GetLength(1))
            {
                return weights[fromIndex, toIndex];
            }
            
            return 0f;
        }

        private Color GetWeightColor(float weight)
        {
            return weight >= 0 ? Color.green : Color.red;
        }

        private List<Transform> GetLayerNodes(LayerVisualization layer)
        {
            var nodes = new List<Transform>();
            
            // Get all child transforms that represent nodes
            for (int i = 0; i < layer.transform.childCount; i++)
            {
                var child = layer.transform.GetChild(i);
                if (child.name.StartsWith("Node_"))
                {
                    nodes.Add(child);
                }
            }
            
            return nodes;
        }

        private Mesh CreateConnectionMesh()
        {
            // Create a simple quad mesh for connection rendering
            var mesh = new Mesh();
            
            Vector3[] vertices = {
                new Vector3(-0.5f, 0, 0),
                new Vector3(0.5f, 0, 0),
                new Vector3(0.5f, 1, 0),
                new Vector3(-0.5f, 1, 0)
            };
            
            int[] triangles = { 0, 2, 1, 0, 3, 2 };
            Vector2[] uvs = {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.name = "ConnectionMesh";
            
            return mesh;
        }

        private Material CreateDefaultConnectionMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = Color.white;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.5f);
            return material;
        }

        private Material CreatePositiveWeightMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = Color.green;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.8f);
            return material;
        }

        private Material CreateNegativeWeightMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = Color.red;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.8f);
            return material;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            ClearDataParticles();
            
            foreach (var connection in connections)
            {
                if (connection.gameObject != null)
                {
                    DestroyImmediate(connection.gameObject);
                }
            }
            
            connections.Clear();
        }

        #endregion

        #region Data Structures

        [System.Serializable]
        private class ConnectionLine
        {
            public GameObject gameObject;
            public LineRenderer lineRenderer;
            public Transform fromNode;
            public Transform toNode;
            public int fromIndex;
            public int toIndex;
            public float weight;
            public bool isVisible;
        }

        private class DataFlowParticle
        {
            public GameObject gameObject;
            public ConnectionLine connection;
            public float progress;
            public float speed;
        }

        #endregion
    }
}