using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// WebSocket-based NetworkManager that replaces SignalR implementation
/// Provides real-time bidirectional communication without external DLL dependencies
/// </summary>
public class NetworkManager : MonoBehaviour
{
    [Header("Connection Settings")]
    public string ServerUrl = "ws://localhost:5207/ws";
    public float ReconnectDelay = 5f;
    public float HeartbeatInterval = 30f;

    // Events - same as SignalR version for compatibility
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<PlayerState> OnPlayerMoved;
    public static event Action<NetworkMessages.CombatActionMessage> OnCombatAction;
    public static event Action<NetworkMessages.ChatMessage> OnChatMessage;
    public static event Action<NetworkMessages.WorldUpdateMessage> OnWorldUpdate;
    public static event Action<NetworkMessages.PlayerJoinNotification> OnPlayerJoined;
    public static event Action<NetworkMessages.SystemNotification> OnSystemNotification;

    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isConnecting = false;
    private float _lastHeartbeat = 0f;
    private Queue<Action> _mainThreadActions = new Queue<Action>();
    private object _queueLock = new object();

    // Connection properties
    public bool IsConnected => _webSocket?.State == WebSocketState.Open;
    public string ConnectionId { get; private set; } = Guid.NewGuid().ToString();

    private void Start()
    {
        Debug.Log($"NetworkManager started with WebSockets - Connecting to: {ServerUrl}");
        ConnectToServer();
    }

    private void Update()
    {
        ProcessMainThreadQueue();

        // Heartbeat mechanism
        if (IsConnected && Time.time - _lastHeartbeat > HeartbeatInterval)
        {
            _lastHeartbeat = Time.time;
            _ = SendHeartbeat();
        }
    }

    #region Connection Management

    public async void ConnectToServer()
    {
        if (_isConnecting || IsConnected) return;
        
        _isConnecting = true;
        Debug.Log($"Connecting to WebSocket server: {ServerUrl}");

        try
        {
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            await _webSocket.ConnectAsync(new Uri(ServerUrl), _cancellationTokenSource.Token);
            
            QueueMainThreadAction(() => {
                Debug.Log($"Connected to WebSocket server successfully! Connection ID: {ConnectionId}");
                OnConnected?.Invoke();
            });

            // Start listening for messages
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to connect to WebSocket server: {ex.Message}");
            QueueMainThreadAction(() => OnDisconnected?.Invoke());
            
            // Auto-reconnect after delay
            await Task.Delay(TimeSpan.FromSeconds(ReconnectDelay));
            if (!IsConnected)
            {
                ConnectToServer();
            }
        }
        finally
        {
            _isConnecting = false;
        }
    }

    public async void DisconnectFromServer()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            _cancellationTokenSource?.Cancel();
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
        }
    }

    #endregion

    #region Message Receiving

    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 4];
        
        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("WebSocket connection closed by server");
                    QueueMainThreadAction(() => OnDisconnected?.Invoke());
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket receive error: {ex.Message}");
            QueueMainThreadAction(() => OnDisconnected?.Invoke());
            
            // Auto-reconnect after delay
            await Task.Delay(TimeSpan.FromSeconds(ReconnectDelay));
            if (!IsConnected)
            {
                ConnectToServer();
            }
        }
    }

    private void ProcessMessage(string jsonMessage)
    {
        try
        {
            // Parse the wrapper message to get the type
            var wrapper = JsonConvert.DeserializeObject<MessageWrapper>(jsonMessage);
            
            switch (wrapper.Type)
            {
                case "PlayerMovement":
                    var movementMsg = JsonConvert.DeserializeObject<NetworkMessages.PlayerMovementMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => {
                        var playerState = new PlayerState
                        {
                            PlayerId = movementMsg.PlayerId,
                            Position = movementMsg.Position,
                            Velocity = movementMsg.Velocity,
                            Rotation = movementMsg.Rotation
                        };
                        OnPlayerMoved?.Invoke(playerState);
                    });
                    break;
                    
                case "CombatAction":
                    var combatMsg = JsonConvert.DeserializeObject<NetworkMessages.CombatActionMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnCombatAction?.Invoke(combatMsg));
                    break;
                    
                case "ChatMessage":
                    var chatMsg = JsonConvert.DeserializeObject<NetworkMessages.ChatMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnChatMessage?.Invoke(chatMsg));
                    break;
                    
                case "WorldUpdate":
                    var worldMsg = JsonConvert.DeserializeObject<NetworkMessages.WorldUpdateMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnWorldUpdate?.Invoke(worldMsg));
                    break;
                    
                case "PlayerJoined":
                    var joinMsg = JsonConvert.DeserializeObject<NetworkMessages.PlayerJoinNotification>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnPlayerJoined?.Invoke(joinMsg));
                    break;
                    
                case "SystemNotification":
                    var sysMsg = JsonConvert.DeserializeObject<NetworkMessages.SystemNotification>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnSystemNotification?.Invoke(sysMsg));
                    break;
                    
                case "ConnectionConfirmed":
                    // Server confirmed our connection and assigned us an ID
                    var connData = JsonConvert.DeserializeObject<ConnectionData>(wrapper.Data.ToString());
                    ConnectionId = connData.ConnectionId;
                    Debug.Log($"Server assigned connection ID: {ConnectionId}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to process message: {ex.Message}");
            Debug.LogError($"Message content: {jsonMessage}");
        }
    }

    #endregion

    #region Message Sending

    // WebSocket Hub methods - same interface as SignalR version for compatibility
    public async Task SendMovement(Vector3 position, Vector3 velocity, float rotation)
    {
        if (!IsConnected) return;
        
        try
        {
            var message = new NetworkMessages.PlayerMovementMessage
            {
                PlayerId = ConnectionId,
                Position = position,
                Velocity = velocity,
                Rotation = rotation,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await SendMessage("PlayerMovement", message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send movement: {ex.Message}");
        }
    }

    public async Task SendAttack(string targetId, string attackType, Vector3 position)
    {
        if (!IsConnected) return;
        
        try
        {
            var message = new NetworkMessages.CombatActionMessage
            {
                AttackerId = ConnectionId,
                TargetId = targetId,
                AttackType = attackType,
                Position = position,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            await SendMessage("CombatAction", message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send attack: {ex.Message}");
        }
    }

    public async Task SendChatMessage(string message, string channelType, string targetId = null)
    {
        if (!IsConnected) return;
        
        try
        {
            var chatMessage = new NetworkMessages.ChatMessage
            {
                SenderId = ConnectionId,
                Message = message,
                ChannelType = channelType,
                TargetId = targetId,
                Timestamp = DateTime.UtcNow
            };
            
            await SendMessage("ChatMessage", chatMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send chat message: {ex.Message}");
        }
    }

    public async Task SendResourceGather(string resourceId, string resourceType, Vector3 position)
    {
        if (!IsConnected) return;
        
        try
        {
            var message = new NetworkMessages.ResourceGatherMessage
            {
                PlayerId = ConnectionId,
                ResourceId = resourceId,
                ResourceType = resourceType,
                Position = position
            };
            
            await SendMessage("ResourceGather", message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send resource gather: {ex.Message}");
        }
    }

    public async Task AuthenticatePlayer(string playerId, string playerName)
    {
        if (!IsConnected) 
        {
            Debug.LogWarning($"Cannot authenticate player {playerName} - not connected to server");
            return;
        }
        
        try
        {
            Debug.Log($"Sending authentication for player: {playerName} (ID: {playerId})");
            var authMessage = new { PlayerId = playerId, PlayerName = playerName };
            await SendMessage("Authentication", authMessage);
            Debug.Log($"Player authentication sent successfully: {playerName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to authenticate player: {ex.Message}");
        }
    }

    private async Task SendHeartbeat()
    {
        if (!IsConnected) return;
        
        try
        {
            var heartbeat = new { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            await SendMessage("Heartbeat", heartbeat);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Heartbeat failed: {ex.Message}");
        }
    }

    private async Task SendMessage(string messageType, object messageData)
    {
        if (!IsConnected) return;

        try
        {
            var wrapper = new MessageWrapper { Type = messageType, Data = messageData };
            var json = JsonConvert.SerializeObject(wrapper);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send message: {ex.Message}");
        }
    }

    #endregion

    #region Threading Helpers

    private void QueueMainThreadAction(Action action)
    {
        lock (_queueLock)
        {
            _mainThreadActions.Enqueue(action);
        }
    }

    private void ProcessMainThreadQueue()
    {
        lock (_queueLock)
        {
            while (_mainThreadActions.Count > 0)
            {
                var action = _mainThreadActions.Dequeue();
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error executing main thread action: {ex.Message}");
                }
            }
        }
    }

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        DisconnectFromServer();
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            DisconnectFromServer();
        }
        else
        {
            ConnectToServer();
        }
    }

    #endregion

    #region Helper Classes

    [Serializable]
    private class MessageWrapper
    {
        public string Type;
        public object Data;
    }

    [Serializable]
    private class ConnectionData
    {
        public string ConnectionId;
    }

    #endregion
}