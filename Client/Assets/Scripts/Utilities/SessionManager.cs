using System;
using UnityEngine;

namespace ClientUtilities
{
    /// <summary>
    /// Manages session tokens for automatic reconnection
    /// Stores authentication state locally using Unity's PlayerPrefs
    /// </summary>
    public static class SessionManager
    {
        private const string SESSION_TOKEN_KEY = "CombatMechanix_SessionToken";
        private const string USERNAME_KEY = "CombatMechanix_Username";
        private const string PLAYER_NAME_KEY = "CombatMechanix_PlayerName";
        private const string PLAYER_ID_KEY = "CombatMechanix_PlayerId";
        private const string SESSION_EXPIRY_KEY = "CombatMechanix_SessionExpiry";

        /// <summary>
        /// Save successful login session information
        /// </summary>
        /// <param name="sessionToken">Server-provided session token</param>
        /// <param name="username">Login username</param>
        /// <param name="playerName">Display player name</param>
        /// <param name="playerId">Server player ID</param>
        /// <param name="expiryMinutes">Session validity in minutes</param>
        public static void SaveSession(string sessionToken, string username, string playerName, string playerId, int expiryMinutes = 10)
        {
            if (string.IsNullOrEmpty(sessionToken))
            {
                Debug.LogWarning("Attempted to save empty session token");
                return;
            }

            PlayerPrefs.SetString(SESSION_TOKEN_KEY, sessionToken);
            PlayerPrefs.SetString(USERNAME_KEY, username ?? string.Empty);
            PlayerPrefs.SetString(PLAYER_NAME_KEY, playerName ?? string.Empty);
            PlayerPrefs.SetString(PLAYER_ID_KEY, playerId ?? string.Empty);
            
            // Calculate expiry time
            DateTime expiryTime = DateTime.UtcNow.AddMinutes(expiryMinutes);
            PlayerPrefs.SetString(SESSION_EXPIRY_KEY, expiryTime.ToBinary().ToString());
            
            PlayerPrefs.Save();
            
            Debug.Log($"Session saved for user: {username} (expires in {expiryMinutes} minutes)");
        }

        /// <summary>
        /// Get stored session token if it's still valid
        /// </summary>
        /// <returns>Valid session token or null if expired/missing</returns>
        public static string GetValidSessionToken()
        {
            if (!HasStoredSession())
                return null;

            // Check if session is expired
            string expiryString = PlayerPrefs.GetString(SESSION_EXPIRY_KEY, string.Empty);
            if (string.IsNullOrEmpty(expiryString))
                return null;

            if (long.TryParse(expiryString, out long expiryBinary))
            {
                DateTime expiryTime = DateTime.FromBinary(expiryBinary);
                if (DateTime.UtcNow > expiryTime)
                {
                    Debug.Log("Session token expired, clearing stored session");
                    ClearSession();
                    return null;
                }
            }
            else
            {
                Debug.LogWarning("Invalid session expiry format, clearing session");
                ClearSession();
                return null;
            }

            return PlayerPrefs.GetString(SESSION_TOKEN_KEY, null);
        }

        /// <summary>
        /// Check if we have a stored session (regardless of expiry)
        /// </summary>
        /// <returns>True if session data exists</returns>
        public static bool HasStoredSession()
        {
            return PlayerPrefs.HasKey(SESSION_TOKEN_KEY) && 
                   !string.IsNullOrEmpty(PlayerPrefs.GetString(SESSION_TOKEN_KEY));
        }

        /// <summary>
        /// Get stored username for display purposes
        /// </summary>
        /// <returns>Stored username or empty string</returns>
        public static string GetStoredUsername()
        {
            return PlayerPrefs.GetString(USERNAME_KEY, string.Empty);
        }

        /// <summary>
        /// Get stored player name
        /// </summary>
        /// <returns>Stored player name or empty string</returns>
        public static string GetStoredPlayerName()
        {
            return PlayerPrefs.GetString(PLAYER_NAME_KEY, string.Empty);
        }

        /// <summary>
        /// Get stored player ID
        /// </summary>
        /// <returns>Stored player ID or empty string</returns>
        public static string GetStoredPlayerId()
        {
            return PlayerPrefs.GetString(PLAYER_ID_KEY, string.Empty);
        }

        /// <summary>
        /// Clear all stored session data
        /// </summary>
        public static void ClearSession()
        {
            PlayerPrefs.DeleteKey(SESSION_TOKEN_KEY);
            PlayerPrefs.DeleteKey(USERNAME_KEY);
            PlayerPrefs.DeleteKey(PLAYER_NAME_KEY);
            PlayerPrefs.DeleteKey(PLAYER_ID_KEY);
            PlayerPrefs.DeleteKey(SESSION_EXPIRY_KEY);
            PlayerPrefs.Save();
            
            Debug.Log("Session data cleared");
        }

        /// <summary>
        /// Check if we should attempt automatic reconnection
        /// </summary>
        /// <returns>True if we have valid credentials for reconnection</returns>
        public static bool ShouldAttemptReconnection()
        {
            string sessionToken = GetValidSessionToken();
            return !string.IsNullOrEmpty(sessionToken);
        }

        /// <summary>
        /// Get session info for display/debugging
        /// </summary>
        /// <returns>Session information string</returns>
        public static string GetSessionInfo()
        {
            if (!HasStoredSession())
                return "No session stored";

            string username = GetStoredUsername();
            string expiryString = PlayerPrefs.GetString(SESSION_EXPIRY_KEY, string.Empty);
            
            if (long.TryParse(expiryString, out long expiryBinary))
            {
                DateTime expiryTime = DateTime.FromBinary(expiryBinary);
                TimeSpan timeLeft = expiryTime - DateTime.UtcNow;
                
                if (timeLeft.TotalMinutes > 0)
                {
                    return $"Session for {username} expires in {timeLeft.TotalMinutes:F1} minutes";
                }
                else
                {
                    return $"Session for {username} expired {Math.Abs(timeLeft.TotalMinutes):F1} minutes ago";
                }
            }
            
            return $"Session for {username} (expiry unknown)";
        }
    }
}