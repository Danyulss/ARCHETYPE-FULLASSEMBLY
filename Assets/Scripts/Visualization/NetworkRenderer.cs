using System.Collections.Generic;
using UnityEngine;

namespace Archetype.Visualization
{
    public class NetworkRenderer : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject nodePrefab;
        public GameObject connectionPrefab;
        
        [Header("Layout Settings")]
        public float layerSpacing = 5.0f;
        public float nodeSpacing = 2.0f;
        public float animationSpeed = 1.0f;
        
        [Header("Visual Settings")]
        public Color inputLayerColor = Color.green;
        public Color hiddenLayerColor = Color.blue;
        public Color outputLayerColor = Color.red;
        
        private List<GameObject> nodeObjects = new List<GameObject>();
        private List<GameObject> connectionObjects = new List<GameObject>();
        private List<NetworkLayer> layers = new List<NetworkLayer>();
        
        private void Start()
        {
            CreateDefaultNetwork();
        }
        
        private void CreateDefaultNetwork()
        {
            // Create a simple default neural network for visualization
            CreateLayer("Input", 4, inputLayerColor);
            CreateLayer("Hidden", 8, hiddenLayerColor);
            CreateLayer("Hidden", 6, hiddenLayerColor);
            CreateLayer("Output", 2, outputLayerColor);
            
            RenderNetwork();
        }
        
        public void CreateLayer(string layerType)
        {
            Color layerColor = layerType switch
            {
                "Dense" => hiddenLayerColor,
                "Convolutional" => Color.yellow,
                "LSTM" => Color.magenta,
                "Dropout" => Color.gray,
                _ => hiddenLayerColor
            };
            
            int nodeCount = layerType switch
            {
                "Dense" => 128,
                "Convolutional" => 64,
                "LSTM" => 256,
                "Dropout" => 0, // Dropout doesn't change node count
                _ => 64
            };
            
            CreateLayer(layerType, nodeCount, layerColor);
            RenderNetwork();
        }
        
        private void CreateLayer(string name, int nodeCount, Color color)
        {
            var layer = new NetworkLayer
            {
                name = name,
                nodeCount = nodeCount,
                color = color,
                layerIndex = layers.Count
            };
            
            layers.Add(layer);
            Debug.Log($"✅ Created {name} layer with {nodeCount} nodes");
        }
        
        private void RenderNetwork()
        {
            ClearVisualization();
            RenderLayers();
            RenderConnections();
        }
        
        private void ClearVisualization()
        {
            foreach (var node in nodeObjects)
            {
                if (node != null) DestroyImmediate(node);
            }
            foreach (var connection in connectionObjects)
            {
                if (connection != null) DestroyImmediate(connection);
            }
            
            nodeObjects.Clear();
            connectionObjects.Clear();
        }
        
        private void RenderLayers()
        {
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                RenderLayer(layers[layerIndex]);
            }
        }
        
        private void RenderLayer(NetworkLayer layer)
        {
            int displayNodeCount = Mathf.Min(layer.nodeCount, 20); // Limit visual nodes for performance
            
            Vector3 layerPosition = new Vector3(
                (layer.layerIndex - layers.Count * 0.5f) * layerSpacing,
                0,
                0
            );
            
            for (int nodeIndex = 0; nodeIndex < displayNodeCount; nodeIndex++)
            {
                Vector3 nodePosition = layerPosition + new Vector3(
                    0,
                    (nodeIndex - displayNodeCount * 0.5f) * nodeSpacing,
                    0
                );
                
                GameObject nodeObj = CreateNode(nodePosition, layer.color);
                nodeObj.name = $"{layer.name}_Node_{nodeIndex}";
                nodeObjects.Add(nodeObj);
            }
        }
        
        private GameObject CreateNode(Vector3 position, Color color)
        {
            GameObject node;
            
            if (nodePrefab != null)
            {
                node = Instantiate(nodePrefab, position, Quaternion.identity, transform);
            }
            else
            {
                // Create default node if no prefab provided
                node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                node.transform.position = position;
                node.transform.localScale = Vector3.one * 0.3f;
                node.transform.SetParent(transform);
            }
            
            var renderer = node.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = color;
                material.SetFloat("_Metallic", 0.2f);
                material.SetFloat("_Smoothness", 0.8f);
                renderer.material = material;
            }
            
            return node;
        }
        
        private void RenderConnections()
        {
            for (int i = 0; i < layers.Count - 1; i++)
            {
                RenderConnectionsBetweenLayers(i, i + 1);
            }
        }
        
        private void RenderConnectionsBetweenLayers(int fromLayerIndex, int toLayerIndex)
        {
            var fromLayer = layers[fromLayerIndex];
            var toLayer = layers[toLayerIndex];
            
            int fromNodeCount = Mathf.Min(fromLayer.nodeCount, 20);
            int toNodeCount = Mathf.Min(toLayer.nodeCount, 20);
            
            // Limit connections for performance
            int maxConnections = 50;
            int connectionCount = 0;
            
            for (int fromNode = 0; fromNode < fromNodeCount && connectionCount < maxConnections; fromNode++)
            {
                for (int toNode = 0; toNode < toNodeCount && connectionCount < maxConnections; toNode++)
                {
                    if (Random.Range(0f, 1f) < 0.3f) // Show only 30% of connections
                    {
                        CreateConnection(fromLayerIndex, fromNode, toLayerIndex, toNode);
                        connectionCount++;
                    }
                }
            }
        }
        
        private void CreateConnection(int fromLayerIndex, int fromNodeIndex, int toLayerIndex, int toNodeIndex)
        {
            Vector3 startPos = GetNodePosition(fromLayerIndex, fromNodeIndex);
            Vector3 endPos = GetNodePosition(toLayerIndex, toNodeIndex);
            
            GameObject connection;
            
            if (connectionPrefab != null)
            {
                connection = Instantiate(connectionPrefab, transform);
            }
            else
            {
                // Create default line connection
                connection = new GameObject("Connection");
                connection.transform.SetParent(transform);
                
                var lineRenderer = connection.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lineRenderer.endColor = Color.white * 0.3f;
                lineRenderer.startWidth = 0.02f;
                lineRenderer.endWidth = 0.02f;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;
                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, endPos);
            }
            
            connectionObjects.Add(connection);
        }
        
        private Vector3 GetNodePosition(int layerIndex, int nodeIndex)
        {
            var layer = layers[layerIndex];
            int displayNodeCount = Mathf.Min(layer.nodeCount, 20);
            
            Vector3 layerPosition = new Vector3(
                (layerIndex - layers.Count * 0.5f) * layerSpacing,
                0,
                0
            );
            
            return layerPosition + new Vector3(
                0,
                (nodeIndex - displayNodeCount * 0.5f) * nodeSpacing,
                0
            );
        }
        
        public void AnimateTrainingPass()
        {
            // Animate nodes during training
            StartCoroutine(TrainingAnimationCoroutine());
        }
        
        private System.Collections.IEnumerator TrainingAnimationCoroutine()
        {
            foreach (var nodeObj in nodeObjects)
            {
                if (nodeObj != null)
                {
                    StartCoroutine(PulseNode(nodeObj));
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        private System.Collections.IEnumerator PulseNode(GameObject node)
        {
            Vector3 originalScale = node.transform.localScale;
            Vector3 targetScale = originalScale * 1.5f;
            
            float duration = 0.5f;
            float elapsed = 0;
            
            // Scale up
            while (elapsed < duration / 2)
            {
                float progress = elapsed / (duration / 2);
                node.transform.localScale = Vector3.Lerp(originalScale, targetScale, progress);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            elapsed = 0;
            
            // Scale down
            while (elapsed < duration / 2)
            {
                float progress = elapsed / (duration / 2);
                node.transform.localScale = Vector3.Lerp(targetScale, originalScale, progress);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            node.transform.localScale = originalScale;
        }
    }
    
    [System.Serializable]
    public class NetworkLayer
    {
        public string name;
        public int nodeCount;
        public Color color;
        public int layerIndex;
    }
}
