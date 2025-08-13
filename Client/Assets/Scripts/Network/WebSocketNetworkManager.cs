using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// Alternative NetworkManager using WebSockets directly instead of SignalR
/// This avoids the DLL dependency issues we're experiencing
/// </summary>
public class WebSocketNetworkManager : MonoBehaviour
{
    [Header("Connection Settings")]
    public string ServerUrl = "ws://localhost:5000/ws";
    public float ReconnectDelay = 5f;
    public float HeartbeatInterval = 30f;

    // Events - same as SignalR version
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
        Debug.Log("WebSocketNetworkManager started - alternative to SignalR");
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
                Debug.Log("Connected to WebSocket server successfully!");
                OnConnected?.Invoke();
            });

            // Start listening for messages
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to connect to WebSocket server: {ex.Message}");
            QueueMainThreadAction(() => OnDisconnected?.Invoke());
        }
        finally
        {
            _isConnecting = false;
        }
    }

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
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket receive error: {ex.Message}");
            QueueMainThreadAction(() => OnDisconnected?.Invoke());
        }
    }

    private void ProcessMessage(string jsonMessage)
    {
        try
        {
            // Simple message routing based on message type
            var baseMessage = JsonConvert.DeserializeObject<BaseMessage>(jsonMessage);
            
            switch (baseMessage.Type)
            {
                case "PlayerMovement":
                    var movementMsg = JsonConvert.DeserializeObject<NetworkMessages.PlayerMovementMessage>(jsonMessage);
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
                    var combatMsg = JsonConvert.DeserializeObject<NetworkMessages.CombatActionMessage>(jsonMessage);
                    QueueMainThreadAction(() => OnCombatAction?.Invoke(combatMsg));
                    break;
                    
                case "ChatMessage":
                    var chatMsg = JsonConvert.DeserializeObject<NetworkMessages.ChatMessage>(jsonMessage);
                    QueueMainThreadAction(() => OnChatMessage?.Invoke(chatMsg));
                    break;
                    
                case "WorldUpdate":
                    var worldMsg = JsonConvert.DeserializeObject<NetworkMessages.WorldUpdateMessage>(jsonMessage);
                    QueueMainThreadAction(() => OnWorldUpdate?.Invoke(worldMsg));
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to process message: {ex.Message}");
        }
    }

    // WebSocket Hub methods - same interface as SignalR version
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

    public async Task AuthenticatePlayer(string playerId, string playerName)
    {
        if (!IsConnected) return;
        
        try
        {
            var authMessage = new { Type = "Authentication", PlayerId = playerId, PlayerName = playerName };
            await SendMessage("Authentication", authMessage);
            Debug.Log($"Player authenticated: {playerName}");
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
            var heartbeat = new { Type = "Heartbeat", Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
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
            var wrapper = new { Type = messageType, Data = messageData };
            var json = JsonConvert.SerializeObject(wrapper);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send message: {ex.Message}");
        }
    }

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

    public async void DisconnectFromServer()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            _cancellationTokenSource?.Cancel();
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
        }
    }

    private void OnDestroy()
    {
        DisconnectFromServer();
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
    }

    // Helper class for message routing
    [Serializable]
    private class BaseMessage
    {
        public string Type;
    }
}