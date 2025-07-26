using System;
using System.Collections.Generic;
using UnityEngine;

namespace Archetype.Plugins.Core
{
    /// <summary>
    /// Convolutional Neural Network plugin for Unity
    /// </summary>
    public class CNNPlugin : IUnityPlugin
    {
        private bool initialized = false;
        private PluginManifest manifest;

        public bool Initialize()
        {
            try
            {
                manifest = new PluginManifest
                {
                    id = "cnn_core",
                    name = "Convolutional Neural Network",
                    version = "1.0.0",
                    description = "CNN implementations with feature map visualization",
                    author = "Archetype Core Team",
                    category = "core",
                    uiComponents = new List<string> { "CNNBuilder", "ConvLayerConfig", "PoolingConfig" },
                    visualizationComponents = new List<string> { "CNNVisualizer", "FeatureMapRenderer", "ConvolutionAnimator" },
                    neuralComponentTypes = new List<string> { "conv2d_layer", "cnn_block", "resnet_block" }
                };

                initialized = true;
                Debug.Log("✅ CNN Plugin initialized");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to initialize CNN Plugin: {e.Message}");
                return false;
            }
        }

        public void Cleanup()
        {
            initialized = false;
            Debug.Log("🧹 CNN Plugin cleanup complete");
        }

        public PluginManifest GetManifest()
        {
            return manifest;
        }

        public GameObject CreateUIComponent(string componentType, Transform parent)
        {
            switch (componentType)
            {
                case "CNNBuilder":
                    return CreateCNNBuilder(parent);
                case "ConvLayerConfig":
                    return CreateConvLayerConfig(parent);
                case "PoolingConfig":
                    return CreatePoolingConfig(parent);
                default:
                    throw new ArgumentException($"Unknown UI component type: {componentType}");
            }
        }

        public MonoBehaviour CreateVisualizationComponent(string componentType, Transform parent)
        {
            switch (componentType)
            {
                case "CNNVisualizer":
                    return CreateCNNVisualizer(parent);
                case "FeatureMapRenderer":
                    return CreateFeatureMapRenderer(parent);
                case "ConvolutionAnimator":
                    return CreateConvolutionAnimator(parent);
                default:
                    throw new ArgumentException($"Unknown visualization component type: {componentType}");
            }
        }

        public List<string> GetAvailableUIComponents()
        {
            return manifest.uiComponents;
        }

        public List<string> GetAvailableVisualizationComponents()
        {
            return manifest.visualizationComponents;
        }

        #region Component Creation

        private GameObject CreateCNNBuilder(Transform parent)
        {
            var go = new GameObject("CNNBuilder");
            if (parent != null) go.transform.SetParent(parent, false);

            var cnnBuilder = go.AddComponent<CNNBuilderUI>();
            return go;
        }

        private GameObject CreateConvLayerConfig(Transform parent)
        {
            var go = new GameObject("ConvLayerConfig");
            if (parent != null) go.transform.SetParent(parent, false);

            var convConfig = go.AddComponent<ConvLayerConfigUI>();
            return go;
        }

        private GameObject CreatePoolingConfig(Transform parent)
        {
            var go = new GameObject("PoolingConfig");
            if (parent != null) go.transform.SetParent(parent, false);

            var poolingConfig = go.AddComponent<PoolingConfigUI>();
            return go;
        }

        private MonoBehaviour CreateCNNVisualizer(Transform parent)
        {
            var go = new GameObject("CNNVisualizer");
            if (parent != null) go.transform.SetParent(parent, false);

            return go.AddComponent<CNNVisualizer>();
        }

        private MonoBehaviour CreateFeatureMapRenderer(Transform parent)
        {
            var go = new GameObject("FeatureMapRenderer");
            if (parent != null) go.transform.SetParent(parent, false);

            return go.AddComponent<FeatureMapRenderer>();
        }

        private MonoBehaviour CreateConvolutionAnimator(Transform parent)
        {
            var go = new GameObject("ConvolutionAnimator");
            if (parent != null) go.transform.SetParent(parent, false);

            return go.AddComponent<ConvolutionAnimator>();
        }

        #endregion
    }

    #region CNN UI Components

    /// <summary>
    /// UI component for building CNN networks
    /// </summary>
    public class CNNBuilderUI : MonoBehaviour
    {
        [Header("CNN Configuration")]
        public int inputChannels = 3;
        public int numClasses = 10;
        public List<int> convLayers = new List<int> { 32, 64, 128 };
        public List<int> kernelSizes = new List<int> { 3, 3, 3 };
        public List<int> fcLayers = new List<int> { 512, 256 };
        public float dropout = 0.5f;
        public bool batchNorm = true;
        public string pooling = "max";

        [Header("UI References")]
        public UnityEngine.UI.Button createModelButton;
        public UnityEngine.UI.Button addConvLayerButton;
        public UnityEngine.UI.Button removeConvLayerButton;
        public UnityEngine.UI.InputField inputChannelsField;
        public UnityEngine.UI.InputField numClassesField;
        public UnityEngine.UI.Slider dropoutSlider;
        public UnityEngine.UI.Toggle batchNormToggle;
        public UnityEngine.UI.Dropdown poolingDropdown;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (createModelButton != null)
                createModelButton.onClick.AddListener(CreateCNNModel);

            if (addConvLayerButton != null)
                addConvLayerButton.onClick.AddListener(AddConvLayer);

            if (removeConvLayerButton != null)
                removeConvLayerButton.onClick.AddListener(RemoveConvLayer);

            if (dropoutSlider != null)
                dropoutSlider.onValueChanged.AddListener(OnDropoutChanged);

            if (batchNormToggle != null)
                batchNormToggle.onValueChanged.AddListener(OnBatchNormChanged);

            if (poolingDropdown != null)
            {
                poolingDropdown.options.Clear();
                poolingDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("max"));
                poolingDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("avg"));
                poolingDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("adaptive"));
                poolingDropdown.onValueChanged.AddListener(OnPoolingChanged);
            }
        }

        private void AddConvLayer()
        {
            convLayers.Add(64);
            kernelSizes.Add(3);
            Debug.Log($"Added conv layer - New architecture: [{string.Join(", ", convLayers)}]");
        }

        private void RemoveConvLayer()
        {
            if (convLayers.Count > 1)
            {
                convLayers.RemoveAt(convLayers.Count - 1);
                kernelSizes.RemoveAt(kernelSizes.Count - 1);
                Debug.Log($"Removed conv layer - New architecture: [{string.Join(", ", convLayers)}]");
            }
        }

        private void OnDropoutChanged(float value)
        {
            dropout = value;
        }

        private void OnBatchNormChanged(bool value)
        {
            batchNorm = value;
        }

        private void OnPoolingChanged(int value)
        {
            string[] poolingTypes = { "max", "avg", "adaptive" };
            if (value >= 0 && value < poolingTypes.Length)
                pooling = poolingTypes[value];
        }

        private async void CreateCNNModel()
        {
            try
            {
                var architecture = new Dictionary<string, object>
                {
                    ["input_channels"] = inputChannels,
                    ["num_classes"] = numClasses,
                    ["conv_layers"] = convLayers,
                    ["kernel_sizes"] = kernelSizes,
                    ["fc_layers"] = fcLayers
                };

                var hyperparameters = new Dictionary<string, object>
                {
                    ["dropout"] = dropout,
                    ["batch_norm"] = batchNorm,
                    ["pooling"] = pooling
                };

                var request = new Backend.API.ModelCreateRequest
                {
                    name = $"CNN_{DateTime.Now:yyyyMMdd_HHmmss}",
                    model_type = "cnn",
                    architecture = architecture,
                    hyperparameters = hyperparameters
                };

                var response = await Backend.API.ModelAPI.CreateModel(request);
                Debug.Log($"✅ Created CNN model: {response.id} with {response.parameter_count:N0} parameters");

                // Trigger visualization
                await Archetype.Visualization.NeuralNetworkVisualizer.Instance.VisualizeNetwork(response.id);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to create CNN model: {e.Message}");
            }
        }
    }

    /// <summary>
    /// UI component for convolutional layer configuration
    /// </summary>
    public class ConvLayerConfigUI : MonoBehaviour
    {
        [Header("Convolution Settings")]
        public int filters = 32;
        public int kernelSize = 3;
        public int stride = 1;
        public int padding = 1;
        public string activation = "relu";

        [Header("UI References")]
        public UnityEngine.UI.InputField filtersField;
        public UnityEngine.UI.InputField kernelSizeField;
        public UnityEngine.UI.InputField strideField;
        public UnityEngine.UI.InputField paddingField;
        public UnityEngine.UI.Dropdown activationDropdown;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (activationDropdown != null)
            {
                activationDropdown.options.Clear();
                activationDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("relu"));
                activationDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("leaky_relu"));
                activationDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("tanh"));
                activationDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("sigmoid"));
                activationDropdown.onValueChanged.AddListener(OnActivationChanged);
            }
        }

        private void OnActivationChanged(int value)
        {
            string[] activations = { "relu", "leaky_relu", "tanh", "sigmoid" };
            if (value >= 0 && value < activations.Length)
                activation = activations[value];
        }

        public Dictionary<string, object> GetConfiguration()
        {
            return new Dictionary<string, object>
            {
                ["filters"] = filters,
                ["kernel_size"] = kernelSize,
                ["stride"] = stride,
                ["padding"] = padding,
                ["activation"] = activation
            };
        }
    }

    /// <summary>
    /// UI component for pooling configuration
    /// </summary>
    public class PoolingConfigUI : MonoBehaviour
    {
        [Header("Pooling Settings")]
        public string poolingType = "max";
        public int poolSize = 2;
        public int poolStride = 2;

        [Header("UI References")]
        public UnityEngine.UI.Dropdown poolingTypeDropdown;
        public UnityEngine.UI.InputField poolSizeField;
        public UnityEngine.UI.InputField poolStrideField;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (poolingTypeDropdown != null)
            {
                poolingTypeDropdown.options.Clear();
                poolingTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("max"));
                poolingTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("avg"));
                poolingTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("global_avg"));
                poolingTypeDropdown.onValueChanged.AddListener(OnPoolingTypeChanged);
            }
        }

        private void OnPoolingTypeChanged(int value)
        {
            string[] poolingTypes = { "max", "avg", "global_avg" };
            if (value >= 0 && value < poolingTypes.Length)
                poolingType = poolingTypes[value];
        }

        public Dictionary<string, object> GetConfiguration()
        {
            return new Dictionary<string, object>
            {
                ["pooling_type"] = poolingType,
                ["pool_size"] = poolSize,
                ["pool_stride"] = poolStride
            };
        }
    }

    #endregion

    #region CNN Visualization Components

    /// <summary>
    /// 3D visualizer for CNN networks
    /// </summary>
    public class CNNVisualizer : MonoBehaviour
    {
        [Header("CNN Visualization")]
        public float layerSpacing = 4.0f;
        public float featureMapSpacing = 0.5f;
        public float channelDepth = 0.2f;
        public Material featureMapMaterial;
        public Material kernelMaterial;

        private List<GameObject> convLayers = new List<GameObject>();
        private List<GameObject> featureMaps = new List<GameObject>();
        private List<GameObject> kernels = new List<GameObject>();

        public void VisualizeCNN(List<int> convLayers, List<int> kernelSizes)
        {
            ClearVisualization();
            CreateConvLayers(convLayers, kernelSizes);
            CreateKernelVisualizations(convLayers, kernelSizes);
        }

        private void ClearVisualization()
        {
            foreach (var layer in convLayers)
                if (layer != null) DestroyImmediate(layer);
            convLayers.Clear();

            foreach (var map in featureMaps)
                if (map != null) DestroyImmediate(map);
            featureMaps.Clear();

            foreach (var kernel in kernels)
                if (kernel != null) DestroyImmediate(kernel);
            kernels.Clear();
        }

        private void CreateConvLayers(List<int> layers, List<int> kernels)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                CreateConvLayer(i, layers[i], kernels[i]);
            }
        }

        private void CreateConvLayer(int layerIndex, int filters, int kernelSize)
        {
            var layer = new GameObject($"ConvLayer_{layerIndex}");
            layer.transform.SetParent(transform);
            layer.transform.localPosition = new Vector3(layerIndex * layerSpacing, 0, 0);

            // Create feature maps for this layer (limit for performance)
            int visibleMaps = Mathf.Min(filters, 16);
            for (int f = 0; f < visibleMaps; f++)
            {
                var featureMap = CreateFeatureMap(layerIndex, f, kernelSize);
                featureMap.transform.SetParent(layer.transform);
            }

            convLayers.Add(layer);
        }

        private GameObject CreateFeatureMap(int layerIndex, int filterIndex, int kernelSize)
        {
            var featureMap = GameObject.CreatePrimitive(PrimitiveType.Quad);
            featureMap.name = $"FeatureMap_L{layerIndex}_F{filterIndex}";

            // Position feature maps in a grid
            int gridSize = 4;
            float x = (filterIndex % gridSize) * featureMapSpacing;
            float y = (filterIndex / gridSize) * featureMapSpacing;
            float z = filterIndex * channelDepth;

            featureMap.transform.localPosition = new Vector3(x, y, z);

            // Scale based on kernel size
            float scale = 0.3f + (kernelSize * 0.1f);
            featureMap.transform.localScale = Vector3.one * scale;

            if (featureMapMaterial != null)
            {
                var renderer = featureMap.GetComponent<Renderer>();
                renderer.material = featureMapMaterial;

                // Color based on layer and filter
                float hue = (layerIndex * 0.3f + filterIndex * 0.1f) % 1.0f;
                Color mapColor = Color.HSVToRGB(hue, 0.7f, 0.9f);
                renderer.material.color = mapColor;
            }

            // Add feature map component
            var mapComponent = featureMap.AddComponent<FeatureMap>();
            mapComponent.layerIndex = layerIndex;
            mapComponent.filterIndex = filterIndex;

            featureMaps.Add(featureMap);
            return featureMap;
        }

        private void CreateKernelVisualizations(List<int> layers, List<int> kernelSizes)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                CreateKernelVisualization(i, kernelSizes[i]);
            }
        }

        private void CreateKernelVisualization(int layerIndex, int kernelSize)
        {
            var kernelContainer = new GameObject($"Kernels_L{layerIndex}");
            kernelContainer.transform.SetParent(transform);

            // Position kernels above the feature maps
            Vector3 layerPos = new Vector3(layerIndex * layerSpacing, 2.0f, 0);
            kernelContainer.transform.localPosition = layerPos;

            // Create a few example kernels
            int visibleKernels = Mathf.Min(4, kernelSize);
            for (int k = 0; k < visibleKernels; k++)
            {
                var kernel = CreateKernel(layerIndex, k, kernelSize);
                kernel.transform.SetParent(kernelContainer.transform);
                kernels.Add(kernel);
            }
        }

        private GameObject CreateKernel(int layerIndex, int kernelIndex, int kernelSize)
        {
            var kernel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kernel.name = $"Kernel_L{layerIndex}_K{kernelIndex}";

            // Position kernels in a line
            float x = kernelIndex * 0.8f;
            kernel.transform.localPosition = new Vector3(x, 0, 0);

            // Scale based on kernel size
            float scale = 0.2f + (kernelSize * 0.05f);
            kernel.transform.localScale = Vector3.one * scale;

            if (kernelMaterial != null)
            {
                var renderer = kernel.GetComponent<Renderer>();
                renderer.material = kernelMaterial;
                renderer.material.color = Color.red;
            }

            return kernel;
        }

        public void UpdateFeatureMapActivation(int layerIndex, int filterIndex, float[,] activationData)
        {
            foreach (var map in featureMaps)
            {
                var mapComponent = map.GetComponent<FeatureMap>();
                if (mapComponent != null && mapComponent.layerIndex == layerIndex && mapComponent.filterIndex == filterIndex)
                {
                    mapComponent.UpdateActivation(activationData);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Feature map renderer component
    /// </summary>
    public class FeatureMapRenderer : MonoBehaviour
    {
        [Header("Feature Map Settings")]
        public Texture2D featureMapTexture;
        public float intensity = 1.0f;
        public bool showActivationValues = true;

        private Renderer mapRenderer;

        private void Start()
        {
            mapRenderer = GetComponent<Renderer>();
        }

        public void UpdateFeatureMap(float[,] data)
        {
            if (data == null) return;

            int width = data.GetLength(0);
            int height = data.GetLength(1);

            // Create or resize texture
            if (featureMapTexture == null || featureMapTexture.width != width || featureMapTexture.height != height)
            {
                featureMapTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            // Convert data to texture
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float value = Mathf.Clamp01(data[x, y] * intensity);
                    Color color = new Color(value, value, value, 1.0f);
                    featureMapTexture.SetPixel(x, y, color);
                }
            }

            featureMapTexture.Apply();

            if (mapRenderer != null)
                mapRenderer.material.mainTexture = featureMapTexture;
        }

        public void SetIntensity(float newIntensity)
        {
            intensity = newIntensity;
        }
    }

    /// <summary>
    /// Convolution animation component
    /// </summary>
    public class ConvolutionAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float animationSpeed = 1.0f;
        public GameObject kernelPrefab;
        public Material convolutionMaterial;
        public bool showConvolutionProcess = true;

        private bool isAnimating = false;

        public void AnimateConvolution(Vector2 inputSize, Vector2 kernelSize, Vector2 outputSize)
        {
            if (!isAnimating)
                StartCoroutine(ConvolutionAnimation(inputSize, kernelSize, outputSize));
        }

        private System.Collections.IEnumerator ConvolutionAnimation(Vector2 inputSize, Vector2 kernelSize, Vector2 outputSize)
        {
            isAnimating = true;

            // Create visual elements for animation
            var inputPlane = CreateAnimationPlane("InputMap", Vector3.zero, inputSize, Color.blue);
            var kernelCube = CreateAnimationKernel("Kernel", Vector3.zero, kernelSize, Color.red);
            var outputPlane = CreateAnimationPlane("OutputMap", new Vector3(3, 0, 0), outputSize, Color.green);

            // Animate kernel sliding over input
            int stepsX = Mathf.RoundToInt(outputSize.x);
            int stepsY = Mathf.RoundToInt(outputSize.y);

            for (int y = 0; y < stepsY; y++)
            {
                for (int x = 0; x < stepsX; x++)
                {
                    // Move kernel to current position
                    float kernelX = (x - stepsX * 0.5f) * 0.2f;
                    float kernelY = (y - stepsY * 0.5f) * 0.2f;

                    Vector3 targetPos = new Vector3(kernelX, kernelY, -0.5f);

                    // Animate movement
                    yield return StartCoroutine(MoveToPosition(kernelCube.transform, targetPos, 0.1f / animationSpeed));

                    // Highlight current output pixel
                    HighlightOutputPixel(outputPlane, x, y);

                    yield return new WaitForSeconds(0.05f / animationSpeed);
                }
            }

            // Cleanup animation objects
            yield return new WaitForSeconds(1.0f);

            if (inputPlane) DestroyImmediate(inputPlane);
            if (kernelCube) DestroyImmediate(kernelCube);
            if (outputPlane) DestroyImmediate(outputPlane);

            isAnimating = false;
        }

        private GameObject CreateAnimationPlane(string name, Vector3 position, Vector2 size, Color color)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = name;
            plane.transform.SetParent(transform);
            plane.transform.localPosition = position;
            plane.transform.localScale = new Vector3(size.x * 0.1f, size.y * 0.1f, 1);

            var renderer = plane.GetComponent<Renderer>();
            if (convolutionMaterial != null)
            {
                renderer.material = convolutionMaterial;
                renderer.material.color = color;
            }

            return plane;
        }

        private GameObject CreateAnimationKernel(string name, Vector3 position, Vector2 size, Color color)
        {
            var kernel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kernel.name = name;
            kernel.transform.SetParent(transform);
            kernel.transform.localPosition = position;
            kernel.transform.localScale = new Vector3(size.x * 0.05f, size.y * 0.05f, 0.1f);

            var renderer = kernel.GetComponent<Renderer>();
            if (convolutionMaterial != null)
            {
                renderer.material = convolutionMaterial;
                renderer.material.color = color;
            }

            return kernel;
        }

        private System.Collections.IEnumerator MoveToPosition(Transform target, Vector3 destination, float duration)
        {
            Vector3 startPos = target.localPosition;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                target.localPosition = Vector3.Lerp(startPos, destination, t);
                yield return null;
            }

            target.localPosition = destination;
        }

        private void HighlightOutputPixel(GameObject outputPlane, int x, int y)
        {
            // Simple highlight effect - could be enhanced with actual pixel highlighting
            var renderer = outputPlane.GetComponent<Renderer>();
            if (renderer != null)
            {
                float pulse = Mathf.Sin(Time.time * 10) * 0.5f + 0.5f;
                Color highlightColor = Color.Lerp(Color.green, Color.white, pulse);
                renderer.material.color = highlightColor;
            }
        }
    }

    /// <summary>
    /// Individual feature map component
    /// </summary>
    public class FeatureMap : MonoBehaviour
    {
        public int layerIndex;
        public int filterIndex;
        public float[,] activationData;

        private Renderer mapRenderer;
        private FeatureMapRenderer mapRendererComponent;

        private void Start()
        {
            mapRenderer = GetComponent<Renderer>();
            mapRendererComponent = GetComponent<FeatureMapRenderer>();

            if (mapRendererComponent == null)
                mapRendererComponent = gameObject.AddComponent<FeatureMapRenderer>();
        }

        public void UpdateActivation(float[,] data)
        {
            activationData = data;

            if (mapRendererComponent != null)
                mapRendererComponent.UpdateFeatureMap(data);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (mapRenderer != null)
            {
                Color baseColor = mapRenderer.material.color;
                Color targetColor = highlighted ? Color.yellow : baseColor;
                mapRenderer.material.color = Color.Lerp(baseColor, targetColor, 0.5f);
            }
        }

        private void OnMouseDown()
        {
            Debug.Log($"Feature Map - Layer: {layerIndex}, Filter: {filterIndex}");
            if (activationData != null)
            {
                Debug.Log($"Activation Data Size: {activationData.GetLength(0)}x{activationData.GetLength(1)}");
            }
        }
    }

    #endregion
}