using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Linq;

public class NetworkManager : MonoBehaviour
{
    [Header("Connection Settings")]
    public string ServerUrl = "http://localhost:5000/gamehub";
    public float ReconnectDelay = 5f;
    public float HeartbeatInterval = 30f;

    // Events
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<PlayerState> OnPlayerMoved;
    public static event Action<NetworkMessages.CombatActionMessage> OnCombatAction;
    public static event Action<NetworkMessages.ChatMessage> OnChatMessage;
    public static event Action<NetworkMessages.WorldUpdateMessage> OnWorldUpdate;
    public static event Action<NetworkMessages.PlayerJoinNotification> OnPlayerJoined;
    public static event Action<NetworkMessages.SystemNotification> OnSystemNotification;

    private HubConnection _connection;
    private bool _isConnecting = false;
    private float _lastHeartbeat = 0f;
    private Queue<Action> _mainThreadActions = new Queue<Action>();
    private object _queueLock = new object();

    // Connection properties
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
    public string ConnectionId => _connection?.ConnectionId;

    private void Start()
    {
        Debug.Log("NetworkManager started with SignalR");
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

    // SignalR Hub methods
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
            
            await _connection.InvokeAsync("SendMovement", message);
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
            
            await _connection.InvokeAsync("SendCombatAction", message);
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
            
            await _connection.InvokeAsync("SendChatMessage", chatMessage);
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
            
            await _connection.InvokeAsync("SendResourceGather", message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send resource gather: {ex.Message}");
        }
    }

    public async Task AuthenticatePlayer(string playerId, string playerName)
    {
        if (!IsConnected) return;
        
        try
        {
            await _connection.InvokeAsync("AuthenticatePlayer", playerId, playerName);
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
            await _connection.InvokeAsync("Heartbeat");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Heartbeat failed: {ex.Message}");
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

    public async void ConnectToServer()
    {
        if (_isConnecting || IsConnected) return;
        
        _isConnecting = true;
        Debug.Log($"Connecting to server: {ServerUrl}");

        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(ServerUrl)
                .WithAutomaticReconnect()
                .Build();

            SetupEventHandlers();
            await _connection.StartAsync();
            
            QueueMainThreadAction(() => {
                Debug.Log("Connected to server successfully!");
                OnConnected?.Invoke();
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to connect to server: {ex.Message}");
            QueueMainThreadAction(() => OnDisconnected?.Invoke());
        }
        finally
        {
            _isConnecting = false;
        }
    }

    private void SetupEventHandlers()
    {
        // Server -> Client event handlers
        _connection.On<NetworkMessages.PlayerMovementMessage>("ReceivePlayerMovement", (message) =>
        {
            QueueMainThreadAction(() => {
                var playerState = new PlayerState
                {
                    PlayerId = message.PlayerId,
                    Position = message.Position,
                    Velocity = message.Velocity,
                    Rotation = message.Rotation
                };
                OnPlayerMoved?.Invoke(playerState);
            });
        });

        _connection.On<NetworkMessages.CombatActionMessage>("ReceiveCombatAction", (message) =>
        {
            QueueMainThreadAction(() => OnCombatAction?.Invoke(message));
        });

        _connection.On<NetworkMessages.ChatMessage>("ReceiveChatMessage", (message) =>
        {
            QueueMainThreadAction(() => OnChatMessage?.Invoke(message));
        });

        _connection.On<NetworkMessages.WorldUpdateMessage>("ReceiveWorldUpdate", (message) =>
        {
            QueueMainThreadAction(() => OnWorldUpdate?.Invoke(message));
        });

        _connection.On<NetworkMessages.PlayerJoinNotification>("PlayerJoined", (message) =>
        {
            QueueMainThreadAction(() => OnPlayerJoined?.Invoke(message));
        });

        _connection.On<NetworkMessages.SystemNotification>("SystemNotification", (message) =>
        {
            QueueMainThreadAction(() => OnSystemNotification?.Invoke(message));
        });

        // Connection state events
        _connection.Closed += async (error) =>
        {
            QueueMainThreadAction(() => {
                Debug.Log("Connection closed");
                OnDisconnected?.Invoke();
            });
            
            if (error != null)
            {
                Debug.LogError($"Connection closed with error: {error.Message}");
                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelay));
                ConnectToServer();
            }
        };

        _connection.Reconnecting += (error) =>
        {
            QueueMainThreadAction(() => {
                Debug.Log("Attempting to reconnect...");
                OnDisconnected?.Invoke();
            });
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            QueueMainThreadAction(() => {
                Debug.Log($"Reconnected with connection ID: {connectionId}");
                OnConnected?.Invoke();
            });
            return Task.CompletedTask;
        };
    }

    public async void DisconnectFromServer()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private void OnDestroy()
    {
        DisconnectFromServer();
    }
}