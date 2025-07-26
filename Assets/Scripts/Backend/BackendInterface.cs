using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Archetype.Backend.API;

namespace Archetype.Backend
{
    /// <summary>
    /// Main interface for communicating with the Python FastAPI backend
    /// </summary>

    public class BackendInterface : MonoBehaviour
    {
        public static BackendInterface Instance { get; private set; }
        
        private IEnumerator PeriodicHealthCheck()
        {
            while (isConnected)
            {
                yield return new WaitForSeconds(10.0f); // Check every 10 seconds
                
                yield return StartCoroutine(CheckBackendHealth());
                
                if (!isConnected)
                {
                    Debug.LogWarning("⚠️ Lost connection to backend");
                    OnBackendDisconnected?.Invoke();
                    break;
                }
            }
        }
        
        private IEnumerator CheckBackendHealth()
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{baseUrl}/api/v1/health"))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var healthResponse = JsonConvert.DeserializeObject<HealthResponse>(request.downloadHandler.text);

                        if (healthResponse?.status == "healthy")
                        {
                            isConnected = true;
                            connectionStatus = "Connected";
                            lastPingTime = Time.time;
                            Debug.Log($"🏥 Backend health check passed - GPU: {healthResponse.gpu_available}");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ Backend unhealthy: {healthResponse?.status}");
                        }
                    }
                    catch (Exception e)
                    {
                        // Fallback: if we get any response, assume backend is running
                        Debug.Log($"✅ Backend responding (JSON parse failed: {e.Message})");
                        isConnected = true;
                        connectionStatus = "Connected";
                        lastPingTime = Time.time;
                    }
                }
                else
                {
                    isConnected = false;
                    connectionStatus = $"Error: {request.error}";
                    Debug.Log($"⚠️ Health check failed: {request.error} (Code: {request.responseCode})");
                }
            }
        }
        
        private IEnumerator StartPythonBackend()
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pythonExecutable,
                    Arguments = $"\"{backendScriptPath}\" --host {backendHost} --port {backendPort}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = System.Diagnostics.Process.Start(startInfo);

                if (process != null)
                {
                    Debug.Log("🐍 Python backend process started");
                }
                else
                {
                    Debug.LogError("❌ Failed to start Python backend process");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Exception starting Python backend: {e.Message}");
            }

            yield return null;
        }

        private IEnumerator WaitForBackendReady()
        {
            float elapsed = 0f;
            const int maxAttempts = 15; // 30 seconds total
            int attempts = 0;

            while (elapsed < connectionTimeout && attempts < maxAttempts)
            {
                yield return StartCoroutine(CheckBackendHealth());

                if (isConnected)
                {
                    Debug.Log($"✅ Backend ready after {elapsed:F1}s ({attempts + 1} attempts)");
                    yield break;
                }

                attempts++;
                elapsed += 2.0f;
                yield return new WaitForSeconds(2.0f);

                Debug.Log($"⏳ Backend check {attempts}/{maxAttempts} - still waiting...");
            }

            Debug.LogWarning($"⚠️ Backend connection timeout after {elapsed:F1}s");
        }

        private IEnumerator ConnectToBackend()
        {
            connectionStatus = "Connecting...";

            if (autoStartBackend)
            {
                Debug.Log("🚀 Attempting to start Python backend...");
                yield return StartCoroutine(StartPythonBackend());
                yield return new WaitForSeconds(3.0f); // Give backend time to start
            }

            Debug.Log("🔍 Attempting to connect to backend...");
            yield return StartCoroutine(WaitForBackendReady());

            if (isConnected)
            {
                OnBackendConnected?.Invoke();
                Debug.Log("✅ Backend connected successfully");

                // Start periodic health checks
                healthCheckCoroutine = StartCoroutine(PeriodicHealthCheck());

                // Connect WebSocket
                webSocketManager.Connect();
            }
            else
            {
                string errorMsg = "Failed to connect to backend. Please ensure Python backend is running.";
                OnBackendError?.Invoke(errorMsg);
                Debug.LogError($"❌ {errorMsg}");
                connectionStatus = "Failed";
            }
        }
        
        private void Start()
        {
            StartCoroutine(ConnectToBackend());
        }

        private void OnDestroy()
        {
            if (healthCheckCoroutine != null)
            {
                StopCoroutine(healthCheckCoroutine);
            }
            webSocketManager?.Disconnect();
        }

        private void InitializeBackend()
        {
            baseUrl = $"http://{backendHost}:{backendPort}";
            webSocketManager = new WebSocketManager($"ws://{backendHost}:{backendPort}/ws");

            Debug.Log($"🔧 Backend Interface initialized - Target: {baseUrl}");
        }

        //////////////////////////////////////////////////////////////////////////////////////
        
        public async Task<T> GetAsync<T>(string endpoint)
        {
            if (!isConnected)
                throw new InvalidOperationException("Backend not connected");

            string url = $"{baseUrl}/api/v1/{endpoint.TrimStart('/')}";
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    return JsonConvert.DeserializeObject<T>(json);
                }
                else
                {
                    throw new Exception($"GET {endpoint} failed: {request.error}");
                }
            }
        }

        public async Task<T> PostAsync<T>(string endpoint, object data = null)
        {
            if (!isConnected)
                throw new InvalidOperationException("Backend not connected");

            string url = $"{baseUrl}/api/v1/{endpoint.TrimStart('/')}";
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                if (data != null)
                {
                    string json = JsonConvert.SerializeObject(data);
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                
                request.downloadHandler = new DownloadHandlerBuffer();
                
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    return JsonConvert.DeserializeObject<T>(json);
                }
                else
                {
                    throw new Exception($"POST {endpoint} failed: {request.error}");
                }
            }
        }

        public async Task<T> PutAsync<T>(string endpoint, object data)
        {
            if (!isConnected)
                throw new InvalidOperationException("Backend not connected");

            string url = $"{baseUrl}/api/v1/{endpoint.TrimStart('/')}";
            
            using (UnityWebRequest request = UnityWebRequest.Put(url, JsonConvert.SerializeObject(data)))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    return JsonConvert.DeserializeObject<T>(json);
                }
                else
                {
                    throw new Exception($"PUT {endpoint} failed: {request.error}");
                }
            }
        }

        public async Task DeleteAsync(string endpoint)
        {
            if (!isConnected)
                throw new InvalidOperationException("Backend not connected");

            string url = $"{baseUrl}/api/v1/{endpoint.TrimStart('/')}";
            
            using (UnityWebRequest request = UnityWebRequest.Delete(url))
            {
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"DELETE {endpoint} failed: {request.error}");
                }
            }
        }

        public bool IsConnected => isConnected;
        public string ConnectionStatus => connectionStatus;
        public string BaseUrl => baseUrl;
        public WebSocketManager WebSocket => webSocketManager;

        public void RetryConnection()
        {
            if (!isConnected)
            {
                StartCoroutine(ConnectToBackend());
            }
        }

        public void Disconnect()
        {
            isConnected = false;
            connectionStatus = "Disconnected";
            
            if (healthCheckCoroutine != null)
            {
                StopCoroutine(healthCheckCoroutine);
                healthCheckCoroutine = null;
            }
            
            webSocketManager?.Disconnect();
            OnBackendDisconnected?.Invoke();
        }

        /////////////////////////////////////////////////////////////////////////////////////

        [Header("Backend Configuration")]
        public string backendHost = "localhost";
        public int backendPort = 8000;
        public float connectionTimeout = 30.0f;
        public bool autoStartBackend = true;
        public string pythonExecutable = "python";
        public string backendScriptPath = "Backend/main.py";

        [Header("Status")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private string connectionStatus = "Disconnected";
        [SerializeField] private float lastPingTime = 0f;

        // Events
        public event Action OnBackendConnected;
        public event Action<string> OnBackendError;
        public event Action OnBackendDisconnected;

        // Internal state
        private string baseUrl;
        private Coroutine healthCheckCoroutine;
        private WebSocketManager webSocketManager;

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeBackend();
            }

        }

        [Serializable]
        public class TrainingResponse
        {
            public string training_id;
            public string model_id;
            public string status;
            public int current_epoch;
            public int total_epochs;
            public Dictionary<string, float> metrics;
            public float estimated_time_remaining;
        }

        [Serializable]
        public class TrainingListResponse
        {
            public List<TrainingResponse> trainings;
            public int total;
        }

        [Serializable]
        public class TrainingMetrics
        {
            public string training_id;
            public Dictionary<string, float> current_metrics;
            public float progress;
            public float elapsed_time;
            public float estimated_remaining;
            public string status;
        }

        #endregion
    }
}