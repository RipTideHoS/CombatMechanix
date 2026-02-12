using CombatMechanix.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace CombatMechanix.Data
{
    public interface IPlayerStatsRepository
    {
        Task<PlayerStats?> GetByIdAsync(string playerId);
        Task<PlayerStats> CreateAsync(PlayerStats playerStats);
        Task<PlayerStats> UpdateAsync(PlayerStats playerStats);
        Task<bool> DeleteAsync(string playerId);
        Task<List<PlayerStats>> GetTopByLevelAsync(int count = 10);
        Task<bool> ExistsAsync(string playerId);
        
        // Authentication methods
        Task<PlayerStats?> GetByUsernameAsync(string username);
        Task<PlayerStats?> GetBySessionTokenAsync(string sessionToken);
        Task UpdateLoginAttemptAsync(string playerId, bool success);
        Task ResetFailedAttemptsAsync(string playerId);
        Task UpdateSessionTokenAsync(string playerId, string sessionToken, DateTime expiry);
        Task ClearSessionTokenAsync(string playerId);
        
        // Health management methods
        Task<PlayerStats?> GetPlayerStatsAsync(string playerId);
        Task UpdatePlayerHealthAsync(string playerId, int newHealth);
        
        // Gold management methods
        Task UpdatePlayerGoldAsync(string playerId, int newGold);
        Task AddGoldAsync(string playerId, int goldAmount);

        // Skill tree methods
        Task UpdateSkillAllocationAsync(string playerId, PlayerStats updatedStats);
    }

    public class SqlPlayerStatsRepository : IPlayerStatsRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlPlayerStatsRepository> _logger;

        public SqlPlayerStatsRepository(IConfiguration configuration, ILogger<SqlPlayerStatsRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection string is required");
            _logger = logger;
            
            // Ensure database schema is up to date
            _ = Task.Run(EnsureDatabaseSchemaAsync);
        }
        
        /// <summary>
        /// Ensure the NextLevelExp column exists and update existing player records
        /// </summary>
        private async Task EnsureDatabaseSchemaAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if NextLevelExp column exists
                const string checkColumnSql = @"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Players' AND COLUMN_NAME = 'NextLevelExp'";

                using var checkCommand = new SqlCommand(checkColumnSql, connection);
                var columnExists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;

                if (!columnExists)
                {
                    _logger.LogInformation("Adding NextLevelExp column to Players table");

                    // Add the column
                    const string addColumnSql = @"
                        ALTER TABLE Players
                        ADD NextLevelExp BIGINT NOT NULL DEFAULT 0";

                    using var addCommand = new SqlCommand(addColumnSql, connection);
                    await addCommand.ExecuteNonQueryAsync();

                    // Update existing records with calculated NextLevelExp values
                    const string updateSql = @"
                        UPDATE Players
                        SET NextLevelExp = (Level * Level * 100) - Experience
                        WHERE NextLevelExp = 0";

                    using var updateCommand = new SqlCommand(updateSql, connection);
                    var updatedRows = await updateCommand.ExecuteNonQueryAsync();

                    _logger.LogInformation("Added NextLevelExp column and updated {UpdatedRows} player records", updatedRows);
                }

                // Skill tree columns migration
                await EnsureSkillTreeSchemaAsync(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring database schema");
            }
        }

        private async Task EnsureSkillTreeSchemaAsync(SqlConnection connection)
        {
            try
            {
                const string checkSql = @"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Players' AND COLUMN_NAME = 'SkillPoints'";

                using var checkCmd = new SqlCommand(checkSql, connection);
                var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;

                if (!exists)
                {
                    _logger.LogInformation("Adding skill tree columns to Players table");

                    const string alterSql = @"
                        ALTER TABLE Players ADD
                            SkillPoints INT NOT NULL DEFAULT 0,
                            SkillStrength INT NOT NULL DEFAULT 0,
                            SkillRangedSkill INT NOT NULL DEFAULT 0,
                            SkillMagicPower INT NOT NULL DEFAULT 0,
                            SkillHealth INT NOT NULL DEFAULT 0,
                            SkillMovementSpeed INT NOT NULL DEFAULT 0,
                            SkillAttackSpeed INT NOT NULL DEFAULT 0,
                            SkillIntelligence INT NOT NULL DEFAULT 0";

                    using var alterCmd = new SqlCommand(alterSql, connection);
                    await alterCmd.ExecuteNonQueryAsync();

                    // Grant retroactive skill points: (Level - 1) * 5 for existing players above level 1
                    const string retroSql = @"
                        UPDATE Players
                        SET SkillPoints = (Level - 1) * 5
                        WHERE Level > 1";

                    using var retroCmd = new SqlCommand(retroSql, connection);
                    var updated = await retroCmd.ExecuteNonQueryAsync();

                    _logger.LogInformation("Added skill tree columns and granted retroactive points to {Count} players", updated);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring skill tree schema");
            }
        }

        public async Task<PlayerStats?> GetByIdAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT PlayerId, PlayerName, LoginName, PasswordHash, PasswordSalt, SessionToken, SessionExpiry,
                           FailedLoginAttempts, LastLoginAttempt, Level, Experience,
                           ISNULL(NextLevelExp, 0) as NextLevelExp, Health, MaxHealth,
                           Defense, AttackDamage, MovementSpeed, ISNULL(Gold, 100) as Gold, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave,
                           ISNULL(SkillPoints, 0) as SkillPoints, ISNULL(SkillStrength, 0) as SkillStrength,
                           ISNULL(SkillRangedSkill, 0) as SkillRangedSkill, ISNULL(SkillMagicPower, 0) as SkillMagicPower,
                           ISNULL(SkillHealth, 0) as SkillHealth, ISNULL(SkillMovementSpeed, 0) as SkillMovementSpeed,
                           ISNULL(SkillAttackSpeed, 0) as SkillAttackSpeed, ISNULL(SkillIntelligence, 0) as SkillIntelligence
                    FROM Players 
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var playerStats = MapFromDataReader(reader);
                    
                    // Update last login time
                    reader.Close();
                    await UpdateLastLoginAsync(connection, playerId);
                    
                    return playerStats;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player stats for {PlayerId}", playerId);
                throw;
            }
        }

        public async Task<PlayerStats> CreateAsync(PlayerStats playerStats)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    INSERT INTO Players (PlayerId, PlayerName, Email, LoginName, PasswordHash, PasswordSalt, Level, Experience, NextLevelExp, Health, MaxHealth,
                                       Defense, AttackDamage, MovementSpeed, PositionX, PositionY, PositionZ,
                                       LastLogin, CreatedAt, LastSave, FailedLoginAttempts,
                                       SkillPoints, SkillStrength, SkillRangedSkill, SkillMagicPower,
                                       SkillHealth, SkillMovementSpeed, SkillAttackSpeed, SkillIntelligence)
                    VALUES (@PlayerId, @PlayerName, @Email, @LoginName, @PasswordHash, @PasswordSalt, @Level, @Experience, @NextLevelExp, @Health, @MaxHealth,
                            @Defense, @AttackDamage, @MovementSpeed, @PositionX, @PositionY, @PositionZ,
                            @LastLogin, @CreatedAt, @LastSave, @FailedLoginAttempts,
                            @SkillPoints, @SkillStrength, @SkillRangedSkill, @SkillMagicPower,
                            @SkillHealth, @SkillMovementSpeed, @SkillAttackSpeed, @SkillIntelligence)";

                using var command = new SqlCommand(sql, connection);
                AddPlayerStatsParameters(command, playerStats);

                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Created new player: {PlayerName} ({PlayerId})", playerStats.PlayerName, playerStats.PlayerId);

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player {PlayerId} - {PlayerName}", playerStats.PlayerId, playerStats.PlayerName);
                throw;
            }
        }

        public async Task<PlayerStats> UpdateAsync(PlayerStats playerStats)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players
                    SET PlayerName = @PlayerName, Level = @Level, Experience = @Experience, NextLevelExp = @NextLevelExp,
                        Health = @Health, MaxHealth = @MaxHealth, Defense = @Defense,
                        AttackDamage = @AttackDamage, MovementSpeed = @MovementSpeed, Gold = @Gold,
                        PositionX = @PositionX, PositionY = @PositionY, PositionZ = @PositionZ,
                        LastLogin = @LastLogin, LastSave = @LastSave,
                        SkillPoints = @SkillPoints, SkillStrength = @SkillStrength,
                        SkillRangedSkill = @SkillRangedSkill, SkillMagicPower = @SkillMagicPower,
                        SkillHealth = @SkillHealth, SkillMovementSpeed = @SkillMovementSpeed,
                        SkillAttackSpeed = @SkillAttackSpeed, SkillIntelligence = @SkillIntelligence
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                AddPlayerStatsParameters(command, playerStats);
                playerStats.UpdatedAt = DateTime.UtcNow;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException($"Player {playerStats.PlayerId} not found for update");
                }

                _logger.LogDebug("Updated player stats for {PlayerId}", playerStats.PlayerId);
                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player stats for {PlayerId}", playerStats.PlayerId);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = "DELETE FROM Players WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting player {PlayerId}", playerId);
                throw;
            }
        }

        public async Task<List<PlayerStats>> GetTopByLevelAsync(int count = 10)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT TOP (@Count) PlayerId, PlayerName, LoginName, PasswordHash, PasswordSalt, SessionToken, SessionExpiry,
                           FailedLoginAttempts, LastLoginAttempt, Level, Experience, ISNULL(NextLevelExp, 0) as NextLevelExp, Health, MaxHealth,
                           Defense, AttackDamage, MovementSpeed, ISNULL(Gold, 100) as Gold, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave,
                           ISNULL(SkillPoints, 0) as SkillPoints, ISNULL(SkillStrength, 0) as SkillStrength,
                           ISNULL(SkillRangedSkill, 0) as SkillRangedSkill, ISNULL(SkillMagicPower, 0) as SkillMagicPower,
                           ISNULL(SkillHealth, 0) as SkillHealth, ISNULL(SkillMovementSpeed, 0) as SkillMovementSpeed,
                           ISNULL(SkillAttackSpeed, 0) as SkillAttackSpeed, ISNULL(SkillIntelligence, 0) as SkillIntelligence
                    FROM Players
                    ORDER BY Level DESC, Experience DESC";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@Count", SqlDbType.Int).Value = count;

                using var reader = await command.ExecuteReaderAsync();
                var players = new List<PlayerStats>();

                while (await reader.ReadAsync())
                {
                    players.Add(MapFromDataReader(reader));
                }

                return players;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top players by level");
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = "SELECT COUNT(1) FROM Players WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;

                var count = await command.ExecuteScalarAsync();
                return Convert.ToInt32(count) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if player exists {PlayerId}", playerId);
                throw;
            }
        }

        private async Task UpdateLastLoginAsync(SqlConnection connection, string playerId)
        {
            const string sql = "UPDATE Players SET LastLogin = @LastLogin, LastSave = @LastSave WHERE PlayerId = @PlayerId";
            
            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
            command.Parameters.Add("@LastLogin", SqlDbType.DateTime2).Value = DateTime.UtcNow;
            command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

            await command.ExecuteNonQueryAsync();
        }

        private static void AddPlayerStatsParameters(SqlCommand command, PlayerStats playerStats)
        {
            command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerStats.PlayerId;
            command.Parameters.Add("@PlayerName", SqlDbType.NVarChar, 100).Value = playerStats.PlayerName;
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 255).Value = $"{playerStats.PlayerId}@combat-mechanix.local"; // Generate unique email
            command.Parameters.Add("@LoginName", SqlDbType.NVarChar, 50).Value = (object?)playerStats.LoginName ?? DBNull.Value;
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar).Value = (object?)playerStats.PasswordHash ?? DBNull.Value;
            command.Parameters.Add("@PasswordSalt", SqlDbType.NVarChar, 32).Value = (object?)playerStats.PasswordSalt ?? DBNull.Value;
            command.Parameters.Add("@Level", SqlDbType.Int).Value = playerStats.Level;
            command.Parameters.Add("@Experience", SqlDbType.Int).Value = (int)Math.Min(playerStats.Experience, int.MaxValue); // Convert long to int
            command.Parameters.Add("@NextLevelExp", SqlDbType.BigInt).Value = playerStats.NextLevelExp;
            command.Parameters.Add("@Health", SqlDbType.Float).Value = (float)playerStats.Health; // Convert int to float
            command.Parameters.Add("@MaxHealth", SqlDbType.Float).Value = (float)playerStats.MaxHealth; // Convert int to float
            command.Parameters.Add("@Defense", SqlDbType.Float).Value = (float)playerStats.Defense; // Convert int to float
            command.Parameters.Add("@AttackDamage", SqlDbType.Float).Value = (float)playerStats.Strength; // Map Strength to AttackDamage
            command.Parameters.Add("@MovementSpeed", SqlDbType.Float).Value = (float)playerStats.Speed; // Map Speed to MovementSpeed
            command.Parameters.Add("@Gold", SqlDbType.Int).Value = playerStats.Gold;
            command.Parameters.Add("@FailedLoginAttempts", SqlDbType.Int).Value = playerStats.FailedLoginAttempts;
            
            // Handle position mapping
            command.Parameters.Add("@PositionX", SqlDbType.Float).Value = playerStats.LastPosition?.X ?? 0f;
            command.Parameters.Add("@PositionY", SqlDbType.Float).Value = playerStats.LastPosition?.Y ?? 0f;
            command.Parameters.Add("@PositionZ", SqlDbType.Float).Value = playerStats.LastPosition?.Z ?? 0f;
            
            command.Parameters.Add("@LastLogin", SqlDbType.DateTime2).Value = playerStats.LastLogin;
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = playerStats.CreatedAt;
            command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow; // Map UpdatedAt to LastSave
            command.Parameters.Add("@SkillPoints", SqlDbType.Int).Value = playerStats.SkillPoints;
            command.Parameters.Add("@SkillStrength", SqlDbType.Int).Value = playerStats.SkillStrength;
            command.Parameters.Add("@SkillRangedSkill", SqlDbType.Int).Value = playerStats.SkillRangedSkill;
            command.Parameters.Add("@SkillMagicPower", SqlDbType.Int).Value = playerStats.SkillMagicPower;
            command.Parameters.Add("@SkillHealth", SqlDbType.Int).Value = playerStats.SkillHealth;
            command.Parameters.Add("@SkillMovementSpeed", SqlDbType.Int).Value = playerStats.SkillMovementSpeed;
            command.Parameters.Add("@SkillAttackSpeed", SqlDbType.Int).Value = playerStats.SkillAttackSpeed;
            command.Parameters.Add("@SkillIntelligence", SqlDbType.Int).Value = playerStats.SkillIntelligence;
        }

        private static PlayerStats MapFromDataReader(SqlDataReader reader)
        {
            // Create Vector3Data from individual position columns
            var positionX = Convert.ToSingle(reader["PositionX"]);
            var positionY = Convert.ToSingle(reader["PositionY"]);
            var positionZ = Convert.ToSingle(reader["PositionZ"]);
            var lastPosition = new Vector3Data(positionX, positionY, positionZ);

            return new PlayerStats
            {
                PlayerId = reader["PlayerId"].ToString() ?? string.Empty,
                PlayerName = reader["PlayerName"].ToString() ?? string.Empty,
                LoginName = reader["LoginName"] as string,
                PasswordHash = reader["PasswordHash"] as string,
                PasswordSalt = reader["PasswordSalt"] as string,
                SessionToken = reader["SessionToken"] as string,
                SessionExpiry = reader["SessionExpiry"] as DateTime?,
                FailedLoginAttempts = reader["FailedLoginAttempts"] != DBNull.Value ? Convert.ToInt32(reader["FailedLoginAttempts"]) : 0,
                LastLoginAttempt = reader["LastLoginAttempt"] as DateTime?,
                Level = Convert.ToInt32(reader["Level"]),
                Experience = Convert.ToInt64(reader["Experience"]), // Convert int to long
                NextLevelExp = Convert.ToInt64(reader["NextLevelExp"]),
                Health = Convert.ToInt32(reader["Health"]), // Convert float to int
                MaxHealth = Convert.ToInt32(reader["MaxHealth"]), // Convert float to int
                Strength = Convert.ToInt32(reader["AttackDamage"]), // Map AttackDamage to Strength
                Defense = Convert.ToInt32(reader["Defense"]), // Convert float to int
                Speed = Convert.ToInt32(reader["MovementSpeed"]), // Map MovementSpeed to Speed
                Gold = reader["Gold"] != DBNull.Value ? Convert.ToInt32(reader["Gold"]) : 100, // Default to 100 if null
                LastPosition = lastPosition,
                LastLogin = Convert.ToDateTime(reader["LastLogin"]),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                UpdatedAt = Convert.ToDateTime(reader["LastSave"]), // Map LastSave to UpdatedAt
                SkillPoints = reader["SkillPoints"] != DBNull.Value ? Convert.ToInt32(reader["SkillPoints"]) : 0,
                SkillStrength = reader["SkillStrength"] != DBNull.Value ? Convert.ToInt32(reader["SkillStrength"]) : 0,
                SkillRangedSkill = reader["SkillRangedSkill"] != DBNull.Value ? Convert.ToInt32(reader["SkillRangedSkill"]) : 0,
                SkillMagicPower = reader["SkillMagicPower"] != DBNull.Value ? Convert.ToInt32(reader["SkillMagicPower"]) : 0,
                SkillHealth = reader["SkillHealth"] != DBNull.Value ? Convert.ToInt32(reader["SkillHealth"]) : 0,
                SkillMovementSpeed = reader["SkillMovementSpeed"] != DBNull.Value ? Convert.ToInt32(reader["SkillMovementSpeed"]) : 0,
                SkillAttackSpeed = reader["SkillAttackSpeed"] != DBNull.Value ? Convert.ToInt32(reader["SkillAttackSpeed"]) : 0,
                SkillIntelligence = reader["SkillIntelligence"] != DBNull.Value ? Convert.ToInt32(reader["SkillIntelligence"]) : 0
            };
        }

        public async Task<PlayerStats?> GetByUsernameAsync(string username)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT PlayerId, PlayerName, LoginName, PasswordHash, PasswordSalt, SessionToken, SessionExpiry,
                           FailedLoginAttempts, LastLoginAttempt, Level, Experience, ISNULL(NextLevelExp, 0) as NextLevelExp, Health, MaxHealth,
                           Defense, AttackDamage, MovementSpeed, ISNULL(Gold, 100) as Gold, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave,
                           ISNULL(SkillPoints, 0) as SkillPoints, ISNULL(SkillStrength, 0) as SkillStrength,
                           ISNULL(SkillRangedSkill, 0) as SkillRangedSkill, ISNULL(SkillMagicPower, 0) as SkillMagicPower,
                           ISNULL(SkillHealth, 0) as SkillHealth, ISNULL(SkillMovementSpeed, 0) as SkillMovementSpeed,
                           ISNULL(SkillAttackSpeed, 0) as SkillAttackSpeed, ISNULL(SkillIntelligence, 0) as SkillIntelligence
                    FROM Players
                    WHERE LoginName = @LoginName";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@LoginName", SqlDbType.NVarChar, 50).Value = username;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapFromDataReader(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player by username: {Username}", username);
                throw;
            }
        }

        public async Task<PlayerStats?> GetBySessionTokenAsync(string sessionToken)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT PlayerId, PlayerName, LoginName, PasswordHash, PasswordSalt, SessionToken, SessionExpiry,
                           FailedLoginAttempts, LastLoginAttempt, Level, Experience, ISNULL(NextLevelExp, 0) as NextLevelExp, Health, MaxHealth,
                           Defense, AttackDamage, MovementSpeed, ISNULL(Gold, 100) as Gold, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave,
                           ISNULL(SkillPoints, 0) as SkillPoints, ISNULL(SkillStrength, 0) as SkillStrength,
                           ISNULL(SkillRangedSkill, 0) as SkillRangedSkill, ISNULL(SkillMagicPower, 0) as SkillMagicPower,
                           ISNULL(SkillHealth, 0) as SkillHealth, ISNULL(SkillMovementSpeed, 0) as SkillMovementSpeed,
                           ISNULL(SkillAttackSpeed, 0) as SkillAttackSpeed, ISNULL(SkillIntelligence, 0) as SkillIntelligence
                    FROM Players
                    WHERE SessionToken = @SessionToken";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@SessionToken", SqlDbType.NVarChar, 128).Value = sessionToken;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapFromDataReader(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player by session token");
                throw;
            }
        }

        public async Task UpdateLoginAttemptAsync(string playerId, bool success)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string sql;
                if (success)
                {
                    sql = @"
                        UPDATE Players 
                        SET FailedLoginAttempts = 0, 
                            LastLoginAttempt = @LastLoginAttempt,
                            LastLogin = @LastLogin,
                            LastSave = @LastSave
                        WHERE PlayerId = @PlayerId";
                }
                else
                {
                    sql = @"
                        UPDATE Players 
                        SET FailedLoginAttempts = FailedLoginAttempts + 1, 
                            LastLoginAttempt = @LastLoginAttempt,
                            LastSave = @LastSave
                        WHERE PlayerId = @PlayerId";
                }

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@LastLoginAttempt", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                
                if (success)
                {
                    command.Parameters.Add("@LastLogin", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                }

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating login attempt for player: {PlayerId}", playerId);
                throw;
            }
        }

        public async Task ResetFailedAttemptsAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players 
                    SET FailedLoginAttempts = 0, 
                        LastSave = @LastSave
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting failed attempts for player: {PlayerId}", playerId);
                throw;
            }
        }

        public async Task UpdateSessionTokenAsync(string playerId, string sessionToken, DateTime expiry)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players 
                    SET SessionToken = @SessionToken,
                        SessionExpiry = @SessionExpiry,
                        LastSave = @LastSave
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@SessionToken", SqlDbType.NVarChar, 128).Value = sessionToken;
                command.Parameters.Add("@SessionExpiry", SqlDbType.DateTime2).Value = expiry;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session token for player: {PlayerId}", playerId);
                throw;
            }
        }

        public async Task ClearSessionTokenAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players 
                    SET SessionToken = NULL,
                        SessionExpiry = NULL,
                        LastSave = @LastSave
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing session token for player: {PlayerId}", playerId);
                throw;
            }
        }

        public async Task<PlayerStats?> GetPlayerStatsAsync(string playerId)
        {
            return await GetByIdAsync(playerId);
        }

        public async Task UpdatePlayerHealthAsync(string playerId, int newHealth)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players 
                    SET Health = @Health,
                        LastSave = @LastSave
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@Health", SqlDbType.Float).Value = (float)newHealth;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException($"Player {playerId} not found for health update");
                }

                _logger.LogDebug("Updated health for player {PlayerId} to {Health}", playerId, newHealth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player health for {PlayerId}", playerId);
                throw;
            }
        }

        /// <summary>
        /// Update player gold to a specific value (thread-safe single column update)
        /// </summary>
        public async Task UpdatePlayerGoldAsync(string playerId, int newGold)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players 
                    SET Gold = @Gold,
                        LastSave = @LastSave
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@Gold", SqlDbType.Int).Value = newGold;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException($"Player {playerId} not found for gold update");
                }

                _logger.LogDebug("Updated gold for player {PlayerId} to {Gold}", playerId, newGold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player gold for {PlayerId}", playerId);
                throw;
            }
        }

        /// <summary>
        /// Add gold to player's current amount (atomic database operation)
        /// </summary>
        public async Task AddGoldAsync(string playerId, int goldAmount)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players 
                    SET Gold = Gold + @GoldAmount,
                        LastSave = @LastSave
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@GoldAmount", SqlDbType.Int).Value = goldAmount;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException($"Player {playerId} not found for gold addition");
                }

                _logger.LogDebug("Added {GoldAmount} gold to player {PlayerId}", goldAmount, playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding gold to player {PlayerId}", playerId);
                throw;
            }
        }

        public async Task UpdateSkillAllocationAsync(string playerId, PlayerStats updatedStats)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Players
                    SET SkillPoints = @SkillPoints,
                        SkillStrength = @SkillStrength, SkillRangedSkill = @SkillRangedSkill,
                        SkillMagicPower = @SkillMagicPower, SkillHealth = @SkillHealth,
                        SkillMovementSpeed = @SkillMovementSpeed, SkillAttackSpeed = @SkillAttackSpeed,
                        SkillIntelligence = @SkillIntelligence, LastSave = @LastSave
                    WHERE PlayerId = @PlayerId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@SkillPoints", SqlDbType.Int).Value = updatedStats.SkillPoints;
                command.Parameters.Add("@SkillStrength", SqlDbType.Int).Value = updatedStats.SkillStrength;
                command.Parameters.Add("@SkillRangedSkill", SqlDbType.Int).Value = updatedStats.SkillRangedSkill;
                command.Parameters.Add("@SkillMagicPower", SqlDbType.Int).Value = updatedStats.SkillMagicPower;
                command.Parameters.Add("@SkillHealth", SqlDbType.Int).Value = updatedStats.SkillHealth;
                command.Parameters.Add("@SkillMovementSpeed", SqlDbType.Int).Value = updatedStats.SkillMovementSpeed;
                command.Parameters.Add("@SkillAttackSpeed", SqlDbType.Int).Value = updatedStats.SkillAttackSpeed;
                command.Parameters.Add("@SkillIntelligence", SqlDbType.Int).Value = updatedStats.SkillIntelligence;
                command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException($"Player {playerId} not found for skill allocation update");
                }

                _logger.LogDebug("Updated skill allocation for player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating skill allocation for {PlayerId}", playerId);
                throw;
            }
        }
    }
}