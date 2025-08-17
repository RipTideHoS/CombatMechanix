using CombatMechanix.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CombatMechanix.Data
{
    public interface IPlayerInventoryRepository
    {
        Task<List<InventoryItem>> GetPlayerInventoryAsync(string playerId);
        Task<bool> AddItemToInventoryAsync(string playerId, InventoryItem item);
        Task<bool> UpdateItemQuantityAsync(string playerId, string itemTypeId, int newQuantity);
        Task<bool> RemoveItemFromInventoryAsync(string playerId, int slotIndex);
        Task<int> GetNextAvailableSlotAsync(string playerId);
        Task<InventoryItem?> GetItemInSlotAsync(string playerId, int slotIndex);
        Task<bool> CanStackItemAsync(string playerId, string itemTypeId, int quantity);
        Task<bool> StackItemAsync(string playerId, string itemTypeId, int quantity);
    }

    public class PlayerInventoryRepository : IPlayerInventoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PlayerInventoryRepository> _logger;

        public PlayerInventoryRepository(IConfiguration configuration, ILogger<PlayerInventoryRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection string is required");
            _logger = logger;
        }

        /// <summary>
        /// Get all inventory items for a player
        /// </summary>
        public async Task<List<InventoryItem>> GetPlayerInventoryAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT 
                        pi.InventoryId,
                        pi.ItemTypeId,
                        it.ItemName,
                        it.Description as ItemDescription,
                        it.ItemRarity as Rarity,
                        it.ItemCategory,
                        pi.Quantity,
                        pi.SlotPosition,
                        it.MaxStackSize,
                        0 as AttackPower,
                        0 as DefensePower,
                        COALESCE(it.IconPath, '') as IconName,
                        CASE WHEN it.MaxStackSize > 1 THEN 1 ELSE 0 END as IsStackable,
                        10 as Value,
                        pi.ItemLevel as Level
                    FROM PlayerInventory pi
                    INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
                    WHERE pi.PlayerId = @PlayerId
                    ORDER BY pi.SlotPosition";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;

                using var reader = await command.ExecuteReaderAsync();
                var items = new List<InventoryItem>();

                while (await reader.ReadAsync())
                {
                    items.Add(MapFromDataReader(reader));
                }

                _logger.LogDebug("Retrieved {Count} inventory items for player {PlayerId}", items.Count, playerId);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inventory for player: {PlayerId}", playerId);
                throw;
            }
        }

        /// <summary>
        /// Add a new item to player's inventory
        /// </summary>
        public async Task<bool> AddItemToInventoryAsync(string playerId, InventoryItem item)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // First try to stack with existing items if stackable
                if (item.IsStackable && await CanStackItemAsync(playerId, item.ItemType, item.Quantity))
                {
                    return await StackItemAsync(playerId, item.ItemType, item.Quantity);
                }

                // Find next available slot
                int nextSlot = await GetNextAvailableSlotAsync(playerId);
                if (nextSlot == -1)
                {
                    _logger.LogWarning("No available inventory slots for player {PlayerId}", playerId);
                    return false; // Inventory full
                }

                const string sql = @"
                    INSERT INTO PlayerInventory (PlayerId, ItemTypeId, Quantity, SlotPosition, ItemCondition, ItemLevel, AcquiredAt)
                    VALUES (@PlayerId, @ItemTypeId, @Quantity, @SlotPosition, 100.0, 1, GETUTCDATE())";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@ItemTypeId", SqlDbType.NVarChar, 50).Value = item.ItemType;
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = item.Quantity;
                command.Parameters.Add("@SlotPosition", SqlDbType.Int).Value = nextSlot;

                int result = await command.ExecuteNonQueryAsync();
                
                if (result > 0)
                {
                    _logger.LogDebug("Added item {ItemType} x{Quantity} to slot {SlotIndex} for player {PlayerId}", 
                        item.ItemType, item.Quantity, nextSlot, playerId);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to inventory for player: {PlayerId}", playerId);
                return false;
            }
        }

        /// <summary>
        /// Check if item can be stacked with existing items
        /// </summary>
        public async Task<bool> CanStackItemAsync(string playerId, string itemTypeId, int quantity)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT pi.Quantity, it.MaxStackSize
                    FROM PlayerInventory pi
                    INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
                    WHERE pi.PlayerId = @PlayerId AND pi.ItemTypeId = @ItemTypeId
                    AND pi.Quantity < it.MaxStackSize";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@ItemTypeId", SqlDbType.NVarChar, 50).Value = itemTypeId;

                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    int currentQuantity = reader.GetInt32("Quantity");
                    int maxStackSize = reader.GetInt32("MaxStackSize");
                    
                    if (currentQuantity + quantity <= maxStackSize)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if item can be stacked: {ItemTypeId}", itemTypeId);
                return false;
            }
        }

        /// <summary>
        /// Stack item with existing items in inventory
        /// </summary>
        public async Task<bool> StackItemAsync(string playerId, string itemTypeId, int quantity)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE pi SET 
                        Quantity = pi.Quantity + @Quantity,
                        LastUsed = GETUTCDATE()
                    FROM PlayerInventory pi
                    INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
                    WHERE pi.PlayerId = @PlayerId 
                    AND pi.ItemTypeId = @ItemTypeId 
                    AND pi.Quantity + @Quantity <= it.MaxStackSize";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@ItemTypeId", SqlDbType.NVarChar, 50).Value = itemTypeId;
                command.Parameters.Add("@Quantity", SqlDbType.Int).Value = quantity;

                int result = await command.ExecuteNonQueryAsync();
                
                if (result > 0)
                {
                    _logger.LogDebug("Stacked {Quantity} {ItemType} for player {PlayerId}", quantity, itemTypeId, playerId);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stacking item: {ItemTypeId}", itemTypeId);
                return false;
            }
        }

        /// <summary>
        /// Get next available inventory slot for a player
        /// </summary>
        public async Task<int> GetNextAvailableSlotAsync(string playerId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    WITH SlotNumbers AS (
                        SELECT TOP 20 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS SlotPosition
                        FROM sys.objects
                    )
                    SELECT TOP 1 s.SlotPosition
                    FROM SlotNumbers s
                    LEFT JOIN PlayerInventory pi ON pi.PlayerId = @PlayerId AND pi.SlotPosition = s.SlotPosition
                    WHERE pi.SlotPosition IS NULL
                    ORDER BY s.SlotPosition";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;

                _logger.LogDebug("DEBUG: Executing GetNextAvailableSlotAsync for player {PlayerId}", playerId);
                var result = await command.ExecuteScalarAsync();
                _logger.LogDebug("DEBUG: Query result: {Result} (type: {Type})", result, result?.GetType().Name);
                
                if (result == null)
                {
                    _logger.LogWarning("DEBUG: No available slots found for player {PlayerId}", playerId);
                    return -1;
                }
                
                int slot = Convert.ToInt32(result);
                _logger.LogDebug("DEBUG: Found available slot {Slot} for player {PlayerId}", slot, playerId);
                return slot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next available slot for player: {PlayerId}", playerId);
                return -1;
            }
        }

        /// <summary>
        /// Get item in specific inventory slot
        /// </summary>
        public async Task<InventoryItem?> GetItemInSlotAsync(string playerId, int slotIndex)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT 
                        pi.InventoryId,
                        pi.ItemTypeId,
                        it.ItemName,
                        it.Description as ItemDescription,
                        it.ItemRarity as Rarity,
                        it.ItemCategory,
                        pi.Quantity,
                        pi.SlotPosition,
                        it.MaxStackSize,
                        0 as AttackPower,
                        0 as DefensePower,
                        COALESCE(it.IconPath, '') as IconName,
                        CASE WHEN it.MaxStackSize > 1 THEN 1 ELSE 0 END as IsStackable,
                        10 as Value,
                        pi.ItemLevel as Level
                    FROM PlayerInventory pi
                    INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
                    WHERE pi.PlayerId = @PlayerId AND pi.SlotPosition = @SlotIndex";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@SlotIndex", SqlDbType.Int).Value = slotIndex;

                using var reader = await command.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    return MapFromDataReader(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item in slot {SlotIndex} for player: {PlayerId}", slotIndex, playerId);
                return null;
            }
        }

        /// <summary>
        /// Update quantity of an item in inventory
        /// </summary>
        public async Task<bool> UpdateItemQuantityAsync(string playerId, string itemTypeId, int newQuantity)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE PlayerInventory 
                    SET Quantity = @NewQuantity, LastUsed = GETUTCDATE()
                    WHERE PlayerId = @PlayerId AND ItemTypeId = @ItemTypeId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@ItemTypeId", SqlDbType.NVarChar, 50).Value = itemTypeId;
                command.Parameters.Add("@NewQuantity", SqlDbType.Int).Value = newQuantity;

                int result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item quantity for player: {PlayerId}", playerId);
                return false;
            }
        }

        /// <summary>
        /// Remove item from inventory slot
        /// </summary>
        public async Task<bool> RemoveItemFromInventoryAsync(string playerId, int slotIndex)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    DELETE FROM PlayerInventory 
                    WHERE PlayerId = @PlayerId AND SlotPosition = @SlotIndex";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@PlayerId", SqlDbType.NVarChar, 50).Value = playerId;
                command.Parameters.Add("@SlotIndex", SqlDbType.Int).Value = slotIndex;

                int result = await command.ExecuteNonQueryAsync();
                
                if (result > 0)
                {
                    _logger.LogDebug("Removed item from slot {SlotIndex} for player {PlayerId}", slotIndex, playerId);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from inventory for player: {PlayerId}", playerId);
                return false;
            }
        }

        /// <summary>
        /// Map database reader to InventoryItem model
        /// </summary>
        private static InventoryItem MapFromDataReader(SqlDataReader reader)
        {
            return new InventoryItem
            {
                ItemId = reader["InventoryId"].ToString() ?? string.Empty,
                ItemType = reader["ItemTypeId"].ToString() ?? string.Empty,
                ItemName = reader["ItemName"].ToString() ?? string.Empty,
                ItemDescription = reader["ItemDescription"]?.ToString() ?? string.Empty,
                Rarity = reader["Rarity"].ToString() ?? "Common",
                Quantity = Convert.ToInt32(reader["Quantity"]),
                SlotIndex = Convert.ToInt32(reader["SlotPosition"]),
                IconName = reader["IconName"]?.ToString() ?? string.Empty,
                IsStackable = Convert.ToBoolean(reader["IsStackable"]),
                MaxStackSize = Convert.ToInt32(reader["MaxStackSize"]),
                AttackPower = Convert.ToInt32(reader["AttackPower"]),
                DefensePower = Convert.ToInt32(reader["DefensePower"]),
                Value = Convert.ToInt32(reader["Value"]),
                Level = Convert.ToInt32(reader["Level"])
            };
        }
    }
}