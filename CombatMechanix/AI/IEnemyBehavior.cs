using CombatMechanix.Models;

namespace CombatMechanix.AI
{
    /// <summary>
    /// Interface for enemy AI behaviors using the Strategy pattern
    /// Allows different enemy types to have different movement and combat behaviors
    /// </summary>
    public interface IEnemyBehavior
    {
        /// <summary>
        /// Unique identifier for this behavior type
        /// </summary>
        string BehaviorType { get; }
        
        /// <summary>
        /// Update the enemy's AI behavior
        /// </summary>
        /// <param name="enemy">The enemy to update</param>
        /// <param name="context">Current world context (players, other enemies, etc.)</param>
        /// <param name="deltaTime">Time since last update in seconds</param>
        /// <returns>True if the enemy state changed and needs network sync</returns>
        Task<bool> UpdateBehavior(EnemyState enemy, AIWorldContext context, float deltaTime);
        
        /// <summary>
        /// Handle when this enemy takes damage (can trigger behavior changes)
        /// </summary>
        /// <param name="enemy">The enemy that took damage</param>
        /// <param name="damage">Amount of damage taken</param>
        /// <param name="attackerId">ID of the attacker</param>
        /// <param name="context">Current world context</param>
        /// <returns>True if behavior state changed</returns>
        Task<bool> OnDamageTaken(EnemyState enemy, float damage, string attackerId, AIWorldContext context);
        
        /// <summary>
        /// Initialize behavior-specific data for an enemy
        /// </summary>
        /// <param name="enemy">Enemy to initialize</param>
        void InitializeBehavior(EnemyState enemy);
        
        /// <summary>
        /// Get configuration parameters for this behavior
        /// </summary>
        BehaviorConfig GetConfig();
    }
    
    /// <summary>
    /// Configuration parameters for AI behaviors
    /// </summary>
    public class BehaviorConfig
    {
        /// <summary>
        /// How often this behavior should be updated (in milliseconds)
        /// </summary>
        public int UpdateIntervalMs { get; set; } = 50;
        
        /// <summary>
        /// Movement speed multiplier for this behavior
        /// </summary>
        public float MovementSpeed { get; set; } = 2.0f;
        
        /// <summary>
        /// Maximum detection range for players
        /// </summary>
        public float DetectionRange { get; set; } = 15.0f;
        
        /// <summary>
        /// Minimum distance to maintain from target when chasing
        /// </summary>
        public float MinChaseDistance { get; set; } = 1.5f;
        
        /// <summary>
        /// Custom parameters specific to behavior implementation
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new();
    }
    
    /// <summary>
    /// World context information for AI decision making
    /// </summary>
    public class AIWorldContext
    {
        /// <summary>
        /// All active players in the world
        /// </summary>
        public List<PlayerState> ActivePlayers { get; set; } = new();
        
        /// <summary>
        /// Other enemies in the world (for group behaviors)
        /// </summary>
        public List<EnemyState> OtherEnemies { get; set; } = new();
        
        /// <summary>
        /// Current server time
        /// </summary>
        public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Function to broadcast messages to all clients
        /// </summary>
        public Func<string, object, Task>? BroadcastMessage { get; set; }
        
        /// <summary>
        /// Function to send messages to a specific player
        /// </summary>
        public Func<string, string, object, Task>? SendToPlayer { get; set; }
        
        /// <summary>
        /// Function to persist player health changes to database
        /// </summary>
        public Func<string, int, Task>? PersistPlayerHealth { get; set; }
        
        /// <summary>
        /// Function to update player health in all in-memory caches
        /// </summary>
        public Func<string, int, Task>? UpdatePlayerHealthInMemory { get; set; }
        
        /// <summary>
        /// Find the nearest player to a given position
        /// </summary>
        public PlayerState? FindNearestPlayer(Vector3Data position, float maxRange = float.MaxValue)
        {
            PlayerState? nearest = null;
            float nearestDistance = maxRange;
            
            foreach (var player in ActivePlayers)
            {
                if (!player.IsOnline) continue;
                
                // Skip dead players (Health <= 0)
                if (player.Health <= 0) continue;
                
                float distance = CalculateDistance(position, player.Position);
                if (distance < nearestDistance)
                {
                    nearest = player;
                    nearestDistance = distance;
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// Calculate distance between two positions
        /// </summary>
        public static float CalculateDistance(Vector3Data pos1, Vector3Data pos2)
        {
            float dx = pos1.X - pos2.X;
            float dy = pos1.Y - pos2.Y;
            float dz = pos1.Z - pos2.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        
        /// <summary>
        /// Get all players within range of a position
        /// </summary>
        public List<PlayerState> GetPlayersInRange(Vector3Data position, float range)
        {
            return ActivePlayers
                .Where(p => p.IsOnline && p.Health > 0 && CalculateDistance(position, p.Position) <= range)
                .ToList();
        }
    }
}