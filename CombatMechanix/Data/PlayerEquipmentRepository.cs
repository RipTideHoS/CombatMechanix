using CombatMechanix.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CombatMechanix.Data
{
    public interface IPlayerEquipmentRepository
    {
        Task<List<EquippedItem>> GetPlayerEquipmentAsync(string playerId);
        Task<bool> EquipItemAsync(string playerId, string itemTypeId, string slotType);
        Task<bool> UnequipItemAsync(string playerId, string slotType);
        Task<EquippedItem?> GetEquippedItemInSlotAsync(string playerId, string slotType);
        Task<bool> IsSlotOccupiedAsync(string playerId, string slotType);
        Task<bool> HasEquippedItemAsync(string playerId, string itemTypeId);
    }

    public class PlayerEquipmentRepository : IPlayerEquipmentRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PlayerEquipmentRepository> _logger;

        public PlayerEquipmentRepository(IConfiguration configuration, ILogger<PlayerEquipmentRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection string is required");
            _logger = logger;
        }

        /// <summary>
        /// Get all equipped items for a player
        /// </summary>
        public async Task<List<EquippedItem>> GetPlayerEquipmentAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT 
                        pe.EquipmentId,
                        pe.ItemTypeId,
                        pe.SlotType,
                        it.ItemName,
                        it.Description as ItemDescription,
                        it.ItemRarity as Rarity,
                        it.ItemCategory,
                        it.AttackPower,
                        it.DefensePower,
                        it.IconPath as IconName,
                        COALESCE(it.BaseValue, 10) as Value,
                        pe.DateEquipped,
                        pe.DateModified
                    FROM PlayerEquipment pe
                    INNER JOIN ItemTypes it ON pe.ItemTypeId = it.ItemTypeId
                    WHERE pe.PlayerId = @PlayerId
                    ORDER BY pe.SlotType";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;

                using var reader = await command.ExecuteReaderAsync();
                var equippedItems = new List<EquippedItem>();

                while (await reader.ReadAsync())
                {
                    equippedItems.Add(MapFromDataReader(reader));
                }

                _logger.LogDebug("Retrieved {Count} equipped items for player {PlayerId}", equippedItems.Count, playerId);
                return equippedItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving equipment for player {PlayerId}", playerId);
                throw;
            }
        }

        /// <summary>
        /// Equip an item to a specific slot
        /// </summary>
        public async Task<bool> EquipItemAsync(string playerId, string itemTypeId, string slotType)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check if slot is already occupied
                if (await IsSlotOccupiedAsync(playerId, slotType))
                {
                    _logger.LogWarning("Cannot equip item - slot {SlotType} already occupied for player {PlayerId}", slotType, playerId);
                    return false;
                }

                const string sql = @"
                    INSERT INTO PlayerEquipment (PlayerId, ItemTypeId, SlotType, DateEquipped, DateModified)
                    VALUES (@PlayerId, @ItemTypeId, @SlotType, @DateEquipped, @DateModified)";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@ItemTypeId", SqlDbType.NVarChar, 50).Value = itemTypeId;
                command.Parameters.Add("@SlotType", SqlDbType.NVarChar, 20).Value = slotType;
                command.Parameters.Add("@DateEquipped", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                command.Parameters.Add("@DateModified", SqlDbType.DateTime2).Value = DateTime.UtcNow;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                var success = rowsAffected > 0;

                if (success)
                {
                    _logger.LogInformation("Equipped item {ItemTypeId} to {SlotType} for player {PlayerId}", itemTypeId, slotType, playerId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error equipping item {ItemTypeId} to {SlotType} for player {PlayerId}", itemTypeId, slotType, playerId);
                throw;
            }
        }

        /// <summary>
        /// Unequip an item from a specific slot
        /// </summary>
        public async Task<bool> UnequipItemAsync(string playerId, string slotType)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    DELETE FROM PlayerEquipment 
                    WHERE PlayerId = @PlayerId AND SlotType = @SlotType";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@SlotType", SqlDbType.NVarChar, 20).Value = slotType;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                var success = rowsAffected > 0;

                if (success)
                {
                    _logger.LogInformation("Unequipped item from {SlotType} for player {PlayerId}", slotType, playerId);
                }
                else
                {
                    _logger.LogWarning("No item to unequip from {SlotType} for player {PlayerId}", slotType, playerId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unequipping item from {SlotType} for player {PlayerId}", slotType, playerId);
                throw;
            }
        }

        /// <summary>
        /// Get the equipped item in a specific slot
        /// </summary>
        public async Task<EquippedItem?> GetEquippedItemInSlotAsync(string playerId, string slotType)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT 
                        pe.EquipmentId,
                        pe.ItemTypeId,
                        pe.SlotType,
                        it.ItemName,
                        it.Description as ItemDescription,
                        it.ItemRarity as Rarity,
                        it.ItemCategory,
                        it.AttackPower,
                        it.DefensePower,
                        it.IconPath as IconName,
                        COALESCE(it.BaseValue, 10) as Value,
                        pe.DateEquipped,
                        pe.DateModified
                    FROM PlayerEquipment pe
                    INNER JOIN ItemTypes it ON pe.ItemTypeId = it.ItemTypeId
                    WHERE pe.PlayerId = @PlayerId AND pe.SlotType = @SlotType";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@SlotType", SqlDbType.NVarChar, 20).Value = slotType;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapFromDataReader(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting equipped item in {SlotType} for player {PlayerId}", slotType, playerId);
                throw;
            }
        }

        /// <summary>
        /// Check if a specific slot is occupied
        /// </summary>
        public async Task<bool> IsSlotOccupiedAsync(string playerId, string slotType)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT COUNT(1) 
                    FROM PlayerEquipment 
                    WHERE PlayerId = @PlayerId AND SlotType = @SlotType";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@SlotType", SqlDbType.NVarChar, 20).Value = slotType;

                var count = await command.ExecuteScalarAsync();
                return Convert.ToInt32(count) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if slot {SlotType} is occupied for player {PlayerId}", slotType, playerId);
                throw;
            }
        }

        /// <summary>
        /// Check if player has a specific item type equipped
        /// </summary>
        public async Task<bool> HasEquippedItemAsync(string playerId, string itemTypeId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT COUNT(1) 
                    FROM PlayerEquipment 
                    WHERE PlayerId = @PlayerId AND ItemTypeId = @ItemTypeId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@ItemTypeId", SqlDbType.NVarChar, 50).Value = itemTypeId;

                var count = await command.ExecuteScalarAsync();
                return Convert.ToInt32(count) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if player {PlayerId} has item {ItemTypeId} equipped", playerId, itemTypeId);
                throw;
            }
        }

        /// <summary>
        /// Map database reader to EquippedItem object
        /// </summary>
        private static EquippedItem MapFromDataReader(SqlDataReader reader)
        {
            return new EquippedItem
            {
                EquipmentId = reader["EquipmentId"].ToString() ?? string.Empty,
                ItemType = reader["ItemTypeId"].ToString() ?? string.Empty,
                SlotType = reader["SlotType"].ToString() ?? string.Empty,
                ItemName = reader["ItemName"].ToString() ?? string.Empty,
                ItemDescription = reader["ItemDescription"].ToString() ?? string.Empty,
                Rarity = reader["Rarity"].ToString() ?? "Common",
                ItemCategory = reader["ItemCategory"].ToString() ?? string.Empty,
                AttackPower = reader["AttackPower"] != DBNull.Value ? Convert.ToInt32(reader["AttackPower"]) : 0,
                DefensePower = reader["DefensePower"] != DBNull.Value ? Convert.ToInt32(reader["DefensePower"]) : 0,
                IconName = reader["IconName"].ToString() ?? string.Empty,
                Value = reader["Value"] != DBNull.Value ? Convert.ToInt32(reader["Value"]) : 10,
                DateEquipped = Convert.ToDateTime(reader["DateEquipped"]),
                DateModified = Convert.ToDateTime(reader["DateModified"])
            };
        }
    }
}