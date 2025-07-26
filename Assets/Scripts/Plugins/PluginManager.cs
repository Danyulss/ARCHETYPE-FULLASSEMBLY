using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Archetype.Plugins.Core;

namespace Archetype.Plugins
{
    /// <summary>
    /// Manages Unity-side plugin loading and UI component instantiation
    /// </summary>
    public class PluginManager : MonoBehaviour
    {
        public static PluginManager Instance { get; private set; }

        [Header("Plugin Configuration")]
        public string pluginDirectory = "Plugins";
        public bool enableHotReload = true;
        public float hotReloadCheckInterval = 2.0f;

        [Header("Debug")]
        [SerializeField] private List<string> loadedPlugins = new List<string>();
        [SerializeField] private List<string> availableComponents = new List<string>();

        // Internal state
        private Dictionary<string, IUnityPlugin> plugins = new Dictionary<string, IUnityPlugin>();
        private Dictionary<string, PluginManifest> manifests = new Dictionary<string, PluginManifest>();
        private Dictionary<string, Assembly> pluginAssemblies = new Dictionary<string, Assembly>();
        
        // Events
        public event Action<string> OnPluginLoaded;
        public event Action<string> OnPluginUnloaded;
        public event Action<string> OnPluginError;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePluginManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            LoadCorePlugins();
            
            if (enableHotReload)
            {
                InvokeRepeating(nameof(CheckForPluginChanges), hotReloadCheckInterval, hotReloadCheckInterval);
            }
        }

        #endregion

        #region Initialization

        private void InitializePluginManager()
        {
            // Create plugin directory if it doesn't exist
            string pluginPath = Path.Combine(Application.streamingAssetsPath, pluginDirectory);
            if (!Directory.Exists(pluginPath))
            {
                Directory.CreateDirectory(pluginPath);
            }

            Debug.Log($"🔌 Plugin Manager initialized - Directory: {pluginPath}");
        }

        private void LoadCorePlugins()
        {
            Debug.Log("🔌 Loading core Unity plugins...");

            // Load built-in plugins from assemblies
            LoadPluginFromAssembly("MLPPlugin", typeof(MLPPlugin));
            LoadPluginFromAssembly("RNNPlugin", typeof(RNNPlugin));
            LoadPluginFromAssembly("CNNPlugin", typeof(CNNPlugin));

            Debug.Log($"✅ Loaded {plugins.Count} core plugins");
            UpdateDebugLists();
        }

        #endregion

        #region Plugin Loading

        private void LoadPluginFromAssembly(string pluginId, Type pluginType)
        {
            try
            {
                if (!typeof(IUnityPlugin).IsAssignableFrom(pluginType))
                {
                    Debug.LogError($"❌ {pluginType.Name} does not implement IUnityPlugin");
                    return;
                }

                // Create plugin instance
                var plugin = (IUnityPlugin)Activator.CreateInstance(pluginType);
                
                // Initialize plugin
                if (plugin.Initialize())
                {
                    plugins[pluginId] = plugin;
                    manifests[pluginId] = plugin.GetManifest();
                    
                    Debug.Log($"✅ Loaded plugin: {pluginId}");
                    OnPluginLoaded?.Invoke(pluginId);
                }
                else
                {
                    Debug.LogError($"❌ Failed to initialize plugin: {pluginId}");
                    OnPluginError?.Invoke($"Failed to initialize {pluginId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error loading plugin {pluginId}: {e.Message}");
                OnPluginError?.Invoke($"Error loading {pluginId}: {e.Message}");
            }
        }

        public async Task<bool> LoadExternalPlugin(string pluginPath)
        {
            try
            {
                // Load manifest
                string manifestPath = Path.Combine(pluginPath, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    Debug.LogError($"❌ Plugin manifest not found: {manifestPath}");
                    return false;
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestJson);

                // Load assembly
                string assemblyPath = Path.Combine(pluginPath, manifest.assemblyName);
                if (!File.Exists(assemblyPath))
                {
                    Debug.LogError($"❌ Plugin assembly not found: {assemblyPath}");
                    return false;
                }

                var assembly = Assembly.LoadFrom(assemblyPath);
                var pluginType = assembly.GetType(manifest.pluginClassName);

                if (pluginType == null)
                {
                    Debug.LogError($"❌ Plugin class not found: {manifest.pluginClassName}");
                    return false;
                }

                // Load plugin
                LoadPluginFromAssembly(manifest.id, pluginType);
                pluginAssemblies[manifest.id] = assembly;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error loading external plugin: {e.Message}");
                OnPluginError?.Invoke($"Error loading external plugin: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Plugin Management

        public bool UnloadPlugin(string pluginId)
        {
            if (!plugins.ContainsKey(pluginId))
                return false;

            try
            {
                // Cleanup plugin
                plugins[pluginId].Cleanup();
                
                // Remove from collections
                plugins.Remove(pluginId);
                manifests.Remove(pluginId);
                pluginAssemblies.Remove(pluginId);

                Debug.Log($"🔌 Unloaded plugin: {pluginId}");
                OnPluginUnloaded?.Invoke(pluginId);
                UpdateDebugLists();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error unloading plugin {pluginId}: {e.Message}");
                OnPluginError?.Invoke($"Error unloading {pluginId}: {e.Message}");
                return false;
            }
        }

        public bool IsPluginLoaded(string pluginId)
        {
            return plugins.ContainsKey(pluginId);
        }

        public IUnityPlugin GetPlugin(string pluginId)
        {
            return plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }

        public PluginManifest GetPluginManifest(string pluginId)
        {
            return manifests.TryGetValue(pluginId, out var manifest) ? manifest : null;
        }

        public List<string> GetLoadedPlugins()
        {
            return plugins.Keys.ToList();
        }

        public List<PluginManifest> GetAllManifests()
        {
            return manifests.Values.ToList();
        }

        #endregion

        #region Component Creation

        public GameObject CreateUIComponent(string pluginId, string componentType, Transform parent = null)
        {
            if (!plugins.TryGetValue(pluginId, out var plugin))
            {
                Debug.LogError($"❌ Plugin not found: {pluginId}");
                return null;
            }

            try
            {
                return plugin.CreateUIComponent(componentType, parent);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error creating UI component {componentType} from plugin {pluginId}: {e.Message}");
                OnPluginError?.Invoke($"Error creating component {componentType}: {e.Message}");
                return null;
            }
        }

        public MonoBehaviour CreateVisualizationComponent(string pluginId, string componentType, Transform parent = null)
        {
            if (!plugins.TryGetValue(pluginId, out var plugin))
            {
                Debug.LogError($"❌ Plugin not found: {pluginId}");
                return null;
            }

            try
            {
                return plugin.CreateVisualizationComponent(componentType, parent);
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error creating visualization component {componentType} from plugin {pluginId}: {e.Message}");
                OnPluginError?.Invoke($"Error creating visualization component {componentType}: {e.Message}");
                return null;
            }
        }

        #endregion

        #region Hot Reload

        private void CheckForPluginChanges()
        {
            // Implementation for hot reload functionality
            // Check file timestamps, reload changed plugins
        }

        #endregion

        #region Debug Helpers

        private void UpdateDebugLists()
        {
            loadedPlugins = plugins.Keys.ToList();
            availableComponents.Clear();
            
            foreach (var manifest in manifests.Values)
            {
                availableComponents.AddRange(manifest.uiComponents);
                availableComponents.AddRange(manifest.visualizationComponents);
            }
        }

        [ContextMenu("Debug: List All Plugins")]
        private void DebugListPlugins()
        {
            Debug.Log($"🔌 Loaded Plugins ({plugins.Count}):");
            foreach (var kvp in plugins)
            {
                var manifest = manifests[kvp.Key];
                Debug.Log($"  - {kvp.Key}: {manifest.name} v{manifest.version}");
            }
        }

        #endregion
    }

    #region Plugin Interfaces

    /// <summary>
    /// Interface that all Unity plugins must implement
    /// </summary>
    public interface IUnityPlugin
    {
        bool Initialize();
        void Cleanup();
        PluginManifest GetManifest();
        GameObject CreateUIComponent(string componentType, Transform parent);
        MonoBehaviour CreateVisualizationComponent(string componentType, Transform parent);
        List<string> GetAvailableUIComponents();
        List<string> GetAvailableVisualizationComponents();
    }

    #endregion

    #region Data Structures

    [Serializable]
    public class PluginManifest
    {
        public string id;
        public string name;
        public string version;
        public string description;
        public string author;
        public string category;
        public List<string> dependencies = new List<string>();
        public List<string> uiComponents = new List<string>();
        public List<string> visualizationComponents = new List<string>();
        public List<string> neuralComponentTypes = new List<string>();
        public string assemblyName;
        public string pluginClassName;
        public Dictionary<string, object> parameters = new Dictionary<string, object>();
    }

    #endregion
}