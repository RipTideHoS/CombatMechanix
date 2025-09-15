using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Numerics;
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
        
        // Phase 1: Projectile state tracking for collision-based damage system
        private readonly ConcurrentDictionary<string, ProjectileState> _activeProjectiles = new();
        private readonly Timer _projectileCleanupTimer;

        public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // Initialize projectile cleanup timer (cleanup every 30 seconds)
            _projectileCleanupTimer = new Timer(CleanupExpiredProjectiles, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
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
            var messageBuffer = new List<byte>();
            _logger.LogInformation($"Starting message receive loop for {connection.ConnectionId}");

            while (connection.WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await connection.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    _logger.LogDebug($"Received message from {connection.ConnectionId}: Type={result.MessageType}, Count={result.Count}, EndOfMessage={result.EndOfMessage}");

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Add received bytes to message buffer
                        messageBuffer.AddRange(buffer.Take(result.Count));
                        
                        // If this is the end of the message, process the complete message
                        if (result.EndOfMessage)
                        {
                            var message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                            _logger.LogInformation($"Complete text message from {connection.ConnectionId} ({messageBuffer.Count} bytes)");
                            await ProcessMessage(connection, message);
                            messageBuffer.Clear(); // Clear for next message
                        }
                        else
                        {
                            _logger.LogDebug($"Partial message received from {connection.ConnectionId}, waiting for more data");
                        }
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

                // Log non-movement message types
                if (wrapper.Type != "PlayerMovement" && wrapper.Type != "Heartbeat")
                {
                    _logger.LogInformation($"Processing message type: {wrapper.Type} from {connection.ConnectionId}");
                }
                
                // Special logging for equipment-related messages
                if (wrapper.Type.Contains("Equip") || wrapper.Type == "ItemEquipRequest" || wrapper.Type == "ItemUnequipRequest")
                {
                    _logger.LogInformation($"[DEBUG EQUIPMENT] Processing message type: {wrapper.Type} from {connection.ConnectionId}");
                    _logger.LogInformation($"[DEBUG RAW MESSAGE] Raw message ({jsonMessage.Length} chars): {jsonMessage}");
                }
                
                // Special logging for combat messages
                if (wrapper.Type == "CombatAction")
                {
                }

                switch (wrapper.Type)
                {
                    case "PlayerMovement":
                        await HandlePlayerMovement(connection, wrapper.Data);
                        break;
                    case "CombatAction":
                        await HandleCombatAction(connection, wrapper.Data);
                        break;
                    case "ProjectileHit":
                        await HandleProjectileHit(connection, wrapper.Data);
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
                    case "ItemUseRequest":
                        await HandleItemUseRequest(connection, wrapper.Data);
                        break;
                    case "ItemSellRequest":
                        await HandleItemSellRequest(connection, wrapper.Data);
                        break;
                    case "EquipmentRequest":
                        await HandleEquipmentRequest(connection, wrapper.Data);
                        break;
                    case "ItemEquipRequest":
                        _logger.LogInformation($"[DEBUG SWITCH] About to call HandleItemEquipRequest for {connection.ConnectionId}");
                        await HandleItemEquipRequest(connection, wrapper.Data);
                        _logger.LogInformation($"[DEBUG SWITCH] Finished HandleItemEquipRequest for {connection.ConnectionId}");
                        break;
                    case "ItemUnequipRequest":
                        await HandleItemUnequipRequest(connection, wrapper.Data);
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
                    case "RespawnRequest":
                        await HandleRespawnRequest(connection, wrapper.Data);
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

            // Player movement is too frequent for logging
            // _logger.LogInformation($"Received PlayerMovement from {connection.ConnectionId}: Position=({movementData.Position.X:F2}, {movementData.Position.Y:F2}, {movementData.Position.Z:F2})");

            // Update player state
            if (_players.TryGetValue(connection.ConnectionId, out var player))
            {
                // Reject movement from dead players
                if (player.Health <= 0)
                {
                    return;
                }
                
                player.Position = movementData.Position;
                player.Velocity = movementData.Velocity;
                player.Rotation = movementData.Rotation;
                player.LastUpdate = DateTime.UtcNow;
                
                // Also update the connection's LastPosition for AI access
                connection.LastPosition = movementData.Position;
                connection.PlayerName = player.PlayerName;
                
                _logger.LogDebug($"Updated position for {player.PlayerName}: ({movementData.Position.X:F2}, {movementData.Position.Y:F2}, {movementData.Position.Z:F2})");

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

            // Determine actual attack type based on equipped weapon
            var equippedWeapon = await GetPlayerEquippedWeapon(connection.PlayerId);
            if (equippedWeapon != null)
            {
                // Update attack type based on weapon
                combatData.AttackType = equippedWeapon.WeaponType == "Ranged" ? "RangedAttack" : "MeleeAttack";
                
                // Validate weapon range for both melee and ranged weapons
                if (!ValidateWeaponRange(connection, combatData, equippedWeapon))
                {
                    _logger.LogWarning($"{equippedWeapon.WeaponType} attack from {connection.PlayerId} failed range validation");
                    return;
                }
            }
            else
            {
                // No weapon equipped - default to melee with basic unarmed range
                combatData.AttackType = "MeleeAttack";
                // TODO: Validate unarmed range (very short, like 1.2 units)
            }

            // Validate attack timing and other conditions
            if (!ValidateAttack(connection, combatData))
            {
                _logger.LogWarning($"Invalid attack from {connection.PlayerId} to {combatData.TargetId}");
                return;
            }

            // Phase 1: New projectile-based system for ranged weapons
            if (equippedWeapon != null && equippedWeapon.WeaponType == "Ranged")
            {
                // Ranged weapons use new collision-based system
                await HandleRangedAttack(connection, combatData, equippedWeapon);
            }
            else
            {
                _logger.LogInformation($"[DEBUG MELEE] Taking melee attack path for weapon {(equippedWeapon != null ? equippedWeapon.ItemName + " (" + equippedWeapon.WeaponType + ")" : "unarmed")}");
                // Melee weapons keep existing instant-hit system
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
                    // Ground attack - broadcast for visual effects
                    combatData.AttackerId = connection.PlayerId;
                    await BroadcastToAll("CombatAction", combatData);
                }
            }
        }

        #region Phase 1: New Projectile-Based Combat System

        /// <summary>
        /// Handle ranged attacks using new projectile collision system
        /// Phase 3: Supports multi-projectile weapons with spread patterns
        /// </summary>
        private async Task HandleRangedAttack(WebSocketConnection connection, NetworkMessages.CombatActionMessage combatData, EquippedItem weapon)
        {
            if (string.IsNullOrEmpty(connection.PlayerId))
            {
                _logger.LogWarning($"Ranged attack attempted by unauthenticated connection: {connection.ConnectionId}");
                return;
            }

            // Get player position from stored state
            Vector3Data launchPosition = new Vector3Data(0, 0, 0); // Default fallback
            if (_players.TryGetValue(connection.ConnectionId, out var playerState) && playerState != null)
            {
                launchPosition = playerState.Position;
            }

            // Phase 3: Create weapon data with multi-projectile support
            var weaponData = new NetworkMessages.ProjectileWeaponData
            {
                ProjectileSpeed = weapon.ProjectileSpeed > 0 ? weapon.ProjectileSpeed : 20f,
                WeaponRange = weapon.WeaponRange > 0 ? weapon.WeaponRange : 25f,
                Accuracy = weapon.Accuracy > 0 ? weapon.Accuracy : 0.7f,
                BaseDamage = weapon.AttackPower > 0 ? weapon.AttackPower : 10,
                WeaponType = weapon.WeaponType,
                WeaponName = weapon.ItemName,
                // Phase 3: Add multi-projectile properties (TODO: Get from weapon data)
                ProjectileCount = GetProjectileCount(weapon),
                SpreadAngle = GetSpreadAngle(weapon),
                SpreadPattern = GetSpreadPattern(weapon)
            };

            // Phase 3: Generate multiple projectiles if needed
            var projectiles = GenerateProjectiles(connection.PlayerId, launchPosition, combatData.Position, weaponData);

            // Store all projectile states for validation
            foreach (var projectile in projectiles)
            {
                var projectileState = new ProjectileState
                {
                    ProjectileId = projectile.ProjectileId,
                    ShooterId = connection.PlayerId,
                    IntendedTargetId = combatData.TargetId,
                    LaunchPosition = projectile.LaunchPosition,
                    TargetPosition = projectile.TargetPosition,
                    WeaponData = weaponData,
                    LaunchTime = DateTime.UtcNow,
                    MaxTravelDistance = weaponData.WeaponRange,
                    ExpirationTime = DateTime.UtcNow.AddSeconds(10) // 10 second max lifetime
                };

                _activeProjectiles.TryAdd(projectile.ProjectileId, projectileState);
            }

            // Phase 3: Send multi-projectile launch message
            var launchMessage = new NetworkMessages.ProjectileLaunchMessage
            {
                ShooterId = connection.PlayerId,
                IntendedTargetId = combatData.TargetId,
                LaunchPosition = launchPosition,
                TargetPosition = combatData.Position,
                WeaponData = weaponData,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Phase 3: Support both single and multi-projectile mode
            if (projectiles.Count == 1)
            {
                // Single projectile - use legacy field for compatibility
                launchMessage.ProjectileId = projectiles[0].ProjectileId;
                launchMessage.Projectiles.Clear();
            }
            else
            {
                // Multi-projectile - use new array system
                launchMessage.ProjectileId = string.Empty;
                launchMessage.Projectiles = projectiles;
            }

            await BroadcastToAll("ProjectileLaunch", launchMessage);

        }

        #region Phase 3: Multi-Projectile Helper Methods

        /// <summary>
        /// Get projectile count for weapon (Phase 3)
        /// </summary>
        private int GetProjectileCount(EquippedItem weapon)
        {
            // TODO: Read from weapon database/configuration
            // For now, hardcode some test weapons
            if (weapon.ItemName.Contains("Shotgun", StringComparison.OrdinalIgnoreCase) ||
                weapon.ItemName.Contains("Scatter", StringComparison.OrdinalIgnoreCase))
            {
                return 5; // Shotgun fires 5 pellets
            }
            if (weapon.ItemName.Contains("Burst", StringComparison.OrdinalIgnoreCase))
            {
                return 3; // Burst weapon fires 3 shots
            }
            return 1; // Default single projectile
        }

        /// <summary>
        /// Get spread angle for weapon (Phase 3)
        /// </summary>
        private float GetSpreadAngle(EquippedItem weapon)
        {
            // TODO: Read from weapon database/configuration
            if (weapon.ItemName.Contains("Shotgun", StringComparison.OrdinalIgnoreCase))
            {
                return 25f; // 25 degree spread for shotgun
            }
            if (weapon.ItemName.Contains("Scatter", StringComparison.OrdinalIgnoreCase))
            {
                return 15f; // 15 degree spread
            }
            if (weapon.ItemName.Contains("Burst", StringComparison.OrdinalIgnoreCase))
            {
                return 5f; // 5 degree spread for burst
            }
            return 0f; // No spread for single projectile
        }

        /// <summary>
        /// Get spread pattern for weapon (Phase 3)
        /// </summary>
        private string GetSpreadPattern(EquippedItem weapon)
        {
            // TODO: Read from weapon database/configuration
            if (weapon.ItemName.Contains("Shotgun", StringComparison.OrdinalIgnoreCase))
            {
                return "Cone"; // Shotgun uses cone pattern
            }
            if (weapon.ItemName.Contains("Burst", StringComparison.OrdinalIgnoreCase))
            {
                return "Horizontal"; // Burst uses horizontal line
            }
            return "None"; // Single projectile
        }

        /// <summary>
        /// Generate multiple projectiles with spread patterns (Phase 3)
        /// </summary>
        private List<NetworkMessages.ProjectileData> GenerateProjectiles(string playerId, Vector3Data launchPos, Vector3Data targetPos, NetworkMessages.ProjectileWeaponData weaponData)
        {
            var projectiles = new List<NetworkMessages.ProjectileData>();

            if (weaponData.ProjectileCount <= 1)
            {
                // Single projectile
                var projectileId = GenerateProjectileId(playerId);
                projectiles.Add(new NetworkMessages.ProjectileData
                {
                    ProjectileId = projectileId,
                    LaunchPosition = launchPos,
                    TargetPosition = targetPos,
                    SpeedMultiplier = 1.0f,
                    AccuracyMultiplier = 1.0f
                });
                return projectiles;
            }

            // Multi-projectile with spread
            for (int i = 0; i < weaponData.ProjectileCount; i++)
            {
                var projectileId = GenerateProjectileId(playerId, i);
                var spreadTargetPos = CalculateSpreadTarget(launchPos, targetPos, weaponData.SpreadPattern, weaponData.SpreadAngle, i, weaponData.ProjectileCount);

                projectiles.Add(new NetworkMessages.ProjectileData
                {
                    ProjectileId = projectileId,
                    LaunchPosition = launchPos,
                    TargetPosition = spreadTargetPos,
                    SpeedMultiplier = 1.0f + (Random.Shared.NextSingle() - 0.5f) * 0.2f, // ±10% speed variation
                    AccuracyMultiplier = 1.0f + (Random.Shared.NextSingle() - 0.5f) * 0.3f  // ±15% accuracy variation
                });
            }

            return projectiles;
        }

        /// <summary>
        /// Calculate spread target position for individual projectile (Phase 3)
        /// </summary>
        private Vector3Data CalculateSpreadTarget(Vector3Data launchPos, Vector3Data centerTarget, string pattern, float spreadAngle, int projectileIndex, int totalProjectiles)
        {
            // Calculate base direction vector
            var direction = new Vector3(
                centerTarget.X - launchPos.X,
                centerTarget.Y - launchPos.Y,
                centerTarget.Z - launchPos.Z
            );
            var distance = direction.Length();
            direction = Vector3.Normalize(direction);

            Vector3 spreadDirection;

            switch (pattern)
            {
                case "Cone":
                    spreadDirection = CalculateConeSpread(direction, spreadAngle);
                    break;
                case "Horizontal":
                    spreadDirection = CalculateHorizontalSpread(direction, spreadAngle, projectileIndex, totalProjectiles);
                    break;
                case "Circle":
                    spreadDirection = CalculateCircleSpread(direction, spreadAngle, projectileIndex, totalProjectiles);
                    break;
                default:
                    spreadDirection = direction; // No spread
                    break;
            }

            // Calculate new target position
            var newTarget = new Vector3(launchPos.X, launchPos.Y, launchPos.Z) + spreadDirection * distance;

            return new Vector3Data(newTarget.X, newTarget.Y, newTarget.Z);
        }

        /// <summary>
        /// Calculate cone spread (shotgun pattern)
        /// </summary>
        private Vector3 CalculateConeSpread(Vector3 baseDirection, float spreadAngle)
        {
            var random = Random.Shared;
            var angle = random.NextSingle() * spreadAngle * (Math.PI / 180); // Convert to radians
            var azimuth = random.NextSingle() * 2 * Math.PI; // Random rotation around horizontal axis only

            // Force horizontal spread only - no vertical component
            // Create a horizontal right vector (perpendicular to direction in XZ plane)
            var horizontalDirection = Vector3.Normalize(new Vector3(baseDirection.X, 0, baseDirection.Z));
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, horizontalDirection));

            // Apply spread only in horizontal plane (no Y component)
            var spreadX = (float)(Math.Sin(angle) * Math.Cos(azimuth));
            var spreadZ = (float)Math.Cos(angle);

            // Combine base direction with horizontal spread only
            var spreadDirection = horizontalDirection * spreadZ + right * spreadX;

            // Preserve original Y direction (flat trajectory)
            return Vector3.Normalize(new Vector3(spreadDirection.X, baseDirection.Y, spreadDirection.Z));
        }

        /// <summary>
        /// Calculate horizontal spread (burst pattern)
        /// </summary>
        private Vector3 CalculateHorizontalSpread(Vector3 baseDirection, float spreadAngle, int index, int total)
        {
            if (total == 1) return baseDirection;

            // Distribute projectiles evenly across horizontal arc
            var angleStep = spreadAngle / (total - 1);
            var currentAngle = -spreadAngle / 2 + (index * angleStep);
            var radians = currentAngle * (Math.PI / 180);

            // Rotate around Y axis (horizontal spread)
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);

            return new Vector3(
                baseDirection.X * cos - baseDirection.Z * sin,
                baseDirection.Y,
                baseDirection.X * sin + baseDirection.Z * cos
            );
        }

        /// <summary>
        /// Calculate circle spread (special pattern)
        /// </summary>
        private Vector3 CalculateCircleSpread(Vector3 baseDirection, float spreadAngle, int index, int total)
        {
            if (total == 1) return baseDirection;

            var angleStep = (2 * Math.PI) / total;
            var currentAngle = index * angleStep;
            var radians = spreadAngle * (Math.PI / 180); // Use spread angle as radius

            var up = Math.Abs(baseDirection.Y) < 0.9f ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0);
            var right = Vector3.Normalize(Vector3.Cross(baseDirection, up));
            var forward = Vector3.Normalize(Vector3.Cross(right, baseDirection));

            var offsetX = (float)(Math.Cos(currentAngle) * Math.Sin(radians));
            var offsetY = (float)(Math.Sin(currentAngle) * Math.Sin(radians));

            return Vector3.Normalize(baseDirection + right * offsetX + forward * offsetY);
        }

        #endregion

        /// <summary>
        /// Handle projectile collision reports from clients
        /// </summary>
        private async Task HandleProjectileHit(WebSocketConnection connection, object? data)
        {
            if (data == null) return;

            var hitData = JsonSerializer.Deserialize<NetworkMessages.ProjectileHitMessage>(data.ToString()!);
            if (hitData == null) return;

            _logger.LogInformation($"Projectile hit report: {hitData.ProjectileId} hit {hitData.TargetType} {hitData.TargetId} at {hitData.HitPosition.X},{hitData.HitPosition.Y},{hitData.HitPosition.Z}");

            // Validate projectile exists and hasn't already hit
            if (!_activeProjectiles.TryGetValue(hitData.ProjectileId, out var projectileState))
            {
                _logger.LogWarning($"Projectile hit report for unknown projectile: {hitData.ProjectileId}");
                return;
            }

            if (projectileState.HasHit)
            {
                _logger.LogWarning($"Duplicate hit report for projectile: {hitData.ProjectileId}");
                return;
            }

            // Validate the hit reporter is the shooter (anti-cheat)
            if (projectileState.ShooterId != connection.PlayerId)
            {
                _logger.LogWarning($"Hit report from wrong player: {connection.PlayerId} vs {projectileState.ShooterId} for projectile {hitData.ProjectileId}");
                return;
            }

            // Validate hit timing (projectile can't hit too quickly)
            var timeSinceLaunch = DateTime.UtcNow - projectileState.LaunchTime;
            if (timeSinceLaunch < TimeSpan.FromMilliseconds(0)) // No minimum travel time - allow all hits
            {
                _logger.LogWarning($"Projectile hit too quickly: {hitData.ProjectileId} hit after {timeSinceLaunch.TotalMilliseconds}ms");
                return;
            }

            // Validate hit distance is reasonable
            var hitDistance = CalculateDistance(projectileState.LaunchPosition, hitData.HitPosition);
            if (hitDistance > projectileState.MaxTravelDistance * 1.2f) // 20% tolerance
            {
                _logger.LogWarning($"Projectile hit beyond max range: {hitData.ProjectileId} traveled {hitDistance} units, max {projectileState.MaxTravelDistance}");
                return;
            }

            // Mark projectile as hit to prevent duplicates
            projectileState.HasHit = true;

            // Calculate and apply damage based on target type
            if (hitData.TargetType == "Enemy" && !string.IsNullOrEmpty(hitData.TargetId))
            {
                await ProcessEnemyProjectileHit(projectileState, hitData);
            }
            else if (hitData.TargetType == "Player" && !string.IsNullOrEmpty(hitData.TargetId))
            {
                await ProcessPlayerProjectileHit(projectileState, hitData);
            }
            else
            {
                // Terrain/obstacle hit - no damage but remove projectile
                _logger.LogInformation($"Projectile {hitData.ProjectileId} hit {hitData.TargetType} at {hitData.HitPosition.X},{hitData.HitPosition.Y},{hitData.HitPosition.Z}");
            }

            // Clean up projectile
            _activeProjectiles.TryRemove(hitData.ProjectileId, out _);
        }

        /// <summary>
        /// Process projectile hit on enemy
        /// </summary>
        private async Task ProcessEnemyProjectileHit(ProjectileState projectile, NetworkMessages.ProjectileHitMessage hitData)
        {
            if (_enemyManager == null)
            {
                _logger.LogError("EnemyManager not available for projectile damage processing");
                return;
            }

            // Calculate damage based on shooter's stats and weapon
            float damage = CalculatePlayerDamage(projectile.ShooterId, "RangedAttack");
            
            // Apply damage to enemy
            bool success = await _enemyManager.DamageEnemy(hitData.TargetId!, damage, projectile.ShooterId);
            
            if (success)
            {
                // Send damage confirmation to all clients
                var confirmationMessage = new NetworkMessages.DamageConfirmationMessage
                {
                    ProjectileId = projectile.ProjectileId,
                    AttackerId = projectile.ShooterId,
                    TargetId = hitData.TargetId!,
                    ActualDamage = damage,
                    DamagePosition = hitData.HitPosition,
                    DamageType = "Projectile",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    IsCritical = false // TODO: Implement critical hit calculation
                };

                await BroadcastToAll("DamageConfirmation", confirmationMessage);
                
                // Handle combat rewards
                await HandleCombatRewards(projectile.ShooterId, hitData.TargetId!, damage);
                
                _logger.LogInformation($"Projectile damage confirmed: {projectile.ProjectileId} dealt {damage} damage to {hitData.TargetId}");
            }
            else
            {
                _logger.LogWarning($"Failed to apply projectile damage: {projectile.ProjectileId} to {hitData.TargetId}");
            }
        }

        /// <summary>
        /// Process projectile hit on player (PvP)
        /// </summary>
        private async Task ProcessPlayerProjectileHit(ProjectileState projectile, NetworkMessages.ProjectileHitMessage hitData)
        {
            // TODO: Implement PvP projectile damage when PvP system is ready
            _logger.LogInformation($"PvP projectile hit: {projectile.ProjectileId} hit player {hitData.TargetId} (PvP not implemented)");
        }

        /// <summary>
        /// Calculate distance between two points
        /// </summary>
        private float CalculateDistance(Vector3Data point1, Vector3Data point2)
        {
            float dx = point2.X - point1.X;
            float dy = point2.Y - point1.Y;
            float dz = point2.Z - point1.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        #endregion

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

            // Broadcast CombatAction for visual effects (this attack has already passed timing validation)
            combatData.AttackerId = connection.PlayerId;
            combatData.Damage = damage;
            await BroadcastToAll("CombatAction", combatData);

            // Phase 2A: HandleEnemyAttack now only handles melee attacks
            // Ranged attacks are routed to HandleRangedAttack() in HandleCombatAction()
            _logger.LogInformation($"[PHASE 2A] HandleEnemyAttack processing melee attack for {damage} damage");

            // Immediate damage for melee attacks (animation already played)
            bool success = await _enemyManager.DamageEnemy(combatData.TargetId, damage, connection.PlayerId);
            // Note: No need to call HandleDamageResult for animation since we already broadcasted
            if (!success)
            {
                _logger.LogWarning($"Failed to damage enemy {combatData.TargetId}");
            }
            else
            {
                // Handle rewards but don't re-broadcast animation
                await HandleCombatRewards(connection.PlayerId, combatData.TargetId, damage);
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
                
                // Use TotalAttackPower instead of just Strength - this includes equipment bonuses!
                float attackPowerBonus = player.TotalAttackPower * 0.8f; // Equipment + Strength scaling (increased from 0.5f)
                float levelBonus = player.Level * 2f; // Level scaling
                
                // Attack type modifiers
                float typeMultiplier = attackType switch
                {
                    "BasicAttack" => 1.0f,
                    "MeleeAttack" => 1.0f,
                    "RangedAttack" => 0.9f, // Ranged attacks do slightly less damage for balance
                    "PowerAttack" => 1.5f,
                    "CriticalStrike" => 2.0f,
                    _ => 1.0f
                };
                
                float totalDamage = (baseDamage + attackPowerBonus + levelBonus) * typeMultiplier;
                
                _logger.LogInformation($"Combat damage calculation for player {playerId}: Base={baseDamage}, AttackPower={attackPowerBonus} (Total ATK: {player.TotalAttackPower} = Base STR {player.Strength} + Equipment {player.EquipmentAttackPower}), Level={levelBonus}, Type={typeMultiplier}x, Final={totalDamage}");
                
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

            // Reject attacks from dead players
            if (_players.TryGetValue(connection.ConnectionId, out var player) && player.Health <= 0)
            {
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
            
            // Attack timing validation based on weapon speed
            if (_players.TryGetValue(connection.ConnectionId, out var playerState))
            {
                _logger.LogInformation($"🕒 Timing validation for player {connection.PlayerId} - checking attack cooldown");
                using var scope = _serviceProvider.CreateScope();
                var attackTimingService = scope.ServiceProvider.GetRequiredService<IAttackTimingService>();
                
                var timingResult = attackTimingService.ValidateAttackTiming(playerState);
                _logger.LogInformation($"🕒 Timing result: IsValid={timingResult.IsValid}, TimeUntilNext={timingResult.TimeUntilNextAttack.TotalMilliseconds:F0}ms");
                if (!timingResult.IsValid)
                {
                    _logger.LogDebug("Attack timing validation failed for player {PlayerId}: {Message} (Remaining: {RemainingMs}ms)", 
                        connection.PlayerId, timingResult.Message, timingResult.TimeUntilNextAttack.TotalMilliseconds);
                    
                    // Send user-friendly system message to chat
                    var remainingSeconds = timingResult.TimeUntilNextAttack.TotalSeconds;
                    string friendlyMessage;
                    
                    if (remainingSeconds > 1.0)
                    {
                        friendlyMessage = $"Weapon not ready yet! ({remainingSeconds:F1}s remaining)";
                    }
                    else if (remainingSeconds > 0.5)
                    {
                        friendlyMessage = "I'm not ready to attack!";
                    }
                    else
                    {
                        friendlyMessage = "Too soon! Let me prepare...";
                    }
                    
                    _ = Task.Run(async () =>
                    {
                        await SendSystemMessage(connection.ConnectionId, friendlyMessage);
                    });
                    
                    return false;
                }
                
                // Record this attack attempt for future timing validation
                attackTimingService.RecordAttack(playerState);
            }
            else
            {
                _logger.LogWarning("⚠️ Could not find player state for timing validation: {PlayerId} - ALLOWING ATTACK BY DEFAULT", connection.PlayerId);
            }
            
            return true;
        }

        private async Task DelayedDamageApplication(float travelTime, NetworkMessages.CombatActionMessage combatData, float damage, WebSocketConnection connection)
        {
            await Task.Delay(TimeSpan.FromSeconds(travelTime));
            bool success = await _enemyManager.DamageEnemy(combatData.TargetId, damage, connection.PlayerId);
            // Note: Animation already played immediately when attack was validated
            if (!success)
            {
                _logger.LogWarning($"Failed to damage enemy {combatData.TargetId} after projectile travel");
            }
            else
            {
                // Handle rewards but don't re-broadcast animation
                await HandleCombatRewards(connection.PlayerId, combatData.TargetId, damage);
            }
        }

        private float CalculateProjectileTravelTime(WebSocketConnection connection, NetworkMessages.CombatActionMessage combatData, EquippedItem weapon)
        {
            try
            {
                // Get player position (use stored player state or approximate from connection)
                float playerX = 0, playerY = 0, playerZ = 0; // Default fallback
                if (_players.TryGetValue(connection.ConnectionId, out var playerState) && playerState != null)
                {
                    playerX = playerState.Position.X;
                    playerY = playerState.Position.Y;
                    playerZ = playerState.Position.Z;
                }

                // Calculate distance to target
                float targetX = combatData.Position.X;
                float targetY = combatData.Position.Y;
                float targetZ = combatData.Position.Z;
                
                float deltaX = targetX - playerX;
                float deltaY = targetY - playerY;
                float deltaZ = targetZ - playerZ;
                float distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);

                // Calculate travel time based on projectile speed
                float projectileSpeed = weapon.ProjectileSpeed > 0 ? weapon.ProjectileSpeed : 20f; // Default speed
                float travelTime = distance / projectileSpeed;

                // Cap maximum travel time to prevent issues
                float maxTravelTime = weapon.WeaponRange / projectileSpeed;
                travelTime = Math.Min(travelTime, maxTravelTime);

                _logger.LogInformation($"Projectile travel: Distance={distance:F2}, Speed={projectileSpeed}, Time={travelTime:F2}s");
                return travelTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating projectile travel time, using default");
                return 1.0f; // Default 1 second if calculation fails
            }
        }

        private async Task HandleDamageResult(bool success, NetworkMessages.CombatActionMessage combatData, WebSocketConnection connection)
        {
            if (success)
            {
                // Update combat data with calculated damage
                combatData.AttackerId = connection.PlayerId;
                
                // Broadcast attack effects to all clients
                await BroadcastToAll("CombatAction", combatData);
                
                // Handle potential rewards (experience, loot)
                await HandleCombatRewards(connection.PlayerId, combatData.TargetId, combatData.Damage);
            }
            else
            {
                _logger.LogWarning($"Failed to damage enemy {combatData.TargetId}");
            }
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
                    PlayerId = authData.PlayerId,  // Use actual player ID from authentication data
                    PlayerName = playerStats.PlayerName,
                    Position = playerStats.LastPosition ?? new Vector3Data(0, 1, 0),
                    Health = playerStats.Health,
                    MaxHealth = playerStats.MaxHealth,
                    Level = playerStats.Level,
                    Experience = playerStats.Experience,
                    Strength = playerStats.Strength,
                    Defense = playerStats.Defense,
                    Speed = playerStats.Speed,
                    Gold = playerStats.Gold,
                    IsOnline = true,
                    LastUpdate = DateTime.UtcNow
                };

                // Populate equipment stats from database
                await PopulateEquipmentStatsAsync(player);

                _players.TryAdd(connection.ConnectionId, player);
                connection.PlayerId = authData.PlayerId;  // Use actual player ID from authentication data
                
                // Set connection.LastPosition for AI system access
                connection.LastPosition = player.Position;
                connection.PlayerName = player.PlayerName;
                
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
                        var oldHealth = player.Health;
                        player.Health = healthData.NewHealth;
                    }

                    _logger.LogInformation($"Player {healthData.PlayerId} health persisted by {healthData.HealthChange} to {healthData.NewHealth} from {healthData.Source}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling health change for player {healthData.PlayerId}");
            }
        }

        private async Task HandleRespawnRequest(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var respawnData = JsonSerializer.Deserialize<NetworkMessages.RespawnRequestMessage>(data.ToString()!);
            if (respawnData == null) return;

            _logger.LogInformation($"Respawn request for player: {respawnData.PlayerId}");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var playerStatsRepository = scope.ServiceProvider.GetRequiredService<IPlayerStatsRepository>();

                // Get current player stats
                var player = await playerStatsRepository.GetPlayerStatsAsync(respawnData.PlayerId);
                if (player == null)
                {
                    await SendToConnection(connection.ConnectionId, "RespawnResponse", new NetworkMessages.RespawnResponseMessage
                    {
                        Success = false,
                        PlayerId = respawnData.PlayerId,
                        ErrorMessage = "Player not found"
                    });
                    return;
                }

                // Check if player is actually dead
                if (player.Health > 0)
                {
                    await SendToConnection(connection.ConnectionId, "RespawnResponse", new NetworkMessages.RespawnResponseMessage
                    {
                        Success = false,
                        PlayerId = respawnData.PlayerId,
                        ErrorMessage = "Player is not dead"
                    });
                    return;
                }

                // Respawn player with full health
                await playerStatsRepository.UpdatePlayerHealthAsync(respawnData.PlayerId, player.MaxHealth);

                // Update in-memory player state
                if (_players.TryGetValue(connection.ConnectionId, out var playerState))
                {
                    playerState.Health = player.MaxHealth;
                }

                // Send successful respawn response
                await SendToConnection(connection.ConnectionId, "RespawnResponse", new NetworkMessages.RespawnResponseMessage
                {
                    Success = true,
                    PlayerId = respawnData.PlayerId,
                    NewHealth = player.MaxHealth
                });

                // Send health change notification to update UI
                await SendToConnection(connection.ConnectionId, "HealthChange", new NetworkMessages.HealthChangeMessage
                {
                    PlayerId = respawnData.PlayerId,
                    NewHealth = player.MaxHealth,
                    HealthChange = player.MaxHealth - 0, // Full heal from 0
                    Source = "Respawn"
                });

                _logger.LogInformation($"Player {respawnData.PlayerId} respawned successfully with {player.MaxHealth} health");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling respawn request for player {respawnData.PlayerId}");
                
                await SendToConnection(connection.ConnectionId, "RespawnResponse", new NetworkMessages.RespawnResponseMessage
                {
                    Success = false,
                    PlayerId = respawnData.PlayerId,
                    ErrorMessage = "Server error during respawn"
                });
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
                    var actualPlayerId = result.PlayerId ?? result.PlayerStats.PlayerId;
                    var player = new PlayerState
                    {
                        PlayerId = actualPlayerId,  // Use actual player ID from database, not connection ID
                        PlayerName = result.PlayerStats.PlayerName,
                        Position = result.PlayerStats.LastPosition ?? new Vector3Data(0, 1, 0),
                        Health = result.PlayerStats.Health,
                        MaxHealth = result.PlayerStats.MaxHealth,
                        Level = result.PlayerStats.Level,
                        Experience = result.PlayerStats.Experience,
                        Strength = result.PlayerStats.Strength,
                        Defense = result.PlayerStats.Defense,
                        Speed = result.PlayerStats.Speed,
                        Gold = result.PlayerStats.Gold,
                        IsOnline = true,
                        LastUpdate = DateTime.UtcNow
                    };

                    // Populate equipment stats from database
                    await PopulateEquipmentStatsAsync(player);

                    _players.TryAdd(connection.ConnectionId, player);
                    connection.PlayerId = actualPlayerId;  // Set connection PlayerId to actual database player ID
                    
                    // Set connection.LastPosition for AI system access
                    connection.LastPosition = player.Position;
                    connection.PlayerName = player.PlayerName;
                    
                    _logger.LogInformation($"Login Success: Connection {connection.ConnectionId} mapped to Player {connection.PlayerId} ({player.PlayerName})");
                
                // TEMPORARY: Auto-reset to Level 1 for testing - RE-ENABLED for death system testing
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
                            ExperienceToNextLevel = result.PlayerStats.ExperienceToNextLevel,
                            Gold = result.PlayerStats.Gold
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

                    // Cache PlayerState for AI system to avoid database queries on every attack
                    var playerState = new PlayerState
                    {
                        PlayerId = player.PlayerId,
                        PlayerName = player.PlayerName,
                        Position = player.Position,
                        Health = result.PlayerStats.Health,  // Use ACTUAL database health, not reset value
                        MaxHealth = result.PlayerStats.MaxHealth,
                        Level = result.PlayerStats.Level,
                        Strength = result.PlayerStats.Strength,
                        Defense = result.PlayerStats.Defense,
                        Speed = result.PlayerStats.Speed,
                        Gold = result.PlayerStats.Gold,
                        Experience = result.PlayerStats.Experience,
                        IsOnline = true,
                        LastUpdate = DateTime.UtcNow
                    };

                    // CRITICAL FIX: Populate equipment stats before caching to avoid overwriting
                    await PopulateEquipmentStatsAsync(playerState);
                    
                    _players[connection.ConnectionId] = playerState;
                    
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
                        Gold = result.PlayerStats.Gold,
                        IsOnline = true,
                        LastUpdate = DateTime.UtcNow
                    };

                    // Populate equipment stats from database
                    await PopulateEquipmentStatsAsync(player);

                    _players.TryAdd(connection.ConnectionId, player);
                    connection.PlayerId = player.PlayerId;
                    
                    // Set connection.LastPosition for AI system access
                    connection.LastPosition = player.Position;
                    connection.PlayerName = player.PlayerName;

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

        /// <summary>
        /// Send a system message to a specific connection's chat
        /// </summary>
        private Task SendSystemMessage(string connectionId, string message)
        {
            var systemMessage = new NetworkMessages.ChatMessage
            {
                SenderId = "SYSTEM",
                SenderName = "System",
                Message = message,
                ChannelType = "System",
                TargetId = null,
                Timestamp = DateTime.UtcNow
            };

            return SendToConnection(connectionId, "ChatMessage", systemMessage);
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

        /// <summary>
        /// Get cached player state by player ID for diagnostics
        /// </summary>
        public PlayerState? GetCachedPlayerByPlayerId(string playerId)
        {
            var connectionEntry = _connections.FirstOrDefault(kvp => kvp.Value.PlayerId == playerId);
            if (connectionEntry.Value != null && _players.TryGetValue(connectionEntry.Key, out var cachedPlayerState))
            {
                return cachedPlayerState;
            }
            return null;
        }
        
        /// <summary>
        /// Update player health in all in-memory caches (for AI consistency)
        /// NOTE: This is now redundant since AI modifies shared cached objects directly
        /// </summary>
        public async Task UpdatePlayerHealthInMemory(string playerId, int newHealth)
        {
            // Since AI now works with shared cached objects, this update is redundant
            // but we'll keep it for logging and potential future use
            var playerToUpdate = _players.Values.FirstOrDefault(p => p.PlayerId == playerId);
            if (playerToUpdate != null)
            {
                // Health should already be updated by AI, but verify
                if (playerToUpdate.Health != newHealth)
                {
                    _logger.LogWarning($"Health mismatch for player {playerId}: cache={playerToUpdate.Health}, expected={newHealth}");
                    playerToUpdate.Health = newHealth;
                }
                _logger.LogDebug($"Verified in-memory health for player {playerId}: {newHealth}");
            }
            
            await Task.CompletedTask; // Method signature requires async
        }
        
        /// <summary>
        /// Get active players for AI context using cached in-memory data
        /// Returns the ACTUAL cached objects, not copies, so AI modifies the shared instances
        /// </summary>
        public async Task<List<PlayerState>> GetActivePlayersForAI()
        {
            var activePlayers = new List<PlayerState>();
            
            foreach (var connection in _connections.Values)
            {
                if (!string.IsNullOrEmpty(connection.PlayerId))
                {
                    // Try to get from in-memory cache first
                    if (_players.TryGetValue(connection.ConnectionId, out var cachedPlayer))
                    {
                        // Update position from latest connection data
                        cachedPlayer.Position = connection.LastPosition ?? new Vector3Data();
                        cachedPlayer.LastUpdate = DateTime.UtcNow;
                        
                        // DEBUG: Log health value from cache
                        // Too frequent for logging
                        // Console.WriteLine($"[DEBUG] GetActivePlayersForAI: Returning cached player {cachedPlayer.PlayerId} with Health={cachedPlayer.Health}");
                        
                        // Return the ACTUAL cached object, not a copy
                        activePlayers.Add(cachedPlayer);
                    }
                    else
                    {
                        // Fallback to database if not in cache
                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();
                            var playerStats = await playerStatsService.GetPlayerStatsAsync(connection.PlayerId);
                            
                            if (playerStats != null)
                            {
                                var playerState = new PlayerState
                                {
                                    PlayerId = connection.PlayerId,
                                    PlayerName = connection.PlayerName ?? "Unknown",
                                    Position = connection.LastPosition ?? new Vector3Data(),
                                    Health = playerStats.Health,
                                    MaxHealth = playerStats.MaxHealth,
                                    Level = playerStats.Level,
                                    Strength = playerStats.Strength,
                                    Defense = playerStats.Defense,
                                    Speed = playerStats.Speed,
                                    Gold = playerStats.Gold,
                                    Experience = playerStats.Experience,
                                    IsOnline = true,
                                    LastUpdate = DateTime.UtcNow
                                };
                                
                                // CRITICAL FIX: Populate equipment stats for complete PlayerState
                                try
                                {
                                    var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                                    var (attackPower, defensePower, attackSpeed) = await equipmentManager.CalculateEquipmentStatsAsync(connection.PlayerId);
                                    playerState.EquipmentAttackPower = attackPower;
                                    playerState.EquipmentDefensePower = defensePower;
                                    playerState.EquipmentAttackSpeed = attackSpeed;
                                    
                                }
                                catch (Exception equipEx)
                                {
                                    _logger.LogError(equipEx, $"Failed to populate equipment stats for player {connection.PlayerId} during cache miss");
                                    // Leave equipment stats at 0 on error
                                }
                                
                                activePlayers.Add(playerState);
                                
                                // Cache it for next time with complete equipment stats
                                _players[connection.ConnectionId] = playerState;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to get player stats for {connection.PlayerId}");
                        }
                    }
                }
            }
            
            return activePlayers;
        }
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

        private async Task HandleEquipmentRequest(WebSocketConnection connection, object data)
        {
            try
            {
                _logger.LogInformation($"Handling equipment request for connection: {connection.ConnectionId}");

                if (string.IsNullOrEmpty(connection.PlayerId))
                {
                    _logger.LogWarning($"Equipment request from unauthenticated connection: {connection.ConnectionId}");
                    await SendToConnection(connection.ConnectionId, "EquipmentResponse", new NetworkMessages.EquipmentResponseMessage
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated"
                    });
                    return;
                }

                // Get player's equipment from database
                using var scope = _serviceProvider.CreateScope();
                var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                
                var equippedItems = await equipmentManager.GetPlayerEquipmentAsync(connection.PlayerId);
                
                // Get equipment stats from cached PlayerState (more efficient than recalculating)
                int totalAttackPower = 0;
                int totalDefensePower = 0;
                
                if (_players.TryGetValue(connection.ConnectionId, out var cachedPlayer))
                {
                    totalAttackPower = cachedPlayer.TotalAttackPower;
                    totalDefensePower = cachedPlayer.TotalDefensePower;
                    _logger.LogDebug("Using cached equipment stats for player {PlayerId}: ATK {Attack}, DEF {Defense}", 
                        connection.PlayerId, totalAttackPower, totalDefensePower);
                }
                else
                {
                    // Fallback: calculate fresh from database if not cached
                    var (equipmentAttackPower, equipmentDefensePower, _) = await equipmentManager.CalculateEquipmentStatsAsync(connection.PlayerId);
                    totalAttackPower = equipmentAttackPower;
                    totalDefensePower = equipmentDefensePower;
                    _logger.LogWarning("Player {PlayerId} not found in cache, calculated equipment stats fresh from database", connection.PlayerId);
                }

                // Send equipment response
                await SendToConnection(connection.ConnectionId, "EquipmentResponse", new NetworkMessages.EquipmentResponseMessage
                {
                    PlayerId = connection.PlayerId,
                    Items = equippedItems,
                    Success = true,
                    TotalAttackPower = totalAttackPower,
                    TotalDefensePower = totalDefensePower
                });

                _logger.LogInformation($"Sent equipment response to player {connection.PlayerId} with {equippedItems.Count} items");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling equipment request");
                await SendToConnection(connection.ConnectionId, "EquipmentResponse", new NetworkMessages.EquipmentResponseMessage
                {
                    Success = false,
                    ErrorMessage = "Server error"
                });
            }
        }

        private async Task HandleItemEquipRequest(WebSocketConnection connection, object data)
        {
            try
            {
                _logger.LogInformation($"Handling item equip request for connection: {connection.ConnectionId}");

                if (string.IsNullOrEmpty(connection.PlayerId))
                {
                    _logger.LogWarning($"Equip request from unauthenticated connection: {connection.ConnectionId}");
                    await SendToConnection(connection.ConnectionId, "ItemEquipResponse", new NetworkMessages.ItemEquipResponseMessage
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated"
                    });
                    return;
                }


                var equipRequest = JsonSerializer.Deserialize<NetworkMessages.ItemEquipRequestMessage>(
                    JsonSerializer.Serialize(data));

                if (equipRequest == null)
                {
                    await SendToConnection(connection.ConnectionId, "ItemEquipResponse", new NetworkMessages.ItemEquipResponseMessage
                    {
                        Success = false,
                        ErrorMessage = "Invalid request format"
                    });
                    return;
                }


                // Process equip request using EquipmentManager
                using var scope = _serviceProvider.CreateScope();
                var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                
                var result = await equipmentManager.EquipItemAsync(connection.PlayerId, equipRequest.SlotIndex, equipRequest.SlotType);

                // Send equip response
                await SendToConnection(connection.ConnectionId, "ItemEquipResponse", new NetworkMessages.ItemEquipResponseMessage
                {
                    PlayerId = connection.PlayerId,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                    EquippedItem = result.EquippedItem,
                    UnequippedItem = result.ReplacedItem
                });

                if (result.Success)
                {
                    _logger.LogInformation($"Player {connection.PlayerId} equipped item to {equipRequest.SlotType} slot");
                    
                    // Update cached equipment stats in PlayerState
                    await UpdateCachedEquipmentStatsAsync(connection.PlayerId);
                    
                    // Send inventory update to refresh client inventory
                    await RefreshPlayerInventory(connection.PlayerId);
                    
                    // Send equipment update to refresh client equipment
                    await RefreshPlayerEquipment(connection.PlayerId);
                    
                    // Send weapon timing info if this was a weapon equip
                    if (equipRequest.SlotType.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendWeaponTimingInfo(connection.ConnectionId, connection.PlayerId);
                    }
                }
                else
                {
                    _logger.LogWarning($"Failed to equip item for player {connection.PlayerId}: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item equip request");
                await SendToConnection(connection.ConnectionId, "ItemEquipResponse", new NetworkMessages.ItemEquipResponseMessage
                {
                    Success = false,
                    ErrorMessage = "Server error"
                });
            }
        }

        private async Task HandleItemUnequipRequest(WebSocketConnection connection, object data)
        {
            try
            {
                _logger.LogInformation($"Handling item unequip request for connection: {connection.ConnectionId}");

                if (string.IsNullOrEmpty(connection.PlayerId))
                {
                    _logger.LogWarning($"Unequip request from unauthenticated connection: {connection.ConnectionId}");
                    await SendToConnection(connection.ConnectionId, "ItemUnequipResponse", new NetworkMessages.ItemUnequipResponseMessage
                    {
                        Success = false,
                        ErrorMessage = "Not authenticated"
                    });
                    return;
                }

                var unequipRequest = JsonSerializer.Deserialize<NetworkMessages.ItemUnequipRequestMessage>(
                    JsonSerializer.Serialize(data));

                if (unequipRequest == null)
                {
                    await SendToConnection(connection.ConnectionId, "ItemUnequipResponse", new NetworkMessages.ItemUnequipResponseMessage
                    {
                        Success = false,
                        ErrorMessage = "Invalid request format"
                    });
                    return;
                }

                // Process unequip request using EquipmentManager
                using var scope = _serviceProvider.CreateScope();
                var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                
                var result = await equipmentManager.UnequipItemAsync(connection.PlayerId, unequipRequest.SlotType);

                // Send unequip response
                await SendToConnection(connection.ConnectionId, "ItemUnequipResponse", new NetworkMessages.ItemUnequipResponseMessage
                {
                    PlayerId = connection.PlayerId,
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                    UnequippedItem = result.UnequippedItem
                });

                if (result.Success)
                {
                    _logger.LogInformation($"Player {connection.PlayerId} unequipped item from {unequipRequest.SlotType} slot");
                    
                    // Update cached equipment stats in PlayerState
                    await UpdateCachedEquipmentStatsAsync(connection.PlayerId);
                    
                    // Send inventory update to refresh client inventory
                    await RefreshPlayerInventory(connection.PlayerId);
                    
                    // Send equipment update to refresh client equipment
                    await RefreshPlayerEquipment(connection.PlayerId);
                    
                    // Send weapon timing info if this was a weapon unequip (will send default timing)
                    if (unequipRequest.SlotType.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
                    {
                        await SendWeaponTimingInfo(connection.ConnectionId, connection.PlayerId);
                    }
                }
                else
                {
                    _logger.LogWarning($"Failed to unequip item for player {connection.PlayerId}: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item unequip request");
                await SendToConnection(connection.ConnectionId, "ItemUnequipResponse", new NetworkMessages.ItemUnequipResponseMessage
                {
                    Success = false,
                    ErrorMessage = "Server error"
                });
            }
        }

        // Helper methods for refreshing client data
        private async Task RefreshPlayerInventory(string playerId)
        {
            try
            {
                var connectionId = GetConnectionIdByPlayerId(playerId);
                if (connectionId != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var inventoryRepository = scope.ServiceProvider.GetRequiredService<IPlayerInventoryRepository>();
                    var inventoryItems = await inventoryRepository.GetPlayerInventoryAsync(playerId);

                    await SendToConnection(connectionId, "InventoryUpdate", new NetworkMessages.InventoryUpdateMessage
                    {
                        PlayerId = playerId,
                        UpdatedItems = inventoryItems,
                        UpdateType = "Refresh"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing inventory for player {PlayerId}", playerId);
            }
        }

        private async Task RefreshPlayerEquipment(string playerId)
        {
            try
            {
                var connectionId = GetConnectionIdByPlayerId(playerId);
                if (connectionId != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                    var equippedItems = await equipmentManager.GetPlayerEquipmentAsync(playerId);

                    // Get updated stats from cached PlayerState
                    int totalAttackPower = 0;
                    int totalDefensePower = 0;
                    
                    if (_players.TryGetValue(connectionId, out var cachedPlayer))
                    {
                        totalAttackPower = cachedPlayer.TotalAttackPower;
                        totalDefensePower = cachedPlayer.TotalDefensePower;
                    }

                    await SendToConnection(connectionId, "EquipmentUpdate", new NetworkMessages.EquipmentUpdateMessage
                    {
                        PlayerId = playerId,
                        UpdatedItems = equippedItems,
                        UpdateType = "Refresh",
                        TotalAttackPower = totalAttackPower,
                        TotalDefensePower = totalDefensePower
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing equipment for player {PlayerId}", playerId);
            }
        }

        private string? GetConnectionIdByPlayerId(string playerId)
        {
            return _connections.FirstOrDefault(kvp => kvp.Value.PlayerId == playerId).Key;
        }

        private async Task HandleItemUseRequest(WebSocketConnection connection, object data)
        {
            try
            {
                _logger.LogInformation($"Handling item use request for connection: {connection.ConnectionId}");

                if (string.IsNullOrEmpty(connection.PlayerId))
                {
                    _logger.LogWarning($"Item use request from unauthenticated connection: {connection.ConnectionId}");
                    await SendToConnection(connection.ConnectionId, "ItemUseResponse", new NetworkMessages.ItemUseResponseMessage
                    {
                        Success = false,
                        Message = "Not authenticated"
                    });
                    return;
                }

                var useRequest = JsonSerializer.Deserialize<NetworkMessages.ItemUseRequestMessage>(data.ToString()!);
                if (useRequest == null)
                {
                    await SendToConnection(connection.ConnectionId, "ItemUseResponse", new NetworkMessages.ItemUseResponseMessage
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                    return;
                }

                _logger.LogInformation($"Player {connection.PlayerId} attempting to use item {useRequest.ItemType} from slot {useRequest.SlotIndex}");

                // Get player's inventory to verify item exists
                using var scope = _serviceProvider.CreateScope();
                var inventoryRepository = scope.ServiceProvider.GetRequiredService<IPlayerInventoryRepository>();
                
                var item = await inventoryRepository.GetItemInSlotAsync(connection.PlayerId, useRequest.SlotIndex);
                
                if (item == null || item.ItemType != useRequest.ItemType)
                {
                    await SendToConnection(connection.ConnectionId, "ItemUseResponse", new NetworkMessages.ItemUseResponseMessage
                    {
                        Success = false,
                        Message = "Item not found in specified slot"
                    });
                    return;
                }

                // Apply item effects based on item type
                bool success = await ApplyItemEffects(connection.PlayerId, item);
                
                if (success)
                {
                    // Reduce item quantity or remove item
                    int remainingQuantity = item.Quantity - 1;
                    
                    if (remainingQuantity <= 0)
                    {
                        await inventoryRepository.RemoveItemFromInventoryAsync(connection.PlayerId, useRequest.SlotIndex);
                        remainingQuantity = 0;
                    }
                    else
                    {
                        await inventoryRepository.UpdateItemQuantityAsync(connection.PlayerId, useRequest.ItemType, remainingQuantity);
                    }

                    await SendToConnection(connection.ConnectionId, "ItemUseResponse", new NetworkMessages.ItemUseResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        Success = true,
                        Message = $"Used {item.ItemName}",
                        ItemType = useRequest.ItemType,
                        RemainingQuantity = remainingQuantity
                    });

                    _logger.LogInformation($"Player {connection.PlayerId} successfully used {item.ItemName}. Remaining quantity: {remainingQuantity}");
                    
                    // Send inventory update to refresh client display
                    var inventoryItems = await inventoryRepository.GetPlayerInventoryAsync(connection.PlayerId);
                    await SendToConnection(connection.ConnectionId, "InventoryResponse", new NetworkMessages.InventoryResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        Items = inventoryItems,
                        Success = true
                    });
                }
                else
                {
                    await SendToConnection(connection.ConnectionId, "ItemUseResponse", new NetworkMessages.ItemUseResponseMessage
                    {
                        Success = false,
                        Message = $"Failed to use {item.ItemName}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item use request");
                await SendToConnection(connection.ConnectionId, "ItemUseResponse", new NetworkMessages.ItemUseResponseMessage
                {
                    Success = false,
                    Message = "Server error"
                });
            }
        }

        private async Task HandleItemSellRequest(WebSocketConnection connection, object data)
        {
            try
            {
                _logger.LogInformation($"Handling item sell request for connection: {connection.ConnectionId}");

                if (string.IsNullOrEmpty(connection.PlayerId))
                {
                    _logger.LogWarning($"Item sell request from unauthenticated connection: {connection.ConnectionId}");
                    await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                    {
                        Success = false,
                        Message = "Not authenticated"
                    });
                    return;
                }

                // Parse the sell request
                var sellRequestJson = data?.ToString();
                if (string.IsNullOrEmpty(sellRequestJson))
                {
                    _logger.LogWarning($"Empty sell request data from {connection.PlayerId}");
                    await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        Success = false,
                        Message = "Invalid request data"
                    });
                    return;
                }

                var sellRequest = JsonSerializer.Deserialize<NetworkMessages.ItemSellRequestMessage>(sellRequestJson);
                if (sellRequest == null)
                {
                    _logger.LogWarning($"Failed to parse sell request from {connection.PlayerId}");
                    await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        Success = false,
                        Message = "Invalid request format"
                    });
                    return;
                }

                _logger.LogInformation($"Processing sell request for player {connection.PlayerId} - Item: {sellRequest.ItemType}, Slot: {sellRequest.SlotIndex}, Quantity: {sellRequest.Quantity}");

                // Get player's inventory to find the item
                using var scope = _serviceProvider.CreateScope();
                var inventoryRepository = scope.ServiceProvider.GetRequiredService<IPlayerInventoryRepository>();
                
                var itemInSlot = await inventoryRepository.GetItemInSlotAsync(connection.PlayerId, sellRequest.SlotIndex);
                if (itemInSlot == null)
                {
                    _logger.LogWarning($"No item found in slot {sellRequest.SlotIndex} for player {connection.PlayerId}");
                    await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        Success = false,
                        Message = "No item in that slot",
                        ItemType = sellRequest.ItemType
                    });
                    return;
                }

                // Verify the item type matches
                if (itemInSlot.ItemType != sellRequest.ItemType)
                {
                    _logger.LogWarning($"Item type mismatch for player {connection.PlayerId} - Expected: {sellRequest.ItemType}, Found: {itemInSlot.ItemType}");
                    await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        Success = false,
                        Message = "Item type mismatch",
                        ItemType = sellRequest.ItemType
                    });
                    return;
                }

                // Calculate sell price (half of item value, minimum 1 gold)
                int sellPrice = Math.Max(itemInSlot.Value / 2, 1);
                int quantityToSell = Math.Min(sellRequest.Quantity, itemInSlot.Quantity);
                int totalGoldEarned = sellPrice * quantityToSell;

                _logger.LogInformation($"Selling {quantityToSell}x {itemInSlot.ItemName} for {sellPrice}g each (total: {totalGoldEarned}g) for player {connection.PlayerId}");

                // Handle quantity reduction or item removal
                int remainingQuantity = itemInSlot.Quantity - quantityToSell;
                bool success = false;

                if (remainingQuantity <= 0)
                {
                    // Remove item completely
                    success = await inventoryRepository.RemoveItemFromInventoryAsync(connection.PlayerId, sellRequest.SlotIndex);
                    remainingQuantity = 0;
                }
                else
                {
                    // Update quantity
                    success = await inventoryRepository.UpdateItemQuantityAsync(connection.PlayerId, itemInSlot.ItemType, remainingQuantity);
                }

                if (!success)
                {
                    _logger.LogError($"Failed to update inventory after sell for player {connection.PlayerId}");
                    await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                    {
                        PlayerId = connection.PlayerId,
                        Success = false,
                        Message = "Failed to update inventory",
                        ItemType = sellRequest.ItemType
                    });
                    return;
                }

                // Add gold to player (thread-safe cache + database update)
                using var playerStatsScope = _serviceProvider.CreateScope();
                var playerStatsRepository = playerStatsScope.ServiceProvider.GetRequiredService<IPlayerStatsRepository>();
                
                // Update PlayerState cache first
                if (_players.TryGetValue(connection.ConnectionId, out var player))
                {
                    player.Gold += totalGoldEarned;
                    _logger.LogDebug("Updated cached gold for player {PlayerId}: {Gold}", connection.PlayerId, player.Gold);
                }
                
                // Update database atomically
                await playerStatsRepository.AddGoldAsync(connection.PlayerId, totalGoldEarned);
                _logger.LogDebug("Added {GoldAmount} gold to database for player {PlayerId}", totalGoldEarned, connection.PlayerId);

                // Send successful response with current gold
                int currentGold = _players.TryGetValue(connection.ConnectionId, out var playerForGold) ? playerForGold.Gold : 0;
                await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                {
                    PlayerId = connection.PlayerId,
                    Success = true,
                    Message = $"Sold {quantityToSell}x {itemInSlot.ItemName} for {totalGoldEarned} gold",
                    ItemType = sellRequest.ItemType,
                    GoldEarned = totalGoldEarned,
                    CurrentGold = currentGold,
                    RemainingQuantity = remainingQuantity
                });

                // Send updated inventory to refresh client display
                var updatedInventory = await inventoryRepository.GetPlayerInventoryAsync(connection.PlayerId);
                await SendToConnection(connection.ConnectionId, "InventoryUpdate", new NetworkMessages.InventoryUpdateMessage
                {
                    PlayerId = connection.PlayerId,
                    UpdatedItems = updatedInventory,
                    UpdateType = "Refresh"
                });

                _logger.LogInformation($"Successfully processed sell request for player {connection.PlayerId} - Earned {totalGoldEarned} gold");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item sell request");
                await SendToConnection(connection.ConnectionId, "ItemSellResponse", new NetworkMessages.ItemSellResponseMessage
                {
                    Success = false,
                    Message = "Server error"
                });
            }
        }

        /// <summary>
        /// Apply effects when a player uses an item
        /// </summary>
        private async Task<bool> ApplyItemEffects(string playerId, InventoryItem item)
        {
            try
            {
                _logger.LogInformation($"Applying effects for item: {item.ItemType} to player: {playerId}");

                // Check if item is consumable
                if (string.IsNullOrEmpty(item.ItemCategory) || 
                    (!item.ItemCategory.Equals("Consumable", StringComparison.OrdinalIgnoreCase) &&
                     !item.ItemCategory.Equals("Medical", StringComparison.OrdinalIgnoreCase) &&
                     !item.ItemCategory.Equals("Food", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning($"Item {item.ItemType} is not consumable. Category: {item.ItemCategory}");
                    return false;
                }

                // Apply effects based on item type
                switch (item.ItemType.ToLower())
                {
                    case "health_potion":
                    case "common_health_potion":
                        return await ApplyHealthPotion(playerId, 50); // Heal 50 HP
                    
                    case "bandage":
                    case "common_bandage":
                        return await ApplyHealthPotion(playerId, 25); // Heal 25 HP (less than potion)
                    
                    case "bread":
                    case "common_bread":
                        return await ApplyHealthPotion(playerId, 10); // Heal 10 HP (basic food)
                    
                    case "uncommon_magic_potion":
                        return await ApplyMagicPotion(playerId, 25); // Restore 25 mana (placeholder)
                    
                    default:
                        _logger.LogWarning($"Unknown consumable item type: {item.ItemType}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error applying item effects for {item.ItemType}");
                return false;
            }
        }

        /// <summary>
        /// Apply health potion effect using cached player data (follows combat system pattern)
        /// </summary>
        private async Task<bool> ApplyHealthPotion(string playerId, int healAmount)
        {
            try
            {
                // Find player in cached memory first (same pattern as combat)
                var playerToHeal = _players.Values.FirstOrDefault(p => p.PlayerId == playerId);
                if (playerToHeal == null)
                {
                    _logger.LogWarning($"Player {playerId} not found in cached memory for health potion");
                    return false;
                }

                // Get max health from database only once (could be cached in future)
                using var scope = _serviceProvider.CreateScope();
                var playerStatsRepository = scope.ServiceProvider.GetRequiredService<IPlayerStatsRepository>();
                var playerStats = await playerStatsRepository.GetPlayerStatsAsync(playerId);
                if (playerStats == null)
                {
                    _logger.LogWarning($"Player stats not found for {playerId}");
                    return false;
                }

                // Calculate new health using cached current health (don't exceed max health)
                int currentHealth = playerToHeal.Health;
                int newHealth = Math.Min(currentHealth + healAmount, playerStats.MaxHealth);
                int actualHealAmount = newHealth - currentHealth;

                if (actualHealAmount <= 0)
                {
                    _logger.LogInformation($"Player {playerId} already at full health ({currentHealth}/{playerStats.MaxHealth})");
                    return false; // Already at full health
                }

                // Update cached player health immediately (same as combat)
                playerToHeal.Health = newHealth;

                // Persist to database asynchronously (same pattern as combat)
                await playerStatsRepository.UpdatePlayerHealthAsync(playerId, newHealth);

                // Create and send health change message (same pattern as combat)
                var healthChangeMessage = new NetworkMessages.HealthChangeMessage
                {
                    PlayerId = playerId,
                    NewHealth = newHealth,
                    HealthChange = actualHealAmount, // Positive for healing
                    Source = "Health Potion"
                };

                // Send to the specific player (not broadcast to all like combat damage)
                var connectionEntry = _connections.FirstOrDefault(kvp => kvp.Value.PlayerId == playerId);
                if (connectionEntry.Value != null)
                {
                    await SendToConnection(connectionEntry.Key, "HealthChange", healthChangeMessage);
                }

                _logger.LogInformation($"Player {playerId} healed for {actualHealAmount} HP using cached data (new health: {newHealth})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error applying health potion to player {playerId}");
                return false;
            }
        }

        /// <summary>
        /// Helper method to populate equipment stats in PlayerState during login/authentication
        /// </summary>
        private async Task PopulateEquipmentStatsAsync(PlayerState playerState)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                
                // Calculate equipment stats from database
                var (equipmentAttackPower, equipmentDefensePower, equipmentAttackSpeed) = await equipmentManager.CalculateEquipmentStatsAsync(playerState.PlayerId);
                
                // Populate equipment stats in PlayerState
                playerState.EquipmentAttackPower = equipmentAttackPower;
                playerState.EquipmentDefensePower = equipmentDefensePower;
                playerState.EquipmentAttackSpeed = equipmentAttackSpeed;
                
                _logger.LogInformation("Populated equipment stats for player {PlayerId} during login: ATK +{Attack}, DEF +{Defense}, SPD {Speed} (Base: STR {Strength}, DEF {Defense} | Total: ATK {TotalAttack}, DEF {TotalDefense}, SPD {TotalSpeed})", 
                    playerState.PlayerId, equipmentAttackPower, equipmentDefensePower, equipmentAttackSpeed, playerState.Strength, playerState.Defense, playerState.TotalAttackPower, playerState.TotalDefensePower, playerState.TotalAttackSpeed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating equipment stats for player {PlayerId}", playerState.PlayerId);
                // Leave equipment stats as 0 on error
                playerState.EquipmentAttackPower = 0;
                playerState.EquipmentDefensePower = 0;
            }
        }

        /// <summary>
        /// Update cached equipment stats in PlayerState after equipment changes
        /// This method handles cross-session updates by finding PlayerState by PlayerId
        /// </summary>
        private async Task UpdateCachedEquipmentStatsAsync(string playerId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                
                // Calculate fresh equipment stats from database
                var (equipmentAttackPower, equipmentDefensePower, equipmentAttackSpeed) = await equipmentManager.CalculateEquipmentStatsAsync(playerId);
                
                // Find and update PlayerState in cache (search by PlayerId, not ConnectionId)
                var connectionEntry = _connections.FirstOrDefault(kvp => kvp.Value.PlayerId == playerId);
                if (connectionEntry.Value != null && _players.TryGetValue(connectionEntry.Key, out var cachedPlayerState))
                {
                    // Update the cached equipment stats
                    cachedPlayerState.EquipmentAttackPower = equipmentAttackPower;
                    cachedPlayerState.EquipmentDefensePower = equipmentDefensePower;
                    cachedPlayerState.EquipmentAttackSpeed = equipmentAttackSpeed;
                    cachedPlayerState.LastUpdate = DateTime.UtcNow;
                    
                    _logger.LogInformation("Updated cached equipment stats for player {PlayerId}: ATK +{Attack}, DEF +{Defense}, SPD {Speed} (Total: ATK {TotalAttack}, DEF {TotalDefense}, SPD {TotalSpeed})", 
                        playerId, equipmentAttackPower, equipmentDefensePower, equipmentAttackSpeed, cachedPlayerState.TotalAttackPower, cachedPlayerState.TotalDefensePower, cachedPlayerState.TotalAttackSpeed);
                }
                else
                {
                    _logger.LogWarning("Could not find cached PlayerState for player {PlayerId} to update equipment stats", playerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cached equipment stats for player {PlayerId}", playerId);
            }
        }

        /// <summary>
        /// Apply magic potion effect (placeholder for mana restoration)
        /// </summary>
        private async Task<bool> ApplyMagicPotion(string playerId, int manaAmount)
        {
            try
            {
                // TODO: Implement mana system when magic/spells are added
                _logger.LogInformation($"Magic potion used by {playerId} - mana system not yet implemented");
                return true; // Return true for now so the item is consumed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error applying magic potion to player {playerId}");
                return false;
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
        
        /// <summary>
        /// Get the equipped weapon for a player
        /// </summary>
        private async Task<EquippedItem?> GetPlayerEquippedWeapon(string playerId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
                
                var equippedItems = await equipmentManager.GetPlayerEquipmentAsync(playerId);
                var weapon = equippedItems.FirstOrDefault(item => 
                    item.SlotType.Equals("Weapon", StringComparison.OrdinalIgnoreCase));
                
                return weapon;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting equipped weapon for player {PlayerId}", playerId);
                return null;
            }
        }
        
        /// <summary>
        /// Validate weapon attack based on weapon range (both melee and ranged)
        /// </summary>
        private bool ValidateWeaponRange(WebSocketConnection connection, NetworkMessages.CombatActionMessage combatData, EquippedItem weapon)
        {
            if (!_players.TryGetValue(connection.ConnectionId, out var player))
            {
                return false;
            }
            
            // Calculate distance from player to attack position
            var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
            var attackPos = new Vector3(combatData.Position.X, combatData.Position.Y, combatData.Position.Z);
            var distance = Vector3.Distance(playerPos, attackPos);
            
            // Check if attack is within weapon range
            if (distance > weapon.WeaponRange)
            {
                _logger.LogWarning($"{weapon.WeaponType} attack from {connection.PlayerId} failed: distance {distance:F2} > range {weapon.WeaponRange}");
                return false;
            }
            
            _logger.LogInformation($"{weapon.WeaponType} attack validated: {weapon.ItemName} distance {distance:F2}/{weapon.WeaponRange} range");
            return true;
        }

        /// <summary>
        /// Send weapon timing information to client for client-side cooldown validation
        /// </summary>
        private async Task SendWeaponTimingInfo(string connectionId, string playerId)
        {
            try
            {
                var weapon = await GetPlayerEquippedWeapon(playerId);
                
                // Calculate timing info
                decimal attackSpeed = 1.0m; // Default attack speed (1 attack per second)
                int cooldownMs = 1000; // Default cooldown
                string weaponType = "Melee";
                string weaponName = "Unarmed";
                bool hasWeaponEquipped = false;
                
                if (weapon != null)
                {
                    attackSpeed = weapon.AttackSpeed > 0 ? weapon.AttackSpeed : 1.0m;
                    cooldownMs = (int)((1.0m / attackSpeed) * 1000); // Convert attacks/sec to ms
                    weaponType = weapon.WeaponType;
                    weaponName = weapon.ItemName;
                    hasWeaponEquipped = true;
                }
                
                var timingMessage = new WeaponTimingMessage
                {
                    PlayerId = playerId,
                    AttackSpeed = attackSpeed,
                    CooldownMs = cooldownMs,
                    ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    WeaponType = weaponType,
                    WeaponName = weaponName,
                    HasWeaponEquipped = hasWeaponEquipped
                };
                
                await SendToConnection(connectionId, "WeaponTiming", timingMessage);
                _logger.LogInformation($"Sent weapon timing info to {playerId}: {weaponName} ({attackSpeed} attacks/sec, {cooldownMs}ms cooldown)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending weapon timing info to player {PlayerId}", playerId);
            }
        }
        
        /// <summary>
        /// Get all active connections
        /// </summary>
        public IEnumerable<WebSocketConnection> GetAllConnections()
        {
            return _connections.Values.Where(c => c.WebSocket.State == WebSocketState.Open);
        }

        #region Phase 1: Projectile State Tracking

        /// <summary>
        /// Cleanup expired projectiles that haven't hit anything
        /// </summary>
        private void CleanupExpiredProjectiles(object? state)
        {
            try
            {
                var expiredProjectiles = _activeProjectiles
                    .Where(p => DateTime.UtcNow - p.Value.LaunchTime > TimeSpan.FromSeconds(10))
                    .Select(p => p.Key)
                    .ToList();

                foreach (var projectileId in expiredProjectiles)
                {
                    if (_activeProjectiles.TryRemove(projectileId, out var projectile))
                    {
                        _logger.LogDebug($"Cleaned up expired projectile: {projectileId} from {projectile.ShooterId}");
                    }
                }

                if (expiredProjectiles.Count > 0)
                {
                    _logger.LogInformation($"Cleaned up {expiredProjectiles.Count} expired projectiles");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during projectile cleanup");
            }
        }

        /// <summary>
        /// Generate unique projectile ID
        /// </summary>
        private string GenerateProjectileId(string shooterId)
        {
            return $"proj_{shooterId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N")[..8]}";
        }

        /// <summary>
        /// Generate unique projectile ID with index (Phase 3: Multi-projectile support)
        /// </summary>
        private string GenerateProjectileId(string shooterId, int index)
        {
            return $"proj_{shooterId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{index:D2}_{Guid.NewGuid().ToString("N")[..6]}";
        }

        #endregion
    }

    /// <summary>
    /// Tracks active projectile state for collision-based damage validation
    /// </summary>
    public class ProjectileState
    {
        public string ProjectileId { get; set; } = string.Empty;
        public string ShooterId { get; set; } = string.Empty;
        public string? IntendedTargetId { get; set; }
        public Vector3Data LaunchPosition { get; set; } = new();
        public Vector3Data TargetPosition { get; set; } = new();
        public NetworkMessages.ProjectileWeaponData WeaponData { get; set; } = new();
        public DateTime LaunchTime { get; set; } = DateTime.UtcNow;
        public bool HasHit { get; set; } = false;
        
        // Validation data
        public float MaxTravelDistance { get; set; }
        public DateTime ExpirationTime { get; set; }
    }

    public class WebSocketConnection
    {
        public string ConnectionId { get; }
        public WebSocket WebSocket { get; }
        public DateTime ConnectedAt { get; }
        public string? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public Vector3Data? LastPosition { get; set; }

        public WebSocketConnection(string connectionId, WebSocket webSocket)
        {
            ConnectionId = connectionId;
            WebSocket = webSocket;
            ConnectedAt = DateTime.UtcNow;
        }
    }
}