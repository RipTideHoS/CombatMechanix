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
    public static event Action<NetworkMessages.HealthChangeMessage> OnHealthChange;
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
        Debug.Log($"NetworkManager started with WebSockets - Server URL: {ServerUrl}");
        Debug.Log("Connection will be initiated by the login system, not automatically");
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
            
            // Auto-reconnect disabled - login system will handle reconnection
            Debug.Log("Connection failed, auto-reconnect disabled. Use login system to reconnect.");
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
            
            // Auto-reconnect disabled - login system will handle reconnection
            Debug.Log("Connection failed, auto-reconnect disabled. Use login system to reconnect.");
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
            
            // LOG ALL INCOMING MESSAGES FOR DEBUGGING
            Debug.Log($"[NetworkManager] Received message type: {wrapper.Type}");
            if (wrapper.Type == "LevelUp" || wrapper.Type == "ExperienceGain" || wrapper.Type == "PlayerStatsUpdate" || wrapper.Type == "InventoryResponse" || wrapper.Type == "InventoryUpdate")
            {
                Debug.Log($"[NetworkManager] IMPORTANT MESSAGE: {wrapper.Type} - Data: {wrapper.Data}");
            }
            
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
                            Debug.Log($"Authentication successful for player: {authResponse.PlayerName}");
                            var gameManager = GameManager.Instance;
                            if (gameManager != null)
                            {
                                gameManager.OnPlayerAuthenticated();
                            }
                        }
                        else
                        {
                            Debug.LogError($"Authentication failed: {authResponse.Message}");
                        }
                    });
                    break;
                    
                case "LoginResponse":
                    var loginResponse = JsonConvert.DeserializeObject<NetworkMessages.LoginResponseMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => {
                        var loginUI = FindObjectOfType<LoginUI>();
                        if (loginResponse.Success)
                        {
                            Debug.Log($"Login successful for player: {loginResponse.PlayerName}");
                            
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
                            Debug.LogError($"Login failed: {loginResponse.ErrorMessage}");
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
                    Debug.Log($"[NetworkManager] LEVEL UP MESSAGE RECEIVED! Player: {levelUpMsg.PlayerId}, New Level: {levelUpMsg.NewLevel}");
                    QueueMainThreadAction(() => {
                        Debug.Log($"[NetworkManager] Invoking OnLevelUp event for level {levelUpMsg.NewLevel}");
                        Debug.Log($"[NetworkManager] OnLevelUp has {(OnLevelUp?.GetInvocationList()?.Length ?? 0)} subscribers");
                        OnLevelUp?.Invoke(levelUpMsg);
                        Debug.Log($"[NetworkManager] OnLevelUp event invoked");
                    });
                    break;
                    
                case "HealthChange":
                    var healthMsg = JsonConvert.DeserializeObject<NetworkMessages.HealthChangeMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnHealthChange?.Invoke(healthMsg));
                    break;
                    
                case "ExperienceGain":
                    var expMsg = JsonConvert.DeserializeObject<NetworkMessages.ExperienceGainMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnExperienceGain?.Invoke(expMsg));
                    break;
                    
                case "ConnectionConfirmed":
                    // Server confirmed our connection and assigned us an ID
                    var connData = JsonConvert.DeserializeObject<ConnectionData>(wrapper.Data.ToString());
                    ConnectionId = connData.ConnectionId;
                    Debug.Log($"Server assigned connection ID: {ConnectionId}");
                    
                    // Check authentication method based on available data
                    QueueMainThreadAction(async () => {
                        // Check if we have pending login credentials
                        string pendingUsername = PlayerPrefs.GetString("PendingUsername", "");
                        string pendingPasswordHash = PlayerPrefs.GetString("PendingPasswordHash", "");
                        
                        if (!string.IsNullOrEmpty(pendingUsername) && !string.IsNullOrEmpty(pendingPasswordHash))
                        {
                            // Handle login authentication
                            Debug.Log($"Sending login credentials for user: {pendingUsername}");
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
                                Debug.Log("Attempting session token validation for reconnection");
                                var sessionMessage = new
                                {
                                    SessionToken = sessionToken
                                };
                                await SendMessage("SessionValidation", sessionMessage);
                            }
                            else
                            {
                                // No valid session token - require login
                                Debug.Log("No valid session token found, showing login UI");
                                
                                // Don't disconnect immediately - just show login UI
                                // The connection will be closed naturally when no authentication happens
                                
                                // Show login UI using multiple approaches
                                var uiManager = FindObjectOfType<UIManager>();
                                var loginUI = FindObjectOfType<LoginUI>();
                                
                                if (uiManager != null)
                                {
                                    Debug.Log("Found UIManager, showing login panel");
                                    uiManager.ShowLoginPanel();
                                }
                                
                                if (loginUI != null)
                                {
                                    Debug.Log("Found LoginUI, showing login panel");
                                    loginUI.ShowLoginPanel();
                                }
                                
                                if (uiManager == null && loginUI == null)
                                {
                                    Debug.LogError("Neither UIManager nor LoginUI found!");
                                }
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
                    Debug.Log($"[NetworkManager] Inventory response received for player {inventoryResponseMsg.PlayerId} with {inventoryResponseMsg.Items.Count} items");
                    QueueMainThreadAction(() => OnInventoryResponse?.Invoke(inventoryResponseMsg));
                    break;
                    
                case "InventoryUpdate":
                    var inventoryUpdateMsg = JsonConvert.DeserializeObject<NetworkMessages.InventoryUpdateMessage>(wrapper.Data.ToString());
                    Debug.Log($"[NetworkManager] Inventory update received: {inventoryUpdateMsg.UpdateType} - {inventoryUpdateMsg.UpdatedItems.Count} items");
                    QueueMainThreadAction(() => OnInventoryUpdate?.Invoke(inventoryUpdateMsg));
                    break;
                    
                case "LootDrop":
                    var lootDropMsg = JsonConvert.DeserializeObject<NetworkMessages.LootDropMessage>(wrapper.Data.ToString());
                    Debug.Log($"[NetworkManager] Loot drop received: {lootDropMsg.Item.ItemName} at position ({lootDropMsg.Position.X}, {lootDropMsg.Position.Y}, {lootDropMsg.Position.Z})");
                    QueueMainThreadAction(() => OnLootDrop?.Invoke(lootDropMsg));
                    break;
                    
                case "LootPickupResponse":
                    var lootPickupMsg = JsonConvert.DeserializeObject<NetworkMessages.LootPickupResponseMessage>(wrapper.Data.ToString());
                    Debug.Log($"[NetworkManager] Loot pickup response: Success={lootPickupMsg.Success}, Message={lootPickupMsg.Message}");
                    QueueMainThreadAction(() => OnLootPickupResponse?.Invoke(lootPickupMsg));
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
            Debug.Log($"Movement sent: Pos={position}, Vel={velocity}, Rot={rotation}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send movement: {ex.Message}");
        }
    }

    public async Task SendAttack(string targetId, string attackType, Vector3 position)
    {
        Debug.Log($"[NetworkManager] SendAttack called - Connected: {IsConnected}, Target: {targetId}, AttackType: {attackType}");
        
        if (!IsConnected) 
        {
            Debug.LogError("[NetworkManager] Cannot send attack - not connected to server!");
            return;
        }
        
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
            
            Debug.Log($"[NetworkManager] Sending CombatAction message: Attacker={message.AttackerId}, Target={message.TargetId}");
            await SendMessage("CombatAction", message);
            Debug.Log($"[NetworkManager] CombatAction message sent successfully");
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

    public async Task RequestInventory()
    {
        if (!IsConnected)
        {
            Debug.LogWarning("Cannot request inventory - not connected to server");
            return;
        }

        try
        {
            var inventoryRequest = new NetworkMessages.InventoryRequestMessage
            {
                PlayerId = ConnectionId
            };
            
            Debug.Log($"Requesting inventory for player: {ConnectionId}");
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
            
            Debug.Log($"Sending loot pickup request for loot: {lootId}");
            await SendMessage("LootPickupRequest", pickupRequest);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send loot pickup request: {ex.Message}");
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

    public async Task SendMessage(string messageType, object messageData)
    {
        if (!IsConnected) return;

        try
        {
            var wrapper = new MessageWrapper { Type = messageType, Data = messageData };
            var json = JsonConvert.SerializeObject(wrapper);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Enhanced logging for inventory messages
            if (messageType == "InventoryRequest")
            {
                Debug.Log($"[NetworkManager] SENDING INVENTORY REQUEST: {json}");
            }
            
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
            Debug.Log($"Message sent: {messageType}");
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
            // Auto-reconnect on unpause disabled - login system will handle reconnection
            Debug.Log("Application unpaused - use login system to reconnect if needed");
            // ConnectToServer();
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