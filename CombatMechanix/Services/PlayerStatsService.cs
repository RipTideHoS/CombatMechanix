using CombatMechanix.Data;
using CombatMechanix.Models;

namespace CombatMechanix.Services
{
    public interface IPlayerStatsService
    {
        Task<PlayerStats> GetPlayerStatsAsync(string playerId);
        Task<PlayerStats> CreatePlayerAsync(string playerId, string playerName);
        Task<PlayerStats> UpdatePlayerStatsAsync(PlayerStats playerStats);
        Task<bool> AddExperienceAsync(string playerId, long experience);
        Task<bool> UpdateHealthAsync(string playerId, int newHealth);
        Task<bool> UpdatePositionAsync(string playerId, Vector3Data position);
        Task<List<PlayerStats>> GetTopPlayersByLevelAsync(int count = 10);
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
            try
            {
                var player = await GetPlayerStatsAsync(playerId);
                if (player == null) return false;

                var oldLevel = player.Level;
                player.Experience += experience;

                // Check for level ups
                var levelsGained = 0;
                while (player.ShouldLevelUp())
                {
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
    }
}