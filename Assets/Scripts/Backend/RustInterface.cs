using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Collections;
using UnityEditor.Compilation;

namespace Archetype.Backend
{
    public class RustInterface : MonoBehaviour
    {
        public static RustInterface Instance { get; private set; }
        
        [Header("Backend Configuration")]
        public int backendPort = 8080;
        public float connectionTimeout = 10.0f;
        
        private Process rustBackend;
        private HttpClient httpClient;
        private bool isInitialized = false;
        private string baseUrl;
        
        public event Action OnBackendConnected;
        public event Action<string> OnBackendError;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeHTTPClient();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            StartCoroutine(InitializeBackend());
        }
        
        private void InitializeHTTPClient()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            baseUrl = $"http://localhost:{backendPort}";
        }
        
        private IEnumerator InitializeBackend()
        {
            yield return StartCoroutine(StartRustBackend());
            yield return StartCoroutine(WaitForBackendReady());
            
            if (isInitialized)
            {
                OnBackendConnected?.Invoke();
                UnityEngine.Debug.Log("✅ Rust backend connected successfully");
            }
            else
            {
                OnBackendError?.Invoke("Failed to connect to Rust backend");
                UnityEngine.Debug.LogError("❌ Rust backend connection failed");
            }
        }
        
        private IEnumerator StartRustBackend()
        {
            try
            {
                string backendPath = GetRustBackendPath();
                
                if (!System.IO.File.Exists(backendPath))
                {
                    UnityEngine.Debug.LogError($"Rust backend not found at: {backendPath}");
                    yield break;
                }
                
                rustBackend = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = backendPath,
                        Arguments = $"--headless --port {backendPort}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                rustBackend.OutputDataReceived += OnBackendOutput;
                
                rustBackend.Start();
                rustBackend.BeginOutputReadLine();
                rustBackend.BeginErrorReadLine();
                
                UnityEngine.Debug.Log("🚀 Starting Rust backend...");
                new WaitForSeconds(3.0f); // Give backend time to start
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to start Rust backend: {e.Message}");
            }
        }

        private bool returnHealthCheck()
        {
            return SendHealthCheck();
        }
        
        private IEnumerator WaitForBackendReady()
        {
            float elapsed = 0;

            while (elapsed < connectionTimeout)
            {
                try
                {
                    var response = returnHealthCheck();

                    if (response)
                    {
                        isInitialized = true;
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log($"Backend not ready yet: {e.Message}");
                }

                elapsed += 1.0f;
                yield return new WaitForSeconds(1.0f);
            }
        }
        
        private bool SendHealthCheck()
        {
            var request = new UnityEngine.Networking.UnityWebRequest($"{baseUrl}/health", "GET");
            //return request.SendWebRequest();
            
            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public async Task<T> CallRustCommand<T>(string command, object parameters = null)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Rust backend not initialized");
            }
            
            try
            {
                var payload = new
                {
                    command = command,
                    parameters = parameters
                };
                
                string jsonPayload = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{baseUrl}/command", content);
                response.EnsureSuccessStatusCode();
                
                string responseContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseContent);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Rust command '{command}' failed: {e.Message}");
                throw;
            }
        }
        
        private string GetRustBackendPath()
        {
            #if UNITY_EDITOR
                return Application.streamingAssetsPath + "/archetype-backend.exe";
            #elif UNITY_STANDALONE_WIN
                return Application.streamingAssetsPath + "/archetype-backend.exe";
            #elif UNITY_STANDALONE_OSX
                return Application.streamingAssetsPath + "/archetype-backend";
            #elif UNITY_STANDALONE_LINUX
                return Application.streamingAssetsPath + "/archetype-backend";
            #else
                throw new PlatformNotSupportedException("Platform not supported");
            #endif
        }
        
        private void OnBackendOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.Log($"[Rust Backend] {e.Data}");
            }
        }
        
        private void OnDestroy()
        {
            if (rustBackend != null && !rustBackend.HasExited)
            {
                rustBackend.Kill();
                rustBackend.Dispose();
            }
            
            httpClient?.Dispose();
        }
        
        private void OnApplicationQuit()
        {
            OnDestroy();
        }
    }
}
