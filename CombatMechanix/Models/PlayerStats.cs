using System;
using System.ComponentModel.DataAnnotations;

namespace CombatMechanix.Models
{
    public class PlayerStats
    {
        [Key]
        public string PlayerId { get; set; } = string.Empty;
        
        [Required]
        public string PlayerName { get; set; } = string.Empty;
        
        // Authentication fields (using existing and new columns)
        public string? LoginName { get; set; }        // New column - user's login name
        public string? PasswordHash { get; set; }     // Existing column in Players table
        public string? PasswordSalt { get; set; }     // New column - for password salt
        public string? SessionToken { get; set; }     // New column - for session management
        public DateTime? SessionExpiry { get; set; }  // New column - session expiration
        public int FailedLoginAttempts { get; set; } = 0;  // New column - security tracking
        public DateTime? LastLoginAttempt { get; set; }    // New column - security tracking
        
        // Core Statistics
        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        
        // Derived/Calculated Stats
        public int Strength { get; set; } = 10;
        public int Defense { get; set; } = 10;
        public int Speed { get; set; } = 10;
        
        // Session Information
        public Vector3Data? LastPosition { get; set; }
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Calculate required experience for next level
        public long ExperienceToNextLevel => CalculateExperienceForLevel(Level + 1) - Experience;
        
        // Static method to calculate experience required for a specific level
        public static long CalculateExperienceForLevel(int level)
        {
            if (level <= 1) return 0;
            // Simple exponential formula: level^2 * 100
            return (level - 1) * (level - 1) * 100;
        }
        
        // Check if player should level up based on current experience
        public bool ShouldLevelUp()
        {
            return Experience >= CalculateExperienceForLevel(Level + 1);
        }
        
        // Level up the player and return gained stat points
        public int LevelUp()
        {
            if (!ShouldLevelUp()) return 0;
            
            int oldLevel = Level;
            Level++;
            
            // Increase stats on level up
            MaxHealth += 10;
            Health = MaxHealth; // Full heal on level up
            Strength += 2;
            Defense += 2;
            Speed += 1;
            
            UpdatedAt = DateTime.UtcNow;
            
            return Level - oldLevel;
        }
    }
}