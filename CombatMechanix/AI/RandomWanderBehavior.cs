using CombatMechanix.Models;

namespace CombatMechanix.AI
{
    /// <summary>
    /// AI behavior that makes enemies wander randomly until they detect a player
    /// When damaged or player is detected, switches to chase behavior
    /// </summary>
    public class RandomWanderBehavior : IEnemyBehavior
    {
        private readonly Random _random = new();
        private readonly BehaviorConfig _config;
        
        // Behavior state tracking for each enemy
        private readonly Dictionary<string, WanderState> _enemyStates = new();
        
        public string BehaviorType => "RandomWander";
        
        public RandomWanderBehavior(BehaviorConfig? config = null)
        {
            _config = config ?? new BehaviorConfig
            {
                UpdateIntervalMs = 300, // Update every 300ms for wandering
                MovementSpeed = 1.5f,   // Slow wandering speed
                DetectionRange = 12.0f, // Detection range for players
                CustomParameters = new Dictionary<string, object>
                {
                    { "WanderRadius", 8.0f },      // How far to wander from spawn
                    { "DirectionChangeInterval", 3.0f }, // Seconds between direction changes
                    { "PauseChance", 0.3f },       // 30% chance to pause instead of move
                    { "PauseDuration", 2.0f }      // How long to pause when stopping
                }
            };
        }
        
        public BehaviorConfig GetConfig() => _config;
        
        public void InitializeBehavior(EnemyState enemy)
        {
            var wanderRadius = (float)_config.CustomParameters["WanderRadius"];
            
            _enemyStates[enemy.EnemyId] = new WanderState
            {
                SpawnPosition = new Vector3Data 
                { 
                    X = enemy.Position.X, 
                    Y = enemy.Position.Y, 
                    Z = enemy.Position.Z 
                },
                CurrentTarget = enemy.Position,
                LastDirectionChange = DateTime.UtcNow,
                IsChasing = false,
                IsPaused = false,
                PauseEndTime = DateTime.UtcNow
            };
        }
        
        public async Task<bool> UpdateBehavior(EnemyState enemy, AIWorldContext context, float deltaTime)
        {
            if (!_enemyStates.TryGetValue(enemy.EnemyId, out var state))
            {
                InitializeBehavior(enemy);
                state = _enemyStates[enemy.EnemyId];
            }
            
            bool stateChanged = false;
            
            // Check for nearby players first
            var nearestPlayer = context.FindNearestPlayer(enemy.Position, _config.DetectionRange);
            
            if (nearestPlayer != null && !state.IsChasing)
            {
                // Switch to chase mode
                state.IsChasing = true;
                state.ChaseTarget = nearestPlayer;
                state.IsPaused = false;
                stateChanged = true;
            }
            else if (nearestPlayer == null && state.IsChasing)
            {
                // Lost player, return to wandering
                state.IsChasing = false;
                state.ChaseTarget = null;
                state.LastDirectionChange = DateTime.UtcNow.AddSeconds(-10); // Force new direction
                stateChanged = true;
            }
            
            if (state.IsChasing && state.ChaseTarget != null)
            {
                // Chase behavior
                stateChanged |= await UpdateChaseBehavior(enemy, state, context, deltaTime);
            }
            else
            {
                // Wander behavior
                stateChanged |= await UpdateWanderBehavior(enemy, state, context, deltaTime);
            }
            
            return stateChanged;
        }
        
        private async Task<bool> UpdateChaseBehavior(EnemyState enemy, WanderState state, AIWorldContext context, float deltaTime)
        {
            if (state.ChaseTarget == null) return false;
            
            var targetPosition = state.ChaseTarget.Position;
            var distance = AIWorldContext.CalculateDistance(enemy.Position, targetPosition);
            
            // If too close, stop chasing (to avoid overlap)
            if (distance <= _config.MinChaseDistance)
            {
                return false;
            }
            
            // Move towards the player
            var direction = new Vector3Data
            {
                X = targetPosition.X - enemy.Position.X,
                Y = 0, // Keep Y level
                Z = targetPosition.Z - enemy.Position.Z
            };
            
            // Normalize direction
            var magnitude = (float)Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
            if (magnitude > 0)
            {
                direction.X /= magnitude;
                direction.Z /= magnitude;
                
                // Apply movement (faster when chasing)
                var chaseSpeed = _config.MovementSpeed * 1.5f; // 50% faster when chasing
                enemy.Position.X += direction.X * chaseSpeed * deltaTime;
                enemy.Position.Z += direction.Z * chaseSpeed * deltaTime;
                
                // Update rotation to face target
                enemy.Rotation = (float)Math.Atan2(direction.X, direction.Z) * 180f / (float)Math.PI;
                
                enemy.LastUpdate = DateTime.UtcNow;
                return true;
            }
            
            return false;
        }
        
        private async Task<bool> UpdateWanderBehavior(EnemyState enemy, WanderState state, AIWorldContext context, float deltaTime)
        {
            var now = context.CurrentTime;
            var directionChangeInterval = (float)_config.CustomParameters["DirectionChangeInterval"];
            var pauseChance = (float)_config.CustomParameters["PauseChance"];
            var pauseDuration = (float)_config.CustomParameters["PauseDuration"];
            
            // Check if still paused
            if (state.IsPaused && now < state.PauseEndTime)
            {
                return false;
            }
            else if (state.IsPaused)
            {
                state.IsPaused = false;
            }
            
            // Check if it's time to change direction or choose new action
            if ((now - state.LastDirectionChange).TotalSeconds >= directionChangeInterval)
            {
                // Random chance to pause instead of moving
                if (_random.NextDouble() < pauseChance)
                {
                    state.IsPaused = true;
                    state.PauseEndTime = now.AddSeconds(pauseDuration);
                    state.LastDirectionChange = now;
                    return false;
                }
                
                // Choose new random target within wander radius
                var wanderRadius = (float)_config.CustomParameters["WanderRadius"];
                var angle = _random.NextDouble() * 2 * Math.PI;
                var distance = _random.NextDouble() * wanderRadius;
                
                state.CurrentTarget = new Vector3Data
                {
                    X = state.SpawnPosition.X + (float)(Math.Cos(angle) * distance),
                    Y = state.SpawnPosition.Y,
                    Z = state.SpawnPosition.Z + (float)(Math.Sin(angle) * distance)
                };
                
                state.LastDirectionChange = now;
            }
            
            // Move towards current target
            var targetDistance = AIWorldContext.CalculateDistance(enemy.Position, state.CurrentTarget);
            
            if (targetDistance > 0.5f) // If not close enough to target
            {
                var direction = new Vector3Data
                {
                    X = state.CurrentTarget.X - enemy.Position.X,
                    Y = 0,
                    Z = state.CurrentTarget.Z - enemy.Position.Z
                };
                
                // Normalize and apply movement
                var magnitude = (float)Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
                if (magnitude > 0)
                {
                    direction.X /= magnitude;
                    direction.Z /= magnitude;
                    
                    enemy.Position.X += direction.X * _config.MovementSpeed * deltaTime;
                    enemy.Position.Z += direction.Z * _config.MovementSpeed * deltaTime;
                    
                    // Update rotation to face movement direction
                    enemy.Rotation = (float)Math.Atan2(direction.X, direction.Z) * 180f / (float)Math.PI;
                    
                    enemy.LastUpdate = DateTime.UtcNow;
                    return true;
                }
            }
            
            return false;
        }
        
        public async Task<bool> OnDamageTaken(EnemyState enemy, float damage, string attackerId, AIWorldContext context)
        {
            if (!_enemyStates.TryGetValue(enemy.EnemyId, out var state))
            {
                InitializeBehavior(enemy);
                state = _enemyStates[enemy.EnemyId];
            }
            
            // When damaged, immediately enter chase mode if not already chasing
            if (!state.IsChasing)
            {
                // Find the attacker or nearest player
                var attacker = context.ActivePlayers.FirstOrDefault(p => p.PlayerId == attackerId);
                var target = attacker ?? context.FindNearestPlayer(enemy.Position, _config.DetectionRange);
                
                if (target != null)
                {
                    state.IsChasing = true;
                    state.ChaseTarget = target;
                    state.IsPaused = false;
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Internal state for wandering behavior
        /// </summary>
        private class WanderState
        {
            public Vector3Data SpawnPosition { get; set; } = new();
            public Vector3Data CurrentTarget { get; set; } = new();
            public DateTime LastDirectionChange { get; set; }
            public bool IsChasing { get; set; }
            public PlayerState? ChaseTarget { get; set; }
            public bool IsPaused { get; set; }
            public DateTime PauseEndTime { get; set; }
        }
    }
}