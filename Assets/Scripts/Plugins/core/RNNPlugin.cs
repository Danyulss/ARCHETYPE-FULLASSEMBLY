using System;
using System.Collections.Generic;
using UnityEngine;
using Archetype.Visualization;

namespace Archetype.Plugins.Core
{
    /// <summary>
    /// Recurrent Neural Network plugin for Unity
    /// </summary>
    public class RNNPlugin : IUnityPlugin
    {
        private bool initialized = false;
        private PluginManifest manifest;

        public bool Initialize()
        {
            try
            {
                manifest = new PluginManifest
                {
                    id = "rnn_core",
                    name = "Recurrent Neural Network",
                    version = "1.0.0", 
                    description = "LSTM, GRU, and RNN implementations with temporal visualization",
                    author = "Archetype Core Team",
                    category = "core",
                    uiComponents = new List<string> { "RNNBuilder", "TemporalConfig", "SequenceConfig" },
                    visualizationComponents = new List<string> { "RNNVisualizer", "TemporalFlow", "MemoryStateVisualizer" },
                    neuralComponentTypes = new List<string> { "lstm_layer", "gru_layer", "rnn_layer" }
                };

                initialized = true;
                Debug.Log("✅ RNN Plugin initialized");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to initialize RNN Plugin: {e.Message}");
                return false;
            }
        }

        public void Cleanup()
        {
            initialized = false;
            Debug.Log("🧹 RNN Plugin cleanup complete");
        }

        public PluginManifest GetManifest()
        {
            return manifest;
        }

        public GameObject CreateUIComponent(string componentType, Transform parent)
        {
            switch (componentType)
            {
                case "RNNBuilder":
                    return CreateRNNBuilder(parent);
                case "TemporalConfig":
                    return CreateTemporalConfig(parent);
                case "SequenceConfig":
                    return CreateSequenceConfig(parent);
                default:
                    throw new ArgumentException($"Unknown UI component type: {componentType}");
            }
        }

        public MonoBehaviour CreateVisualizationComponent(string componentType, Transform parent)
        {
            switch (componentType)
            {
                case "RNNVisualizer":
                    return CreateRNNVisualizer(parent);
                case "TemporalFlow":
                    return CreateTemporalFlow(parent);
                case "MemoryStateVisualizer":
                    return CreateMemoryStateVisualizer(parent);
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

        private GameObject CreateRNNBuilder(Transform parent)
        {
            var go = new GameObject("RNNBuilder");
            if (parent != null) go.transform.SetParent(parent, false);
            
            var rnnBuilder = go.AddComponent<RNNBuilderUI>();
            return go;
        }

        private GameObject CreateTemporalConfig(Transform parent)
        {
            var go = new GameObject("TemporalConfig");
            if (parent != null) go.transform.SetParent(parent, false);
            
            var temporalConfig = go.AddComponent<TemporalConfigUI>();
            return go;
        }

        private GameObject CreateSequenceConfig(Transform parent)
        {
            var go = new GameObject("SequenceConfig");
            if (parent != null) go.transform.SetParent(parent, false);
            
            var sequenceConfig = go.AddComponent<SequenceConfigUI>();
            return go;
        }

        private MonoBehaviour CreateRNNVisualizer(Transform parent)
        {
            var go = new GameObject("RNNVisualizer");
            if (parent != null) go.transform.SetParent(parent, false);
            
            return go.AddComponent<RNNVisualizer>();
        }

        private MonoBehaviour CreateTemporalFlow(Transform parent)
        {
            var go = new GameObject("TemporalFlow");
            if (parent != null) go.transform.SetParent(parent, false);
            
            return go.AddComponent<TemporalFlow>();
        }

        private MonoBehaviour CreateMemoryStateVisualizer(Transform parent)
        {
            var go = new GameObject("MemoryStateVisualizer");
            if (parent != null) go.transform.SetParent(parent, false);
            
            return go.AddComponent<MemoryStateVisualizer>();
        }

        #endregion
    }

    #region RNN UI Components

    /// <summary>
    /// UI component for building RNN networks
    /// </summary>
    public class RNNBuilderUI : MonoBehaviour
    {
        [Header("RNN Configuration")]
        public int inputSize = 100;
        public int hiddenSize = 128;
        public int numLayers = 2;
        public int outputSize = 10;
        public string rnnType = "LSTM";
        public bool bidirectional = false;
        public float dropout = 0.0f;

        [Header("UI References")]
        public UnityEngine.UI.Button createModelButton;
        public UnityEngine.UI.Dropdown rnnTypeDropdown;
        public UnityEngine.UI.InputField inputSizeField;
        public UnityEngine.UI.InputField hiddenSizeField;
        public UnityEngine.UI.InputField numLayersField;
        public UnityEngine.UI.InputField outputSizeField;
        public UnityEngine.UI.Toggle bidirectionalToggle;
        public UnityEngine.UI.Slider dropoutSlider;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (createModelButton != null)
                createModelButton.onClick.AddListener(CreateRNNModel);

            if (rnnTypeDropdown != null)
            {
                rnnTypeDropdown.options.Clear();
                rnnTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("LSTM"));
                rnnTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("GRU"));
                rnnTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("RNN"));
                rnnTypeDropdown.onValueChanged.AddListener(OnRNNTypeChanged);
            }

            if (bidirectionalToggle != null)
                bidirectionalToggle.onValueChanged.AddListener(OnBidirectionalChanged);

            if (dropoutSlider != null)
                dropoutSlider.onValueChanged.AddListener(OnDropoutChanged);
        }

        private void OnRNNTypeChanged(int value)
        {
            string[] types = { "LSTM", "GRU", "RNN" };
            if (value >= 0 && value < types.Length)
                rnnType = types[value];
        }

        private void OnBidirectionalChanged(bool value)
        {
            bidirectional = value;
        }

        private void OnDropoutChanged(float value)
        {
            dropout = value;
        }

        private async void CreateRNNModel()
        {
            try
            {
                var architecture = new Dictionary<string, object>
                {
                    ["input_size"] = inputSize,
                    ["hidden_size"] = hiddenSize,
                    ["num_layers"] = numLayers,
                    ["output_size"] = outputSize,
                    ["rnn_type"] = rnnType,
                    ["bidirectional"] = bidirectional
                };

                var hyperparameters = new Dictionary<string, object>
                {
                    ["dropout"] = dropout,
                    ["batch_first"] = true
                };

                var request = new Backend.API.ModelCreateRequest
                {
                    name = $"RNN_{rnnType}_{DateTime.Now:yyyyMMdd_HHmmss}",
                    model_type = "rnn",
                    architecture = architecture,
                    hyperparameters = hyperparameters
                };

                var response = await Backend.API.ModelAPI.CreateModel(request);
                Debug.Log($"✅ Created RNN model: {response.id} with {response.parameter_count:N0} parameters");

                // Trigger visualization
                await NeuralNetworkVisualizer.Instance.VisualizeNetwork(response.id);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to create RNN model: {e.Message}");
            }
        }
    }

    /// <summary>
    /// UI component for temporal configuration
    /// </summary>
    public class TemporalConfigUI : MonoBehaviour
    {
        [Header("Temporal Settings")]
        public int sequenceLength = 50;
        public float timeStep = 0.1f;
        public bool showHistory = true;
        public int maxHistorySteps = 100;

        [Header("UI References")]
        public UnityEngine.UI.InputField sequenceLengthField;
        public UnityEngine.UI.InputField timeStepField;
        public UnityEngine.UI.Toggle showHistoryToggle;
        public UnityEngine.UI.Slider historyStepsSlider;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (sequenceLengthField != null)
                sequenceLengthField.onValueChanged.AddListener(OnSequenceLengthChanged);

            if (timeStepField != null)
                timeStepField.onValueChanged.AddListener(OnTimeStepChanged);

            if (showHistoryToggle != null)
                showHistoryToggle.onValueChanged.AddListener(OnShowHistoryChanged);

            if (historyStepsSlider != null)
                historyStepsSlider.onValueChanged.AddListener(OnHistoryStepsChanged);
        }

        private void OnSequenceLengthChanged(string value)
        {
            if (int.TryParse(value, out int length))
                sequenceLength = Mathf.Max(1, length);
        }

        private void OnTimeStepChanged(string value)
        {
            if (float.TryParse(value, out float step))
                timeStep = Mathf.Max(0.001f, step);
        }

        private void OnShowHistoryChanged(bool value)
        {
            showHistory = value;
        }

        private void OnHistoryStepsChanged(float value)
        {
            maxHistorySteps = Mathf.RoundToInt(value);
        }
    }

    /// <summary>
    /// UI component for sequence configuration
    /// </summary>
    public class SequenceConfigUI : MonoBehaviour
    {
        [Header("Sequence Settings")]
        public string inputType = "sequential";
        public bool variableLength = false;
        public int minLength = 10;
        public int maxLength = 100;

        [Header("UI References")]
        public UnityEngine.UI.Dropdown inputTypeDropdown;
        public UnityEngine.UI.Toggle variableLengthToggle;
        public UnityEngine.UI.InputField minLengthField;
        public UnityEngine.UI.InputField maxLengthField;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (inputTypeDropdown != null)
            {
                inputTypeDropdown.options.Clear();
                inputTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("sequential"));
                inputTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("batch"));
                inputTypeDropdown.options.Add(new UnityEngine.UI.Dropdown.OptionData("streaming"));
                inputTypeDropdown.onValueChanged.AddListener(OnInputTypeChanged);
            }

            if (variableLengthToggle != null)
                variableLengthToggle.onValueChanged.AddListener(OnVariableLengthChanged);
        }

        private void OnInputTypeChanged(int value)
        {
            string[] types = { "sequential", "batch", "streaming" };
            if (value >= 0 && value < types.Length)
                inputType = types[value];
        }

        private void OnVariableLengthChanged(bool value)
        {
            variableLength = value;
        }
    }

    #endregion

    #region RNN Visualization Components

    /// <summary>
    /// 3D visualizer for RNN networks
    /// </summary>
    public class RNNVisualizer : MonoBehaviour
    {
        [Header("RNN Visualization")]
        public float cellSpacing = 3.0f;
        public float timeStepSpacing = 2.0f;
        public int visibleTimeSteps = 10;
        public Material cellMaterial;
        public Material hiddenStateMaterial;
        public Material memoryMaterial;

        private List<GameObject> rnnCells = new List<GameObject>();
        private List<GameObject> hiddenStates = new List<GameObject>();
        private List<GameObject> memoryStates = new List<GameObject>();
        private List<LineRenderer> temporalConnections = new List<LineRenderer>();

        public void VisualizeRNN(int hiddenSize, int numLayers, string rnnType)
        {
            ClearVisualization();
            CreateRNNCells(hiddenSize, numLayers, rnnType);
            CreateTemporalConnections();
        }

        private void ClearVisualization()
        {
            foreach (var cell in rnnCells)
                if (cell != null) DestroyImmediate(cell);
            rnnCells.Clear();

            foreach (var state in hiddenStates)
                if (state != null) DestroyImmediate(state);
            hiddenStates.Clear();

            foreach (var memory in memoryStates)
                if (memory != null) DestroyImmediate(memory);
            memoryStates.Clear();

            foreach (var connection in temporalConnections)
                if (connection != null) DestroyImmediate(connection.gameObject);
            temporalConnections.Clear();
        }

        private void CreateRNNCells(int hiddenSize, int numLayers, string rnnType)
        {
            for (int layer = 0; layer < numLayers; layer++)
            {
                for (int step = 0; step < visibleTimeSteps; step++)
                {
                    var cell = CreateRNNCell(layer, step, rnnType);
                    rnnCells.Add(cell);
                }
            }
        }

        private GameObject CreateRNNCell(int layer, int timeStep, string rnnType)
        {
            var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.name = $"RNNCell_L{layer}_T{timeStep}";
            cell.transform.SetParent(transform);

            // Position calculation
            float x = timeStep * timeStepSpacing;
            float y = layer * cellSpacing;
            float z = 0;

            cell.transform.localPosition = new Vector3(x, y, z);
            
            // Scale based on RNN type
            Vector3 scale = rnnType == "LSTM" ? new Vector3(1.2f, 1.2f, 1.2f) : Vector3.one;
            cell.transform.localScale = scale;

            // Apply material and color based on type
            var renderer = cell.GetComponent<Renderer>();
            if (cellMaterial != null)
            {
                renderer.material = cellMaterial;
                
                // Color by RNN type
                Color typeColor = rnnType switch
                {
                    "LSTM" => Color.blue,
                    "GRU" => Color.green,
                    "RNN" => Color.cyan,
                    _ => Color.white
                };
                renderer.material.color = typeColor;
            }

            // Add RNN cell component
            var cellComponent = cell.AddComponent<RNNCell>();
            cellComponent.layer = layer;
            cellComponent.timeStep = timeStep;
            cellComponent.cellType = rnnType;

            return cell;
        }

        private void CreateTemporalConnections()
        {
            // Create connections between time steps
            for (int layer = 0; layer < rnnCells.Count / visibleTimeSteps; layer++)
            {
                for (int step = 0; step < visibleTimeSteps - 1; step++)
                {
                    int currentIndex = layer * visibleTimeSteps + step;
                    int nextIndex = layer * visibleTimeSteps + (step + 1);
                    
                    if (currentIndex < rnnCells.Count && nextIndex < rnnCells.Count)
                    {
                        CreateTemporalConnection(rnnCells[currentIndex], rnnCells[nextIndex]);
                    }
                }
            }
        }

        private void CreateTemporalConnection(GameObject fromCell, GameObject toCell)
        {
            var connectionObj = new GameObject($"TemporalConnection_{fromCell.name}_to_{toCell.name}");
            connectionObj.transform.SetParent(transform);

            var lineRenderer = connectionObj.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.positionCount = 2;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.material.color = Color.yellow;

            lineRenderer.SetPosition(0, fromCell.transform.position);
            lineRenderer.SetPosition(1, toCell.transform.position);

            temporalConnections.Add(lineRenderer);
        }

        public void UpdateRNNState(int layer, int timeStep, float[] hiddenState, float[] cellState = null)
        {
            int cellIndex = layer * visibleTimeSteps + timeStep;
            if (cellIndex < rnnCells.Count)
            {
                var cellComponent = rnnCells[cellIndex].GetComponent<RNNCell>();
                cellComponent?.UpdateState(hiddenState, cellState);
            }
        }
    }

    /// <summary>
    /// Temporal flow visualization
    /// </summary>
    public class TemporalFlow : MonoBehaviour
    {
        [Header("Flow Settings")]
        public float flowSpeed = 1.0f;
        public Color flowColor = Color.cyan;
        public float particleLifetime = 2.0f;
        public int maxParticles = 100;

        private ParticleSystem flowParticles;

        private void Start()
        {
            SetupParticleSystem();
        }

        private void SetupParticleSystem()
        {
            flowParticles = gameObject.AddComponent<ParticleSystem>();
            
            var main = flowParticles.main;
            main.startColor = flowColor;
            main.startSpeed = flowSpeed;
            main.startLifetime = particleLifetime;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = flowParticles.emission;
            emission.rateOverTime = 10;

            var shape = flowParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
        }

        public void ShowTemporalFlow(Vector3 start, Vector3 end)
        {
            if (flowParticles != null)
            {
                transform.position = start;
                
                var shape = flowParticles.shape;
                Vector3 direction = (end - start).normalized;
                transform.LookAt(end);
                
                var velocityOverLifetime = flowParticles.velocityOverLifetime;
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
                velocityOverLifetime.x = 0;
                velocityOverLifetime.y = 0;
                velocityOverLifetime.z = Vector3.Distance(start, end) / particleLifetime;
            }
        }
    }

    /// <summary>
    /// Memory state visualization for LSTM cells
    /// </summary>
    public class MemoryStateVisualizer : MonoBehaviour
    {
        [Header("Memory Visualization")]
        public float memoryIntensity = 1.0f;
        public Color shortTermColor = Color.yellow;
        public Color longTermColor = Color.red;
        public Material memoryMaterial;

        private List<GameObject> memoryVisualizers = new List<GameObject>();

        public void InitializeMemoryVisualizers(int memorySize)
        {
            ClearMemoryVisualizers();

            for (int i = 0; i < memorySize; i++)
            {
                var memViz = CreateMemoryVisualizer(i);
                memoryVisualizers.Add(memViz);
            }
        }

        private GameObject CreateMemoryVisualizer(int index)
        {
            var memViz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            memViz.name = $"MemoryViz_{index}";
            memViz.transform.SetParent(transform);
            
            // Arrange in a grid
            int gridWidth = 8;
            float x = (index % gridWidth) * 0.3f;
            float y = (index / gridWidth) * 0.3f;
            
            memViz.transform.localPosition = new Vector3(x, y, 0);
            memViz.transform.localScale = Vector3.one * 0.2f;

            if (memoryMaterial != null)
                memViz.GetComponent<Renderer>().material = memoryMaterial;

            return memViz;
        }

        public void UpdateMemoryState(float[] memoryValues)
        {
            for (int i = 0; i < memoryValues.Length && i < memoryVisualizers.Count; i++)
            {
                var renderer = memoryVisualizers[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    float value = Mathf.Abs(memoryValues[i]) * memoryIntensity;
                    Color memoryColor = Color.Lerp(shortTermColor, longTermColor, value);
                    renderer.material.color = memoryColor;

                    // Scale based on memory strength
                    float scale = 0.1f + (value * 0.3f);
                    memoryVisualizers[i].transform.localScale = Vector3.one * scale;
                }
            }
        }

        private void ClearMemoryVisualizers()
        {
            foreach (var viz in memoryVisualizers)
            {
                if (viz != null) DestroyImmediate(viz);
            }
            memoryVisualizers.Clear();
        }
    }

    /// <summary>
    /// Individual RNN cell component
    /// </summary>
    public class RNNCell : MonoBehaviour
    {
        public int layer;
        public int timeStep;
        public string cellType;
        public float[] hiddenState;
        public float[] cellState; // For LSTM

        private Renderer cellRenderer;
        private MaterialPropertyBlock propertyBlock;

        private void Start()
        {
            cellRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        public void UpdateState(float[] hidden, float[] cell = null)
        {
            hiddenState = hidden;
            if (cell != null) cellState = cell;
            
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (hiddenState != null && cellRenderer != null)
            {
                // Calculate average activation
                float avgActivation = 0f;
                foreach (float val in hiddenState)
                    avgActivation += Mathf.Abs(val);
                avgActivation /= hiddenState.Length;

                // Update color based on activation
                Color stateColor = Color.Lerp(Color.gray, Color.green, avgActivation);
                
                propertyBlock.SetColor("_Color", stateColor);
                cellRenderer.SetPropertyBlock(propertyBlock);

                // Update scale based on activation strength
                float scale = 0.8f + (avgActivation * 0.4f);
                transform.localScale = Vector3.one * scale;
            }
        }

        private void OnMouseDown()
        {
            if (hiddenState != null)
            {
                Debug.Log($"RNN Cell - Layer: {layer}, Time: {timeStep}, Type: {cellType}");
                Debug.Log($"Hidden State Length: {hiddenState.Length}");
                if (cellState != null)
                    Debug.Log($"Cell State Length: {cellState.Length}");
            }
        }
    }

    #endregion
}