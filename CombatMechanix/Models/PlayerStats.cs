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
        public long NextLevelExp { get; set; } = 100; // Stored in database for quick access
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        
        // Derived/Calculated Stats
        public int Strength { get; set; } = 10;
        public int Defense { get; set; } = 10;
        public int Speed { get; set; } = 10;
        
        // Currency
        public int Gold { get; set; } = 100;

        // Skill Tree
        public int SkillPoints { get; set; } = 0;
        public int SkillStrength { get; set; } = 0;
        public int SkillRangedSkill { get; set; } = 0;
        public int SkillMagicPower { get; set; } = 0;
        public int SkillHealth { get; set; } = 0;
        public int SkillMovementSpeed { get; set; } = 0;
        public int SkillAttackSpeed { get; set; } = 0;
        public int SkillIntelligence { get; set; } = 0;
        
        // Session Information
        public Vector3Data? LastPosition { get; set; }
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Effective max health including skill bonus (not persisted, computed)
        public int EffectiveMaxHealth => MaxHealth + (SkillHealth * 10);

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
            
            // Update NextLevelExp for the new level
            NextLevelExp = CalculateExperienceForLevel(Level + 1) - Experience;
            
            // Reduced auto-stats on level up (skill points replace the rest)
            MaxHealth += 5;
            Health = EffectiveMaxHealth; // Full heal on level up (includes skill bonus)
            Strength += 1;
            Defense += 1;

            // Grant 5 skill points per level
            SkillPoints += 5;
            
            UpdatedAt = DateTime.UtcNow;
            
            return Level - oldLevel;
        }
    }
}