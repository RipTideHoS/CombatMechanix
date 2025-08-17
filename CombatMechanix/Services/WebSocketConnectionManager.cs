using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CombatMechanix.Models;
using CombatMechanix.Services;
using CombatMechanix.Data;

namespace CombatMechanix.Services
{
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
        private readonly ConcurrentDictionary<string, PlayerState> _players = new();
        private readonly ILogger<WebSocketConnectionManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private EnemyManager? _enemyManager;
        private LootManager? _lootManager;

        public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Set the enemy manager reference (called after both services are initialized)
        /// </summary>
        public void SetEnemyManager(EnemyManager enemyManager)
        {
            _enemyManager = enemyManager;
        }

        /// <summary>
        /// Set the loot manager reference (called after both services are initialized)
        /// </summary>
        public void SetLootManager(LootManager lootManager)
        {
            _lootManager = lootManager;
        }

        public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket)
        {
            var connectionId = Guid.NewGuid().ToString();
            var connection = new WebSocketConnection(connectionId, webSocket);
            
            _connections.TryAdd(connectionId, connection);
            _logger.LogInformation($"WebSocket connection established: {connectionId}");

            try
            {
                // Give the client a moment to establish the receive loop
                await Task.Delay(100);
                
                // Send connection confirmation
                await SendToConnection(connectionId, "ConnectionConfirmed", new ConnectionData { ConnectionId = connectionId });
                _logger.LogInformation($"Sent ConnectionConfirmed to {connectionId}");

                // Handle incoming messages
                await ReceiveMessages(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling WebSocket connection {connectionId}");
            }
            finally
            {
                // Clean up connection
                _connections.TryRemove(connectionId, out _);
                _players.TryRemove(connectionId, out _);
                _logger.LogInformation($"WebSocket connection closed: {connectionId}");

                // Notify other players that this player left
                if (_players.ContainsKey(connectionId))
                {
                    await BroadcastToAll("SystemNotification", new NetworkMessages.SystemNotification
                    {
                        Message = $"Player {_players[connectionId].PlayerName} has left the game",
                        Type = "PlayerLeft",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }

        private async Task ReceiveMessages(WebSocketConnection connection)
        {
            var buffer = new byte[1024 * 4];
            _logger.LogInformation($"Starting message receive loop for {connection.ConnectionId}");

            while (connection.WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await connection.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    _logger.LogDebug($"Received message from {connection.ConnectionId}: Type={result.MessageType}, Count={result.Count}");

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _logger.LogInformation($"Text message from {connection.ConnectionId}: {message}");
                        await ProcessMessage(connection, message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation($"Close message received from {connection.ConnectionId}");
                        await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                        break;
                    }
                    else
                    {
                        _logger.LogWarning($"Unexpected message type from {connection.ConnectionId}: {result.MessageType}");
                    }
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogWarning($"Client {connection.ConnectionId} disconnected abruptly: {ex.Message}");
                    break;
                }
            }
            
            _logger.LogInformation($"Message receive loop ended for {connection.ConnectionId}, State: {connection.WebSocket.State}");
        }

        private async Task ProcessMessage(WebSocketConnection connection, string jsonMessage)
        {
            try
            {
                var wrapper = JsonSerializer.Deserialize<MessageWrapper>(jsonMessage);
                if (wrapper?.Type == null) return;

                _logger.LogDebug($"Processing message type: {wrapper.Type} from {connection.ConnectionId}");

                switch (wrapper.Type)
                {
                    case "PlayerMovement":
                        await HandlePlayerMovement(connection, wrapper.Data);
                        break;
                    case "CombatAction":
                        await HandleCombatAction(connection, wrapper.Data);
                        break;
                    case "ChatMessage":
                        await HandleChatMessage(connection, wrapper.Data);
                        break;
                    case "ResourceGather":
                        await HandleResourceGather(connection, wrapper.Data);
                        break;
                    case "Authentication":
                        await HandleAuthentication(connection, wrapper.Data);
                        break;
                    case "Login":
                        await HandleLogin(connection, wrapper.Data);
                        break;
                    case "AdminResetStats":
                        await HandleAdminResetStats(connection, wrapper.Data);
                        break;
                    case "InventoryRequest":
                        await HandleInventoryRequest(connection, wrapper.Data);
                        break;
                    case "SessionValidation":
                        await HandleSessionValidation(connection, wrapper.Data);
                        break;
                    case "Logout":
                        await HandleLogout(connection, wrapper.Data);
                        break;
                    case "Heartbeat":
                        await HandleHeartbeat(connection, wrapper.Data);
                        break;
                    case "ExperienceGain":
                        await HandleExperienceGain(connection, wrapper.Data);
                        break;
                    case "HealthChange":
                        await HandleHealthChange(connection, wrapper.Data);
                        break;
                    case "LootPickupRequest":
                        await HandleLootPickupRequest(connection, wrapper.Data);
                        break;
                    default:
                        _logger.LogWarning($"Unknown message type: {wrapper.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message from {connection.ConnectionId}");
            }
        }

        #region Message Handlers

        private async Task HandlePlayerMovement(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var movementData = JsonSerializer.Deserialize<NetworkMessages.PlayerMovementMessage>(data.ToString()!);
            if (movementData == null) return;

            // Update player state
            if (_players.TryGetValue(connection.ConnectionId, out var player))
            {
                player.Position = movementData.Position;
                player.Velocity = movementData.Velocity;
                player.Rotation = movementData.Rotation;
                player.LastUpdate = DateTime.UtcNow;

                // Persist position to database periodically (every 5 seconds)
                var timeSinceLastUpdate = DateTime.UtcNow - player.LastUpdate;
                if (timeSinceLastUpdate.TotalSeconds >= 5)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();
                            await playerStatsService.UpdatePositionAsync(movementData.PlayerId, movementData.Position);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error updating player position for {movementData.PlayerId}");
                        }
                    });
                }
            }

            // Broadcast to all other players
            await BroadcastToOthers(connection.ConnectionId, "PlayerMovement", movementData);
        }

        private async Task HandleCombatAction(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var combatData = JsonSerializer.Deserialize<NetworkMessages.CombatActionMessage>(data.ToString()!);
            if (combatData == null) return;

            _logger.LogInformation($"Combat action: {combatData.AttackType} from {combatData.AttackerId} to {combatData.TargetId}");

            // Validate attack
            if (!ValidateAttack(connection, combatData))
            {
                _logger.LogWarning($"Invalid attack from {connection.PlayerId} to {combatData.TargetId}");
                return;
            }

            // Route to appropriate handler based on target type
            if (!string.IsNullOrEmpty(combatData.TargetId) && combatData.TargetId.StartsWith("enemy_"))
            {
                await HandleEnemyAttack(connection, combatData);
            }
            else if (!string.IsNullOrEmpty(combatData.TargetId))
            {
                await HandlePlayerAttack(connection, combatData);
            }
            else
            {
                // Ground attack or area effect - just broadcast for now
                await BroadcastToAll("CombatAction", combatData);
            }
        }

        private async Task HandleEnemyAttack(WebSocketConnection connection, NetworkMessages.CombatActionMessage combatData)
        {
            if (_enemyManager == null)
            {
                _logger.LogError("EnemyManager not available for enemy attack processing");
                return;
            }

            // Check if player is properly authenticated
            if (string.IsNullOrEmpty(connection.PlayerId))
            {
                _logger.LogWarning($"Combat attempted by unauthenticated connection: {connection.ConnectionId}");
                return;
            }
            
            // Calculate player damage based on stats
            float damage = CalculatePlayerDamage(connection.PlayerId, combatData.AttackType);
            
            _logger.LogInformation($"DEBUG: Combat - ConnectionId: {connection.ConnectionId}, PlayerId: {connection.PlayerId}");
            _logger.LogInformation($"Player {connection.PlayerId} attacking enemy {combatData.TargetId} for {damage} damage");

            // Apply damage to enemy via EnemyManager
            bool success = await _enemyManager.DamageEnemy(combatData.TargetId, damage, connection.PlayerId);
            
            if (success)
            {
                // Update combat data with calculated damage
                combatData.Damage = damage;
                combatData.AttackerId = connection.PlayerId;
                
                // Broadcast attack effects to all clients
                await BroadcastToAll("CombatAction", combatData);
                
                // Handle potential rewards (experience, loot)
                await HandleCombatRewards(connection.PlayerId, combatData.TargetId, damage);
            }
            else
            {
                _logger.LogWarning($"Failed to damage enemy {combatData.TargetId}");
            }
        }

        private async Task HandlePlayerAttack(WebSocketConnection connection, NetworkMessages.CombatActionMessage combatData)
        {
            // For now, just broadcast player-vs-player attacks
            // TODO: Add player-vs-player damage calculation and validation
            combatData.AttackerId = connection.PlayerId;
            await BroadcastToAll("CombatAction", combatData);
        }

        private float CalculatePlayerDamage(string playerId, string attackType)
        {
            // Find player by connection ID (playerId in this context is connection.PlayerId)
            var connection = _connections.Values.FirstOrDefault(c => c.PlayerId == playerId);
            if (connection != null && _players.TryGetValue(connection.ConnectionId, out var player))
            {
                float baseDamage = 10f; // Base attack damage
                float strengthBonus = player.Strength * 0.5f; // Strength scaling
                float levelBonus = player.Level * 2f; // Level scaling
                
                // Attack type modifiers
                float typeMultiplier = attackType switch
                {
                    "BasicAttack" => 1.0f,
                    "PowerAttack" => 1.5f,
                    "CriticalStrike" => 2.0f,
                    _ => 1.0f
                };
                
                float totalDamage = (baseDamage + strengthBonus + levelBonus) * typeMultiplier;
                
                _logger.LogDebug($"Damage calculation for {playerId}: Base={baseDamage}, Str={strengthBonus}, Level={levelBonus}, Type={typeMultiplier}, Total={totalDamage}");
                
                return totalDamage;
            }
            
            _logger.LogWarning($"Could not find player {playerId} for damage calculation");
            return 10f; // Default damage
        }

        private bool ValidateAttack(WebSocketConnection connection, NetworkMessages.CombatActionMessage combatData)
        {
            // Basic validation
            if (string.IsNullOrEmpty(connection.PlayerId))
            {
                _logger.LogWarning("Attack validation failed: No player ID");
                return false;
            }

            // Range validation for enemy attacks
            if (!string.IsNullOrEmpty(combatData.TargetId) && combatData.TargetId.StartsWith("enemy_"))
            {
                if (_enemyManager != null)
                {
                    bool isValid = _enemyManager.ValidateAttackOnEnemy(
                        combatData.TargetId, 
                        connection.PlayerId, 
                        combatData.Position
                    );
                    
                    if (!isValid)
                    {
                        _logger.LogWarning($"Enemy attack validation failed: {connection.PlayerId} -> {combatData.TargetId}");
                    }
                    
                    return isValid;
                }
            }
            
            // Add more validation as needed (cooldowns, resources, etc.)
            return true;
        }

        private async Task HandleCombatRewards(string playerId, string enemyId, float damageDealt)
        {
            var enemy = _enemyManager?.GetEnemy(enemyId);
            if (enemy != null && !enemy.IsAlive)
            {
                // Enemy was killed - award experience
                long expGain = enemy.Level * 25; // 25 exp per enemy level
                
                _logger.LogInformation($"Player {playerId} killed enemy {enemyId} (Level {enemy.Level}) - awarding {expGain} experience");
                
                // Send experience gain message
                var expMessage = new NetworkMessages.ExperienceGainMessage
                {
                    PlayerId = playerId,
                    ExperienceGained = expGain,
                    Source = $"Killed {enemy.EnemyName}"
                };
                
                // Find connection by player ID
                _logger.LogInformation($"DEBUG: Looking for connection with PlayerId: {playerId}");
                _logger.LogInformation($"DEBUG: Available connections: [{string.Join(", ", _connections.Values.Select(c => $"{c.ConnectionId}:{c.PlayerId}"))}]");
                
                var connection = _connections.Values.FirstOrDefault(c => c.PlayerId == playerId);
                if (connection != null)
                {
                    await SendToConnection(connection.ConnectionId, "ExperienceGain", expMessage);
                    
                    // Update player stats in database with level-up detection
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();
                        
                        // Get current stats BEFORE adding experience (to track level changes)
                        var preUpdateStats = await playerStatsService.GetPlayerStatsAsync(playerId);
                        var preUpdateLevel = preUpdateStats.Level;
                        
                        // Add experience in database
                        var success = await playerStatsService.AddExperienceAsync(playerId, expGain);
                        
                        _logger.LogInformation($"DEBUG: AddExperienceAsync returned success: {success}");
                        
                        if (success)
                        {
                            // Get updated stats
                            var updatedStats = await playerStatsService.GetPlayerStatsAsync(playerId);
                            
                            _logger.LogInformation($"DEBUG: Pre-update level: {preUpdateLevel}, Post-update level: {updatedStats.Level}");
                            
                            // Update in-memory player state
                            _logger.LogDebug($"DEBUG: Looking for player in _players dictionary with ConnectionId: {connection.ConnectionId}");
                            _logger.LogDebug($"DEBUG: _players dictionary contains {_players.Count} entries: [{string.Join(", ", _players.Keys)}]");
                            
                            if (_players.TryGetValue(connection.ConnectionId, out var player))
                            {
                                var oldLevel = player.Level;
                                _logger.LogInformation($"DEBUG: In-memory player oldLevel: {oldLevel}, DB updatedStats.Level: {updatedStats.Level}");
                                player.Level = updatedStats.Level;
                                player.Experience = updatedStats.Experience;
                                player.Health = updatedStats.Health;
                                player.MaxHealth = updatedStats.MaxHealth;
                                player.Strength = updatedStats.Strength;
                                player.Defense = updatedStats.Defense;
                                player.Speed = updatedStats.Speed;

                                // Send updated stats to player
                                await SendToConnection(connection.ConnectionId, "PlayerStatsUpdate", new NetworkMessages.PlayerStatsUpdateMessage
                                {
                                    PlayerId = player.PlayerId,
                                    Level = updatedStats.Level,
                                    Experience = updatedStats.Experience,
                                    Health = updatedStats.Health,
                                    MaxHealth = updatedStats.MaxHealth,
                                    Strength = updatedStats.Strength,
                                    Defense = updatedStats.Defense,
                                    Speed = updatedStats.Speed,
                                    ExperienceToNextLevel = updatedStats.ExperienceToNextLevel
                                });

                                // Check if player leveled up (using pre-update level from database)
                                _logger.LogInformation($"DEBUG: Level check - PreUpdateLevel: {preUpdateLevel}, PostUpdateLevel: {updatedStats.Level}");
                                if (updatedStats.Level > preUpdateLevel)
                                {
                                    _logger.LogInformation($"DEBUG: LEVEL UP DETECTED! Player {playerId} leveled up from {preUpdateLevel} to {updatedStats.Level}");
                                    await SendToConnection(connection.ConnectionId, "LevelUp", new NetworkMessages.LevelUpMessage
                                    {
                                        PlayerId = player.PlayerId,
                                        NewLevel = updatedStats.Level,
                                        StatPointsGained = (updatedStats.Level - preUpdateLevel) * 5, // 5 points per level
                                        NewStats = new NetworkMessages.PlayerStatsUpdateMessage
                                        {
                                            PlayerId = player.PlayerId,
                                            Level = updatedStats.Level,
                                            Experience = updatedStats.Experience,
                                            Health = updatedStats.Health,
                                            MaxHealth = updatedStats.MaxHealth,
                                            Strength = updatedStats.Strength,
                                            Defense = updatedStats.Defense,
                                            Speed = updatedStats.Speed,
                                            ExperienceToNextLevel = updatedStats.ExperienceToNextLevel
                                        }
                                    });

                                    // Notify all players about the level up
                                    await BroadcastToAll("SystemNotification", new NetworkMessages.SystemNotification
                                    {
                                        Message = $"{player.PlayerName} reached level {updatedStats.Level}!",
                                        Type = "LevelUp",
                                        Priority = "Medium",
                                        Timestamp = DateTime.UtcNow
                                    });
                                    
                                    _logger.LogInformation($"Player {playerId} leveled up to level {updatedStats.Level}!");
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"DEBUG: Player not found in _players dictionary for ConnectionId: {connection.ConnectionId}, PlayerId: {playerId}");
                            }
                        }
                        else
                        {
                            _logger.LogError($"DEBUG: AddExperienceAsync returned false for player {playerId}");
                        }
                        
                        _logger.LogInformation($"DEBUG: Experience update process completed for player {playerId} (gain: {expGain})");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to update experience for player {playerId}");
                    }
                }
            }
        }

        private async Task HandleChatMessage(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var chatData = JsonSerializer.Deserialize<NetworkMessages.ChatMessage>(data.ToString()!);
            if (chatData == null) return;

            _logger.LogInformation($"Chat message from {chatData.SenderId}: {chatData.Message}");

            // Determine message routing based on channel type
            switch (chatData.ChannelType.ToLower())
            {
                case "global":
                case "all":
                    await BroadcastToAll("ChatMessage", chatData);
                    break;
                case "whisper":
                case "private":
                    if (!string.IsNullOrEmpty(chatData.TargetId))
                    {
                        await SendToConnection(chatData.TargetId, "ChatMessage", chatData);
                        await SendToConnection(connection.ConnectionId, "ChatMessage", chatData); // Echo back to sender
                    }
                    break;
                default:
                    await BroadcastToAll("ChatMessage", chatData);
                    break;
            }
        }

        private async Task HandleResourceGather(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var gatherData = JsonSerializer.Deserialize<NetworkMessages.ResourceGatherMessage>(data.ToString()!);
            if (gatherData == null) return;

            _logger.LogInformation($"Resource gather: {gatherData.ResourceType} by {gatherData.PlayerId}");

            // Process resource gathering logic here
            // For now, just acknowledge the action
            await SendToConnection(connection.ConnectionId, "SystemNotification", new NetworkMessages.SystemNotification
            {
                Message = $"You gathered {gatherData.ResourceType}",
                Type = "ResourceGathered",
                Timestamp = DateTime.UtcNow
            });
        }

        private async Task HandleAuthentication(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var authData = JsonSerializer.Deserialize<NetworkMessages.AuthenticationMessage>(data.ToString()!);
            if (authData == null) return;

            _logger.LogInformation($"Player authentication: {authData.PlayerName} ({authData.PlayerId})");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();

                // Get or create player stats from database
                PlayerStats playerStats;
                try
                {
                    playerStats = await playerStatsService.GetPlayerStatsAsync(authData.PlayerId);
                }
                catch (KeyNotFoundException)
                {
                    // Player doesn't exist, create new one
                    playerStats = await playerStatsService.CreatePlayerAsync(authData.PlayerId, authData.PlayerName);
                }

                // Create player state from database stats
                var player = new PlayerState
                {
                    PlayerId = connection.ConnectionId,
                    PlayerName = playerStats.PlayerName,
                    Position = playerStats.LastPosition ?? new Vector3Data(0, 1, 0),
                    Health = playerStats.Health,
                    MaxHealth = playerStats.MaxHealth,
                    Level = playerStats.Level,
                    Experience = playerStats.Experience,
                    Strength = playerStats.Strength,
                    Defense = playerStats.Defense,
                    Speed = playerStats.Speed,
                    IsOnline = true,
                    LastUpdate = DateTime.UtcNow
                };

                _players.TryAdd(connection.ConnectionId, player);
                connection.PlayerId = player.PlayerId;
                
                _logger.LogInformation($"Login Success: Connection {connection.ConnectionId} mapped to Player {player.PlayerId} ({player.PlayerName})");

                // Send authentication success response
                await SendToConnection(connection.ConnectionId, "AuthenticationResponse", new NetworkMessages.AuthenticationResponseMessage
                {
                    Success = true,
                    PlayerId = authData.PlayerId,
                    PlayerName = authData.PlayerName,
                    Message = "Authentication successful"
                });

                // Send player stats to the authenticated player
                await SendToConnection(connection.ConnectionId, "PlayerStatsUpdate", new NetworkMessages.PlayerStatsUpdateMessage
                {
                    PlayerId = player.PlayerId,
                    Level = playerStats.Level,
                    Experience = playerStats.Experience,
                    Health = playerStats.Health,
                    MaxHealth = playerStats.MaxHealth,
                    Strength = playerStats.Strength,
                    Defense = playerStats.Defense,
                    Speed = playerStats.Speed,
                    ExperienceToNextLevel = playerStats.ExperienceToNextLevel
                });

                // Notify all players about the new player
                await BroadcastToAll("PlayerJoined", new NetworkMessages.PlayerJoinNotification
                {
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    Position = player.Position,
                    JoinTime = DateTime.UtcNow
                });

                // Send current world state to the new player
                await SendWorldUpdate(connection.ConnectionId);

                _logger.LogInformation($"Player {authData.PlayerName} authenticated successfully with Level {playerStats.Level}, Health {playerStats.Health}/{playerStats.MaxHealth}, Experience {playerStats.Experience}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during authentication for player {authData.PlayerName}");
                await SendToConnection(connection.ConnectionId, "SystemNotification", new NetworkMessages.SystemNotification
                {
                    Message = "Authentication failed. Please try again.",
                    Type = "Error",
                    Priority = "High",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private async Task HandleHeartbeat(WebSocketConnection connection, object? data)
        {
            // Simple heartbeat acknowledgment
            _logger.LogDebug($"Heartbeat from {connection.ConnectionId}");
            
            if (_players.TryGetValue(connection.ConnectionId, out var player))
            {
                player.LastUpdate = DateTime.UtcNow;
            }
        }

        private async Task HandleExperienceGain(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var expData = JsonSerializer.Deserialize<NetworkMessages.ExperienceGainMessage>(data.ToString()!);
            if (expData == null) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();

                // Add experience in database
                var success = await playerStatsService.AddExperienceAsync(expData.PlayerId, expData.ExperienceGained);
                
                if (success)
                {
                    // Get updated stats
                    var updatedStats = await playerStatsService.GetPlayerStatsAsync(expData.PlayerId);
                    
                    // Update in-memory player state
                    if (_players.TryGetValue(connection.ConnectionId, out var player))
                    {
                        var oldLevel = player.Level;
                        player.Level = updatedStats.Level;
                        player.Experience = updatedStats.Experience;
                        player.Health = updatedStats.Health;
                        player.MaxHealth = updatedStats.MaxHealth;
                        player.Strength = updatedStats.Strength;
                        player.Defense = updatedStats.Defense;
                        player.Speed = updatedStats.Speed;

                        // Send updated stats to player
                        await SendToConnection(connection.ConnectionId, "PlayerStatsUpdate", new NetworkMessages.PlayerStatsUpdateMessage
                        {
                            PlayerId = player.PlayerId,
                            Level = updatedStats.Level,
                            Experience = updatedStats.Experience,
                            Health = updatedStats.Health,
                            MaxHealth = updatedStats.MaxHealth,
                            Strength = updatedStats.Strength,
                            Defense = updatedStats.Defense,
                            Speed = updatedStats.Speed,
                            ExperienceToNextLevel = updatedStats.ExperienceToNextLevel
                        });

                        // Check if player leveled up
                        if (updatedStats.Level > oldLevel)
                        {
                            await SendToConnection(connection.ConnectionId, "LevelUp", new NetworkMessages.LevelUpMessage
                            {
                                PlayerId = player.PlayerId,
                                NewLevel = updatedStats.Level,
                                StatPointsGained = (updatedStats.Level - oldLevel) * 5, // 5 points per level
                                NewStats = new NetworkMessages.PlayerStatsUpdateMessage
                                {
                                    PlayerId = player.PlayerId,
                                    Level = updatedStats.Level,
                                    Experience = updatedStats.Experience,
                                    Health = updatedStats.Health,
                                    MaxHealth = updatedStats.MaxHealth,
                                    Strength = updatedStats.Strength,
                                    Defense = updatedStats.Defense,
                                    Speed = updatedStats.Speed,
                                    ExperienceToNextLevel = updatedStats.ExperienceToNextLevel
                                }
                            });

                            // Notify all players about the level up
                            await BroadcastToAll("SystemNotification", new NetworkMessages.SystemNotification
                            {
                                Message = $"{player.PlayerName} reached level {updatedStats.Level}!",
                                Type = "LevelUp",
                                Priority = "Medium",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }

                    _logger.LogInformation($"Player {expData.PlayerId} gained {expData.ExperienceGained} experience from {expData.Source}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling experience gain for player {expData.PlayerId}");
            }
        }

        private async Task HandleHealthChange(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var healthData = JsonSerializer.Deserialize<NetworkMessages.HealthChangeMessage>(data.ToString()!);
            if (healthData == null) return;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();

                // Update health in database
                var success = await playerStatsService.UpdateHealthAsync(healthData.PlayerId, healthData.NewHealth);
                
                if (success)
                {
                    // Update in-memory player state
                    if (_players.TryGetValue(connection.ConnectionId, out var player))
                    {
                        player.Health = healthData.NewHealth;

                        // Send health update to player
                        await SendToConnection(connection.ConnectionId, "HealthChange", healthData);

                        // Broadcast health change to nearby players (for visual effects)
                        await BroadcastToOthers(connection.ConnectionId, "HealthChange", healthData);
                    }

                    _logger.LogInformation($"Player {healthData.PlayerId} health changed by {healthData.HealthChange} to {healthData.NewHealth} from {healthData.Source}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling health change for player {healthData.PlayerId}");
            }
        }

        private async Task HandleLogin(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var loginData = JsonSerializer.Deserialize<NetworkMessages.LoginMessage>(data.ToString()!);
            if (loginData == null) return;

            _logger.LogInformation($"Login attempt for username: {loginData.Username}");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                // Authenticate using the new authentication service
                var result = await authService.LoginAsync(loginData.Username, loginData.ClientHashedPassword);

                if (result.Success && result.PlayerStats != null)
                {
                    // Create player state from authenticated player
                    var player = new PlayerState
                    {
                        PlayerId = result.PlayerId ?? result.PlayerStats.PlayerId,
                        PlayerName = result.PlayerStats.PlayerName,
                        Position = result.PlayerStats.LastPosition ?? new Vector3Data(0, 1, 0),
                        Health = result.PlayerStats.Health,
                        MaxHealth = result.PlayerStats.MaxHealth,
                        Level = result.PlayerStats.Level,
                        Experience = result.PlayerStats.Experience,
                        Strength = result.PlayerStats.Strength,
                        Defense = result.PlayerStats.Defense,
                        Speed = result.PlayerStats.Speed,
                        IsOnline = true,
                        LastUpdate = DateTime.UtcNow
                    };

                    _players.TryAdd(connection.ConnectionId, player);
                    connection.PlayerId = result.PlayerId ?? result.PlayerStats.PlayerId;
                    
                    _logger.LogInformation($"Login Success: Connection {connection.ConnectionId} mapped to Player {connection.PlayerId} ({player.PlayerName})");
                
                // TEMPORARY: Auto-reset to Level 1 for testing
                if (player.PlayerName == "RipTide")
                {
                    try
                    {
                        using var resetScope = _serviceProvider.CreateScope();
                        var playerStatsService = resetScope.ServiceProvider.GetRequiredService<IPlayerStatsService>();
                        
                        var currentStats = await playerStatsService.GetPlayerStatsAsync(connection.PlayerId);
                        currentStats.Level = 1;
                        currentStats.Experience = 0;
                        currentStats.NextLevelExp = 100;
                        currentStats.Health = 100;
                        currentStats.MaxHealth = 100;
                        currentStats.Strength = 10;
                        currentStats.Defense = 10;
                        currentStats.Speed = 10;
                        
                        await playerStatsService.UpdatePlayerStatsAsync(currentStats);
                        
                        // Update in-memory player state
                        player.Level = 1;
                        player.Experience = 0;
                        player.Health = 100;
                        player.MaxHealth = 100;
                        player.Strength = 10;
                        player.Defense = 10;
                        player.Speed = 10;
                        
                        _logger.LogInformation($"AUTO-RESET: Player {connection.PlayerId} reset to Level 1 for testing");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error auto-resetting player stats");
                    }
                }

                    // Send successful login response
                    await SendToConnection(connection.ConnectionId, "LoginResponse", new NetworkMessages.LoginResponseMessage
                    {
                        Success = true,
                        PlayerId = result.PlayerId ?? string.Empty,
                        PlayerName = result.PlayerName ?? string.Empty,
                        SessionToken = result.SessionToken,
                        PlayerStats = new NetworkMessages.PlayerStatsUpdateMessage
                        {
                            PlayerId = result.PlayerId ?? string.Empty,
                            Level = result.PlayerStats.Level,
                            Experience = result.PlayerStats.Experience,
                            Health = result.PlayerStats.Health,
                            MaxHealth = result.PlayerStats.MaxHealth,
                            Strength = result.PlayerStats.Strength,
                            Defense = result.PlayerStats.Defense,
                            Speed = result.PlayerStats.Speed,
                            ExperienceToNextLevel = result.PlayerStats.ExperienceToNextLevel
                        }
                    });

                    // Notify all players about the new player
                    await BroadcastToAll("PlayerJoined", new NetworkMessages.PlayerJoinNotification
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.PlayerName,
                        Position = player.Position,
                        JoinTime = DateTime.UtcNow
                    });

                    // Send current world state to the new player
                    await SendWorldUpdate(connection.ConnectionId);

                    // Send enemy states to the new player
                    if (_enemyManager != null)
                    {
                        await _enemyManager.SendEnemyStatesToClient(connection.ConnectionId);
                    }

                    _logger.LogInformation($"Player {result.PlayerName} logged in successfully with Level {result.PlayerStats.Level}");
                }
                else
                {
                    // Send failed login response
                    await SendToConnection(connection.ConnectionId, "LoginResponse", new NetworkMessages.LoginResponseMessage
                    {
                        Success = false,
                        ErrorMessage = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during login for username {loginData.Username}");
                await SendToConnection(connection.ConnectionId, "LoginResponse", new NetworkMessages.LoginResponseMessage
                {
                    Success = false,
                    ErrorMessage = "An error occurred during login"
                });
            }
        }

        private async Task HandleSessionValidation(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var sessionData = JsonSerializer.Deserialize<NetworkMessages.SessionValidationMessage>(data.ToString()!);
            if (sessionData == null) return;

            _logger.LogInformation($"Session validation attempt for connection: {connection.ConnectionId}");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                // Validate session token
                var result = await authService.ValidateSessionTokenAsync(sessionData.SessionToken);

                if (result.Success && result.PlayerStats != null)
                {
                    // Create player state from validated session
                    var player = new PlayerState
                    {
                        PlayerId = result.PlayerStats.PlayerId,
                        PlayerName = result.PlayerStats.PlayerName,
                        Position = result.PlayerStats.LastPosition ?? new Vector3Data(0, 1, 0),
                        Health = result.PlayerStats.Health,
                        MaxHealth = result.PlayerStats.MaxHealth,
                        Level = result.PlayerStats.Level,
                        Experience = result.PlayerStats.Experience,
                        Strength = result.PlayerStats.Strength,
                        Defense = result.PlayerStats.Defense,
                        Speed = result.PlayerStats.Speed,
                        IsOnline = true,
                        LastUpdate = DateTime.UtcNow
                    };

                    _players.TryAdd(connection.ConnectionId, player);
                    connection.PlayerId = player.PlayerId;

                    // Send authentication success response (for compatibility with existing client)
                    await SendToConnection(connection.ConnectionId, "AuthenticationResponse", new NetworkMessages.AuthenticationResponseMessage
                    {
                        Success = true,
                        PlayerId = result.PlayerId ?? string.Empty,
                        PlayerName = result.PlayerName ?? string.Empty,
                        Message = "Session validated successfully"
                    });

                    // Send player stats
                    await SendToConnection(connection.ConnectionId, "PlayerStatsUpdate", new NetworkMessages.PlayerStatsUpdateMessage
                    {
                        PlayerId = result.PlayerId ?? string.Empty,
                        Level = result.PlayerStats.Level,
                        Experience = result.PlayerStats.Experience,
                        Health = result.PlayerStats.Health,
                        MaxHealth = result.PlayerStats.MaxHealth,
                        Strength = result.PlayerStats.Strength,
                        Defense = result.PlayerStats.Defense,
                        Speed = result.PlayerStats.Speed,
                        ExperienceToNextLevel = result.PlayerStats.ExperienceToNextLevel
                    });

                    // Notify all players about the reconnected player
                    await BroadcastToAll("PlayerJoined", new NetworkMessages.PlayerJoinNotification
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.PlayerName,
                        Position = player.Position,
                        JoinTime = DateTime.UtcNow
                    });

                    // Send current world state to the reconnected player
                    await SendWorldUpdate(connection.ConnectionId);

                    // Send enemy states to the reconnected player
                    if (_enemyManager != null)
                    {
                        await _enemyManager.SendEnemyStatesToClient(connection.ConnectionId);
                    }

                    _logger.LogInformation($"Player {result.PlayerName} reconnected successfully via session token");
                }
                else
                {
                    // Session validation failed
                    await SendToConnection(connection.ConnectionId, "AuthenticationResponse", new NetworkMessages.AuthenticationResponseMessage
                    {
                        Success = false,
                        Message = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during session validation");
                await SendToConnection(connection.ConnectionId, "AuthenticationResponse", new NetworkMessages.AuthenticationResponseMessage
                {
                    Success = false,
                    Message = "An error occurred during session validation"
                });
            }
        }

        private async Task HandleLogout(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var logoutData = JsonSerializer.Deserialize<NetworkMessages.LogoutMessage>(data.ToString()!);
            if (logoutData == null) return;

            _logger.LogInformation($"Logout request for player: {logoutData.PlayerId}");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

                // Clear session in database
                var success = await authService.LogoutAsync(logoutData.PlayerId);

                // Remove from in-memory collections
                _players.TryRemove(connection.ConnectionId, out var player);
                
                if (player != null)
                {
                    // Notify other players
                    await BroadcastToOthers(connection.ConnectionId, "SystemNotification", new NetworkMessages.SystemNotification
                    {
                        Message = $"Player {player.PlayerName} has logged out",
                        Type = "PlayerLeft",
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Send logout confirmation
                await SendToConnection(connection.ConnectionId, "SystemNotification", new NetworkMessages.SystemNotification
                {
                    Message = success ? "Logged out successfully" : "Logout completed",
                    Type = "LogoutConfirmed",
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation($"Player {logoutData.PlayerId} logged out successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during logout for player {logoutData.PlayerId}");
            }
        }

        #endregion

        #region Broadcasting Methods

        public async Task SendToConnection(string connectionId, string messageType, object data)
        {
            if (_connections.TryGetValue(connectionId, out var connection) && 
                connection.WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    var wrapper = new MessageWrapper { Type = messageType, Data = data };
                    var json = JsonSerializer.Serialize(wrapper);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    await connection.WebSocket.SendAsync(
                        new ArraySegment<byte>(bytes), 
                        WebSocketMessageType.Text, 
                        true, 
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error sending message to {connectionId}");
                }
            }
        }

        public async Task SendToPlayer(string playerId, string messageType, object data)
        {
            var connection = _connections.Values.FirstOrDefault(conn => conn.PlayerId == playerId);
            if (connection != null)
            {
                await SendToConnection(connection.ConnectionId, messageType, data);
            }
            else
            {
                _logger.LogWarning($"Cannot send message to player {playerId} - not connected");
            }
        }

        public async Task BroadcastToAll(string messageType, object data)
        {
            var tasks = _connections.Values.Select(conn => SendToConnection(conn.ConnectionId, messageType, data));
            await Task.WhenAll(tasks);
        }

        private async Task BroadcastToAllPrivate(string messageType, object data)
        {
            await BroadcastToAll(messageType, data);
        }

        private async Task BroadcastToOthers(string excludeConnectionId, string messageType, object data)
        {
            var tasks = _connections.Values
                .Where(conn => conn.ConnectionId != excludeConnectionId)
                .Select(conn => SendToConnection(conn.ConnectionId, messageType, data));
            await Task.WhenAll(tasks);
        }

        private Task SendWorldUpdate(string connectionId)
        {
            var worldUpdate = new NetworkMessages.WorldUpdateMessage
            {
                Players = _players.Values.ToList(),
                Resources = new List<ResourceNode>(), // Add resource nodes here
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            return SendToConnection(connectionId, "WorldUpdate", worldUpdate);
        }

        #endregion

        public int GetConnectionCount() => _connections.Count;
        public int GetPlayerCount() => _players.Count;
        private async Task HandleAdminResetStats(WebSocketConnection connection, object? data)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();
                
                if (!string.IsNullOrEmpty(connection.PlayerId))
                {
                    var currentStats = await playerStatsService.GetPlayerStatsAsync(connection.PlayerId);
                    
                    // Reset to Level 1 with 0 experience
                    currentStats.Level = 1;
                    currentStats.Experience = 0;
                    currentStats.NextLevelExp = 100;
                    currentStats.Health = 100;
                    currentStats.MaxHealth = 100;
                    currentStats.Strength = 10;
                    currentStats.Defense = 10;
                    currentStats.Speed = 10;
                    
                    await playerStatsService.UpdatePlayerStatsAsync(currentStats);
                    
                    // Update in-memory player state
                    if (_players.TryGetValue(connection.ConnectionId, out var player))
                    {
                        player.Level = 1;
                        player.Experience = 0;
                        player.Health = 100;
                        player.MaxHealth = 100;
                        player.Strength = 10;
                        player.Defense = 10;
                        player.Speed = 10;
                    }
                    
                    _logger.LogInformation($"ADMIN: Reset player {connection.PlayerId} to Level 1");
                    
                    // Send confirmation
                    await SendToConnection(connection.ConnectionId, "AdminResponse", new { 
                        Success = true, 
                        Message = "Player stats reset to Level 1" 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting player stats");
                await SendToConnection(connection.ConnectionId, "AdminResponse", new { 
                    Success = false, 
                    Message = "Failed to reset stats" 
                });
            }
        }

        private async Task HandleInventoryRequest(WebSocketConnection connection, object data)
        {
            try
            {
                _logger.LogInformation($"Handling inventory request for connection: {connection.ConnectionId}");

                if (string.IsNullOrEmpty(connection.PlayerId))
                {
                    _logger.LogWarning($"Inventory request from unauthenticated connection: {connection.ConnectionId}");
                    await SendToConnection(connection.ConnectionId, "InventoryResponse", new NetworkMessages.InventoryResponseMessage
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated"
                    });
                    return;
                }

                // Get player's inventory from database
                using var scope = _serviceProvider.CreateScope();
                var inventoryRepository = scope.ServiceProvider.GetRequiredService<IPlayerInventoryRepository>();
                
                var inventoryItems = await inventoryRepository.GetPlayerInventoryAsync(connection.PlayerId);

                // Send inventory response
                await SendToConnection(connection.ConnectionId, "InventoryResponse", new NetworkMessages.InventoryResponseMessage
                {
                    PlayerId = connection.PlayerId,
                    Items = inventoryItems,
                    Success = true
                });

                _logger.LogInformation($"Sent inventory response to player {connection.PlayerId} with {inventoryItems.Count} items from database");
                
                // Debug log each item being sent
                foreach (var item in inventoryItems)
                {
                    _logger.LogDebug("DEBUG: Sending inventory item - Slot: {Slot}, ItemType: {ItemType}, ItemName: {ItemName}, Quantity: {Quantity}", 
                        item.SlotIndex, item.ItemType, item.ItemName, item.Quantity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling inventory request");
                await SendToConnection(connection.ConnectionId, "InventoryResponse", new NetworkMessages.InventoryResponseMessage
                {
                    Success = false,
                    ErrorMessage = "Server error"
                });
            }
        }

        private async Task HandleLootPickupRequest(WebSocketConnection connection, object? data)
        {
            try
            {
                if (string.IsNullOrEmpty(connection.PlayerId))
                {
                    _logger.LogWarning($"Loot pickup request from unauthenticated connection: {connection.ConnectionId}");
                    return;
                }

                if (_lootManager == null)
                {
                    _logger.LogWarning("LootManager not available for pickup request");
                    return;
                }

                var lootRequest = JsonSerializer.Deserialize<NetworkMessages.LootPickupRequestMessage>(
                    JsonSerializer.Serialize(data));

                if (lootRequest == null)
                {
                    _logger.LogWarning("Failed to deserialize loot pickup request");
                    return;
                }

                _logger.LogInformation($"Player {connection.PlayerId} attempting to pick up loot {lootRequest.LootId}");

                // Handle the pickup request through LootManager
                await _lootManager.HandleLootPickup(connection.PlayerId, lootRequest.LootId, lootRequest.PlayerPosition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling loot pickup request");
                
                // Send error response to player
                if (!string.IsNullOrEmpty(connection.PlayerId))
                {
                    await SendToConnection(connection.ConnectionId, "LootPickupResponse", new NetworkMessages.LootPickupResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        LootId = "unknown",
                        Success = false,
                        Message = "Server error during pickup",
                        Item = null
                    });
                }
            }
        }

        private List<CombatMechanix.Models.InventoryItem> GenerateSampleInventoryItems(string playerId)
        {
            var items = new List<CombatMechanix.Models.InventoryItem>();

            // Add some sample items for testing
            items.Add(new CombatMechanix.Models.InventoryItem
            {
                ItemId = Guid.NewGuid().ToString(),
                ItemType = "sword",
                ItemName = "Iron Sword",
                ItemDescription = "A sturdy iron blade forged by skilled blacksmiths.",
                Quantity = 1,
                SlotIndex = 0,
                IconName = "iron_sword",
                Rarity = "Common",
                Level = 5,
                IsStackable = false,
                MaxStackSize = 1,
                AttackPower = 15,
                DefensePower = 0,
                Value = 100
            });

            items.Add(new CombatMechanix.Models.InventoryItem
            {
                ItemId = Guid.NewGuid().ToString(),
                ItemType = "potion",
                ItemName = "Health Potion",
                ItemDescription = "Restores 50 health points when consumed.",
                Quantity = 5,
                SlotIndex = 1,
                IconName = "health_potion",
                Rarity = "Common",
                Level = 1,
                IsStackable = true,
                MaxStackSize = 10,
                AttackPower = 0,
                DefensePower = 0,
                Value = 25
            });

            items.Add(new InventoryItem
            {
                ItemId = Guid.NewGuid().ToString(),
                ItemType = "shield",
                ItemName = "Wooden Shield",
                ItemDescription = "A simple wooden shield that provides basic protection.",
                Quantity = 1,
                SlotIndex = 2,
                IconName = "wooden_shield",
                Rarity = "Common",
                Level = 3,
                IsStackable = false,
                MaxStackSize = 1,
                AttackPower = 0,
                DefensePower = 8,
                Value = 50
            });

            items.Add(new InventoryItem
            {
                ItemId = Guid.NewGuid().ToString(),
                ItemType = "bow",
                ItemName = "Hunter's Bow",
                ItemDescription = "A well-crafted bow favored by hunters and rangers.",
                Quantity = 1,
                SlotIndex = 5,
                IconName = "hunters_bow",
                Rarity = "Rare",
                Level = 8,
                IsStackable = false,
                MaxStackSize = 1,
                AttackPower = 20,
                DefensePower = 0,
                Value = 200
            });

            items.Add(new InventoryItem
            {
                ItemId = Guid.NewGuid().ToString(),
                ItemType = "gem",
                ItemName = "Ruby",
                ItemDescription = "A precious red gemstone that sparkles with inner fire.",
                Quantity = 2,
                SlotIndex = 10,
                IconName = "ruby",
                Rarity = "Epic",
                Level = 1,
                IsStackable = true,
                MaxStackSize = 50,
                AttackPower = 0,
                DefensePower = 0,
                Value = 500
            });

            items.Add(new InventoryItem
            {
                ItemId = Guid.NewGuid().ToString(),
                ItemType = "armor",
                ItemName = "Leather Armor",
                ItemDescription = "Flexible leather armor that provides moderate protection.",
                Quantity = 1,
                SlotIndex = 15,
                IconName = "leather_armor",
                Rarity = "Common",
                Level = 6,
                IsStackable = false,
                MaxStackSize = 1,
                AttackPower = 0,
                DefensePower = 12,
                Value = 150
            });

            return items;
        }
    }

    public class WebSocketConnection
    {
        public string ConnectionId { get; }
        public WebSocket WebSocket { get; }
        public DateTime ConnectedAt { get; }
        public string? PlayerId { get; set; }

        public WebSocketConnection(string connectionId, WebSocket webSocket)
        {
            ConnectionId = connectionId;
            WebSocket = webSocket;
            ConnectedAt = DateTime.UtcNow;
        }
    }
}