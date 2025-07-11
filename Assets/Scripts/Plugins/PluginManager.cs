using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Archetype.Backend;

namespace Archetype.Plugins
{
    public class PluginManager : MonoBehaviour
    {
        public static PluginManager Instance { get; private set; }
        
        [Header("Plugin Configuration")]
        public bool autoLoadCorePlugins = true;
        
        private List<PluginInfo> availablePlugins = new List<PluginInfo>();
        private List<PluginInfo> loadedPlugins = new List<PluginInfo>();
        
        public event Action<List<PluginInfo>> OnPluginsLoaded;
        public event Action<PluginInfo> OnPluginStatusChanged;
        public event Action<string> OnPluginError;
        
        public List<PluginInfo> AvailablePlugins => new List<PluginInfo>(availablePlugins);
        public List<PluginInfo> LoadedPlugins => new List<PluginInfo>(loadedPlugins);
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            if (RustInterface.Instance != null)
            {
                RustInterface.Instance.OnBackendConnected += OnBackendReady;
            }
        }
        
        private async void OnBackendReady()
        {
            await RefreshAvailablePlugins();
            
            if (autoLoadCorePlugins)
            {
                await LoadCorePlugins();
            }
        }
        
        public async Task RefreshAvailablePlugins()
        {
            try
            {
                Debug.Log("🔍 Refreshing available plugins...");
                
                var response = await RustInterface.Instance.CallRustCommand<PluginListResponse>(
                    "get_available_plugins");
                
                availablePlugins.Clear();
                availablePlugins.AddRange(response.plugins);
                
                Debug.Log($"✅ Found {availablePlugins.Count} available plugins");
                OnPluginsLoaded?.Invoke(availablePlugins);
                
                foreach (var plugin in availablePlugins)
                {
                    Debug.Log($"   Plugin: {plugin.display_name} v{plugin.version} ({plugin.category})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to refresh plugins: {e.Message}");
                OnPluginError?.Invoke($"Failed to refresh plugins: {e.Message}");
            }
        }
        
        public async Task LoadPlugin(string pluginName)
        {
            try
            {
                Debug.Log($"🔄 Loading plugin: {pluginName}");
                
                await RustInterface.Instance.CallRustCommand<object>("load_plugin", 
                    new { plugin_name = pluginName });
                
                var plugin = availablePlugins.Find(p => p.name == pluginName);
                if (plugin != null)
                {
                    plugin.is_loaded = true;
                    if (!loadedPlugins.Contains(plugin))
                    {
                        loadedPlugins.Add(plugin);
                    }
                    OnPluginStatusChanged?.Invoke(plugin);
                }
                
                Debug.Log($"✅ Loaded plugin: {pluginName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to load plugin {pluginName}: {e.Message}");
                OnPluginError?.Invoke($"Failed to load plugin {pluginName}: {e.Message}");
            }
        }
        
        public async Task UnloadPlugin(string pluginName)
        {
            try
            {
                Debug.Log($"🔄 Unloading plugin: {pluginName}");
                
                await RustInterface.Instance.CallRustCommand<object>("unload_plugin", 
                    new { plugin_name = pluginName });
                
                var plugin = loadedPlugins.Find(p => p.name == pluginName);
                if (plugin != null)
                {
                    plugin.is_loaded = false;
                    loadedPlugins.Remove(plugin);
                    OnPluginStatusChanged?.Invoke(plugin);
                }
                
                Debug.Log($"✅ Unloaded plugin: {pluginName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to unload plugin {pluginName}: {e.Message}");
                OnPluginError?.Invoke($"Failed to unload plugin {pluginName}: {e.Message}");
            }
        }
        
        private async Task LoadCorePlugins()
        {
            Debug.Log("🚀 Loading core plugins...");
            
            var corePlugins = availablePlugins.FindAll(p => p.is_core);
            
            foreach (var plugin in corePlugins)
            {
                await LoadPlugin(plugin.name);
            }
            
            Debug.Log($"✅ Loaded {corePlugins.Count} core plugins");
        }
        
        public List<PluginInfo> GetPluginsByCategory(string category)
        {
            return availablePlugins.FindAll(p => p.category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }
        
        public bool IsPluginLoaded(string pluginName)
        {
            return loadedPlugins.Exists(p => p.name == pluginName);
        }
        
        public PluginInfo GetPluginInfo(string pluginName)
        {
            return availablePlugins.Find(p => p.name == pluginName);
        }
        
        private void OnDestroy()
        {
            if (RustInterface.Instance != null)
            {
                RustInterface.Instance.OnBackendConnected -= OnBackendReady;
            }
        }
    }
    
    [Serializable]
    public class PluginListResponse
    {
        public PluginInfo[] plugins;
    }
    
    [Serializable]
    public class PluginInfo
    {
        public string name;
        public string display_name;
        public string version;
        public string description;
        public string category;
        public bool is_loaded;
        public bool is_core;
    }
}
