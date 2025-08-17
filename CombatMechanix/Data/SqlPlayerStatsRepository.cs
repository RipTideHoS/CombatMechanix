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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring database schema");
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
                           Defense, AttackDamage, MovementSpeed, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave
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
                                       LastLogin, CreatedAt, LastSave, FailedLoginAttempts)
                    VALUES (@PlayerId, @PlayerName, @Email, @LoginName, @PasswordHash, @PasswordSalt, @Level, @Experience, @NextLevelExp, @Health, @MaxHealth, 
                            @Defense, @AttackDamage, @MovementSpeed, @PositionX, @PositionY, @PositionZ,
                            @LastLogin, @CreatedAt, @LastSave, @FailedLoginAttempts)";

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
                        AttackDamage = @AttackDamage, MovementSpeed = @MovementSpeed,
                        PositionX = @PositionX, PositionY = @PositionY, PositionZ = @PositionZ, 
                        LastLogin = @LastLogin, LastSave = @LastSave
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
                           Defense, AttackDamage, MovementSpeed, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave
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
            command.Parameters.Add("@FailedLoginAttempts", SqlDbType.Int).Value = playerStats.FailedLoginAttempts;
            
            // Handle position mapping
            command.Parameters.Add("@PositionX", SqlDbType.Float).Value = playerStats.LastPosition?.X ?? 0f;
            command.Parameters.Add("@PositionY", SqlDbType.Float).Value = playerStats.LastPosition?.Y ?? 0f;
            command.Parameters.Add("@PositionZ", SqlDbType.Float).Value = playerStats.LastPosition?.Z ?? 0f;
            
            command.Parameters.Add("@LastLogin", SqlDbType.DateTime2).Value = playerStats.LastLogin;
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = playerStats.CreatedAt;
            command.Parameters.Add("@LastSave", SqlDbType.DateTime2).Value = DateTime.UtcNow; // Map UpdatedAt to LastSave
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
                LastPosition = lastPosition,
                LastLogin = Convert.ToDateTime(reader["LastLogin"]),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                UpdatedAt = Convert.ToDateTime(reader["LastSave"]) // Map LastSave to UpdatedAt
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
                           Defense, AttackDamage, MovementSpeed, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave
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
                           Defense, AttackDamage, MovementSpeed, PositionX, PositionY, PositionZ,
                           LastLogin, CreatedAt, LastSave
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
    }
}