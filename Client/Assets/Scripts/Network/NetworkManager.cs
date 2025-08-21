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
    
    // New stat-related events
    public static event Action<NetworkMessages.PlayerStatsUpdateMessage> OnPlayerStatsUpdate;
    public static event Action<NetworkMessages.LevelUpMessage> OnLevelUp;
    
    private static event Action<NetworkMessages.HealthChangeMessage> _onHealthChange;
    public static event Action<NetworkMessages.HealthChangeMessage> OnHealthChange
    {
        add
        {
            Debug.Log($"[CLIENT] Adding subscriber to OnHealthChange. Current count: {_onHealthChange?.GetInvocationList()?.Length ?? 0}");
            _onHealthChange += value;
            Debug.Log($"[CLIENT] After adding subscriber. New count: {_onHealthChange?.GetInvocationList()?.Length ?? 0}");
        }
        remove
        {
            Debug.Log($"[CLIENT] Removing subscriber from OnHealthChange. Current count: {_onHealthChange?.GetInvocationList()?.Length ?? 0}");
            _onHealthChange -= value;
            Debug.Log($"[CLIENT] After removing subscriber. New count: {_onHealthChange?.GetInvocationList()?.Length ?? 0}");
        }
    }
    
    public static event Action<NetworkMessages.ExperienceGainMessage> OnExperienceGain;
    
    // Enemy-related events
    public static event Action<NetworkMessages.EnemySpawnMessage> OnEnemySpawn;
    public static event Action<NetworkMessages.EnemyUpdateMessage> OnEnemyUpdate;
    public static event Action<NetworkMessages.EnemyDamageMessage> OnEnemyDamage;
    public static event Action<NetworkMessages.EnemyDeathMessage> OnEnemyDeath;
    
    // Inventory-related events
    public static event Action<NetworkMessages.InventoryResponseMessage> OnInventoryResponse;
    public static event Action<NetworkMessages.InventoryUpdateMessage> OnInventoryUpdate;
    
    // Loot-related events
    public static event Action<NetworkMessages.LootDropMessage> OnLootDrop;
    public static event Action<NetworkMessages.LootPickupResponseMessage> OnLootPickupResponse;

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
        // Note: ConnectToServer() is no longer called automatically to enforce proper authentication
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

        try
        {
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            await _webSocket.ConnectAsync(new Uri(ServerUrl), _cancellationTokenSource.Token);
            
            QueueMainThreadAction(() => {
                OnConnected?.Invoke();
            });

            // Start listening for messages
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to connect to WebSocket server: {ex.Message}");
            QueueMainThreadAction(() => OnDisconnected?.Invoke());
            // await Task.Delay(TimeSpan.FromSeconds(ReconnectDelay));
            // if (!IsConnected)
            // {
            //     ConnectToServer();
            // }
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
        var messageBuffer = new List<byte>();
        
        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Add received bytes to message buffer
                    for (int i = 0; i < result.Count; i++)
                    {
                        messageBuffer.Add(buffer[i]);
                    }
                    
                    // If this is the end of the message, process the complete message
                    if (result.EndOfMessage)
                    {
                        var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                        ProcessMessage(message);
                        messageBuffer.Clear(); // Clear for next message
                    }
                    // If not end of message, continue accumulating bytes
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    QueueMainThreadAction(() => OnDisconnected?.Invoke());
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket receive error: {ex.Message}");
            QueueMainThreadAction(() => OnDisconnected?.Invoke());
            // await Task.Delay(TimeSpan.FromSeconds(ReconnectDelay));
            // if (!IsConnected)
            // {
            //     ConnectToServer();
            // }
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
                    
                case "PlayerStatsUpdate":
                    var statsMsg = JsonConvert.DeserializeObject<NetworkMessages.PlayerStatsUpdateMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnPlayerStatsUpdate?.Invoke(statsMsg));
                    break;
                    
                case "AuthenticationResponse":
                    var authResponse = JsonConvert.DeserializeObject<NetworkMessages.AuthenticationResponseMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => {
                        if (authResponse.Success)
                        {
                            var gameManager = GameManager.Instance;
                            if (gameManager != null)
                            {
                                gameManager.OnPlayerAuthenticated();
                            }
                        }
                    });
                    break;
                    
                case "LoginResponse":
                    var loginResponse = JsonConvert.DeserializeObject<NetworkMessages.LoginResponseMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => {
                        var loginUI = FindObjectOfType<LoginUI>();
                        if (loginResponse.Success)
                        {
                            // Set player information in GameManager
                            var gameManager = GameManager.Instance;
                            if (gameManager != null)
                            {
                                gameManager.SetLocalPlayerName(loginResponse.PlayerName);
                                gameManager.LocalPlayerId = loginResponse.PlayerId;
                            }
                            
                            // Notify LoginUI of success
                            if (loginUI != null)
                            {
                                loginUI.OnLoginSuccess(loginResponse.SessionToken, 
                                    PlayerPrefs.GetString("PendingUsername", ""),
                                    loginResponse.PlayerName, 
                                    loginResponse.PlayerId);
                            }
                            
                            // Trigger authenticated event for compatibility
                            if (gameManager != null)
                            {
                                gameManager.OnPlayerAuthenticated();
                            }
                        }
                        else
                        {
                            if (loginUI != null)
                            {
                                loginUI.OnLoginFailed(loginResponse.ErrorMessage);
                            }
                        }
                        
                        // Clean up pending credentials
                        PlayerPrefs.DeleteKey("PendingUsername");
                        PlayerPrefs.DeleteKey("PendingPasswordHash");
                    });
                    break;
                    
                case "LevelUp":
                    var levelUpMsg = JsonConvert.DeserializeObject<NetworkMessages.LevelUpMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnLevelUp?.Invoke(levelUpMsg));
                    break;
                    
                case "HealthChange":
                    var healthMsg = JsonConvert.DeserializeObject<NetworkMessages.HealthChangeMessage>(wrapper.Data.ToString());
                    Debug.Log($"[CLIENT] Received HealthChange message: PlayerId={healthMsg.PlayerId}, NewHealth={healthMsg.NewHealth}, Change={healthMsg.HealthChange}, Source={healthMsg.Source}");
                    Debug.Log($"[CLIENT] OnHealthChange has {GetHealthChangeSubscriberCount()} subscribers");
                    QueueMainThreadAction(() => {
                        Debug.Log($"[CLIENT] Invoking OnHealthChange event for {healthMsg.PlayerId}");
                        _onHealthChange?.Invoke(healthMsg);
                        Debug.Log($"[CLIENT] OnHealthChange event invoked");
                    });
                    break;
                    
                case "ExperienceGain":
                    var expMsg = JsonConvert.DeserializeObject<NetworkMessages.ExperienceGainMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnExperienceGain?.Invoke(expMsg));
                    break;
                    
                case "ConnectionConfirmed":
                    // Server confirmed our connection and assigned us an ID
                    var connData = JsonConvert.DeserializeObject<ConnectionData>(wrapper.Data.ToString());
                    ConnectionId = connData.ConnectionId;
                    
                    // Check authentication method based on available data
                    QueueMainThreadAction(async () => {
                        // Check if we have pending login credentials
                        string pendingUsername = PlayerPrefs.GetString("PendingUsername", "");
                        string pendingPasswordHash = PlayerPrefs.GetString("PendingPasswordHash", "");
                        
                        if (!string.IsNullOrEmpty(pendingUsername) && !string.IsNullOrEmpty(pendingPasswordHash))
                        {
                            // Handle login authentication
                            var loginMessage = new
                            {
                                Username = pendingUsername,
                                ClientHashedPassword = pendingPasswordHash
                            };
                            await SendMessage("Login", loginMessage);
                        }
                        else
                        {
                            // Check for session token (automatic reconnection)
                            string sessionToken = ClientUtilities.SessionManager.GetValidSessionToken();
                            if (!string.IsNullOrEmpty(sessionToken))
                            {
                                var sessionMessage = new
                                {
                                    SessionToken = sessionToken
                                };
                                await SendMessage("SessionValidation", sessionMessage);
                            }
                            else
                            {
                                // No valid session token - require login
                                var uiManager = FindObjectOfType<UIManager>();
                                var loginUI = FindObjectOfType<LoginUI>();
                                
                                uiManager?.ShowLoginPanel();
                                loginUI?.ShowLoginPanel();
                            }
                        }
                    });
                    break;
                    
                case "EnemySpawn":
                    var enemySpawnMsg = JsonConvert.DeserializeObject<NetworkMessages.EnemySpawnMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnEnemySpawn?.Invoke(enemySpawnMsg));
                    break;
                    
                case "EnemyUpdate":
                    var enemyUpdateMsg = JsonConvert.DeserializeObject<NetworkMessages.EnemyUpdateMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnEnemyUpdate?.Invoke(enemyUpdateMsg));
                    break;
                    
                case "EnemyDamage":
                    var enemyDamageMsg = JsonConvert.DeserializeObject<NetworkMessages.EnemyDamageMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnEnemyDamage?.Invoke(enemyDamageMsg));
                    break;
                    
                case "EnemyDeath":
                    var enemyDeathMsg = JsonConvert.DeserializeObject<NetworkMessages.EnemyDeathMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnEnemyDeath?.Invoke(enemyDeathMsg));
                    break;
                    
                case "InventoryResponse":
                    var inventoryResponseMsg = JsonConvert.DeserializeObject<NetworkMessages.InventoryResponseMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnInventoryResponse?.Invoke(inventoryResponseMsg));
                    break;
                    
                case "InventoryUpdate":
                    var inventoryUpdateMsg = JsonConvert.DeserializeObject<NetworkMessages.InventoryUpdateMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnInventoryUpdate?.Invoke(inventoryUpdateMsg));
                    break;
                    
                case "LootDrop":
                    var lootDropMsg = JsonConvert.DeserializeObject<NetworkMessages.LootDropMessage>(wrapper.Data.ToString());
                    if (lootDropMsg != null)
                    {
                        QueueMainThreadAction(() => OnLootDrop?.Invoke(lootDropMsg));
                    }
                    break;
                    
                case "LootPickupResponse":
                    var lootPickupMsg = JsonConvert.DeserializeObject<NetworkMessages.LootPickupResponseMessage>(wrapper.Data.ToString());
                    if (lootPickupMsg != null)
                    {
                        QueueMainThreadAction(() => OnLootPickupResponse?.Invoke(lootPickupMsg));
                    }
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
                Position = new Vector3Data(position),
                Velocity = new Vector3Data(velocity),
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
                Position = new Vector3Data(position),
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
                Position = new Vector3Data(position)
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
        if (!IsConnected) return;
        
        try
        {
            var authMessage = new { PlayerId = playerId, PlayerName = playerName };
            await SendMessage("Authentication", authMessage);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to authenticate player: {ex.Message}");
        }
    }

    public async Task RequestInventory()
    {
        if (!IsConnected) return;

        try
        {
            var inventoryRequest = new NetworkMessages.InventoryRequestMessage
            {
                PlayerId = ConnectionId
            };
            
            await SendMessage("InventoryRequest", inventoryRequest);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to request inventory: {ex.Message}");
        }
    }

    public async Task SendLootPickupRequest(string lootId, Vector3 playerPosition)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;
        
        try
        {
            var pickupRequest = new NetworkMessages.LootPickupRequestMessage
            {
                PlayerId = ConnectionId,
                LootId = lootId,
                PlayerPosition = new Vector3Data(playerPosition.x, playerPosition.y, playerPosition.z)
            };
            
            await SendMessage("LootPickupRequest", pickupRequest);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send loot pickup request: {ex.Message}");
        }
    }

    public async Task SendItemUseRequest(NetworkMessages.ItemUseRequestMessage useRequest)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;
        
        try
        {
            await SendMessage("ItemUseRequest", useRequest);
            Debug.Log($"[NetworkManager] Sent item use request for {useRequest.ItemType} in slot {useRequest.SlotIndex}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send item use request: {ex.Message}");
        }
    }

    public async Task SendItemSellRequest(NetworkMessages.ItemSellRequestMessage sellRequest)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;
        
        try
        {
            await SendMessage("ItemSellRequest", sellRequest);
            Debug.Log($"[NetworkManager] Sent item sell request for {sellRequest.ItemType} in slot {sellRequest.SlotIndex}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send item sell request: {ex.Message}");
        }
    }

    public string GetPlayerId()
    {
        return ConnectionId;
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

    public async Task SendMessage(string messageType, object messageData)
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
            // ConnectToServer();
        }
    }

    #endregion

    #region Debug Helpers

    private int GetHealthChangeSubscriberCount()
    {
        return _onHealthChange?.GetInvocationList()?.Length ?? 0;
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