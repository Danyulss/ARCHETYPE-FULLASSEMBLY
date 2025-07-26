using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Archetype.Backend.API
{
    /// <summary>
    /// API wrapper for training operations
    /// </summary>
    public class TrainingAPI : MonoBehaviour
    {
        /// <summary>
        /// Start training a model
        /// </summary>
        public static async Task<TrainingResponse> StartTraining(TrainingRequest request)
        {
            return await BackendInterface.Instance.PostAsync<TrainingResponse>("training/start", request);
        }

        /// <summary>
        /// Stop training
        /// </summary>
        public static async Task<ApiResponse<object>> StopTraining(string trainingId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"training/{trainingId}/stop");
        }

        /// <summary>
        /// Pause training
        /// </summary>
        public static async Task<ApiResponse<object>> PauseTraining(string trainingId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"training/{trainingId}/pause");
        }

        /// <summary>
        /// Resume training
        /// </summary>
        public static async Task<ApiResponse<object>> ResumeTraining(string trainingId)
        {
            return await BackendInterface.Instance.PostAsync<ApiResponse<object>>($"training/{trainingId}/resume");
        }

        /// <summary>
        /// Get training status
        /// </summary>
        public static async Task<TrainingResponse> GetTrainingStatus(string trainingId)
        {
            return await BackendInterface.Instance.GetAsync<TrainingResponse>($"training/{trainingId}");
        }

        /// <summary>
        /// List all trainings
        /// </summary>
        public static async Task<TrainingListResponse> GetTrainings(string status = null)
        {
            string endpoint = "training";
            if (!string.IsNullOrEmpty(status))
                endpoint += $"?status={status}";

            return await BackendInterface.Instance.GetAsync<TrainingListResponse>(endpoint);
        }

        /// <summary>
        /// Get detailed training metrics
        /// </summary>
        public static async Task<TrainingMetrics> GetTrainingMetrics(string trainingId)
        {
            return await BackendInterface.Instance.GetAsync<TrainingMetrics>($"training/{trainingId}/metrics");
        }
    }

    #region Training Data Classes

    [Serializable]
    public class TrainingRequest
    {
        public string model_id;
        public Dictionary<string, object> dataset_config;
        public Dictionary<string, object> training_config;
        public Dictionary<string, object> validation_config;
    }

    [Serializable]
    public class TrainingResponse
    {
        public string id;
        public string status;
        public string model_id;
        public string dataset_id;
        public string validation_dataset_id;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class TrainingListResponse
    {
        public List<TrainingResponse> trainings;
    }

    [Serializable]
    public class TrainingMetrics
    {
        public List<TrainingMetric> metrics;
    }

    [Serializable]
    public class TrainingMetric
    {
        public string name;
        public List<float> values;
    } //PLACEHOLDERS!!!!!!!!!

    #endregion
}