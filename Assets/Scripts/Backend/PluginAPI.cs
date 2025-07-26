using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Archetype.Backend.API
{
    /// <summary>
    /// API wrapper for plugin operations
    /// </summary>
    public static class PluginAPI
    {
        /// <summary>
        /// Get list of all plugins
        /// </summary>
        public static async Task<PluginListResponse> GetPlugins()
        {
            return await BackendInterface.Instance.GetAsync<PluginListResponse>("plugins");
        }

        /// <summary>
        /// Get specific plugin information
        /// </summary>
        public static async Task<PluginInfo> GetPlugin(string pluginId)
        {
            return await BackendInterface.Instance.GetAsync<PluginInfo>($"plugins/{pluginId}");
        }

        /// <summary>
        /// Load a plugin
        /// </summary>
        public static async Task<ApiResponse<object>> LoadPlugin(string pluginId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"plugins/{pluginId}/load");
        }

        /// <summary>
        /// Unload a plugin
        /// </summary>
        public static async Task<ApiResponse<object>> UnloadPlugin(string pluginId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"plugins/{pluginId}/unload");
        }

        /// <summary>
        /// Enable a plugin
        /// </summary>
        public static async Task<ApiResponse<object>> EnablePlugin(string pluginId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"plugins/{pluginId}/enable");
        }

        /// <summary>
        /// Disable a plugin
        /// </summary>
        public static async Task<ApiResponse<object>> DisablePlugin(string pluginId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"plugins/{pluginId}/disable");
        }

        /// <summary>
        /// Get plugin categories
        /// </summary>
        public static async Task<PluginCategoriesResponse> GetPluginCategories()
        {
            return await BackendInterface.Instance.GetAsync<PluginCategoriesResponse>("plugins/categories");
        }
    }

    #region Plugin Data Classes

    [Serializable]
    public class PluginInfo
    {
        public string id;
        public string name;
        public string version;
        public string description;
        public string author;
        public string plugin_type;
        public bool loaded;
        public bool enabled;
        public List<string> dependencies;
        public Dictionary<string, object> manifest;
    }

    [Serializable]
    public class PluginListResponse
    {
        public List<PluginInfo> plugins;
        public int total;
    }

    [Serializable]
    public class PluginCategoriesResponse
    {
        public List<string> categories;
    }

    #endregion

    #region Response Classes

    [Serializable]
    public class HealthResponse
    {
        public string status;
        public string version;
        public float uptime;
        public string timestamp;
        public bool gpu_available;
        public int gpu_count;
        public float memory_usage_percent;
        public float cpu_usage_percent;
    }

    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public T data;
        public string error;
        public string message;
    }

    #endregion
}