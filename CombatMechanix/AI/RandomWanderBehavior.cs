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
                UpdateIntervalMs = 50, // Update every 50ms for smooth movement
                MovementSpeed = 1.2f,   // Casual wandering speed
                DetectionRange = 15.0f, // Detection range for players
                CustomParameters = new Dictionary<string, object>
                {
                    { "WanderRadius", 8.0f },      // How far to wander from spawn
                    { "DirectionChangeInterval", 3.5f }, // Seconds between direction changes
                    { "PauseChance", 0.25f },      // 25% chance to pause instead of move
                    { "PauseDuration", 2.5f }      // How long to pause when stopping
                }
            };
        }
        
        public BehaviorConfig GetConfig() => _config;
        
        public void InitializeBehavior(EnemyState enemy)
        {
            var wanderRadius = (float)_config.CustomParameters["WanderRadius"];
            
            var wanderRadius = (float)_config.CustomParameters["WanderRadius"];

            // Pick an initial wander target so enemies start moving right away
            var initAngle = _random.NextDouble() * 2 * Math.PI;
            var initDist = _random.NextDouble() * wanderRadius;

            _enemyStates[enemy.EnemyId] = new WanderState
            {
                SpawnPosition = new Vector3Data
                {
                    X = enemy.Position.X,
                    Y = enemy.Position.Y,
                    Z = enemy.Position.Z
                },
                CurrentTarget = new Vector3Data
                {
                    X = enemy.Position.X + (float)(Math.Cos(initAngle) * initDist),
                    Y = enemy.Position.Y,
                    Z = enemy.Position.Z + (float)(Math.Sin(initAngle) * initDist)
                },
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
            
            // Continuously update target position to track moving player
            var currentTarget = context.FindNearestPlayer(enemy.Position, _config.DetectionRange);
            if (currentTarget != null && currentTarget.PlayerId == state.ChaseTarget.PlayerId)
            {
                state.ChaseTarget = currentTarget; // Update with fresh player position
            }
            
            var targetPosition = state.ChaseTarget.Position;
            var distance = AIWorldContext.CalculateDistance(enemy.Position, targetPosition);
            
            
            // If within attack range, attack instead of moving
            if (distance <= _config.MinChaseDistance)
            {
                return await TryAttackTarget(enemy, state, context);
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

            await Task.CompletedTask;
            return false;
        }
        
        private async Task<bool> TryAttackTarget(EnemyState enemy, WanderState state, AIWorldContext context)
        {
            if (state.ChaseTarget == null) return false;
            
            // Don't attack dead players (Health <= 0)
            if (state.ChaseTarget.Health <= 0)
            {
                Console.WriteLine($"[DEBUG] Enemy {enemy.EnemyId} stopped attacking dead player {state.ChaseTarget.PlayerId} (Health={state.ChaseTarget.Health})");
                // Stop chasing dead player
                state.IsChasing = false;
                state.ChaseTarget = null;
                return false;
            }
            
            var now = context.CurrentTime;
            var timeSinceLastAttack = (now - state.LastAttackTime).TotalSeconds;
            
            
            // Check if enough time has passed since last attack
            if (timeSinceLastAttack < state.AttackCooldownSeconds)
            {
                return false; // Still on cooldown
            }
            
            // Perform attack
            state.LastAttackTime = now;
            
            // Calculate damage (server-authoritative)
            var baseDamage = CalculateEnemyDamage(enemy);
            
            // Face the target
            var targetPosition = state.ChaseTarget.Position;
            var direction = new Vector3Data
            {
                X = targetPosition.X - enemy.Position.X,
                Y = 0,
                Z = targetPosition.Z - enemy.Position.Z
            };
            
            var magnitude = (float)Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);
            if (magnitude > 0)
            {
                enemy.Rotation = (float)Math.Atan2(direction.X, direction.Z) * 180f / (float)Math.PI;
            }
            
            // Send attack message to all clients
            await SendEnemyAttackMessage(enemy, state.ChaseTarget, baseDamage, context);
            
            enemy.LastUpdate = DateTime.UtcNow;
            return true; // State changed (rotation updated)
        }
        
        private float CalculateEnemyDamage(EnemyState enemy)
        {
            // Base damage calculation based on enemy level
            var baseDamage = 10f + (enemy.Level * 5f); // Level 1 = 15 damage, Level 2 = 20 damage, etc.
            
            // Add some randomness (±20%)
            var random = new Random();
            var damageVariation = 1.0f + ((random.NextSingle() - 0.5f) * 0.4f); // 0.8 to 1.2 multiplier
            
            return baseDamage * damageVariation;
        }
        
        private async Task SendEnemyAttackMessage(EnemyState enemy, PlayerState target, float damage, AIWorldContext context)
        {
            // Create attack message for visual effects
            var attackMessage = new Models.NetworkMessages.CombatActionMessage
            {
                AttackerId = enemy.EnemyId,
                TargetId = target.PlayerId,
                AttackType = "EnemyAttack",
                Position = enemy.Position,
                Damage = damage,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            // Thread-safe health update with defense calculation (server-authoritative)
            int originalHealth, newHealth;
            float finalDamage;
            lock (target)
            {
                originalHealth = target.Health;
                
                // Calculate damage reduction based on player's total defense (equipment + base defense)
                float damageReduction = target.TotalDefensePower * 0.3f; // 30% reduction per defense point
                float maxReduction = damage * 0.8f; // Maximum 80% damage reduction
                damageReduction = Math.Min(damageReduction, maxReduction);
                
                finalDamage = Math.Max(damage * 0.1f, damage - damageReduction); // Minimum 10% damage gets through
                newHealth = Math.Max(0, originalHealth - (int)finalDamage);
                target.Health = newHealth;
                
                Console.WriteLine($"[DEBUG] Enemy {enemy.EnemyId} attacking player {target.PlayerId}: Raw damage={damage}, Defense={target.TotalDefensePower} (Base DEF {target.Defense} + Equipment {target.EquipmentDefensePower}), Reduction={damageReduction}, Final damage={finalDamage}, Health {originalHealth} -> {newHealth}");
            }
            
            // Persist health change to database immediately (server-authoritative)
            await PersistHealthChange(target.PlayerId, newHealth, context);
            
            // Update all in-memory player states to ensure consistency across AI entities
            await UpdateInMemoryPlayerHealth(target.PlayerId, newHealth, context);
            
            // Create health change message for server-authoritative damage
            var healthChangeMessage = new Models.NetworkMessages.HealthChangeMessage
            {
                PlayerId = target.PlayerId,
                HealthChange = -(int)finalDamage, // Negative for actual damage taken (after defense)
                NewHealth = newHealth,
                Source = $"Enemy attack from {enemy.EnemyName}"
            };
            
            // Send messages to appropriate recipients
            if (context.BroadcastMessage != null && context.SendToPlayer != null)
            {
                // Send attack animation/effect to all clients (for visual effects)
                await context.BroadcastMessage("CombatAction", attackMessage);
                
                // Send authoritative health change ONLY to the affected player
                await context.SendToPlayer(target.PlayerId, "HealthChange", healthChangeMessage);
            }
        }
        
        /// <summary>
        /// Persist health change to database (server-authoritative)
        /// </summary>
        private async Task PersistHealthChange(string playerId, int newHealth, AIWorldContext context)
        {
            try
            {
                // NOTE: This requires a service provider in AIWorldContext to access PlayerStatsService
                // For now, we'll add a database persistence function to AIWorldContext
                if (context.PersistPlayerHealth != null)
                {
                    await context.PersistPlayerHealth(playerId, newHealth);
                    Console.WriteLine($"[DEBUG] Persisted health change for player {playerId} to {newHealth}");
                }
                else
                {
                    Console.WriteLine($"[WARNING] Cannot persist health change - PersistPlayerHealth function not available in context");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to persist health change for player {playerId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update all in-memory player states to ensure consistency across AI entities
        /// </summary>
        private async Task UpdateInMemoryPlayerHealth(string playerId, int newHealth, AIWorldContext context)
        {
            try
            {
                if (context.UpdatePlayerHealthInMemory != null)
                {
                    await context.UpdatePlayerHealthInMemory(playerId, newHealth);
                    Console.WriteLine($"[DEBUG] Updated in-memory health for player {playerId} to {newHealth}");
                }
                else
                {
                    Console.WriteLine($"[WARNING] Cannot update in-memory player health - UpdatePlayerHealthInMemory function not available in context");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to update in-memory player health for player {playerId}: {ex.Message}");
            }
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
            public DateTime LastAttackTime { get; set; }
            public float AttackCooldownSeconds { get; set; } = 2.0f; // Attack every 2 seconds
        }
    }
}