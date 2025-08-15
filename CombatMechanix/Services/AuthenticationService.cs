using BCrypt.Net;
using System.Security.Cryptography;
using System.Text;
using CombatMechanix.Data;
using CombatMechanix.Models;

namespace CombatMechanix.Services
{
    public interface IAuthenticationService
    {
        Task<AuthenticationResult> LoginAsync(string username, string clientHashedPassword);
        Task<AuthenticationResult> ValidateSessionTokenAsync(string sessionToken);
        Task<bool> LogoutAsync(string playerId);
        Task<string> CreatePlayerWithCredentialsAsync(string username, string clientHashedPassword, string playerName);
    }

    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? SessionToken { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public PlayerStats? PlayerStats { get; set; }
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IPlayerStatsRepository _repository;
        private readonly ILogger<AuthenticationService> _logger;
        private const int MaxFailedAttempts = 5;
        private const int SessionTokenValidityMinutes = 10;

        public AuthenticationService(IPlayerStatsRepository repository, ILogger<AuthenticationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<AuthenticationResult> LoginAsync(string username, string clientHashedPassword)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(clientHashedPassword))
                {
                    return new AuthenticationResult { Success = false, ErrorMessage = "Username and password are required" };
                }

                // Sanitize username (alphanumeric + safe special chars only)
                if (!IsValidUsername(username))
                {
                    return new AuthenticationResult { Success = false, ErrorMessage = "Invalid username format" };
                }

                // Get player by username
                var player = await GetPlayerByUsernameAsync(username);
                if (player == null)
                {
                    _logger.LogWarning("Login attempt for non-existent username: {Username}", username);
                    return new AuthenticationResult { Success = false, ErrorMessage = "Invalid username or password" };
                }

                // Check for account lockout
                if (player.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    var timeSinceLastAttempt = DateTime.UtcNow - (player.LastLoginAttempt ?? DateTime.MinValue);
                    if (timeSinceLastAttempt.TotalMinutes < 15) // 15 minute lockout
                    {
                        _logger.LogWarning("Account locked for user: {Username}", username);
                        return new AuthenticationResult { Success = false, ErrorMessage = "Account temporarily locked due to failed login attempts" };
                    }
                    else
                    {
                        // Reset failed attempts after lockout period
                        await ResetFailedAttemptsAsync(player.PlayerId);
                        player.FailedLoginAttempts = 0;
                    }
                }

                // Verify password (double hash: bcrypt(client_sha256_hash))
                bool isValidPassword = false;
                if (!string.IsNullOrEmpty(player.PasswordHash))
                {
                    isValidPassword = BCrypt.Net.BCrypt.Verify(clientHashedPassword, player.PasswordHash);
                }

                // Update login attempt
                await UpdateLoginAttemptAsync(player.PlayerId, isValidPassword);

                if (!isValidPassword)
                {
                    _logger.LogWarning("Failed login attempt for user: {Username}", username);
                    return new AuthenticationResult { Success = false, ErrorMessage = "Invalid username or password" };
                }

                // Generate session token
                var sessionToken = GenerateSessionToken();
                var sessionExpiry = DateTime.UtcNow.AddMinutes(SessionTokenValidityMinutes);

                // Update player with session token
                await UpdateSessionTokenAsync(player.PlayerId, sessionToken, sessionExpiry);

                _logger.LogInformation("Successful login for user: {Username} ({PlayerId})", username, player.PlayerId);

                return new AuthenticationResult
                {
                    Success = true,
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    SessionToken = sessionToken,
                    PlayerStats = player
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", username);
                return new AuthenticationResult { Success = false, ErrorMessage = "An error occurred during login" };
            }
        }

        public async Task<AuthenticationResult> ValidateSessionTokenAsync(string sessionToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    return new AuthenticationResult { Success = false, ErrorMessage = "Session token required" };
                }

                var player = await GetPlayerBySessionTokenAsync(sessionToken);
                if (player == null)
                {
                    return new AuthenticationResult { Success = false, ErrorMessage = "Invalid session token" };
                }

                // Check if token is expired
                if (player.SessionExpiry < DateTime.UtcNow)
                {
                    _logger.LogInformation("Expired session token for player: {PlayerId}", player.PlayerId);
                    await ClearSessionTokenAsync(player.PlayerId);
                    return new AuthenticationResult { Success = false, ErrorMessage = "Session expired" };
                }

                _logger.LogDebug("Valid session token for player: {PlayerId}", player.PlayerId);

                return new AuthenticationResult
                {
                    Success = true,
                    PlayerId = player.PlayerId,
                    PlayerName = player.PlayerName,
                    SessionToken = sessionToken,
                    PlayerStats = player
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session token");
                return new AuthenticationResult { Success = false, ErrorMessage = "An error occurred during session validation" };
            }
        }

        public async Task<bool> LogoutAsync(string playerId)
        {
            try
            {
                await ClearSessionTokenAsync(playerId);
                _logger.LogInformation("Player logged out: {PlayerId}", playerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for player: {PlayerId}", playerId);
                return false;
            }
        }

        public async Task<string> CreatePlayerWithCredentialsAsync(string username, string clientHashedPassword, string playerName)
        {
            try
            {
                // Validate input
                if (!IsValidUsername(username))
                {
                    throw new ArgumentException("Invalid username format");
                }

                // Generate unique player ID
                var playerId = Guid.NewGuid().ToString();

                // Hash the client-hashed password with BCrypt
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(clientHashedPassword);

                // Create player with authentication info
                var playerStats = new PlayerStats
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    LoginName = username,
                    PasswordHash = passwordHash,
                    Level = 1,
                    Experience = 0,
                    Health = 100,
                    MaxHealth = 100,
                    Strength = 10,
                    Defense = 10,
                    Speed = 10,
                    LastPosition = new Vector3Data(0, 1, 0),
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };

                await _repository.CreateAsync(playerStats);
                _logger.LogInformation("Created new player with credentials: {Username} ({PlayerId})", username, playerId);

                return playerId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player with credentials: {Username}", username);
                throw;
            }
        }

        private static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length > 50)
                return false;

            // Allow alphanumeric, underscore, hyphen, and period
            return username.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.');
        }

        private static string GenerateSessionToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        // Repository helper methods would be implemented in the repository
        private async Task<PlayerStats?> GetPlayerByUsernameAsync(string username)
        {
            return await _repository.GetByUsernameAsync(username);
        }

        private async Task<PlayerStats?> GetPlayerBySessionTokenAsync(string sessionToken)
        {
            return await _repository.GetBySessionTokenAsync(sessionToken);
        }

        private async Task UpdateLoginAttemptAsync(string playerId, bool success)
        {
            await _repository.UpdateLoginAttemptAsync(playerId, success);
        }

        private async Task ResetFailedAttemptsAsync(string playerId)
        {
            await _repository.ResetFailedAttemptsAsync(playerId);
        }

        private async Task UpdateSessionTokenAsync(string playerId, string sessionToken, DateTime expiry)
        {
            await _repository.UpdateSessionTokenAsync(playerId, sessionToken, expiry);
        }

        private async Task ClearSessionTokenAsync(string playerId)
        {
            await _repository.ClearSessionTokenAsync(playerId);
        }
    }
}