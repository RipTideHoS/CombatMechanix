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
        private TerrainService? _terrainService;

        // Level/wave tracking
        private int _currentLevel = 1;
        private bool _levelTransitionInProgress = false;
        private bool _waitingForPlayerContinue = false;

        // Level stats tracking
        private int _levelKills = 0;
        private long _levelExperienceEarned = 0;
        private float _levelDamageDealt = 0f;
        private DateTime _levelStartTime = DateTime.UtcNow;

        /// <summary>
        /// Event fired when all enemies are defeated (level complete)
        /// </summary>
        public event Action<int>? OnLevelComplete;
        
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
        /// Set the terrain service reference (called after both services are initialized)
        /// </summary>
        public void SetTerrainService(TerrainService terrainService)
        {
            _terrainService = terrainService;
        }

        /// <summary>
        /// Get current level/wave number
        /// </summary>
        public int CurrentLevel => _currentLevel;

        /// <summary>
        /// Initialize default enemies in the world
        /// </summary>
        public void InitializeDefaultEnemies()
        {
            _logger.LogInformation("Initializing default enemies...");

            // Find terrain-clear positions for initial enemies
            var spawnPos1 = _terrainService?.FindClearSpawnPosition(5f, 5f) ?? new Vector3Data(5f, 0.5f, 5f);

            var testEnemy = new EnemyState
            {
                EnemyId = "enemy_test_001",
                EnemyName = "Test Enemy",
                EnemyType = "Basic",
                Position = spawnPos1,
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
            // Reduced for quick level completion testing (was 20 enemies)
            // Use terrain-aware spawning to avoid placing enemies inside hills
            var preferredPositions = new[] { (10f, 10f) };

            for (int i = 0; i < preferredPositions.Length; i++)
            {
                var (prefX, prefZ) = preferredPositions[i];
                var spawnPos = _terrainService?.FindClearSpawnPosition(prefX, prefZ) ?? new Vector3Data(prefX, 0.5f, prefZ);

                var enemy = new EnemyState
                {
                    EnemyId = $"enemy_test_{i + 2:D3}",
                    EnemyName = $"Test Enemy {i + 2}",
                    EnemyType = "Basic",
                    Position = spawnPos,
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

                _logger.LogInformation($"Spawned enemy: {enemy.EnemyName} (ID: {enemy.EnemyId}) at ({spawnPos.X:F1}, {spawnPos.Y:F1}, {spawnPos.Z:F1}) Level {enemy.Level}");
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

            // Track damage for level stats
            TrackDamageDealt(damage);

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

            // Track level stats
            _levelKills++;
            long expFromKill = CalculateExperienceFromEnemy(enemy);
            _levelExperienceEarned += expFromKill;

            _logger.LogInformation($"Enemy {enemy.EnemyName} (ID: {enemy.EnemyId}) was killed by {killerId}. Level kills: {_levelKills}, Level XP: {_levelExperienceEarned}");

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

            // Check if all enemies are defeated (level complete)
            await CheckLevelComplete(killerId);
        }

        /// <summary>
        /// Calculate experience reward from killing an enemy
        /// </summary>
        private long CalculateExperienceFromEnemy(EnemyState enemy)
        {
            // Base XP based on enemy level and type
            long baseXP = 10 + (enemy.Level * 5);
            if (enemy.EnemyType == "Elite")
            {
                baseXP *= 2;
            }
            return baseXP;
        }

        /// <summary>
        /// Track damage dealt for level stats
        /// </summary>
        public void TrackDamageDealt(float damage)
        {
            _levelDamageDealt += damage;
        }

        /// <summary>
        /// Check if all enemies are defeated and trigger level completion
        /// </summary>
        private async Task CheckLevelComplete(string killerId)
        {
            // Check if any enemies are still alive
            int aliveCount = _enemies.Values.Count(e => e.IsAlive);

            if (aliveCount == 0 && !_levelTransitionInProgress && !_waitingForPlayerContinue)
            {
                _levelTransitionInProgress = true;
                _waitingForPlayerContinue = true;

                // Calculate time taken
                float timeTaken = (float)(DateTime.UtcNow - _levelStartTime).TotalSeconds;

                _logger.LogInformation($"🎉 Level {_currentLevel} complete! Kills: {_levelKills}, XP: {_levelExperienceEarned}, Time: {timeTaken:F1}s");

                // Fire level complete event
                OnLevelComplete?.Invoke(_currentLevel);

                // Send level complete message to all clients (they will show UI and wait for continue)
                var levelCompleteMessage = new NetworkMessages.LevelCompleteMessage
                {
                    CompletedLevel = _currentLevel,
                    NextLevel = _currentLevel + 1,
                    EnemiesKilled = _levelKills,
                    ExperienceEarned = _levelExperienceEarned,
                    DamageDealt = _levelDamageDealt,
                    TimeTaken = timeTaken,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await _connectionManager.BroadcastToAll("LevelComplete", levelCompleteMessage);
                _logger.LogInformation($"📢 Sent LevelComplete message to all clients. Waiting for player to continue...");

                // Clean up dead enemies, stale AI, and uncollected loot immediately
                PerformLevelCleanup();
            }
        }

        /// <summary>
        /// Remove all dead enemies, their AI, and uncollected loot after a level completes
        /// </summary>
        private void PerformLevelCleanup()
        {
            // Remove dead enemies from dictionary and their AI
            var deadEnemyIds = _enemies.Values
                .Where(e => !e.IsAlive)
                .Select(e => e.EnemyId)
                .ToList();

            foreach (var enemyId in deadEnemyIds)
            {
                _enemies.TryRemove(enemyId, out _);
                _aiManager?.RemoveEnemyAI(enemyId);
            }

            // Clear uncollected loot from previous level
            int lootCleared = _lootManager?.ClearAllActiveLoot() ?? 0;

            _logger.LogInformation(
                "Level cleanup: removed {EnemyCount} dead enemies, {LootCount} loot drops, {Remaining} enemies remaining",
                deadEnemyIds.Count, lootCleared, _enemies.Count);
        }

        /// <summary>
        /// Trigger level transition: change terrain and spawn new enemies
        /// </summary>
        private async Task TriggerLevelTransition(string killerId)
        {
            try
            {
                // Get new terrain data from TerrainService
                if (_terrainService != null)
                {
                    // Cycle through available hill sets based on level
                    var hillSets = _terrainService.GetAvailableHillSets();
                    var activeHillSets = _terrainService.GetActiveHillSets();

                    // Simple progression: alternate between hill sets
                    string nextHillSet = GetHillSetForLevel(_currentLevel, hillSets);

                    // Unload current hill sets and load new one
                    foreach (var activeSet in activeHillSets.ToList())
                    {
                        _terrainService.UnloadHillSet(activeSet);
                    }
                    _terrainService.LoadHillSet(nextHillSet);

                    _logger.LogInformation($"🏔️ Loaded hill set '{nextHillSet}' for level {_currentLevel}");

                    // Get updated terrain data
                    var terrainData = _terrainService.GetTerrainData();

                    // Broadcast terrain change to all clients
                    var terrainChangeMessage = new NetworkMessages.TerrainChangeMessage
                    {
                        Reason = "LevelComplete",
                        CurrentLevel = _currentLevel,
                        TerrainData = new NetworkMessages.TerrainData
                        {
                            BaseGroundLevel = terrainData.BaseGroundLevel,
                            Hills = terrainData.Hills.Select(h => new NetworkMessages.TerrainHillData
                            {
                                Id = h.Id,
                                Name = h.Name,
                                HillSet = h.HillSet,
                                Position = h.Position,
                                Scale = h.Scale,
                                Color = new NetworkMessages.TerrainColorData
                                {
                                    R = h.Color.R,
                                    G = h.Color.G,
                                    B = h.Color.B,
                                    A = h.Color.A
                                }
                            }).ToList(),
                            ActiveHillSets = terrainData.ActiveHillSets
                        },
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    await _connectionManager.BroadcastToAll("TerrainChange", terrainChangeMessage);
                    _logger.LogInformation($"🏔️ Broadcast terrain change to all clients for level {_currentLevel}");

                    // Reposition all players to clear spawn positions for the new terrain
                    await _connectionManager.RepositionAllPlayers(_terrainService);
                }

                // Broadcast level complete notification
                await _connectionManager.BroadcastToAll("SystemNotification", new NetworkMessages.SystemNotification
                {
                    Message = $"Level {_currentLevel - 1} Complete! Entering Level {_currentLevel}...",
                    Type = "LevelComplete",
                    Priority = "High",
                    Timestamp = DateTime.UtcNow
                });

                // Wait a moment for terrain transition effect
                await Task.Delay(2000);

                // Spawn new enemies for the new level
                await SpawnEnemiesForLevel(_currentLevel);

                _levelTransitionInProgress = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during level transition");
                _levelTransitionInProgress = false;
            }
        }

        /// <summary>
        /// Get the hill set name for a given level
        /// </summary>
        private string GetHillSetForLevel(int level, List<string> availableHillSets)
        {
            if (availableHillSets.Count == 0) return "default";

            // Cycle through available hill sets based on level
            int setIndex = (level - 1) % availableHillSets.Count;
            return availableHillSets[setIndex];
        }

        /// <summary>
        /// Spawn enemies appropriate for the current level
        /// </summary>
        private async Task SpawnEnemiesForLevel(int level)
        {
            _logger.LogInformation($"🎮 Spawning enemies for level {level}");

            // Clear any remaining dead enemies
            var deadEnemies = _enemies.Values.Where(e => !e.IsAlive).Select(e => e.EnemyId).ToList();
            foreach (var enemyId in deadEnemies)
            {
                _enemies.TryRemove(enemyId, out _);
                _aiManager?.RemoveEnemyAI(enemyId);
            }

            // Calculate number of enemies based on level (2 base + 1 per level)
            int enemyCount = 2 + level;
            int baseHealth = 50 + (level * 25);
            float baseDamage = 10f + (level * 5f);

            var newEnemies = new List<EnemyState>();
            var random = new Random();

            for (int i = 0; i < enemyCount; i++)
            {
                // Randomize preferred position within play area
                float preferredX = (float)(random.NextDouble() * 60 - 30); // -30 to 30
                float preferredZ = (float)(random.NextDouble() * 60 - 30); // -30 to 30

                // Find a clear spawn position (not on hills) that player can reach
                Vector3Data spawnPos;
                if (_terrainService != null)
                {
                    spawnPos = _terrainService.FindClearSpawnPosition(preferredX, preferredZ);
                }
                else
                {
                    spawnPos = new Vector3Data(preferredX, 0.5f, preferredZ);
                }

                var enemy = new EnemyState
                {
                    EnemyId = $"enemy_lvl{level}_{i + 1:D3}",
                    EnemyName = $"Level {level} Enemy {i + 1}",
                    EnemyType = level % 3 == 0 ? "Elite" : "Basic",
                    Position = spawnPos,
                    Rotation = (float)(random.NextDouble() * 360),
                    Health = baseHealth + random.Next(-20, 20),
                    MaxHealth = baseHealth + random.Next(-20, 20),
                    Level = level,
                    Damage = baseDamage + (float)(random.NextDouble() * 5),
                    IsAlive = true,
                    LastUpdate = DateTime.UtcNow
                };

                // Ensure MaxHealth >= Health
                enemy.MaxHealth = Math.Max(enemy.MaxHealth, enemy.Health);

                _enemies.TryAdd(enemy.EnemyId, enemy);
                _aiManager?.InitializeEnemyAI(enemy, "RandomWander");
                newEnemies.Add(enemy);

                _logger.LogInformation($"🎮 Spawned {enemy.EnemyName} (HP: {enemy.Health}) at ({spawnPos.X:F1}, {spawnPos.Y:F1}, {spawnPos.Z:F1})");
            }

            // Broadcast new enemies to all clients
            var spawnMessage = new NetworkMessages.EnemySpawnMessage
            {
                Enemies = newEnemies
            };

            await _connectionManager.BroadcastToAll("EnemySpawn", spawnMessage);
            _logger.LogInformation($"🎮 Spawned {newEnemies.Count} enemies for level {level}");

            // Reset level stats for next level
            _levelKills = 0;
            _levelExperienceEarned = 0;
            _levelDamageDealt = 0f;
            _levelStartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Handle player requesting to continue to next level
        /// Called when player clicks the continue button after level complete
        /// </summary>
        public async Task HandlePlayerContinue(string playerId, int nextLevel)
        {
            if (!_waitingForPlayerContinue)
            {
                _logger.LogWarning($"Player {playerId} sent continue request but not waiting for continue");
                return;
            }

            _logger.LogInformation($"▶️ Player {playerId} is ready to continue to level {nextLevel}");

            _waitingForPlayerContinue = false;
            _currentLevel = nextLevel;

            // Restore all players to max health
            await _connectionManager.RestoreAllPlayersHealth();

            // Trigger terrain change and enemy spawn
            await TriggerLevelTransition(playerId);

            _levelTransitionInProgress = false;
        }

        /// <summary>
        /// Check if waiting for player to continue
        /// </summary>
        public bool IsWaitingForPlayerContinue => _waitingForPlayerContinue;

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
            foreach (var player in activePlayers)
            {
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