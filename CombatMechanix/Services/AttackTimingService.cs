using CombatMechanix.Models;

namespace CombatMechanix.Services
{
    public interface IAttackTimingService
    {
        /// <summary>
        /// Validate if a player can attack based on their attack speed and last attack time
        /// </summary>
        /// <param name="playerState">The player's current state with attack speed and last attack time</param>
        /// <param name="currentTime">Current time for validation (allows for testing with custom time)</param>
        /// <returns>AttackValidationResult with validation outcome and details</returns>
        AttackValidationResult ValidateAttackTiming(PlayerState playerState, DateTime? currentTime = null);
        
        /// <summary>
        /// Calculate the minimum time required between attacks based on attack speed
        /// </summary>
        /// <param name="attackSpeed">Attacks per second (e.g., 2.0 = 2 attacks per second)</param>
        /// <returns>TimeSpan representing the minimum cooldown between attacks</returns>
        TimeSpan CalculateAttackCooldown(decimal attackSpeed);
        
        /// <summary>
        /// Calculate when the player will be able to attack next
        /// </summary>
        /// <param name="playerState">The player's current state</param>
        /// <param name="currentTime">Current time for calculation</param>
        /// <returns>DateTime when the next attack will be allowed</returns>
        DateTime CalculateNextAttackTime(PlayerState playerState, DateTime? currentTime = null);
        
        /// <summary>
        /// Update the player's last attack time after a successful attack
        /// </summary>
        /// <param name="playerState">The player state to update</param>
        /// <param name="attackTime">The time of the attack (defaults to current time)</param>
        void RecordAttack(PlayerState playerState, DateTime? attackTime = null);
    }

    public class AttackTimingService : IAttackTimingService
    {
        private readonly ILogger<AttackTimingService> _logger;

        public AttackTimingService(ILogger<AttackTimingService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validate if a player can attack based on their attack speed and last attack time
        /// </summary>
        public AttackValidationResult ValidateAttackTiming(PlayerState playerState, DateTime? currentTime = null)
        {
            var now = currentTime ?? DateTime.UtcNow;
            
            try
            {
                // If player has never attacked, allow the attack
                if (playerState.LastAttackTime == DateTime.MinValue)
                {
                    _logger.LogDebug("Player {PlayerId} first attack - validation passed", playerState.PlayerId);
                    return new AttackValidationResult 
                    { 
                        IsValid = true, 
                        Message = "First attack allowed",
                        TimeUntilNextAttack = TimeSpan.Zero
                    };
                }

                // Calculate required cooldown based on attack speed
                var requiredCooldown = CalculateAttackCooldown(playerState.TotalAttackSpeed);
                
                // Calculate time since last attack
                var timeSinceLastAttack = now - playerState.LastAttackTime;
                
                // Check if enough time has passed
                if (timeSinceLastAttack >= requiredCooldown)
                {
                    _logger.LogDebug("Player {PlayerId} attack validation passed - {TimeSinceMs}ms since last attack (required: {RequiredMs}ms)", 
                        playerState.PlayerId, timeSinceLastAttack.TotalMilliseconds, requiredCooldown.TotalMilliseconds);
                    
                    return new AttackValidationResult 
                    { 
                        IsValid = true, 
                        Message = "Attack timing valid",
                        TimeUntilNextAttack = TimeSpan.Zero,
                        TimeSinceLastAttack = timeSinceLastAttack,
                        RequiredCooldown = requiredCooldown
                    };
                }
                else
                {
                    var remainingCooldown = requiredCooldown - timeSinceLastAttack;
                    
                    _logger.LogDebug("Player {PlayerId} attack validation failed - {TimeSinceMs}ms since last attack, {RemainingMs}ms remaining", 
                        playerState.PlayerId, timeSinceLastAttack.TotalMilliseconds, remainingCooldown.TotalMilliseconds);
                    
                    return new AttackValidationResult 
                    { 
                        IsValid = false, 
                        Message = $"Attack too soon - {remainingCooldown.TotalMilliseconds:F0}ms remaining",
                        TimeUntilNextAttack = remainingCooldown,
                        TimeSinceLastAttack = timeSinceLastAttack,
                        RequiredCooldown = requiredCooldown
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating attack timing for player {PlayerId}", playerState.PlayerId);
                return new AttackValidationResult 
                { 
                    IsValid = false, 
                    Message = "Attack validation error occurred",
                    TimeUntilNextAttack = TimeSpan.FromSeconds(1) // Safe fallback
                };
            }
        }

        /// <summary>
        /// Calculate the minimum time required between attacks based on attack speed
        /// </summary>
        public TimeSpan CalculateAttackCooldown(decimal attackSpeed)
        {
            if (attackSpeed <= 0)
            {
                _logger.LogWarning("Invalid attack speed {AttackSpeed}, using default cooldown of 1 second", attackSpeed);
                return TimeSpan.FromSeconds(1);
            }

            // Convert attacks per second to seconds per attack
            // Example: 2.0 attacks/sec = 0.5 seconds per attack = 500ms cooldown
            var secondsPerAttack = 1.0m / attackSpeed;
            var cooldownMs = (double)(secondsPerAttack * 1000);
            
            return TimeSpan.FromMilliseconds(cooldownMs);
        }

        /// <summary>
        /// Calculate when the player will be able to attack next
        /// </summary>
        public DateTime CalculateNextAttackTime(PlayerState playerState, DateTime? currentTime = null)
        {
            var now = currentTime ?? DateTime.UtcNow;
            
            // If never attacked, can attack now
            if (playerState.LastAttackTime == DateTime.MinValue)
            {
                return now;
            }

            var cooldown = CalculateAttackCooldown(playerState.TotalAttackSpeed);
            var nextAttackTime = playerState.LastAttackTime.Add(cooldown);
            
            // Return the later of now or the calculated next attack time
            return nextAttackTime > now ? nextAttackTime : now;
        }

        /// <summary>
        /// Update the player's last attack time after a successful attack
        /// </summary>
        public void RecordAttack(PlayerState playerState, DateTime? attackTime = null)
        {
            var attackTimestamp = attackTime ?? DateTime.UtcNow;
            playerState.LastAttackTime = attackTimestamp;
            
            _logger.LogDebug("Recorded attack for player {PlayerId} at {AttackTime} (speed: {AttackSpeed}/sec)", 
                playerState.PlayerId, attackTimestamp.ToString("HH:mm:ss.fff"), playerState.TotalAttackSpeed);
        }
    }

    /// <summary>
    /// Result of attack timing validation
    /// </summary>
    public class AttackValidationResult
    {
        /// <summary>
        /// Whether the attack is allowed
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Human-readable message explaining the result
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Time remaining until next attack is allowed (Zero if attack is valid now)
        /// </summary>
        public TimeSpan TimeUntilNextAttack { get; set; } = TimeSpan.Zero;
        
        /// <summary>
        /// Time that has passed since the last attack (for debugging)
        /// </summary>
        public TimeSpan? TimeSinceLastAttack { get; set; }
        
        /// <summary>
        /// Required cooldown between attacks (for debugging)
        /// </summary>
        public TimeSpan? RequiredCooldown { get; set; }
    }
}