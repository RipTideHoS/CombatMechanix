using System;
using System.Security.Cryptography;
using System.Text;

namespace ClientUtilities
{
    /// <summary>
    /// Client-side password hashing utility for secure authentication
    /// Performs SHA-256 hashing before sending to server (which then uses BCrypt)
    /// This prevents plaintext passwords from being sent over the network
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Hash a password using SHA-256 for client-side pre-hashing
        /// This hash will be sent to the server where it's further secured with BCrypt
        /// </summary>
        /// <param name="password">The plain text password</param>
        /// <param name="username">The username (used as salt for additional security)</param>
        /// <returns>SHA-256 hash of the password</returns>
        public static string HashPasswordForTransport(string password, string username)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Username cannot be null or empty", nameof(username));

            // Combine password and username for client-side salting
            string saltedPassword = $"{username.ToLowerInvariant()}:{password}";
            
            // Use SHA-256 to hash the salted password
            using (var sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
                
                // Convert to hex string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashedBytes.Length; i++)
                {
                    sb.Append(hashedBytes[i].ToString("x2"));
                }
                
                return sb.ToString();
            }
        }

        /// <summary>
        /// Validates that a username meets the requirements
        /// </summary>
        /// <param name="username">Username to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;
            
            if (username.Length > 50)
                return false;
            
            // Allow alphanumeric, underscore, hyphen, and period (same as server validation)
            foreach (char c in username)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                    return false;
            }
            
            return true;
        }

        /// <summary>
        /// Validates basic password requirements
        /// Currently very lenient as specified - no requirements initially
        /// </summary>
        /// <param name="password">Password to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidPassword(string password)
        {
            // Very lenient validation as requested - just check it's not empty
            return !string.IsNullOrWhiteSpace(password);
        }
    }
}