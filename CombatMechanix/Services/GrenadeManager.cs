using CombatMechanix.Models;
using System.Collections.Concurrent;

namespace CombatMechanix.Services
{
    public class GrenadeManager
    {
        private readonly ConcurrentDictionary<string, ActiveGrenade> _activeGrenades = new();
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly EnemyManager _enemyManager;
        private readonly ILogger<GrenadeManager> _logger;
        private readonly Timer _updateTimer;

        public GrenadeManager(
            WebSocketConnectionManager connectionManager,
            EnemyManager enemyManager,
            ILogger<GrenadeManager> logger)
        {
            _connectionManager = connectionManager;
            _enemyManager = enemyManager;
            _logger = logger;

            // Update grenades every 50ms for smooth timing
            _updateTimer = new Timer(UpdateGrenades, null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));

            _logger.LogInformation("GrenadeManager initialized with update interval: 50ms");
        }

        /// <summary>
        /// Throw a grenade from player position to target position
        /// </summary>
        public async Task ThrowGrenade(string playerId, Vector3Data throwPosition, Vector3Data targetPosition, GrenadeData grenadeData)
        {
            try
            {
                var grenadeId = $"grenade_{playerId}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var currentTime = DateTime.UtcNow;

                var activeGrenade = new ActiveGrenade
                {
                    GrenadeId = grenadeId,
                    PlayerId = playerId,
                    GrenadeType = grenadeData.GrenadeType,
                    StartPosition = throwPosition,
                    TargetPosition = targetPosition,
                    CurrentPosition = throwPosition,
                    ThrowTime = currentTime,
                    ExplosionTime = currentTime.AddSeconds(grenadeData.ExplosionDelay),
                    ExplosionRadius = grenadeData.ExplosionRadius,
                    AreaDamage = grenadeData.AreaDamage,
                    State = GrenadeState.Flying,
                    WarningDisplayed = false
                };

                _activeGrenades.TryAdd(grenadeId, activeGrenade);

                // Broadcast grenade spawn to all clients
                var spawnMessage = new NetworkMessages.GrenadeSpawnMessage
                {
                    GrenadeId = grenadeId,
                    PlayerId = playerId,
                    StartPosition = throwPosition,
                    TargetPosition = targetPosition,
                    GrenadeType = grenadeData.GrenadeType,
                    ExplosionDelay = grenadeData.ExplosionDelay,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                _logger.LogInformation($"About to broadcast GrenadeSpawn message for grenade {grenadeId}");
                await _connectionManager.BroadcastToAll("GrenadeSpawn", spawnMessage);
                _logger.LogInformation($"GrenadeSpawn message broadcasted for grenade {grenadeId}");

                _logger.LogInformation($"Grenade {grenadeId} thrown by player {playerId} with {grenadeData.ExplosionDelay}s delay");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error throwing grenade for player {PlayerId}", playerId);
            }
        }

        /// <summary>
        /// Handle grenade explosion and area damage calculation
        /// </summary>
        public async Task HandleGrenadeExplosion(string grenadeId)
        {
            try
            {
                if (!_activeGrenades.TryGetValue(grenadeId, out var grenade))
                {
                    _logger.LogWarning("Attempted to explode non-existent grenade: {GrenadeId}", grenadeId);
                    return;
                }

                grenade.State = GrenadeState.Exploded;

                // Calculate area damage
                var damagedTargets = await CalculateAreaDamage(grenade);

                // Broadcast explosion message
                var explosionMessage = new NetworkMessages.GrenadeExplosionMessage
                {
                    GrenadeId = grenadeId,
                    ExplosionPosition = grenade.CurrentPosition,
                    ExplosionRadius = grenade.ExplosionRadius,
                    Damage = grenade.AreaDamage,
                    DamagedTargets = damagedTargets,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await _connectionManager.BroadcastToAll("GrenadeExplosion", explosionMessage);

                // Remove grenade from active list
                _activeGrenades.TryRemove(grenadeId, out _);

                _logger.LogInformation($"Grenade {grenadeId} exploded, {damagedTargets.Count} targets affected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling grenade explosion for {GrenadeId}", grenadeId);
            }
        }

        /// <summary>
        /// Periodic update for grenade timing and state management
        /// </summary>
        private async void UpdateGrenades(object? state)
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var grenadesForRemoval = new List<string>();
                var tasks = new List<Task>();

                foreach (var kvp in _activeGrenades)
                {
                    var grenade = kvp.Value;
                    var timeRemaining = grenade.ExplosionTime - currentTime;

                    // Check if grenade should explode
                    if (timeRemaining <= TimeSpan.Zero && grenade.State != GrenadeState.Exploded)
                    {
                        tasks.Add(HandleGrenadeExplosion(grenade.GrenadeId));
                    }
                    // Show warning 1 second before explosion
                    else if (timeRemaining <= TimeSpan.FromSeconds(1) && !grenade.WarningDisplayed && grenade.State == GrenadeState.Landed)
                    {
                        tasks.Add(ShowGrenadeWarning(grenade));
                        grenade.WarningDisplayed = true;
                    }
                    // Update grenade state from flying to landed after 1 second
                    else if (grenade.State == GrenadeState.Flying && (currentTime - grenade.ThrowTime) >= TimeSpan.FromSeconds(1))
                    {
                        grenade.State = GrenadeState.Landed;
                        grenade.CurrentPosition = grenade.TargetPosition; // Assume it lands at target position
                    }
                }

                // Execute all tasks in parallel
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during grenade update cycle");
            }
        }

        /// <summary>
        /// Show warning indicator before explosion
        /// </summary>
        private async Task ShowGrenadeWarning(ActiveGrenade grenade)
        {
            try
            {
                var warningMessage = new NetworkMessages.GrenadeWarningMessage
                {
                    GrenadeId = grenade.GrenadeId,
                    ExplosionPosition = grenade.CurrentPosition,
                    ExplosionRadius = grenade.ExplosionRadius,
                    TimeToExplosion = (float)(grenade.ExplosionTime - DateTime.UtcNow).TotalSeconds,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                _logger.LogInformation($"Broadcasting GrenadeWarning for grenade {grenade.GrenadeId}");
                await _connectionManager.BroadcastToAll("GrenadeWarning", warningMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing grenade warning for {GrenadeId}", grenade.GrenadeId);
            }
        }

        /// <summary>
        /// Calculate area damage and return list of affected targets
        /// </summary>
        private async Task<List<NetworkMessages.DamageTarget>> CalculateAreaDamage(ActiveGrenade grenade)
        {
            var damagedTargets = new List<NetworkMessages.DamageTarget>();

            try
            {
                // Get all players and enemies in radius
                var targetsInRadius = GetTargetsInRadius(grenade.CurrentPosition, grenade.ExplosionRadius);

                foreach (var target in targetsInRadius)
                {
                    var distance = CalculateDistance(grenade.CurrentPosition, target.Position);
                    var damage = CalculateAreaDamage(grenade.AreaDamage, distance, grenade.ExplosionRadius);

                    if (damage > 0)
                    {
                        // Apply damage based on target type
                        if (target.TargetType == "Enemy")
                        {
                            await _enemyManager.DamageEnemy(target.TargetId, damage, grenade.PlayerId);
                        }
                        else if (target.TargetType == "Player")
                        {
                            // TODO: Implement player damage in future phase
                            _logger.LogInformation($"Player {target.TargetId} would take {damage} damage from grenade");
                        }

                        damagedTargets.Add(new NetworkMessages.DamageTarget
                        {
                            TargetId = target.TargetId,
                            TargetType = target.TargetType,
                            DamageDealt = damage,
                            Position = target.Position
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating area damage for grenade {GrenadeId}", grenade.GrenadeId);
            }

            return damagedTargets;
        }

        /// <summary>
        /// Get all valid targets within explosion radius
        /// </summary>
        private List<TargetInfo> GetTargetsInRadius(Vector3Data center, float radius)
        {
            var targets = new List<TargetInfo>();

            try
            {
                // Get nearby enemies
                var nearbyEnemies = _enemyManager.GetNearbyEnemies(center, radius);
                foreach (var enemy in nearbyEnemies)
                {
                    targets.Add(new TargetInfo
                    {
                        TargetId = enemy.EnemyId,
                        TargetType = "Enemy",
                        Position = enemy.Position
                    });
                }

                // TODO: Add players when player damage is implemented
                // For now, just include enemies

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting targets in radius");
            }

            return targets;
        }

        /// <summary>
        /// Calculate distance between two 3D points
        /// </summary>
        private float CalculateDistance(Vector3Data pos1, Vector3Data pos2)
        {
            float dx = pos1.X - pos2.X;
            float dy = pos1.Y - pos2.Y;
            float dz = pos1.Z - pos2.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Calculate area damage based on distance from explosion center
        /// </summary>
        private float CalculateAreaDamage(float baseDamage, float distanceFromCenter, float explosionRadius)
        {
            if (distanceFromCenter > explosionRadius) return 0f;

            // Linear damage falloff from center to edge
            float damageMultiplier = 1f - (distanceFromCenter / explosionRadius);
            return baseDamage * damageMultiplier;
        }

        /// <summary>
        /// Get active grenade count for monitoring
        /// </summary>
        public int GetActiveGrenadeCount()
        {
            return _activeGrenades.Count;
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
            _logger.LogInformation("GrenadeManager disposed");
        }
    }

    /// <summary>
    /// Represents an active grenade in the world
    /// </summary>
    public class ActiveGrenade
    {
        public string GrenadeId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string GrenadeType { get; set; } = string.Empty;
        public Vector3Data StartPosition { get; set; } = new();
        public Vector3Data TargetPosition { get; set; } = new();
        public Vector3Data CurrentPosition { get; set; } = new();
        public DateTime ThrowTime { get; set; }
        public DateTime ExplosionTime { get; set; }
        public float ExplosionRadius { get; set; }
        public float AreaDamage { get; set; }
        public GrenadeState State { get; set; }
        public bool WarningDisplayed { get; set; }
    }

    /// <summary>
    /// Grenade state enumeration
    /// </summary>
    public enum GrenadeState
    {
        Flying,
        Landed,
        Warning,
        Exploded
    }

    /// <summary>
    /// Information about a potential damage target
    /// </summary>
    public class TargetInfo
    {
        public string TargetId { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty; // "Player" or "Enemy"
        public Vector3Data Position { get; set; } = new();
    }

    /// <summary>
    /// Grenade configuration data
    /// </summary>
    public class GrenadeData
    {
        public string GrenadeType { get; set; } = string.Empty;
        public float ExplosionRadius { get; set; }
        public float ExplosionDelay { get; set; }
        public float AreaDamage { get; set; }
        public float ThrowRange { get; set; }
    }
}