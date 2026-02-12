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
    
    public static event Action<NetworkMessages.RespawnResponseMessage> OnRespawnResponse;
    
    public static event Action<NetworkMessages.ExperienceGainMessage> OnExperienceGain;
    
    // Enemy-related events
    public static event Action<NetworkMessages.EnemySpawnMessage> OnEnemySpawn;
    public static event Action<NetworkMessages.EnemyUpdateMessage> OnEnemyUpdate;
    public static event Action<NetworkMessages.EnemyDamageMessage> OnEnemyDamage;
    public static event Action<NetworkMessages.EnemyDeathMessage> OnEnemyDeath;
    
    // Inventory-related events
    public static event Action<NetworkMessages.InventoryResponseMessage> OnInventoryResponse;
    public static event Action<NetworkMessages.InventoryUpdateMessage> OnInventoryUpdate;
    public static event Action<NetworkMessages.ItemSellResponseMessage> OnItemSellResponse;
    
    // Equipment-related events
    public static event Action<NetworkMessages.EquipmentResponseMessage> OnEquipmentResponse;
    public static event Action<NetworkMessages.EquipmentUpdateMessage> OnEquipmentUpdate;
    
    // Weapon timing event for client-side cooldown validation
    public static event Action<WeaponTimingMessage> OnWeaponTiming;
    
    // Loot-related events
    public static event Action<NetworkMessages.LootDropMessage> OnLootDrop;
    public static event Action<NetworkMessages.LootPickupResponseMessage> OnLootPickupResponse;
    
    // Phase 1: New projectile collision system events
    public static event Action<NetworkMessages.ProjectileLaunchMessage> OnProjectileLaunch;
    public static event Action<NetworkMessages.DamageConfirmationMessage> OnDamageConfirmation;

    // Grenade system events
    public static event Action<NetworkMessages.GrenadeSpawnMessage> OnGrenadeSpawn;
    public static event Action<NetworkMessages.GrenadeWarningMessage> OnGrenadeWarning;
    public static event Action<NetworkMessages.GrenadeExplosionMessage> OnGrenadeExplosion;
    public static event Action<NetworkMessages.GrenadeErrorMessage> OnGrenadeError;
    public static event Action<NetworkMessages.GrenadeCountUpdateMessage> OnGrenadeCountUpdate;

    // Terrain system events
    public static event Action<TerrainChangeMessage> OnTerrainChange;

    // Level system events
    public static event Action<LevelCompleteMessage> OnLevelComplete;
    public static event Action<PlayerRepositionMessage> OnPlayerReposition;

    // Skill tree events
    public static event Action<NetworkMessages.SkillAllocationResponseMessage> OnSkillAllocationResponse;

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
                    Debug.Log($"[NetworkManager] CombatAction message received - AttackerId: {combatMsg?.AttackerId}, TargetId: {combatMsg?.TargetId}");
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
                            
                            // Process PlayerStats from LoginResponse if present
                            if (loginResponse.PlayerStats != null)
                            {
                                Debug.Log($"[CLIENT] Processing PlayerStats from LoginResponse - Health: {loginResponse.PlayerStats.Health}, Gold: {loginResponse.PlayerStats.Gold}, PlayerId: {loginResponse.PlayerStats.PlayerId}");
                                OnPlayerStatsUpdate?.Invoke(loginResponse.PlayerStats);
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
                    
                case "RespawnResponse":
                    var respawnMsg = JsonConvert.DeserializeObject<NetworkMessages.RespawnResponseMessage>(wrapper.Data.ToString());
                    Debug.Log($"[CLIENT] Received RespawnResponse: Success={respawnMsg.Success}, PlayerId={respawnMsg.PlayerId}, NewHealth={respawnMsg.NewHealth}");
                    QueueMainThreadAction(() => OnRespawnResponse?.Invoke(respawnMsg));
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
                    
                case "ItemSellResponse":
                    var itemSellResponseMsg = JsonConvert.DeserializeObject<NetworkMessages.ItemSellResponseMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnItemSellResponse?.Invoke(itemSellResponseMsg));
                    break;
                    
                case "EquipmentResponse":
                    var equipmentResponseMsg = JsonConvert.DeserializeObject<NetworkMessages.EquipmentResponseMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnEquipmentResponse?.Invoke(equipmentResponseMsg));
                    break;
                    
                case "EquipmentUpdate":
                    var equipmentUpdateMsg = JsonConvert.DeserializeObject<NetworkMessages.EquipmentUpdateMessage>(wrapper.Data.ToString());
                    QueueMainThreadAction(() => OnEquipmentUpdate?.Invoke(equipmentUpdateMsg));
                    break;
                    
                case "WeaponTiming":
                    var weaponTimingMsg = JsonConvert.DeserializeObject<WeaponTimingMessage>(wrapper.Data.ToString());
                    Debug.Log($"[NetworkManager] WeaponTiming message received: {weaponTimingMsg?.WeaponName} ({weaponTimingMsg?.CooldownMs}ms cooldown)");
                    QueueMainThreadAction(() => OnWeaponTiming?.Invoke(weaponTimingMsg));
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
                    
                // Phase 1: New projectile collision system message handlers
                case "ProjectileLaunch":
                    var projectileLaunchMsg = JsonConvert.DeserializeObject<NetworkMessages.ProjectileLaunchMessage>(wrapper.Data.ToString());
                    if (projectileLaunchMsg != null)
                    {
                        Debug.Log($"[NetworkManager] ProjectileLaunch received: {projectileLaunchMsg.ProjectileId} by {projectileLaunchMsg.ShooterId}");
                        QueueMainThreadAction(() => OnProjectileLaunch?.Invoke(projectileLaunchMsg));
                    }
                    break;
                    
                case "DamageConfirmation":
                    var damageConfirmationMsg = JsonConvert.DeserializeObject<NetworkMessages.DamageConfirmationMessage>(wrapper.Data.ToString());
                    if (damageConfirmationMsg != null)
                    {
                        Debug.Log($"[NetworkManager] DamageConfirmation received: {damageConfirmationMsg.ProjectileId} dealt {damageConfirmationMsg.ActualDamage} damage to {damageConfirmationMsg.TargetId}");
                        QueueMainThreadAction(() => OnDamageConfirmation?.Invoke(damageConfirmationMsg));
                    }
                    break;

                // Grenade system message handlers
                case "GrenadeSpawn":
                    Debug.Log($"[NetworkManager] Raw GrenadeSpawn message received: {wrapper.Data}");
                    var grenadeSpawnMsg = JsonConvert.DeserializeObject<NetworkMessages.GrenadeSpawnMessage>(wrapper.Data.ToString());
                    if (grenadeSpawnMsg != null)
                    {
                        Debug.Log($"[NetworkManager] GrenadeSpawn received: {grenadeSpawnMsg.GrenadeId} by {grenadeSpawnMsg.PlayerId}");
                        QueueMainThreadAction(() => OnGrenadeSpawn?.Invoke(grenadeSpawnMsg));
                    }
                    else
                    {
                        Debug.LogError("[NetworkManager] Failed to deserialize GrenadeSpawn message");
                    }
                    break;

                case "GrenadeWarning":
                    var grenadeWarningMsg = JsonConvert.DeserializeObject<NetworkMessages.GrenadeWarningMessage>(wrapper.Data.ToString());
                    if (grenadeWarningMsg != null)
                    {
                        Debug.Log($"[NetworkManager] GrenadeWarning received: {grenadeWarningMsg.GrenadeId} exploding in {grenadeWarningMsg.TimeToExplosion}s");
                        QueueMainThreadAction(() => OnGrenadeWarning?.Invoke(grenadeWarningMsg));
                    }
                    break;

                case "GrenadeExplosion":
                    var grenadeExplosionMsg = JsonConvert.DeserializeObject<NetworkMessages.GrenadeExplosionMessage>(wrapper.Data.ToString());
                    if (grenadeExplosionMsg != null)
                    {
                        Debug.Log($"[NetworkManager] GrenadeExplosion received: {grenadeExplosionMsg.GrenadeId} exploded, {grenadeExplosionMsg.DamagedTargets.Count} targets affected");
                        QueueMainThreadAction(() => OnGrenadeExplosion?.Invoke(grenadeExplosionMsg));
                    }
                    break;

                case "GrenadeError":
                    var grenadeErrorMsg = JsonConvert.DeserializeObject<NetworkMessages.GrenadeErrorMessage>(wrapper.Data.ToString());
                    if (grenadeErrorMsg != null)
                    {
                        Debug.Log($"[NetworkManager] GrenadeError received: {grenadeErrorMsg.ErrorMessage}");
                        QueueMainThreadAction(() => OnGrenadeError?.Invoke(grenadeErrorMsg));
                    }
                    break;

                case "GrenadeCountUpdate":
                    var grenadeCountMsg = JsonConvert.DeserializeObject<NetworkMessages.GrenadeCountUpdateMessage>(wrapper.Data.ToString());
                    if (grenadeCountMsg != null)
                    {
                        Debug.Log($"[NetworkManager] GrenadeCountUpdate received: Frag={grenadeCountMsg.FragGrenades}, Smoke={grenadeCountMsg.SmokeGrenades}, Flash={grenadeCountMsg.FlashGrenades}");
                        QueueMainThreadAction(() => OnGrenadeCountUpdate?.Invoke(grenadeCountMsg));
                    }
                    break;

                // Terrain system message handler
                case "TerrainChange":
                    var terrainChangeMsg = JsonConvert.DeserializeObject<TerrainChangeMessage>(wrapper.Data.ToString());
                    if (terrainChangeMsg != null)
                    {
                        Debug.Log($"[NetworkManager] TerrainChange received: Reason={terrainChangeMsg.reason}, Level={terrainChangeMsg.currentLevel}, Hills={terrainChangeMsg.terrainData?.hills?.Count ?? 0}");
                        QueueMainThreadAction(() => OnTerrainChange?.Invoke(terrainChangeMsg));
                    }
                    break;

                // Level system message handler
                case "LevelComplete":
                    var levelCompleteMsg = JsonConvert.DeserializeObject<LevelCompleteMessage>(wrapper.Data.ToString());
                    if (levelCompleteMsg != null)
                    {
                        Debug.Log($"[NetworkManager] LevelComplete received: Level {levelCompleteMsg.completedLevel}, Kills={levelCompleteMsg.enemiesKilled}, XP={levelCompleteMsg.experienceEarned}");
                        QueueMainThreadAction(() => OnLevelComplete?.Invoke(levelCompleteMsg));
                    }
                    break;

                case "PlayerReposition":
                    var repositionMsg = JsonConvert.DeserializeObject<PlayerRepositionMessage>(wrapper.Data.ToString());
                    if (repositionMsg != null)
                    {
                        Debug.Log($"[NetworkManager] PlayerReposition received: ({repositionMsg.position.X:F1}, {repositionMsg.position.Y:F1}, {repositionMsg.position.Z:F1}) Reason={repositionMsg.reason}");
                        QueueMainThreadAction(() =>
                        {
                            OnPlayerReposition?.Invoke(repositionMsg);
                            // Directly reposition the local player
                            var player = GameManager.Instance?.LocalPlayer;
                            if (player != null)
                            {
                                player.SetPosition(new Vector3(repositionMsg.position.X, repositionMsg.position.Y, repositionMsg.position.Z));
                                Debug.Log($"[NetworkManager] Player repositioned to ({repositionMsg.position.X:F1}, {repositionMsg.position.Y:F1}, {repositionMsg.position.Z:F1})");
                            }
                        });
                    }
                    break;

                case "SkillAllocationResponse":
                    var skillAllocMsg = JsonConvert.DeserializeObject<NetworkMessages.SkillAllocationResponseMessage>(wrapper.Data.ToString());
                    if (skillAllocMsg != null)
                    {
                        Debug.Log($"[NetworkManager] SkillAllocationResponse: Success={skillAllocMsg.Success}, Message={skillAllocMsg.Message}");
                        QueueMainThreadAction(() => OnSkillAllocationResponse?.Invoke(skillAllocMsg));
                    }
                    break;

                default:
                    Debug.LogWarning($"Unknown message type: {wrapper.Type}");
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

    /// <summary>
    /// Send projectile hit report to server for collision-based damage validation
    /// </summary>
    public async Task SendProjectileHit(string projectileId, string targetId, string targetType, Vector3 hitPosition, string collisionContext = "")
    {
        if (!IsConnected) return;
        
        try
        {
            var hitMessage = new NetworkMessages.ProjectileHitMessage
            {
                ProjectileId = projectileId,
                TargetId = targetId,
                TargetType = targetType,
                HitPosition = new Vector3Data(hitPosition),
                ClientTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CollisionContext = collisionContext
            };
            
            await SendMessage("ProjectileHit", hitMessage);
            Debug.Log($"[NetworkManager] Sent ProjectileHit: {projectileId} hit {targetType} {targetId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send projectile hit: {ex.Message}");
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

    public async Task SendGrenadeThrow(string grenadeType, Vector3 throwPosition, Vector3 targetPosition)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;

        try
        {
            // Use the actual logged-in player ID, not the connection ID
            string actualPlayerId = GameManager.Instance?.LocalPlayerId ?? ConnectionId;
            Debug.Log($"[NetworkManager] Sending grenade throw with PlayerId: {actualPlayerId} (ConnectionId: {ConnectionId})");

            var grenadeThrowMessage = new NetworkMessages.GrenadeThrowMessage
            {
                PlayerId = actualPlayerId,
                ThrowPosition = new Vector3Data(throwPosition),
                TargetPosition = new Vector3Data(targetPosition),
                GrenadeType = grenadeType,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await SendMessage("GrenadeThrow", grenadeThrowMessage);
            Debug.Log($"[NetworkManager] Sent grenade throw request: {grenadeType} from {throwPosition} to {targetPosition}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send grenade throw: {ex.Message}");
        }
    }

    public async Task SendSkillAllocationRequest(string skillName, int points, bool deallocate)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;

        try
        {
            string actualPlayerId = GameManager.Instance?.LocalPlayerId ?? ConnectionId;

            var request = new NetworkMessages.SkillAllocationRequestMessage
            {
                PlayerId = actualPlayerId,
                SkillName = skillName,
                Points = points,
                Deallocate = deallocate
            };

            await SendMessage("SkillAllocationRequest", request);
            Debug.Log($"[NetworkManager] Sent skill allocation request: {(deallocate ? "Remove" : "Add")} {points} to {skillName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send skill allocation request: {ex.Message}");
        }
    }

    public async Task SendLevelContinue(int nextLevel)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;

        try
        {
            string actualPlayerId = GameManager.Instance?.LocalPlayerId ?? ConnectionId;

            var continueMessage = new LevelContinueMessage
            {
                playerId = actualPlayerId,
                nextLevel = nextLevel,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await SendMessage("LevelContinue", continueMessage);
            Debug.Log($"[NetworkManager] Sent level continue request for level {nextLevel}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send level continue: {ex.Message}");
        }
    }

    #endregion

    #region Equipment Methods

    public async Task RequestEquipment()
    {
        if (!IsConnected) return;

        try
        {
            var equipmentRequest = new NetworkMessages.EquipmentRequestMessage
            {
                PlayerId = ConnectionId
            };
            
            await SendMessage("EquipmentRequest", equipmentRequest);
            Debug.Log("[NetworkManager] Sent equipment request");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to request equipment: {ex.Message}");
        }
    }

    public async Task SendItemEquipRequest(NetworkMessages.ItemEquipRequestMessage equipRequest)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;
        
        try
        {
            await SendMessage("ItemEquipRequest", equipRequest);
            Debug.Log($"[NetworkManager] Sent item equip request for {equipRequest.ItemType} to slot {equipRequest.SlotType}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send item equip request: {ex.Message}");
        }
    }

    public async Task SendItemUnequipRequest(NetworkMessages.ItemUnequipRequestMessage unequipRequest)
    {
        if (!IsConnected || string.IsNullOrEmpty(ConnectionId)) return;
        
        try
        {
            await SendMessage("ItemUnequipRequest", unequipRequest);
            Debug.Log($"[NetworkManager] Sent item unequip request for slot {unequipRequest.SlotType}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send item unequip request: {ex.Message}");
        }
    }

    public async void SendRespawnRequest()
    {
        Debug.Log("[NetworkManager] SendRespawnRequest called");
        
        if (!IsConnected)
        {
            Debug.LogWarning("Cannot send respawn request - not connected to server");
            return;
        }

        try
        {
            // Use the actual PlayerId from GameManager, not ConnectionId
            string playerId = GameManager.Instance?.LocalPlayerId ?? ConnectionId;
            
            var respawnRequest = new NetworkMessages.RespawnRequestMessage
            {
                PlayerId = playerId
            };

            Debug.Log($"[NetworkManager] Sending respawn request for player {playerId} (ConnectionId: {ConnectionId})");
            await SendMessage("RespawnRequest", respawnRequest);
            Debug.Log($"[NetworkManager] Sent respawn request successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send respawn request: {ex.Message}");
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
        // DEBUG: Enhanced logging for troubleshooting (skip movement messages)
        if (messageType != "PlayerMovement")
            Debug.Log($"[NetworkManager] SendMessage called - Type: {messageType}, IsConnected: {IsConnected}, WebSocket State: {_webSocket?.State}");
        
        if (!IsConnected) 
        {
            Debug.LogWarning($"[NetworkManager] Cannot send {messageType} - not connected");
            return;
        }

        try
        {
            var wrapper = new MessageWrapper { Type = messageType, Data = messageData };
            var json = JsonConvert.SerializeObject(wrapper);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            if (messageType != "PlayerMovement")
                Debug.Log($"[NetworkManager] Sending message - Type: {messageType}, Size: {bytes.Length} bytes");
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
            if (messageType != "PlayerMovement")
                Debug.Log($"[NetworkManager] Message sent successfully - Type: {messageType}");
            
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] Failed to send message {messageType}: {ex.Message}");
            Debug.LogError($"[NetworkManager] Exception details: {ex}");
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