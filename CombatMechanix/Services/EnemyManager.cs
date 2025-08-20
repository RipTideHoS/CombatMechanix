using CombatMechanix.Models;
using CombatMechanix.AI;
using System.Collections.Concurrent;

namespace CombatMechanix.Services
{
    /// <summary>
    /// Server-side enemy management system
    /// Handles enemy spawning, state updates, damage processing, and synchronization
    /// </summary>
    public class EnemyManager
    {
        private readonly ConcurrentDictionary<string, EnemyState> _enemies = new();
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly ILogger<EnemyManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Timer _updateTimer;
        private LootManager? _lootManager;
        private EnemyAIManager? _aiManager;
        
        public EnemyManager(WebSocketConnectionManager connectionManager, ILogger<EnemyManager> logger, IServiceProvider serviceProvider)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // Update enemies every 100ms (10 times per second)
            _updateTimer = new Timer(UpdateEnemies, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
            
            _logger.LogInformation("EnemyManager initialized with update interval: 100ms");
        }

        /// <summary>
        /// Set the loot manager reference (called after both services are initialized)
        /// </summary>
        public void SetLootManager(LootManager lootManager)
        {
            _lootManager = lootManager;
        }

        /// <summary>
        /// Set the AI manager reference (called after both services are initialized)
        /// </summary>
        public void SetAIManager(EnemyAIManager aiManager)
        {
            _aiManager = aiManager;
        }

        /// <summary>
        /// Initialize default enemies in the world
        /// </summary>
        public void InitializeDefaultEnemies()
        {
            _logger.LogInformation("Initializing default enemies...");
            
            // Create a test enemy near the spawn point
            var testEnemy = new EnemyState
            {
                EnemyId = "enemy_test_001",
                EnemyName = "Test Enemy",
                EnemyType = "Basic",
                Position = new Vector3Data(5f, 0.5f, 5f), // Near reference box
                Rotation = 0f,
                Health = 100,
                MaxHealth = 100,
                Level = 1,
                Damage = 15f,
                IsAlive = true,
                LastUpdate = DateTime.UtcNow
            };
            
            _enemies.TryAdd(testEnemy.EnemyId, testEnemy);
            
            // Initialize AI for the enemy
            _aiManager?.InitializeEnemyAI(testEnemy, "RandomWander");
            
            _logger.LogInformation($"Spawned enemy: {testEnemy.EnemyName} (ID: {testEnemy.EnemyId}) at position ({testEnemy.Position.X}, {testEnemy.Position.Y}, {testEnemy.Position.Z})");
            
            // Could add more default enemies here
            CreateAdditionalTestEnemies();
        }

        private void CreateAdditionalTestEnemies()
        {
            // Add a few more test enemies at different positions
            var positions = new[]
            {
                new Vector3Data(10f, 0.5f, 10f),
                new Vector3Data(-5f, 0.5f, 8f),
                new Vector3Data(8f, 0.5f, -3f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var enemy = new EnemyState
                {
                    EnemyId = $"enemy_test_{i + 2:D3}",
                    EnemyName = $"Test Enemy {i + 2}",
                    EnemyType = "Basic",
                    Position = positions[i],
                    Rotation = 0f,
                    Health = 80 + (i * 20), // Varying health
                    MaxHealth = 80 + (i * 20),
                    Level = 1 + i, // Varying levels
                    Damage = 12f + (i * 3f), // Varying damage
                    IsAlive = true,
                    LastUpdate = DateTime.UtcNow
                };

                _enemies.TryAdd(enemy.EnemyId, enemy);
                
                // Initialize AI for each additional enemy
                _aiManager?.InitializeEnemyAI(enemy, "RandomWander");
                
                _logger.LogInformation($"Spawned enemy: {enemy.EnemyName} (ID: {enemy.EnemyId}) Level {enemy.Level}");
            }
        }

        /// <summary>
        /// Get all current enemies
        /// </summary>
        public List<EnemyState> GetAllEnemies()
        {
            return _enemies.Values.ToList();
        }

        /// <summary>
        /// Get enemies near a specific position
        /// </summary>
        public List<EnemyState> GetNearbyEnemies(Vector3Data position, float range = 50f)
        {
            var nearbyEnemies = new List<EnemyState>();
            
            foreach (var enemy in _enemies.Values)
            {
                if (!enemy.IsAlive) continue;
                
                // Calculate distance
                float dx = enemy.Position.X - position.X;
                float dz = enemy.Position.Z - position.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                
                if (distance <= range)
                {
                    nearbyEnemies.Add(enemy);
                }
            }
            
            return nearbyEnemies;
        }

        /// <summary>
        /// Apply damage to an enemy
        /// </summary>
        public async Task<bool> DamageEnemy(string enemyId, float damage, string attackerId)
        {
            if (!_enemies.TryGetValue(enemyId, out var enemy))
            {
                _logger.LogWarning($"Attempted to damage non-existent enemy: {enemyId}");
                return false;
            }

            if (!enemy.IsAlive)
            {
                _logger.LogWarning($"Attempted to damage dead enemy: {enemyId}");
                return false;
            }

            // Apply damage
            var oldHealth = enemy.Health;
            enemy.Health = Math.Max(0, enemy.Health - (int)damage);
            enemy.LastUpdate = DateTime.UtcNow;

            _logger.LogInformation($"Enemy {enemy.EnemyName} (ID: {enemyId}) took {damage} damage. Health: {enemy.Health}/{enemy.MaxHealth}");

            // Notify AI system about damage taken
            if (_aiManager != null)
            {
                var aiContext = await BuildAIWorldContext();
                await _aiManager.OnEnemyDamaged(enemy, damage, attackerId, aiContext);
            }

            // Check for death
            if (enemy.Health <= 0 && enemy.IsAlive)
            {
                await HandleEnemyDeath(enemy, attackerId);
            }
            else
            {
                // Broadcast damage event
                var damageMessage = new NetworkMessages.EnemyDamageMessage
                {
                    EnemyId = enemyId,
                    AttackerId = attackerId,
                    Damage = damage,
                    Position = enemy.Position,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await _connectionManager.BroadcastToAll("EnemyDamage", damageMessage);
            }

            return true;
        }

        /// <summary>
        /// Handle enemy death
        /// </summary>
        private async Task HandleEnemyDeath(EnemyState enemy, string killerId)
        {
            enemy.IsAlive = false;
            enemy.Health = 0;
            enemy.LastUpdate = DateTime.UtcNow;

            _logger.LogInformation($"Enemy {enemy.EnemyName} (ID: {enemy.EnemyId}) was killed by {killerId}");

            // Broadcast death event
            var deathMessage = new NetworkMessages.EnemyDeathMessage
            {
                EnemyId = enemy.EnemyId,
                KillerId = killerId,
                Position = enemy.Position,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await _connectionManager.BroadcastToAll("EnemyDeath", deathMessage);

            // Generate loot drop if loot manager is available
            if (_lootManager != null)
            {
                await _lootManager.GenerateLootDrop(enemy.EnemyId, enemy.Position, killerId);
            }
            else
            {
                _logger.LogWarning("LootManager not available - no loot will be generated for enemy {EnemyId}", enemy.EnemyId);
            }

            // Schedule respawn (for testing - 30 seconds)
            _ = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ => RespawnEnemy(enemy.EnemyId));
        }

        /// <summary>
        /// Respawn a dead enemy
        /// </summary>
        private async Task RespawnEnemy(string enemyId)
        {
            if (!_enemies.TryGetValue(enemyId, out var enemy))
            {
                return;
            }

            // Reset enemy state
            enemy.Health = enemy.MaxHealth;
            enemy.IsAlive = true;
            enemy.LastUpdate = DateTime.UtcNow;

            _logger.LogInformation($"Enemy {enemy.EnemyName} (ID: {enemyId}) respawned");

            // Broadcast respawn as spawn message
            var spawnMessage = new NetworkMessages.EnemySpawnMessage
            {
                Enemies = new List<EnemyState> { enemy }
            };

            await _connectionManager.BroadcastToAll("EnemySpawn", spawnMessage);
        }

        /// <summary>
        /// Send all enemies to a newly connected client
        /// </summary>
        public async Task SendEnemyStatesToClient(string connectionId)
        {
            var allEnemies = GetAllEnemies();
            
            if (allEnemies.Count > 0)
            {
                var spawnMessage = new NetworkMessages.EnemySpawnMessage
                {
                    Enemies = allEnemies
                };

                await _connectionManager.SendToConnection(connectionId, "EnemySpawn", spawnMessage);
                _logger.LogInformation($"Sent {allEnemies.Count} enemy states to new client: {connectionId}");
            }
        }

        /// <summary>
        /// Periodic enemy update (AI, regeneration, etc.)
        /// </summary>
        private async void UpdateEnemies(object? state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var enemiesToUpdate = new List<EnemyState>();

                foreach (var enemy in _enemies.Values)
                {
                    if (!enemy.IsAlive) continue;

                    // Simple health regeneration (1 HP per 5 seconds)
                    if (enemy.Health < enemy.MaxHealth && 
                        (now - enemy.LastUpdate).TotalSeconds >= 5)
                    {
                        enemy.Health = Math.Min(enemy.MaxHealth, enemy.Health + 1);
                        enemy.LastUpdate = now;
                        enemiesToUpdate.Add(enemy);
                    }
                }

                // Broadcast updates if any
                if (enemiesToUpdate.Count > 0)
                {
                    var updateMessage = new NetworkMessages.EnemyUpdateMessage
                    {
                        Enemies = enemiesToUpdate
                    };

                    await _connectionManager.BroadcastToAll("EnemyUpdate", updateMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during enemy update cycle");
            }
        }

        /// <summary>
        /// Get enemy by ID
        /// </summary>
        public EnemyState? GetEnemy(string enemyId)
        {
            _enemies.TryGetValue(enemyId, out var enemy);
            return enemy;
        }

        /// <summary>
        /// Validate if an attack against an enemy is valid
        /// </summary>
        public bool ValidateAttackOnEnemy(string enemyId, string attackerId, Vector3Data attackPosition)
        {
            if (!_enemies.TryGetValue(enemyId, out var enemy))
            {
                return false;
            }

            if (!enemy.IsAlive)
            {
                return false;
            }

            // Simple range check (assume max attack range of 5 units)
            float dx = enemy.Position.X - attackPosition.X;
            float dz = enemy.Position.Z - attackPosition.Z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);

            const float maxAttackRange = 5f;
            return distance <= maxAttackRange;
        }

        /// <summary>
        /// Build AI world context for AI decision making
        /// </summary>
        private async Task<AIWorldContext> BuildAIWorldContext()
        {
            var activePlayers = await GetActivePlayersForAI();
            Console.WriteLine($"[DEBUG] EnemyManager.BuildAIWorldContext: Got {activePlayers.Count} players from cache/DB");
            foreach (var player in activePlayers)
            {
                Console.WriteLine($"[DEBUG] Player {player.PlayerId} has Health={player.Health}");
            }
            
            var context = new AIWorldContext
            {
                CurrentTime = DateTime.UtcNow,
                ActivePlayers = activePlayers,
                OtherEnemies = GetAllEnemies(),
                BroadcastMessage = async (messageType, data) => await _connectionManager.BroadcastToAll(messageType, data),
                SendToPlayer = async (playerId, messageType, data) => await _connectionManager.SendToPlayer(playerId, messageType, data),
                PersistPlayerHealth = async (playerId, newHealth) => 
                {
                    using var scope = _serviceProvider.CreateScope();
                    var playerStatsService = scope.ServiceProvider.GetRequiredService<IPlayerStatsService>();
                    await playerStatsService.UpdateHealthAsync(playerId, newHealth);
                },
                UpdatePlayerHealthInMemory = async (playerId, newHealth) => 
                {
                    // Update player health in the connection manager's player cache
                    await _connectionManager.UpdatePlayerHealthInMemory(playerId, newHealth);
                }
            };
            
            return context;
        }
        
        /// <summary>
        /// Get active players for AI context using in-memory cached data for consistent health values
        /// </summary>
        private async Task<List<PlayerState>> GetActivePlayersForAI()
        {
            // Get players from connection manager's in-memory cache to ensure consistent health values
            // This avoids the database round-trip and uses the already-updated in-memory states
            return await _connectionManager.GetActivePlayersForAI();
        }
        
        /// <summary>
        /// Remove AI when enemy dies permanently
        /// </summary>
        private void RemoveEnemyAI(string enemyId)
        {
            _aiManager?.RemoveEnemyAI(enemyId);
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
            _aiManager?.Dispose();
            _logger.LogInformation("EnemyManager disposed");
        }
    }
}