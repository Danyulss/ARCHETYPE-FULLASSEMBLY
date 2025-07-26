using System.Collections.Generic;
using UnityEngine;

namespace Archetype.Visualization
{
    /// <summary>
    /// Visualization for a single neural network layer
    /// </summary>
    public class LayerVisualization : MonoBehaviour
    {
        [Header("Layer Info")]
        [SerializeField] protected int layerIndex;
        [SerializeField] protected int nodeCount;
        [SerializeField] protected LayerType layerType;

        [Header("Nodes")]
        [SerializeField] protected List<NodeVisualization> nodes = new List<NodeVisualization>();

        protected VisualizationSettings settings;
        protected bool isHighlighted = false;
        protected int currentLODLevel = 0;

        #region Initialization

        public virtual void Initialize(int index, int count, LayerType type, VisualizationSettings visualSettings)
        {
            layerIndex = index;
            nodeCount = count;
            layerType = type;
            settings = visualSettings;

            CreateNodes();
        }

        protected virtual void CreateNodes()
        {
            // Limit nodes for performance
            int visibleNodes = Mathf.Min(nodeCount, NeuralNetworkVisualizer.Instance.maxVisibleNodes / 4);

            for (int i = 0; i < visibleNodes; i++)
            {
                var node = CreateNode(i);
                nodes.Add(node);
            }

            ArrangeNodes();
        }

        protected virtual NodeVisualization CreateNode(int nodeIndex)
        {
            var nodeGO = new GameObject($"Node_{layerIndex}_{nodeIndex}");
            nodeGO.transform.SetParent(transform);

            var nodeViz = nodeGO.AddComponent<NodeVisualization>();
            nodeViz.Initialize(layerIndex, nodeIndex, layerType, settings);

            return nodeViz;
        }

        protected virtual void ArrangeNodes()
        {
            // Arrange in a vertical line by default
            float totalHeight = (nodeCount - 1) * settings.nodeSpacing;
            float startY = totalHeight * 0.5f;

            for (int i = 0; i < nodes.Count; i++)
            {
                float y = startY - (i * settings.nodeSpacing);
                nodes[i].transform.localPosition = new Vector3(0, y, 0);
            }
        }

        #endregion

        #region Updates

        public virtual void UpdateHighDetail()
        {
            foreach (var node in nodes)
            {
                node?.UpdateVisualization();
            }
        }

        public virtual void UpdateLowDetail()
        {
            // Simplified update for performance
            // Maybe only update every few frames or reduce detail
        }

        public virtual void SetTintColor(Color color)
        {
            foreach (var node in nodes)
            {
                node?.SetTintColor(color);
            }
        }

        public virtual void SetHighlighted(bool highlighted)
        {
            isHighlighted = highlighted;

            foreach (var node in nodes)
            {
                node?.SetHighlighted(highlighted);
            }
        }

        public virtual void SetLODLevel(int lodLevel)
        {
            currentLODLevel = lodLevel;

            foreach (var node in nodes)
            {
                node?.SetLODLevel(lodLevel);
            }
        }

        public virtual void ApplySettings(VisualizationSettings newSettings)
        {
            settings = newSettings;

            foreach (var node in nodes)
            {
                node?.ApplySettings(settings);
            }

            ArrangeNodes();
        }

        #endregion

        #region Public Properties

        public int LayerIndex => layerIndex;
        public int NodeCount => nodeCount;
        public LayerType Type => layerType;
        public List<NodeVisualization> Nodes => nodes;

        #endregion
    }

    /// <summary>
    /// RNN-specific layer visualization
    /// </summary>
    public class RNNLayerVisualization : LayerVisualization
    {
        [Header("RNN Specific")]
        public int timeSteps = 5;
        public float timeStepSpacing = 1.0f;

        protected override void ArrangeNodes()
        {
            // Arrange RNN nodes in a grid showing time steps
            int nodesPerTimeStep = Mathf.CeilToInt((float)nodes.Count / timeSteps);

            for (int i = 0; i < nodes.Count; i++)
            {
                int timeStep = i / nodesPerTimeStep;
                int nodeInStep = i % nodesPerTimeStep;

                float x = timeStep * timeStepSpacing;
                float y = (nodeInStep - (nodesPerTimeStep - 1) * 0.5f) * settings.nodeSpacing;

                nodes[i].transform.localPosition = new Vector3(x, y, 0);
            }
        }
    }

    /// <summary>
    /// CNN-specific layer visualization
    /// </summary>
    public class ConvLayerVisualization : LayerVisualization
    {
        [Header("CNN Specific")]
        public int featureMapWidth = 4;
        public int featureMapHeight = 4;
        public float featureMapSpacing = 0.5f;

        protected override void ArrangeNodes()
        {
            // Arrange CNN nodes as feature maps
            int mapsPerRow = 4;
            int mapIndex = 0;

            for (int i = 0; i < nodes.Count; i += featureMapWidth * featureMapHeight)
            {
                int mapRow = mapIndex / mapsPerRow;
                int mapCol = mapIndex % mapsPerRow;

                float mapX = mapCol * (featureMapWidth * featureMapSpacing + 1.0f);
                float mapY = mapRow * (featureMapHeight * featureMapSpacing + 1.0f);

                // Arrange nodes within this feature map
                for (int j = 0; j < featureMapWidth * featureMapHeight && i + j < nodes.Count; j++)
                {
                    int row = j / featureMapWidth;
                    int col = j % featureMapWidth;

                    float x = mapX + col * featureMapSpacing;
                    float y = mapY + row * featureMapSpacing;

                    nodes[i + j].transform.localPosition = new Vector3(x, y, 0);
                }

                mapIndex++;
            }
        }
    }
    
    
}