using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json;

namespace Archetype.Backend
{
    /// <summary>
    /// Manages WebSocket connection for real-time communication
    /// </summary>
    public class WebSocketManager
    {
        private WebSocket webSocket;
        private string wsUrl;
        private bool isConnected = false;

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnMessage;
        public event Action<string> OnError;

        public WebSocketManager(string url)
        {
            wsUrl = url;
        }

        public async void Connect()
        {
            try
            {
                webSocket = new WebSocket(wsUrl);

                webSocket.OnOpen += () =>
                {
                    isConnected = true;
                    Debug.Log("🔌 WebSocket connected");
                    OnConnected?.Invoke();
                };

                webSocket.OnError += (e) =>
                {
                    Debug.LogError($"❌ WebSocket error: {e}");
                    OnError?.Invoke(e);
                };

                webSocket.OnClose += (e) =>
                {
                    isConnected = false;
                    Debug.Log($"🔌 WebSocket closed: {e}");
                    OnDisconnected?.Invoke();
                };

                webSocket.OnMessage += (bytes) =>
                {
                    var message = System.Text.Encoding.UTF8.GetString(bytes);
                    OnMessage?.Invoke(message);
                };

                await webSocket.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Failed to connect WebSocket: {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }

        public async void SendMessage(string message)
        {
            if (isConnected && webSocket != null)
            {
                await webSocket.SendText(message);
            }
        }

        public async void SendJson(object data)
        {
            if (isConnected && webSocket != null)
            {
                string json = JsonConvert.SerializeObject(data);
                await webSocket.SendText(json);
            }
        }

        public void Disconnect()
        {
            if (webSocket != null)
            {
                webSocket.Close();
                webSocket = null;
            }
            isConnected = false;
        }

        public bool IsConnected => isConnected;

        // Call this from MonoBehaviour Update() to process WebSocket messages
        public void Update()
        {
            webSocket?.DispatchMessageQueue();
        }
    }
}