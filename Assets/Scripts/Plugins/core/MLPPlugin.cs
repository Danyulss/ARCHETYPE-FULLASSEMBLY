using System;
using System.Collections.Generic;
using UnityEngine;

namespace Archetype.Plugins.Core
{
    /// <summary>
    /// Multi-Layer Perceptron plugin for Unity
    /// </summary>
    public class MLPPlugin : IUnityPlugin
    {
        private bool initialized = false;
        private PluginManifest manifest;

        public bool Initialize()
        {
            try
            {
                manifest = new PluginManifest
                {
                    id = "mlp_core",
                    name = "Multi-Layer Perceptron",
                    version = "1.0.0",
                    description = "Standard fully-connected neural network layers with 3D visualization",
                    author = "Archetype Core Team",
                    category = "core",
                    uiComponents = new List<string> { "MLPBuilder", "LayerConfig", "ActivationSelector" },
                    visualizationComponents = new List<string> { "MLPVisualizer", "NodeRenderer", "ConnectionRenderer" },
                    neuralComponentTypes = new List<string> { "mlp_layer", "dense_layer", "linear_layer" }
                };

                initialized = true;
                Debug.Log("✅ MLP Plugin initialized");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to initialize MLP Plugin: {e.Message}");
                return false;
            }
        }

        public void Cleanup()
        {
            initialized = false;
            Debug.Log("🧹 MLP Plugin cleanup complete");
        }

        public PluginManifest GetManifest()
        {
            return manifest;
        }

        public GameObject CreateUIComponent(string componentType, Transform parent)
        {
            switch (componentType)
            {
                case "MLPBuilder":
                    return CreateMLPBuilder(parent);
                case "LayerConfig":
                    return CreateLayerConfig(parent);
                case "ActivationSelector":
                    return CreateActivationSelector(parent);
                default:
                    throw new ArgumentException($"Unknown UI component type: {componentType}");
            }
        }

        public MonoBehaviour CreateVisualizationComponent(string componentType, Transform parent)
        {
            switch (componentType)
            {
                case "MLPVisualizer":
                    return CreateMLPVisualizer(parent);
                case "NodeRenderer":
                    return CreateNodeRenderer(parent);
                case "ConnectionRenderer":
                    return CreateConnectionRenderer(parent);
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

        private GameObject CreateMLPBuilder(Transform parent)
        {
            var go = new GameObject("MLPBuilder");
            if (parent != null) go.transform.SetParent(parent, false);
            
            // Add UI components
            var canvas = go.AddComponent<Canvas>();
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            var raycaster = go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Add MLP builder component
            var mlpBuilder = go.AddComponent<MLPBuilderUI>();
            
            return go;
        }

        private GameObject CreateLayerConfig(Transform parent)
        {
            var go = new GameObject("LayerConfig");
            if (parent != null) go.transform.SetParent(parent, false);
            
            var layerConfig = go.AddComponent<LayerConfigUI>();
            
            return go;
        }

        private GameObject CreateActivationSelector(Transform parent)
        {
            var go = new GameObject("ActivationSelector");
            if (parent != null) go.transform.SetParent(parent, false);
            
            var activationSelector = go.AddComponent<ActivationSelectorUI>();
            
            return go;
        }

        private MonoBehaviour CreateMLPVisualizer(Transform parent)
        {
            var go = new GameObject("MLPVisualizer");
            if (parent != null) go.transform.SetParent(parent, false);
            
            return go.AddComponent<MLPVisualizer>();
        }

        private MonoBehaviour CreateNodeRenderer(Transform parent)
        {
            var go = new GameObject("NodeRenderer");
            if (parent != null) go.transform.SetParent(parent, false);
            
            return go.AddComponent<Archetype.Visualization.NodeVisualization>();
        }

        private MonoBehaviour CreateConnectionRenderer(Transform parent)
        {
            var go = new GameObject("ConnectionRenderer");
            if (parent != null) go.transform.SetParent(parent, false);
            
            return go.AddComponent<Archetype.Visualization.ConnectionRenderer>();
        }

        #endregion
    }

    #region UI Components

    /// <summary>
    /// UI component for building MLP networks
    /// </summary>
    public class MLPBuilderUI : MonoBehaviour
    {
        [Header("MLP Configuration")]
        public List<int> layers = new List<int> { 784, 128, 64, 10 };
        public string activation = "relu";
        public float dropout = 0.0f;
        public bool useBias = true;

        [Header("UI References")]
        public UnityEngine.UI.Button addLayerButton;
        public UnityEngine.UI.Button removeLayerButton;
        public UnityEngine.UI.Button createModelButton;
        public UnityEngine.UI.Dropdown activationDropdown;
        public UnityEngine.UI.Slider dropoutSlider;
        public UnityEngine.UI.Toggle biasToggle;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            // Setup UI event handlers
            if (addLayerButton != null)
                addLayerButton.onClick.AddListener(AddLayer);
            
            if (removeLayerButton != null)
                removeLayerButton.onClick.AddListener(RemoveLayer);
            
            if (createModelButton != null)
                createModelButton.onClick.AddListener(CreateModel);
        }

        private void AddLayer()
        {
            layers.Insert(layers.Count - 1, 64); // Add before output layer
            Debug.Log($"Added layer - New architecture: [{string.Join(", ", layers)}]");
        }

        private void RemoveLayer()
        {
            if (layers.Count > 2) // Keep at least input and output
            {
                layers.RemoveAt(layers.Count - 2); // Remove before output layer
                Debug.Log($"Removed layer - New architecture: [{string.Join(", ", layers)}]");
            }
        }

        private async void CreateModel()
        {
            try
            {
                var architecture = new Dictionary<string, object>
                {
                    ["layers"] = layers
                };

                var hyperparameters = new Dictionary<string, object>
                {
                    ["activation"] = activation,
                    ["dropout"] = dropout,
                    ["bias"] = useBias
                };

                var request = new Backend.API.ModelCreateRequest
                {
                    name = $"MLP_{DateTime.Now:yyyyMMdd_HHmmss}",
                    model_type = "mlp",
                    architecture = architecture,
                    hyperparameters = hyperparameters
                };

                var response = await Backend.API.ModelAPI.CreateModel(request);
                Debug.Log($"✅ Created MLP model: {response.id} with {response.parameter_count:N0} parameters");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to create MLP model: {e.Message}");
            }
        }
    }

    /// <summary>
    /// UI component for configuring individual layers
    /// </summary>
    public class LayerConfigUI : MonoBehaviour
    {
        [Header("Layer Settings")]
        public int inputSize = 784;
        public int outputSize = 128;
        public string layerType = "Linear";

        public void UpdateLayerConfig(int input, int output, string type)
        {
            inputSize = input;
            outputSize = output;
            layerType = type;
        }
    }

    /// <summary>
    /// UI component for selecting activation functions
    /// </summary>
    public class ActivationSelectorUI : MonoBehaviour
    {
        [Header("Activation Functions")]
        public List<string> availableActivations = new List<string> 
        { 
            "relu", "tanh", "sigmoid", "leaky_relu", "gelu", "swish" 
        };

        public string selectedActivation = "relu";

        public void SetActivation(string activation)
        {
            if (availableActivations.Contains(activation))
            {
                selectedActivation = activation;
                Debug.Log($"Selected activation: {activation}");
            }
        }
    }

    #endregion

    #region Visualization Components

    /// <summary>
    /// 3D visualizer for MLP networks
    /// </summary>
    public class MLPVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        public float layerSpacing = 5.0f;
        public float nodeSpacing = 2.0f;
        public float nodeScale = 1.0f;
        public Material nodeMaterial;
        public Material connectionMaterial;

        [Header("Animation")]
        public float animationSpeed = 1.0f;
        public bool showDataFlow = true;

        private List<List<GameObject>> layerNodes = new List<List<GameObject>>();
        private List<LineRenderer> connections = new List<LineRenderer>();

        public void VisualizeNetwork(List<int> layers)
        {
            ClearVisualization();
            CreateNodes(layers);
            CreateConnections();
        }

        private void ClearVisualization()
        {
            // Clear existing visualization
            foreach (var layer in layerNodes)
            {
                foreach (var node in layer)
                {
                    if (node != null) DestroyImmediate(node);
                }
            }
            layerNodes.Clear();

            foreach (var connection in connections)
            {
                if (connection != null) DestroyImmediate(connection.gameObject);
            }
            connections.Clear();
        }

        private void CreateNodes(List<int> layers)
        {
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = new List<GameObject>();
                int nodeCount = layers[layerIndex];

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    var node = CreateNode(layerIndex, nodeIndex, nodeCount);
                    layer.Add(node);
                }

                layerNodes.Add(layer);
            }
        }

        private GameObject CreateNode(int layerIndex, int nodeIndex, int totalNodes)
        {
            var node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = $"Node_L{layerIndex}_N{nodeIndex}";
            node.transform.SetParent(transform);

            // Position calculation
            float x = layerIndex * layerSpacing;
            float y = (nodeIndex - (totalNodes - 1) * 0.5f) * nodeSpacing;
            float z = 0;

            node.transform.localPosition = new Vector3(x, y, z);
            node.transform.localScale = Vector3.one * nodeScale;

            // Apply material
            if (nodeMaterial != null)
            {
                node.GetComponent<Renderer>().material = nodeMaterial;
            }

            // Add node component
            var nodeComponent = node.AddComponent<NeuralNode>();
            nodeComponent.layerIndex = layerIndex;
            nodeComponent.nodeIndex = nodeIndex;

            return node;
        }

        private void CreateConnections()
        {
            for (int layerIndex = 0; layerIndex < layerNodes.Count - 1; layerIndex++)
            {
                var currentLayer = layerNodes[layerIndex];
                var nextLayer = layerNodes[layerIndex + 1];

                foreach (var currentNode in currentLayer)
                {
                    foreach (var nextNode in nextLayer)
                    {
                        CreateConnection(currentNode, nextNode);
                    }
                }
            }
        }

        private void CreateConnection(GameObject fromNode, GameObject toNode)
        {
            var connectionObj = new GameObject($"Connection_{fromNode.name}_to_{toNode.name}");
            connectionObj.transform.SetParent(transform);

            var lineRenderer = connectionObj.AddComponent<LineRenderer>();
            lineRenderer.material = connectionMaterial;
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;
            lineRenderer.positionCount = 2;

            lineRenderer.SetPosition(0, fromNode.transform.position);
            lineRenderer.SetPosition(1, toNode.transform.position);

            connections.Add(lineRenderer);
        }
    }



    /// <summary>
    /// Individual neural node component
    /// </summary>
    public class NeuralNode : MonoBehaviour
    {
        public int layerIndex;
        public int nodeIndex;
    }

    #endregion
}