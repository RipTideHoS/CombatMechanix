using CombatMechanix.Data;
using CombatMechanix.Models;
using System.Collections.Concurrent;

namespace CombatMechanix.Services
{
    /// <summary>
    /// Server-side loot management system
    /// Handles loot generation, drop creation, validation, and cleanup
    /// </summary>
    public class LootManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly ILogger<LootManager> _logger;
        private readonly ConcurrentDictionary<string, LootDrop> _activeLoot = new();
        private readonly Random _random = new();
        private readonly Timer _cleanupTimer;

        // Loot generation settings
        private const float LootDropChance = 0.8f; // 80% chance to drop loot on enemy death
        private const string DefaultRarity = "Common"; // Default rarity for loot drops
        private const float MaxPickupRange = 6f; // Maximum distance for loot pickup (accounts for client magnet range)
        private const int LootCleanupMinutes = 5; // Minutes before loot disappears

        public LootManager(
            IServiceProvider serviceProvider, 
            WebSocketConnectionManager connectionManager, 
            ILogger<LootManager> logger)
        {
            _serviceProvider = serviceProvider;
            _connectionManager = connectionManager;
            _logger = logger;

            // Clean up expired loot every minute
            _cleanupTimer = new Timer(CleanupExpiredLoot, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            _logger.LogInformation("LootManager initialized with {DropChance}% loot drop chance", 
                LootDropChance * 100);
        }

        /// <summary>
        /// Generate and drop loot when an enemy dies
        /// </summary>
        /// <param name="enemyId">ID of the enemy that died</param>
        /// <param name="enemyPosition">Position where the enemy died</param>
        /// <param name="killerId">ID of the player who killed the enemy</param>
        public async Task GenerateLootDrop(string enemyId, Vector3Data enemyPosition, string killerId)
        {
            try
            {
                // Check if loot should drop based on drop chance
                if (_random.NextDouble() > LootDropChance)
                {
                    _logger.LogDebug("No loot dropped for enemy {EnemyId} (random chance)", enemyId);
                    return;
                }

                // Get a random item from all rarities (with weighted probability)
                InventoryItem? lootItem;
                using (var scope = _serviceProvider.CreateScope())
                {
                    var itemRepository = scope.ServiceProvider.GetRequiredService<IItemRepository>();
                    lootItem = await itemRepository.GetRandomItemFromAllRaritiesAsync();
                }

                if (lootItem == null)
                {
                    _logger.LogWarning("No items found in database for loot drop");
                    return;
                }

                // Create loot drop instance
                var lootId = Guid.NewGuid().ToString();
                var lootDrop = new LootDrop
                {
                    LootId = lootId,
                    Item = lootItem,
                    Position = enemyPosition,
                    SourceEnemyId = enemyId,
                    CreatedTime = DateTime.UtcNow,
                    ExpiryTime = DateTime.UtcNow.AddMinutes(LootCleanupMinutes)
                };

                // Store active loot
                _activeLoot.TryAdd(lootId, lootDrop);

                // Broadcast loot drop to all clients
                var lootMessage = new NetworkMessages.LootDropMessage
                {
                    LootId = lootId,
                    Item = lootItem,
                    Position = enemyPosition,
                    SourceEnemyId = enemyId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await _connectionManager.BroadcastToAll("LootDrop", lootMessage);

                _logger.LogInformation("Loot dropped: {ItemName} (ID: {LootId}) at position ({X}, {Y}, {Z}) from enemy {EnemyId}", 
                    lootItem.ItemName, lootId, enemyPosition.X, enemyPosition.Y, enemyPosition.Z, enemyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating loot drop for enemy {EnemyId}", enemyId);
            }
        }

        /// <summary>
        /// Handle player request to pick up loot
        /// </summary>
        /// <param name="playerId">ID of the player attempting pickup</param>
        /// <param name="lootId">ID of the loot to pick up</param>
        /// <param name="playerPosition">Player's current position for range validation</param>
        public async Task<bool> HandleLootPickup(string playerId, string lootId, Vector3Data playerPosition)
        {
            try
            {
                // Check if loot exists
                if (!_activeLoot.TryGetValue(lootId, out var lootDrop))
                {
                    await SendPickupResponse(playerId, lootId, false, "Loot no longer available", null);
                    return false;
                }

                // Validate pickup range
                var distance = CalculateDistance(playerPosition, lootDrop.Position);
                if (distance > MaxPickupRange)
                {
                    await SendPickupResponse(playerId, lootId, false, 
                        $"Too far from loot (distance: {distance:F1}m, max: {MaxPickupRange}m)", null);
                    return false;
                }

                // Check if loot has expired
                if (DateTime.UtcNow > lootDrop.ExpiryTime)
                {
                    _activeLoot.TryRemove(lootId, out _);
                    await SendPickupResponse(playerId, lootId, false, "Loot has expired", null);
                    return false;
                }

                // Remove loot from active collection (server authoritative)
                _activeLoot.TryRemove(lootId, out _);

                // Award random gold (1-5)
                int goldAwarded = _random.Next(1, 6);
                using (var goldScope = _serviceProvider.CreateScope())
                {
                    var statsRepository = goldScope.ServiceProvider.GetRequiredService<IPlayerStatsRepository>();
                    await statsRepository.AddGoldAsync(playerId, goldAwarded);

                    // Send updated player stats (with new gold) to client
                    var updatedStats = await statsRepository.GetByIdAsync(playerId);
                    if (updatedStats != null)
                    {
                        await _connectionManager.SendToPlayer(playerId, "PlayerStatsUpdate",
                            new NetworkMessages.PlayerStatsUpdateMessage
                            {
                                PlayerId = playerId,
                                Level = updatedStats.Level,
                                Experience = updatedStats.Experience,
                                Health = updatedStats.Health,
                                MaxHealth = updatedStats.MaxHealth,
                                Strength = updatedStats.Strength,
                                Defense = updatedStats.Defense,
                                Speed = updatedStats.Speed,
                                ExperienceToNextLevel = updatedStats.ExperienceToNextLevel,
                                Gold = updatedStats.Gold
                            });
                    }
                }

                // Send success response with gold awarded (no inventory item)
                await SendPickupResponse(playerId, lootId, true, "Gold picked up", null, goldAwarded);

                _logger.LogInformation("Player {PlayerId} picked up loot (ID: {LootId}), awarded {Gold} gold",
                    playerId, lootId, goldAwarded);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling loot pickup for player {PlayerId}, loot {LootId}", playerId, lootId);
                await SendPickupResponse(playerId, lootId, false, "Server error during pickup", null);
                return false;
            }
        }

        /// <summary>
        /// Send loot pickup response to the requesting player
        /// </summary>
        private async Task SendPickupResponse(string playerId, string lootId, bool success, string message, InventoryItem? item, int goldAwarded = 0)
        {
            var response = new NetworkMessages.LootPickupResponseMessage
            {
                PlayerId = playerId,
                LootId = lootId,
                Success = success,
                Message = message,
                Item = item,
                GoldAwarded = goldAwarded
            };

            await _connectionManager.SendToPlayer(playerId, "LootPickupResponse", response);
        }

        /// <summary>
        /// Send updated inventory to player after successful pickup
        /// </summary>
        private async Task SendUpdatedInventory(string playerId)
        {
            try
            {
                // Get updated inventory from database
                using var scope = _serviceProvider.CreateScope();
                var inventoryRepository = scope.ServiceProvider.GetRequiredService<IPlayerInventoryRepository>();
                var inventoryItems = await inventoryRepository.GetPlayerInventoryAsync(playerId);

                // Send inventory update to player
                var inventoryResponse = new NetworkMessages.InventoryResponseMessage
                {
                    PlayerId = playerId,
                    Items = inventoryItems,
                    Success = true
                };

                await _connectionManager.SendToPlayer(playerId, "InventoryResponse", inventoryResponse);
                
                _logger.LogDebug("Sent inventory update to player {PlayerId} with {Count} items after pickup", 
                    playerId, inventoryItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending inventory update to player {PlayerId}", playerId);
            }
        }

        /// <summary>
        /// Calculate distance between two 3D positions
        /// </summary>
        private static float CalculateDistance(Vector3Data pos1, Vector3Data pos2)
        {
            var dx = pos1.X - pos2.X;
            var dy = pos1.Y - pos2.Y;
            var dz = pos1.Z - pos2.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Clean up expired loot drops
        /// </summary>
        private async void CleanupExpiredLoot(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var expiredLoot = _activeLoot.Values
                    .Where(loot => now > loot.ExpiryTime)
                    .ToList();

                foreach (var loot in expiredLoot)
                {
                    if (_activeLoot.TryRemove(loot.LootId, out _))
                    {
                        _logger.LogDebug("Cleaned up expired loot: {ItemName} (ID: {LootId})", 
                            loot.Item.ItemName, loot.LootId);
                    }
                }

                if (expiredLoot.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired loot drops", expiredLoot.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during loot cleanup");
            }
        }

        /// <summary>
        /// Get all active loot drops (for debugging/admin purposes)
        /// </summary>
        public List<LootDrop> GetActiveLoot()
        {
            return _activeLoot.Values.ToList();
        }

        /// <summary>
        /// Force remove a specific loot drop
        /// </summary>
        public bool RemoveLoot(string lootId)
        {
            return _activeLoot.TryRemove(lootId, out _);
        }

        /// <summary>
        /// Clear all active loot drops (used during level transitions)
        /// </summary>
        public int ClearAllActiveLoot()
        {
            int count = _activeLoot.Count;
            _activeLoot.Clear();
            if (count > 0)
                _logger.LogInformation("Cleared {Count} active loot drops for level transition", count);
            return count;
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _logger.LogInformation("LootManager disposed");
        }
    }

    /// <summary>
    /// Represents an active loot drop in the world
    /// </summary>
    public class LootDrop
    {
        public string LootId { get; set; } = string.Empty;
        public InventoryItem Item { get; set; } = new();
        public Vector3Data Position { get; set; } = new();
        public string SourceEnemyId { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public DateTime ExpiryTime { get; set; }
    }
}