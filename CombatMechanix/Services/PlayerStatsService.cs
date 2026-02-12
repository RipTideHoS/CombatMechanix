using CombatMechanix.Data;
using CombatMechanix.Models;

namespace CombatMechanix.Services
{
    public class SkillAllocationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public PlayerStats? UpdatedStats { get; set; }
    }

    public interface IPlayerStatsService
    {
        Task<PlayerStats> GetPlayerStatsAsync(string playerId);
        Task<PlayerStats> CreatePlayerAsync(string playerId, string playerName);
        Task<PlayerStats> UpdatePlayerStatsAsync(PlayerStats playerStats);
        Task<bool> AddExperienceAsync(string playerId, long experience);
        Task<bool> UpdateHealthAsync(string playerId, int newHealth);
        Task<bool> UpdatePositionAsync(string playerId, Vector3Data position);
        Task<List<PlayerStats>> GetTopPlayersByLevelAsync(int count = 10);
        Task<SkillAllocationResult> AllocateSkillPointAsync(string playerId, string skillName, int points);
        Task<SkillAllocationResult> DeallocateSkillPointAsync(string playerId, string skillName, int points);
    }

    public class PlayerStatsService : IPlayerStatsService
    {
        private readonly IPlayerStatsRepository _repository;
        private readonly ILogger<PlayerStatsService> _logger;

        public PlayerStatsService(IPlayerStatsRepository repository, ILogger<PlayerStatsService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<PlayerStats> GetPlayerStatsAsync(string playerId)
        {
            try
            {
                var playerStats = await _repository.GetByIdAsync(playerId);
                return playerStats ?? throw new KeyNotFoundException($"Player {playerId} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting player stats for {playerId}");
                throw;
            }
        }

        public async Task<PlayerStats> CreatePlayerAsync(string playerId, string playerName)
        {
            try
            {
                // Check if player already exists
                if (await _repository.ExistsAsync(playerId))
                {
                    var existingPlayer = await _repository.GetByIdAsync(playerId);
                    if (existingPlayer != null)
                    {
                        _logger.LogInformation($"Player {playerId} already exists, returning existing stats");
                        return existingPlayer;
                    }
                }

                var newPlayer = new PlayerStats
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    Level = 1,
                    Experience = 0,
                    Health = 100,
                    MaxHealth = 100,
                    Strength = 10,
                    Defense = 10,
                    Speed = 10,
                    LastPosition = new Vector3Data(0, 1, 0), // Start at spawn point
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };

                await _repository.CreateAsync(newPlayer);
                _logger.LogInformation($"Created new player: {playerName} ({playerId})");
                return newPlayer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating player {playerId} - {playerName}");
                throw;
            }
        }

        public async Task<PlayerStats> UpdatePlayerStatsAsync(PlayerStats playerStats)
        {
            try
            {
                await _repository.UpdateAsync(playerStats);
                _logger.LogDebug($"Updated player stats for {playerStats.PlayerId}");
                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating player stats for {playerStats.PlayerId}");
                throw;
            }
        }

        public async Task<bool> AddExperienceAsync(string playerId, long experience)
        {
            _logger.LogInformation($"DEBUG: AddExperienceAsync called - PlayerId: {playerId}, Experience: {experience}");
            try
            {
                var player = await GetPlayerStatsAsync(playerId);
                if (player == null) return false;

                var oldLevel = player.Level;
                var oldExperience = player.Experience;
                player.Experience += experience;
                
                _logger.LogInformation($"DEBUG: Player state - Level: {player.Level}, OldExp: {oldExperience}, NewExp: {player.Experience}");
                _logger.LogInformation($"DEBUG: Experience needed for next level: {PlayerStats.CalculateExperienceForLevel(player.Level + 1)}");
                _logger.LogInformation($"DEBUG: ShouldLevelUp? {player.ShouldLevelUp()}");

                // Check for level ups
                var levelsGained = 0;
                while (player.ShouldLevelUp())
                {
                    _logger.LogInformation($"DEBUG: Leveling up from {player.Level} to {player.Level + 1}");
                    levelsGained += player.LevelUp();
                }

                await UpdatePlayerStatsAsync(player);

                if (levelsGained > 0)
                {
                    _logger.LogInformation($"Player {playerId} gained {levelsGained} level(s)! New level: {player.Level}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding experience to player {playerId}");
                return false;
            }
        }

        public async Task<bool> UpdateHealthAsync(string playerId, int newHealth)
        {
            try
            {
                var player = await GetPlayerStatsAsync(playerId);
                if (player == null) return false;

                // Clamp health between 0 and MaxHealth
                player.Health = Math.Max(0, Math.Min(newHealth, player.MaxHealth));
                await UpdatePlayerStatsAsync(player);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating health for player {playerId}");
                return false;
            }
        }

        public async Task<bool> UpdatePositionAsync(string playerId, Vector3Data position)
        {
            try
            {
                var player = await GetPlayerStatsAsync(playerId);
                if (player == null) return false;

                player.LastPosition = position;
                await UpdatePlayerStatsAsync(player);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating position for player {playerId}");
                return false;
            }
        }

        public async Task<List<PlayerStats>> GetTopPlayersByLevelAsync(int count = 10)
        {
            try
            {
                return await _repository.GetTopByLevelAsync(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top players by level");
                throw;
            }
        }

        private static readonly HashSet<string> ValidSkillNames = new()
        {
            "Strength", "RangedSkill", "MagicPower", "Health",
            "MovementSpeed", "AttackSpeed", "Intelligence"
        };

        public async Task<SkillAllocationResult> AllocateSkillPointAsync(string playerId, string skillName, int points)
        {
            if (points <= 0)
                return new SkillAllocationResult { Success = false, Message = "Points must be positive" };

            if (!ValidSkillNames.Contains(skillName))
                return new SkillAllocationResult { Success = false, Message = $"Invalid skill name: {skillName}" };

            try
            {
                var player = await GetPlayerStatsAsync(playerId);
                if (player.SkillPoints < points)
                    return new SkillAllocationResult { Success = false, Message = $"Not enough skill points ({player.SkillPoints} available, {points} requested)" };

                player.SkillPoints -= points;
                ApplySkillPoints(player, skillName, points);

                await _repository.UpdateSkillAllocationAsync(playerId, player);
                _logger.LogInformation($"Player {playerId} allocated {points} points to {skillName}");

                return new SkillAllocationResult { Success = true, Message = $"Allocated {points} to {skillName}", UpdatedStats = player };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error allocating skill points for {playerId}");
                return new SkillAllocationResult { Success = false, Message = "Server error during allocation" };
            }
        }

        public async Task<SkillAllocationResult> DeallocateSkillPointAsync(string playerId, string skillName, int points)
        {
            if (points <= 0)
                return new SkillAllocationResult { Success = false, Message = "Points must be positive" };

            if (!ValidSkillNames.Contains(skillName))
                return new SkillAllocationResult { Success = false, Message = $"Invalid skill name: {skillName}" };

            try
            {
                var player = await GetPlayerStatsAsync(playerId);
                int currentValue = GetSkillValue(player, skillName);

                if (currentValue < points)
                    return new SkillAllocationResult { Success = false, Message = $"Cannot remove {points} from {skillName} (only {currentValue} allocated)" };

                player.SkillPoints += points;
                ApplySkillPoints(player, skillName, -points);

                await _repository.UpdateSkillAllocationAsync(playerId, player);
                _logger.LogInformation($"Player {playerId} deallocated {points} points from {skillName}");

                return new SkillAllocationResult { Success = true, Message = $"Removed {points} from {skillName}", UpdatedStats = player };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deallocating skill points for {playerId}");
                return new SkillAllocationResult { Success = false, Message = "Server error during deallocation" };
            }
        }

        private static void ApplySkillPoints(PlayerStats player, string skillName, int delta)
        {
            switch (skillName)
            {
                case "Strength": player.SkillStrength += delta; break;
                case "RangedSkill": player.SkillRangedSkill += delta; break;
                case "MagicPower": player.SkillMagicPower += delta; break;
                case "Health": player.SkillHealth += delta; break;
                case "MovementSpeed": player.SkillMovementSpeed += delta; break;
                case "AttackSpeed": player.SkillAttackSpeed += delta; break;
                case "Intelligence": player.SkillIntelligence += delta; break;
            }
        }

        private static int GetSkillValue(PlayerStats player, string skillName)
        {
            return skillName switch
            {
                "Strength" => player.SkillStrength,
                "RangedSkill" => player.SkillRangedSkill,
                "MagicPower" => player.SkillMagicPower,
                "Health" => player.SkillHealth,
                "MovementSpeed" => player.SkillMovementSpeed,
                "AttackSpeed" => player.SkillAttackSpeed,
                "Intelligence" => player.SkillIntelligence,
                _ => 0
            };
        }
    }
}