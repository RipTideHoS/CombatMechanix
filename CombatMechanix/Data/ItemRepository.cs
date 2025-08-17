using CombatMechanix.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CombatMechanix.Data
{
    public interface IItemRepository
    {
        Task<List<InventoryItem>> GetItemsByRarityAsync(string rarity);
        Task<InventoryItem?> GetItemByIdAsync(string itemId);
        Task<InventoryItem?> GetRandomItemByRarityAsync(string rarity);
    }

    public class ItemRepository : IItemRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ItemRepository> _logger;

        public ItemRepository(IConfiguration configuration, ILogger<ItemRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException(nameof(configuration), "DefaultConnection string is required");
            _logger = logger;
        }

        /// <summary>
        /// Get all items of a specific rarity from the ItemType table
        /// </summary>
        public async Task<List<InventoryItem>> GetItemsByRarityAsync(string rarity)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize, IconPath
                    FROM ItemTypes 
                    WHERE ItemRarity = @Rarity";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@Rarity", SqlDbType.NVarChar, 50).Value = rarity;

                using var reader = await command.ExecuteReaderAsync();
                var items = new List<InventoryItem>();

                while (await reader.ReadAsync())
                {
                    items.Add(MapFromDataReader(reader));
                }

                _logger.LogDebug("Retrieved {Count} items with rarity {Rarity}", items.Count, rarity);
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting items by rarity: {Rarity}", rarity);
                throw;
            }
        }

        /// <summary>
        /// Get a specific item by its ID
        /// </summary>
        public async Task<InventoryItem?> GetItemByIdAsync(string itemId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize, IconPath
                    FROM ItemTypes 
                    WHERE ItemTypeId = @ItemTypeId";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@ItemTypeId", SqlDbType.NVarChar, 50).Value = itemId;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapFromDataReader(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item by ID: {ItemId}", itemId);
                throw;
            }
        }

        /// <summary>
        /// Get a random item of a specific rarity
        /// </summary>
        public async Task<InventoryItem?> GetRandomItemByRarityAsync(string rarity)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    SELECT TOP 1 ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize, IconPath
                    FROM ItemTypes 
                    WHERE ItemRarity = @Rarity
                    ORDER BY NEWID()"; // SQL Server random ordering

                using var command = new SqlCommand(sql, connection);
                command.Parameters.Add("@Rarity", SqlDbType.NVarChar, 50).Value = rarity;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var item = MapFromDataReader(reader);
                    _logger.LogDebug("Selected random {Rarity} item: {ItemName}", rarity, item.ItemName);
                    return item;
                }

                _logger.LogWarning("No items found with rarity: {Rarity}", rarity);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting random item by rarity: {Rarity}", rarity);
                throw;
            }
        }

        /// <summary>
        /// Map database reader to InventoryItem model
        /// </summary>
        private static InventoryItem MapFromDataReader(SqlDataReader reader)
        {
            // Handle the MaxStackSize - if null or not present, default to 1
            int maxStackSize = 1;
            if (reader["MaxStackSize"] != DBNull.Value)
            {
                maxStackSize = Convert.ToInt32(reader["MaxStackSize"]);
            }

            return new InventoryItem
            {
                ItemId = Guid.NewGuid().ToString(), // Generate unique instance ID
                ItemType = reader["ItemTypeId"].ToString() ?? string.Empty,
                ItemName = reader["ItemName"].ToString() ?? string.Empty,
                ItemDescription = reader["Description"]?.ToString() ?? string.Empty,
                Rarity = reader["ItemRarity"].ToString() ?? "Common",
                Quantity = 1, // Default quantity for loot drops
                SlotIndex = -1, // Not placed in inventory yet
                IconName = reader["IconPath"]?.ToString() ?? string.Empty,
                IsStackable = maxStackSize > 1, // Determine stackability from max stack size
                MaxStackSize = maxStackSize,
                AttackPower = 0, // Default values since ItemTypes doesn't have these
                DefensePower = 0,
                Value = 10, // Default value for now
                Level = 1 // Default level for dropped items
            };
        }
    }
}