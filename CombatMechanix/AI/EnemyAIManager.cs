using CombatMechanix.Models;
using CombatMechanix.Services;
using System.Collections.Concurrent;

namespace CombatMechanix.AI
{
    /// <summary>
    /// Manages AI behaviors for all enemies with efficient networking and performance optimization
    /// </summary>
    public class EnemyAIManager
    {
        private readonly ILogger<EnemyAIManager> _logger;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<Task<List<EnemyState>>> _getEnemiesFunc;
        private readonly Func<Task<List<PlayerState>>> _getPlayersFunc;
        private readonly Func<string, EnemyState?> _getEnemyFunc;
        
        // Behavior registry
        private readonly Dictionary<string, IEnemyBehavior> _behaviorRegistry = new();
        
        // Enemy behavior assignments
        private readonly ConcurrentDictionary<string, string> _enemyBehaviorTypes = new();
        
        // Update timing management
        private readonly Timer _aiUpdateTimer;
        private readonly Dictionary<string, DateTime> _lastUpdateTimes = new();
        
        // Network optimization
        private readonly List<EnemyState> _pendingUpdates = new();
        private readonly object _updateLock = new object();
        
        // Performance tracking
        private DateTime _lastPerformanceLog = DateTime.UtcNow;
        private int _updateCount = 0;
        private int _networkUpdatesSent = 0;
        
        public EnemyAIManager(
            ILogger<EnemyAIManager> logger, 
            WebSocketConnectionManager connectionManager,
            IServiceProvider serviceProvider,
            Func<Task<List<EnemyState>>> getEnemiesFunc,
            Func<Task<List<PlayerState>>> getPlayersFunc,
            Func<string, EnemyState?> getEnemyFunc)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _serviceProvider = serviceProvider;
            _getEnemiesFunc = getEnemiesFunc;
            _getPlayersFunc = getPlayersFunc;
            _getEnemyFunc = getEnemyFunc;
            
            // Initialize default behaviors
            RegisterDefaultBehaviors();
            
            // Start AI update timer (50ms for responsive AI)
            _aiUpdateTimer = new Timer(UpdateAI, null, 
                TimeSpan.FromMilliseconds(50), 
                TimeSpan.FromMilliseconds(50));
            
            _logger.LogInformation("EnemyAIManager initialized with 50ms update interval");
        }
        
        /// <summary>
        /// Register default AI behaviors
        /// </summary>
        private void RegisterDefaultBehaviors()
        {
            RegisterBehavior(new RandomWanderBehavior());
            
            // Can add more behaviors here:
            // RegisterBehavior(new AggressiveChaseBehavior());
            // RegisterBehavior(new PatrolBehavior());
            // RegisterBehavior(new GuardBehavior());
            
            _logger.LogInformation($"Registered {_behaviorRegistry.Count} AI behaviors");
        }
        
        /// <summary>
        /// Register a new AI behavior type
        /// </summary>
        public void RegisterBehavior(IEnemyBehavior behavior)
        {
            _behaviorRegistry[behavior.BehaviorType] = behavior;
            _logger.LogDebug($"Registered AI behavior: {behavior.BehaviorType}");
        }
        
        /// <summary>
        /// Assign a behavior to an enemy
        /// </summary>
        public void AssignBehavior(string enemyId, string behaviorType)
        {
            if (!_behaviorRegistry.ContainsKey(behaviorType))
            {
                _logger.LogWarning($"Unknown behavior type: {behaviorType}. Using RandomWander as fallback.");
                behaviorType = "RandomWander";
            }
            
            _enemyBehaviorTypes[enemyId] = behaviorType;
            _lastUpdateTimes[enemyId] = DateTime.UtcNow;
            
            _logger.LogDebug($"Assigned behavior {behaviorType} to enemy {enemyId}");
        }
        
        /// <summary>
        /// Initialize AI for a new enemy
        /// </summary>
        public void InitializeEnemyAI(EnemyState enemy, string? behaviorType = null)
        {
            behaviorType ??= "RandomWander"; // Default behavior
            
            if (_behaviorRegistry.TryGetValue(behaviorType, out var behavior))
            {
                behavior.InitializeBehavior(enemy);
                AssignBehavior(enemy.EnemyId, behaviorType);
                
                _logger.LogInformation($"Initialized AI for enemy {enemy.EnemyId} with behavior {behaviorType}");
            }
            else
            {
                _logger.LogError($"Failed to initialize AI: Unknown behavior type {behaviorType}");
            }
        }
        
        /// <summary>
        /// Handle when an enemy takes damage (can trigger behavior changes)
        /// </summary>
        public async Task OnEnemyDamaged(EnemyState enemy, float damage, string attackerId, AIWorldContext context)
        {
            if (_enemyBehaviorTypes.TryGetValue(enemy.EnemyId, out var behaviorType) &&
                _behaviorRegistry.TryGetValue(behaviorType, out var behavior))
            {
                bool stateChanged = await behavior.OnDamageTaken(enemy, damage, attackerId, context);
                
                if (stateChanged)
                {
                    // Queue immediate update for this enemy
                    lock (_updateLock)
                    {
                        if (!_pendingUpdates.Any(e => e.EnemyId == enemy.EnemyId))
                        {
                            _pendingUpdates.Add(enemy);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Main AI update loop
        /// </summary>
        private async void UpdateAI(object? state)
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var context = await BuildWorldContext();
                var enemiesToUpdate = new List<EnemyState>();
                
                // Process each enemy based on their behavior update interval
                foreach (var kvp in _enemyBehaviorTypes.ToList())
                {
                    var enemyId = kvp.Key;
                    var behaviorType = kvp.Value;
                    
                    if (!_behaviorRegistry.TryGetValue(behaviorType, out var behavior))
                        continue;
                    
                    // Check if it's time to update this enemy
                    if (!_lastUpdateTimes.TryGetValue(enemyId, out var lastUpdate))
                    {
                        lastUpdate = currentTime.AddMinutes(-1); // Force update
                    }
                    
                    var config = behavior.GetConfig();
                    var timeSinceUpdate = (currentTime - lastUpdate).TotalMilliseconds;
                    
                    if (timeSinceUpdate >= config.UpdateIntervalMs)
                    {
                        // Get enemy state from EnemyManager (we'll need to inject this)
                        var enemy = await GetEnemyState(enemyId);
                        if (enemy?.IsAlive == true)
                        {
                            var deltaTime = (float)timeSinceUpdate / 1000f; // Convert to seconds
                            
                            bool stateChanged = await behavior.UpdateBehavior(enemy, context, deltaTime);
                            
                            if (stateChanged)
                            {
                                enemiesToUpdate.Add(enemy);
                            }
                            
                            _lastUpdateTimes[enemyId] = currentTime;
                            _updateCount++;
                        }
                    }
                }
                
                // Add any pending immediate updates
                lock (_updateLock)
                {
                    enemiesToUpdate.AddRange(_pendingUpdates);
                    _pendingUpdates.Clear();
                }
                
                // Send network updates if any enemies changed
                if (enemiesToUpdate.Count > 0)
                {
                    await SendEnemyUpdates(enemiesToUpdate);
                    _networkUpdatesSent++;
                }
                
                // Log performance metrics every 30 seconds
                if ((currentTime - _lastPerformanceLog).TotalSeconds >= 30)
                {
                    LogPerformanceMetrics();
                    _lastPerformanceLog = currentTime;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI update loop");
            }
        }
        
        /// <summary>
        /// Build world context for AI decision making
        /// </summary>
        private async Task<AIWorldContext> BuildWorldContext()
        {
            var context = new AIWorldContext
            {
                CurrentTime = DateTime.UtcNow,
                ActivePlayers = await _getPlayersFunc(),
                OtherEnemies = await _getEnemiesFunc(),
                BroadcastMessage = async (messageType, data) => 
                {
                    await _connectionManager.BroadcastToAll(messageType, data);
                },
                SendToPlayer = async (playerId, messageType, data) => 
                {
                    await _connectionManager.SendToPlayer(playerId, messageType, data);
                },
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
        /// Send optimized enemy updates to clients
        /// </summary>
        private async Task SendEnemyUpdates(List<EnemyState> enemies)
        {
            if (enemies.Count == 0) return;
            
            // Group updates by significance for network optimization
            var significantUpdates = new List<EnemyState>();
            var minorUpdates = new List<EnemyState>();
            
            foreach (var enemy in enemies)
            {
                // Check if this is a significant update (position change > 0.5 units or rotation > 15 degrees)
                var lastKnownState = GetLastNetworkState(enemy.EnemyId);
                
                if (lastKnownState == null ||
                    AIWorldContext.CalculateDistance(enemy.Position, lastKnownState.Position) > 0.1f ||
                    Math.Abs(enemy.Rotation - lastKnownState.Rotation) > 10f)
                {
                    significantUpdates.Add(enemy);
                    UpdateLastNetworkState(enemy);
                }
                else
                {
                    minorUpdates.Add(enemy);
                }
            }
            
            // Send significant updates immediately
            if (significantUpdates.Count > 0)
            {
                var updateMessage = new NetworkMessages.EnemyUpdateMessage
                {
                    Enemies = significantUpdates
                };
                
                await _connectionManager.BroadcastToAll("EnemyUpdate", updateMessage);
            }
            
            // Minor updates can be batched and sent less frequently (implemented later if needed)
        }
        
        // Cache for network state optimization
        private readonly Dictionary<string, EnemyState> _lastNetworkStates = new();
        
        private EnemyState? GetLastNetworkState(string enemyId)
        {
            _lastNetworkStates.TryGetValue(enemyId, out var state);
            return state;
        }
        
        private void UpdateLastNetworkState(EnemyState enemy)
        {
            _lastNetworkStates[enemy.EnemyId] = new EnemyState
            {
                EnemyId = enemy.EnemyId,
                Position = new Vector3Data { X = enemy.Position.X, Y = enemy.Position.Y, Z = enemy.Position.Z },
                Rotation = enemy.Rotation
            };
        }
        
        /// <summary>
        /// Log performance metrics
        /// </summary>
        private void LogPerformanceMetrics()
        {
            var activeEnemyCount = _enemyBehaviorTypes.Count;
            var updatesPerSecond = _updateCount / 30f; // Over last 30 seconds
            var networkUpdatesPerSecond = _networkUpdatesSent / 30f;
            
            _logger.LogInformation($"AI Performance: {activeEnemyCount} enemies, " +
                                 $"{updatesPerSecond:F1} updates/sec, " +
                                 $"{networkUpdatesPerSecond:F1} network updates/sec");
            
            _updateCount = 0;
            _networkUpdatesSent = 0;
        }
        
        /// <summary>
        /// Remove AI for an enemy (when they die permanently or are removed)
        /// </summary>
        public void RemoveEnemyAI(string enemyId)
        {
            _enemyBehaviorTypes.TryRemove(enemyId, out _);
            _lastUpdateTimes.Remove(enemyId);
            _lastNetworkStates.Remove(enemyId);
            
            lock (_updateLock)
            {
                _pendingUpdates.RemoveAll(e => e.EnemyId == enemyId);
            }
            
            _logger.LogDebug($"Removed AI for enemy {enemyId}");
        }
        
        /// <summary>
        /// Get enemy state by ID
        /// </summary>
        private async Task<EnemyState?> GetEnemyState(string enemyId)
        {
            return _getEnemyFunc(enemyId);
        }
        
        public void Dispose()
        {
            _aiUpdateTimer?.Dispose();
            _logger.LogInformation("EnemyAIManager disposed");
        }
    }
}