using System;
using UnityEngine;
using System.Collections.Generic;

namespace Archetype.Visualization
{
    /// <summary>
    /// Individual neural node visualization component - MUST inherit from MonoBehaviour
    /// for Unity component attachment
    /// </summary>
    public class NodeVisualization : MonoBehaviour
    {
        [Header("Node Properties")]
        [SerializeField] private int layerIndex;
        [SerializeField] private int nodeIndex;
        [SerializeField] private LayerType layerType;
        [SerializeField] private float activationValue = 0f;
        [SerializeField] private float bias = 0f;

        [Header("Visual Settings")]
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private Material errorMaterial;
        [SerializeField] private float baseScale = 1f;
        [SerializeField] private float maxScale = 2f;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.red;

        [Header("Animation")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private bool enablePulse = true;
        [SerializeField] private AnimationCurve activationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Internal state
        private Renderer nodeRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 basePosition;
        private Vector3 baseLocalScale;
        private bool isHighlighted = false;
        private bool isInitialized = false;
        private float animationTime = 0f;

        // Performance optimization
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissionProperty = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicProperty = Shader.PropertyToID("_Metallic");

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            CacheInitialValues();
        }

        private void Update()
        {
            if (!isInitialized) return;

            UpdateAnimation();
            UpdateVisualState();
        }

        #endregion

        #region Initialization

        public void Initialize(int layer, int node, LayerType type, VisualizationSettings settings)
        {
            layerIndex = layer;
            nodeIndex = node;
            layerType = type;

            // Apply settings
            if (settings != null)
            {
                baseScale = settings.nodeScale;
                enablePulse = settings.enableTrainingAnimation;
                pulseSpeed = settings.trainingAnimationSpeed;
            }

            SetupNodeMesh();
            InitializeComponents();
            CacheInitialValues();

            isInitialized = true;
            Debug.Log($"✅ NodeVisualization initialized: L{layerIndex}_N{nodeIndex}");
        }

        private void InitializeComponents()
        {
            // Get or create renderer
            nodeRenderer = GetComponent<MeshRenderer>();
            if (nodeRenderer == null)
            {
                Debug.LogError($"❌ NodeVisualization {name} missing Renderer component");
                gameObject.AddComponent<MeshRenderer>();

                return;
            }

            // Initialize material property block for efficient per-instance properties
            propertyBlock = new MaterialPropertyBlock();

            // Set initial material
            if (baseMaterial != null)
            {
                nodeRenderer.material = baseMaterial;
            }
        }

        private void SetupNodeMesh()
        {
            // Create appropriate mesh based on layer type
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            // Different shapes for different layer types
            switch (layerType)
            {
                case LayerType.Input:
                    meshFilter.mesh = CreateInputNodeMesh();
                    break;
                case LayerType.Hidden:
                    meshFilter.mesh = CreateHiddenNodeMesh();
                    break;
                case LayerType.Output:
                    meshFilter.mesh = CreateOutputNodeMesh();
                    break;
                case LayerType.Convolutional:
                    meshFilter.mesh = CreateConvolutionalNodeMesh();
                    break;
                case LayerType.Recurrent:
                    meshFilter.mesh = CreateRecurrentNodeMesh();
                    break;
                default:
                    meshFilter.mesh = CreateDefaultNodeMesh();
                    break;
            }

            // Ensure we have a collider for mouse interaction
            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<SphereCollider>();
            }
        }

        private void CacheInitialValues()
        {
            basePosition = transform.localPosition;
            baseLocalScale = transform.localScale;
        }

        #endregion

        #region Mesh Creation

        private Mesh CreateInputNodeMesh()
        {
            // Create a cube for input nodes
            var mesh = new Mesh();
            
            Vector3[] vertices = {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };

            int[] triangles = {
                0, 2, 1, 0, 3, 2, // front
                4, 5, 6, 4, 6, 7, // back
                0, 1, 5, 0, 5, 4, // bottom
                2, 3, 7, 2, 7, 6, // top
                0, 4, 7, 0, 7, 3, // left
                1, 2, 6, 1, 6, 5  // right
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.name = "InputNodeMesh";
            
            return mesh;
        }

        private Mesh CreateHiddenNodeMesh()
        {
            // Use Unity's built-in sphere mesh for hidden nodes
            return Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        }

        private Mesh CreateOutputNodeMesh()
        {
            // Create a diamond shape for output nodes
            var mesh = new Mesh();
            
            Vector3[] vertices = {
                new Vector3(0, 1, 0),    // top
                new Vector3(1, 0, 0),    // right
                new Vector3(0, -1, 0),   // bottom
                new Vector3(-1, 0, 0),   // left
                new Vector3(0, 0, 1),    // front
                new Vector3(0, 0, -1)    // back
            };

            int[] triangles = {
                0, 4, 1, 0, 1, 5, 0, 5, 3, 0, 3, 4, // top pyramid
                2, 1, 4, 2, 5, 1, 2, 3, 5, 2, 4, 3  // bottom pyramid
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.name = "OutputNodeMesh";
            
            return mesh;
        }

        private Mesh CreateConvolutionalNodeMesh()
        {
            // Create a cylinder for convolutional nodes
            return Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        }

        private Mesh CreateRecurrentNodeMesh()
        {
            // Create a torus-like shape for recurrent nodes (using capsule as approximation)
            return Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
        }

        private Mesh CreateDefaultNodeMesh()
        {
            return Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        }

        #endregion

        #region Visual Updates

        public void UpdateVisualization()
        {
            if (!isInitialized || nodeRenderer == null) return;

            UpdateColor();
            UpdateScale();
            UpdateEmission();
        }

        private void UpdateAnimation()
        {
            if (!enablePulse) return;

            animationTime += Time.deltaTime * pulseSpeed;
            
            // Pulse effect based on activation
            float pulseIntensity = Mathf.Abs(activationValue);
            float pulse = Mathf.Sin(animationTime) * pulseIntensity * 0.1f;
            
            Vector3 newScale = baseLocalScale * (baseScale + pulse);
            transform.localScale = newScale;
        }

        private void UpdateVisualState()
        {
            if (propertyBlock == null) return;

            // Update color based on activation
            Color targetColor = Color.Lerp(inactiveColor, activeColor, 
                activationCurve.Evaluate(Mathf.Abs(activationValue)));

            // Apply highlight if needed
            if (isHighlighted)
            {
                targetColor = Color.Lerp(targetColor, Color.yellow, 0.5f);
            }

            propertyBlock.SetColor(ColorProperty, targetColor);
            nodeRenderer.SetPropertyBlock(propertyBlock);
        }

        private void UpdateColor()
        {
            if (propertyBlock == null) return;

            Color currentColor = Color.Lerp(inactiveColor, activeColor, 
                Mathf.Clamp01(activationValue));

            propertyBlock.SetColor(ColorProperty, currentColor);
        }

        private void UpdateScale()
        {
            float scaleMultiplier = Mathf.Lerp(1f, maxScale, Mathf.Abs(activationValue));
            transform.localScale = baseLocalScale * baseScale * scaleMultiplier;
        }

        private void UpdateEmission()
        {
            if (propertyBlock == null) return;

            // Emit light based on activation level
            Color emissionColor = activeColor * (activationValue * 0.5f);
            propertyBlock.SetColor(EmissionProperty, emissionColor);
            
            nodeRenderer.SetPropertyBlock(propertyBlock);
        }

        #endregion

        #region Public Interface

        public void SetActivationValue(float value)
        {
            activationValue = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetBias(float biasValue)
        {
            bias = biasValue;
        }

        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            
            if (highlighted && highlightMaterial != null)
            {
                nodeRenderer.material = highlightMaterial;
            }
            else if (baseMaterial != null)
            {
                nodeRenderer.material = baseMaterial;
            }
        }

        public void SetTintColor(Color color)
        {
            if (propertyBlock == null) return;

            propertyBlock.SetColor(ColorProperty, color);
            nodeRenderer.SetPropertyBlock(propertyBlock);
        }

        public void SetError(bool hasError)
        {
            if (hasError && errorMaterial != null)
            {
                nodeRenderer.material = errorMaterial;
            }
            else if (baseMaterial != null)
            {
                nodeRenderer.material = baseMaterial;
            }
        }

        public void PlayActivationAnimation()
        {
            // Trigger a special activation animation
            StartCoroutine(ActivationPulse());
        }

        #endregion

        #region Animation Coroutines

        private System.Collections.IEnumerator ActivationPulse()
        {
            Vector3 originalScale = transform.localScale;
            Vector3 targetScale = originalScale * 1.5f;
            
            float duration = 0.3f;
            float elapsed = 0f;
            
            // Scale up
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
                yield return null;
            }
            
            elapsed = 0f;
            
            // Scale back down
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
                yield return null;
            }
            
            transform.localScale = originalScale;
        }

        #endregion

        #region Mouse Interaction

        private void OnMouseEnter()
        {
            SetHighlighted(true);
            
            // Show tooltip or debug info
            string info = $"Node L{layerIndex}_N{nodeIndex}\n" +
                         $"Type: {layerType}\n" +
                         $"Activation: {activationValue:F3}\n" +
                         $"Bias: {bias:F3}";
            
            Debug.Log(info);
        }

        private void OnMouseExit()
        {
            SetHighlighted(false);
        }

        private void OnMouseDown()
        {
            PlayActivationAnimation();
            Debug.Log($"🖱️ Clicked node: L{layerIndex}_N{nodeIndex}");
        }

        #endregion

        #region LOD and Settings - MISSING METHODS ADDED

        /// <summary>
        /// Set Level of Detail for performance optimization
        /// </summary>
        /// <param name="lodLevel">0 = High detail, 1 = Low detail, 2+ = Minimal detail</param>
        public void SetLODLevel(int lodLevel)
        {
            switch (lodLevel)
            {
                case 0: // High detail
                    enablePulse = true;
                    gameObject.SetActive(true);
                    SetHighDetailMode();
                    break;
                    
                case 1: // Low detail
                    enablePulse = false;
                    gameObject.SetActive(true);
                    SetLowDetailMode();
                    break;
                    
                case 2: // Minimal detail
                    enablePulse = false;
                    gameObject.SetActive(true);
                    SetMinimalDetailMode();
                    break;
                    
                default: // Ultra low - hide completely
                    gameObject.SetActive(false);
                    break;
            }
        }

        /// <summary>
        /// Apply visualization settings to this node
        /// </summary>
        /// <param name="settings">Visualization settings to apply</param>
        public void ApplySettings(VisualizationSettings settings)
        {
            if (settings == null) return;

            // Apply scale settings
            baseScale = settings.nodeScale;
            transform.localScale = Vector3.one * baseScale;

            // Apply animation settings
            enablePulse = settings.enableTrainingAnimation;
            pulseSpeed = settings.trainingAnimationSpeed;

            // Apply visual settings
            if (settings.nodeMaterial != null && nodeRenderer != null)
            {
                nodeRenderer.material = settings.nodeMaterial;
            }

            // Update cached values
            baseLocalScale = transform.localScale;

            Debug.Log($"📐 Applied settings to node L{layerIndex}_N{nodeIndex}");
        }

        private void SetHighDetailMode()
        {
            // Enable all visual features
            if (nodeRenderer != null)
            {
                nodeRenderer.enabled = true;
                nodeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                nodeRenderer.receiveShadows = true;
            }

            // Enable collider for mouse interaction
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = true;
            }

            // Full resolution mesh
            SetMeshResolution(1.0f);
        }

        private void SetLowDetailMode()
        {
            // Reduce visual features
            if (nodeRenderer != null)
            {
                nodeRenderer.enabled = true;
                nodeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                nodeRenderer.receiveShadows = false;
            }

            // Disable collider to save performance
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            // Reduced resolution mesh
            SetMeshResolution(0.5f);
        }

        private void SetMinimalDetailMode()
        {
            // Minimal visual features
            if (nodeRenderer != null)
            {
                nodeRenderer.enabled = true;
                nodeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                nodeRenderer.receiveShadows = false;
            }

            // Disable all interaction
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            // Very low resolution mesh
            SetMeshResolution(0.25f);

            // Disable property block updates for performance
            propertyBlock = null;
        }

        private void SetMeshResolution(float qualityMultiplier)
        {
            // Adjust mesh quality based on performance needs
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) return;

            switch (layerType)
            {
                case LayerType.Hidden:
                    // Use different sphere resolutions
                    if (qualityMultiplier >= 1.0f)
                        meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                    else if (qualityMultiplier >= 0.5f)
                        meshFilter.mesh = CreateLowResSphere();
                    else
                        meshFilter.mesh = CreateMinimalSphere();
                    break;

                case LayerType.Input:
                case LayerType.Output:
                    // Keep custom meshes but adjust detail
                    if (qualityMultiplier < 0.5f)
                    {
                        // Switch to simple sphere for performance
                        meshFilter.mesh = CreateMinimalSphere();
                    }
                    break;
            }
        }

        private Mesh CreateLowResSphere()
        {
            // Create a low-resolution sphere (8 segments instead of default 24)
            var mesh = new Mesh();
            
            // Simplified sphere with fewer vertices
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            int rings = 4; // Reduced from default
            int segments = 8; // Reduced from default

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = Mathf.PI * ring / rings;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float theta = 2.0f * Mathf.PI * segment / segments;
                    
                    float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                    float y = Mathf.Cos(phi);
                    float z = Mathf.Sin(phi) * Mathf.Sin(theta);
                    
                    vertices.Add(new Vector3(x, y, z) * 0.5f);
                }
            }

            // Generate triangles
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int current = ring * (segments + 1) + segment;
                    int next = current + segments + 1;

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.name = "LowResSphere";

            return mesh;
        }

        private Mesh CreateMinimalSphere()
        {
            // Create a very simple octahedron (8 triangles)
            var mesh = new Mesh();
            
            Vector3[] vertices = {
                new Vector3(0, 0.5f, 0),   // top
                new Vector3(0.5f, 0, 0),   // right
                new Vector3(0, 0, 0.5f),   // front
                new Vector3(-0.5f, 0, 0),  // left
                new Vector3(0, 0, -0.5f),  // back
                new Vector3(0, -0.5f, 0)   // bottom
            };

            int[] triangles = {
                0, 2, 1, // top front right
                0, 1, 4, // top right back
                0, 4, 3, // top back left
                0, 3, 2, // top left front
                5, 1, 2, // bottom right front
                5, 4, 1, // bottom back right
                5, 3, 4, // bottom left back
                5, 2, 3  // bottom front left
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.name = "MinimalSphere";

            return mesh;
        }

        /// <summary>
        /// Get current LOD level based on distance from camera
        /// </summary>
        /// <param name="cameraPosition">Camera world position</param>
        /// <returns>Recommended LOD level</returns>
        public int GetRecommendedLODLevel(Vector3 cameraPosition)
        {
            float distance = Vector3.Distance(transform.position, cameraPosition);
            
            if (distance < 10f) return 0;      // High detail
            if (distance < 25f) return 1;      // Low detail  
            if (distance < 50f) return 2;      // Minimal detail
            return 3;                          // Hide completely
        }

        /// <summary>
        /// Force update visualization settings immediately
        /// </summary>
        public void ForceUpdateSettings()
        {
            if (!isInitialized) return;
            
            UpdateVisualization();
            UpdateVisualState();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            // Clean up any resources
            if (propertyBlock != null)
            {
                propertyBlock = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Enum for different layer types with distinct visual representations
    /// </summary>
    public enum LayerType
    {
        Input,
        Hidden,
        Output,
        Convolutional,
        Recurrent,
        LSTM,
        GRU,
        Attention,
        Normalization,
        Dropout
    }
}