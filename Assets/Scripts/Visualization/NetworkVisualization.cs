using System;
using System.Collections.Generic;
using UnityEngine;

namespace Archetype.Visualization
{
    /// <summary>
    /// Individual network visualization component
    /// </summary>
    public class NetworkVisualization : MonoBehaviour
    {
        [Header("Network Info")]
        [SerializeField] private string modelId;
        [SerializeField] private string modelName;
        [SerializeField] private string modelType;

        [Header("Visualization Elements")]
        [SerializeField] private List<LayerVisualization> layers = new List<LayerVisualization>();
        [SerializeField] private List<ConnectionRenderer> connections = new List<ConnectionRenderer>();

        private Backend.API.ModelResponse modelInfo;
        private VisualizationSettings settings;
        private bool isHighlighted = false;
        private int currentLODLevel = 0;

        #region Initialization

        public void Initialize(Backend.API.ModelResponse info, VisualizationSettings visualSettings)
        {
            modelInfo = info;
            modelId = info.id;
            modelName = info.name;
            modelType = info.model_type;
            settings = visualSettings;

            CreateVisualization();
        }

        private void CreateVisualization()
        {
            switch (modelType.ToLower())
            {
                case "mlp":
                    CreateMLPVisualization();
                    break;
                case "rnn":
                    CreateRNNVisualization();
                    break;
                case "cnn":
                    CreateCNNVisualization();
                    break;
                default:
                    CreateGenericVisualization();
                    break;
            }

            ApplySettings(settings);
        }

        #endregion

        #region Model-Specific Visualizations

        private void CreateMLPVisualization()
        {
            if (!modelInfo.architecture.ContainsKey("layers"))
                return;

            var layerSizes = modelInfo.architecture["layers"] as List<object>;
            if (layerSizes == null) return;

            // Create layers
            for (int i = 0; i < layerSizes.Count; i++)
            {
                int nodeCount = Convert.ToInt32(layerSizes[i]);
                var layer = CreateLayer(i, nodeCount, GetLayerType(i, layerSizes.Count));
                layers.Add(layer);
            }

            // Create connections
            CreateMLPConnections();
        }

        private void CreateRNNVisualization()
        {
            var hiddenSize = Convert.ToInt32(modelInfo.architecture.GetValueOrDefault("hidden_size", 128));
            var numLayers = Convert.ToInt32(modelInfo.architecture.GetValueOrDefault("num_layers", 2));
            var inputSize = Convert.ToInt32(modelInfo.architecture.GetValueOrDefault("input_size", 100));
            var outputSize = Convert.ToInt32(modelInfo.architecture.GetValueOrDefault("output_size", 10));

            // Create RNN-specific visualization
            for (int i = 0; i < numLayers; i++)
            {
                var layer = CreateRNNLayer(i, hiddenSize);
                layers.Add(layer);
            }
        }

        private void CreateCNNVisualization()
        {
            var convLayers = modelInfo.architecture.GetValueOrDefault("conv_layers", new List<object>()) as List<object>;
            if (convLayers == null) return;

            // Create convolutional layers
            for (int i = 0; i < convLayers.Count; i++)
            {
                int filters = Convert.ToInt32(convLayers[i]);
                var layer = CreateConvLayer(i, filters);
                layers.Add(layer);
            }
        }

        private void CreateGenericVisualization()
        {
            // Generic fallback visualization
            var layer = CreateLayer(0, 10, LayerType.Hidden);
            layers.Add(layer);
        }

        #endregion

        #region Layer Creation

        private LayerVisualization CreateLayer(int layerIndex, int nodeCount, LayerType layerType)
        {
            var layerGO = new GameObject($"Layer_{layerIndex}");
            layerGO.transform.SetParent(transform);
            layerGO.transform.localPosition = new Vector3(layerIndex * settings.layerSpacing, 0, 0);

            var layerViz = layerGO.AddComponent<LayerVisualization>();
            layerViz.Initialize(layerIndex, nodeCount, layerType, settings);

            return layerViz;
        }

        private LayerVisualization CreateRNNLayer(int layerIndex, int hiddenSize)
        {
            var layerGO = new GameObject($"RNNLayer_{layerIndex}");
            layerGO.transform.SetParent(transform);
            layerGO.transform.localPosition = new Vector3(layerIndex * settings.layerSpacing, 0, 0);

            var layerViz = layerGO.AddComponent<RNNLayerVisualization>();
            layerViz.Initialize(layerIndex, hiddenSize, LayerType.Hidden, settings);

            return layerViz;
        }

        private LayerVisualization CreateConvLayer(int layerIndex, int filters)
        {
            var layerGO = new GameObject($"ConvLayer_{layerIndex}");
            layerGO.transform.SetParent(transform);
            layerGO.transform.localPosition = new Vector3(layerIndex * settings.layerSpacing, 0, 0);

            var layerViz = layerGO.AddComponent<ConvLayerVisualization>();
            layerViz.Initialize(layerIndex, filters, LayerType.Hidden, settings);

            return layerViz;
        }

        private LayerType GetLayerType(int layerIndex, int totalLayers)
        {
            if (layerIndex == 0) return LayerType.Input;
            if (layerIndex == totalLayers - 1) return LayerType.Output;
            return LayerType.Hidden;
        }

        #endregion

        #region Connections

        private void CreateMLPConnections()
        {
            for (int i = 0; i < layers.Count - 1; i++)
            {
                CreateLayerConnections(layers[i], layers[i + 1]);
            }
        }

        private void CreateLayerConnections(LayerVisualization fromLayer, LayerVisualization toLayer)
        {
            var connectionGO = new GameObject($"Connections_{fromLayer.LayerIndex}_to_{toLayer.LayerIndex}");
            connectionGO.transform.SetParent(transform);

            var connectionRenderer = connectionGO.AddComponent<ConnectionRenderer>();
            connectionRenderer.Initialize(fromLayer, toLayer, settings);

            connections.Add(connectionRenderer);
        }

        #endregion

        #region Updates and Animation

        public void UpdateVisualization()
        {
            // Update based on current LOD level
            if (currentLODLevel == 0)
            {
                // High detail update
                foreach (var layer in layers)
                {
                    layer?.UpdateHighDetail();
                }
            }
            else
            {
                // Low detail update
                foreach (var layer in layers)
                {
                    layer?.UpdateLowDetail();
                }
            }
        }

        public void UpdateMetrics(Dictionary<string, float> metrics)
        {
            // Update visualization based on training metrics
            if (metrics.ContainsKey("loss"))
            {
                float loss = metrics["loss"];
                UpdateLossVisualization(loss);
            }

            if (metrics.ContainsKey("accuracy"))
            {
                float accuracy = metrics["accuracy"];
                UpdateAccuracyVisualization(accuracy);
            }
        }

        private void UpdateLossVisualization(float loss)
        {
            // Color network based on loss
            Color lossColor = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(loss));
            
            foreach (var layer in layers)
            {
                layer?.SetTintColor(lossColor);
            }
        }

        private void UpdateAccuracyVisualization(float accuracy)
        {
            // Scale or pulse network based on accuracy
            float scale = 0.8f + (accuracy * 0.4f); // Scale between 0.8 and 1.2
            transform.localScale = Vector3.one * scale;
        }

        #endregion

        #region Public Interface

        public void ApplySettings(VisualizationSettings newSettings)
        {
            settings = newSettings;
            
            foreach (var layer in layers)
            {
                layer?.ApplySettings(settings);
            }

            foreach (var connection in connections)
            {
                connection?.ApplySettings(settings);
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;
            
            foreach (var layer in layers)
            {
                layer?.SetHighlighted(highlighted);
            }
        }

        public void SetLODLevel(int lodLevel)
        {
            currentLODLevel = lodLevel;
            
            foreach (var layer in layers)
            {
                layer?.SetLODLevel(lodLevel);
            }

            foreach (var connection in connections)
            {
                connection?.SetLODLevel(lodLevel);
            }
        }

        public string ModelId => modelId;
        public string ModelName => modelName;
        public string ModelType => modelType;

        #endregion
    }

    #region Enums

    #endregion
}