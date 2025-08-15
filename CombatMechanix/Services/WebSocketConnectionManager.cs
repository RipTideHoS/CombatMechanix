using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CombatMechanix.Models;
using CombatMechanix.Services;

namespace CombatMechanix.Services
{
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
        private readonly ConcurrentDictionary<string, PlayerState> _players = new();
        private readonly ILogger<WebSocketConnectionManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private EnemyManager? _enemyManager;

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

            // Process combat logic here (damage calculation, etc.)
            // For now, just broadcast the action
            await BroadcastToAll("CombatAction", combatData);
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
                        PlayerId = connection.ConnectionId,
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
                        PlayerId = connection.ConnectionId,
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
    }

    public class WebSocketConnection
    {
        public string ConnectionId { get; }
        public WebSocket WebSocket { get; }
        public DateTime ConnectedAt { get; }

        public WebSocketConnection(string connectionId, WebSocket webSocket)
        {
            ConnectionId = connectionId;
            WebSocket = webSocket;
            ConnectedAt = DateTime.UtcNow;
        }
    }
}