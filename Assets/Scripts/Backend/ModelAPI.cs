using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Archetype.Backend.API
{
    /// <summary>
    /// API wrapper for model management operations
    /// </summary>
    public class ModelAPI: MonoBehaviour
    {
        /// <summary>
        /// Create a new neural network model
        /// </summary>
        public static async Task<ModelResponse> CreateModel(ModelCreateRequest request)
        {
            return await BackendInterface.Instance.PostAsync<ModelResponse>("models", request);
        }

        /// <summary>
        /// Get list of all models
        /// </summary>
        public static async Task<ModelListResponse> GetModels(int skip = 0, int limit = 100)
        {
            return await BackendInterface.Instance.GetAsync<ModelListResponse>($"models?skip={skip}&limit={limit}");
        }

        /// <summary>
        /// Get specific model information
        /// </summary>
        public static async Task<ModelResponse> GetModel(string modelId)
        {
            return await BackendInterface.Instance.GetAsync<ModelResponse>($"models/{modelId}");
        }

        /// <summary>
        /// Update model parameters
        /// </summary>
        public static async Task<ApiResponse<object>> UpdateModel(string modelId, Dictionary<string, object> updates)
        {
            return await BackendInterface.Instance.PutAsync<ApiResponse<object>>($"models/{modelId}", updates);
        }

        /// <summary>
        /// Delete a model
        /// </summary>
        public static async Task DeleteModel(string modelId)
        {
            await BackendInterface.Instance.DeleteAsync($"models/{modelId}");
        }

        /// <summary>
        /// Export model in specified format
        /// </summary>
        public static async Task<ExportResponse> ExportModel(string modelId, string format)
        {
            var data = new { format = format };
            return await BackendInterface.Instance.PostAsync<ExportResponse>($"models/{modelId}/export", data);
        }
    }

    #region Model Data Classes

    [Serializable]
    public class ModelCreateRequest
    {
        public string name;
        public string model_type;
        public Dictionary<string, object> architecture;
        public Dictionary<string, object> hyperparameters;
    }

    [Serializable]
    public class ModelResponse
    {
        public string id;
        public string name;
        public string model_type;
        public Dictionary<string, object> architecture;
        public Dictionary<string, object> hyperparameters;
        public int parameter_count;
        public string created_at;
        public string status;
    }

    [Serializable]
    public class ModelListResponse
    {
        public List<ModelResponse> models;
        public int total;
    }

    [Serializable]
    public class ExportResponse
    {
        public string export_path;
        public string format;
    }

    #endregion
}