using CombatMechanix.Data;
using CombatMechanix.Models;

namespace CombatMechanix.Services
{
    public class EquipmentManager
    {
        private readonly IPlayerEquipmentRepository _equipmentRepository;
        private readonly IPlayerInventoryRepository _inventoryRepository;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<EquipmentManager> _logger;
        

        // Equipment slot type mappings for validation
        private readonly Dictionary<string, List<string>> _slotItemCategories = new()
        {
            { "Helmet", new List<string> { "Helmet", "Armor" } },
            { "Chest", new List<string> { "Chest", "Armor" } },
            { "Legs", new List<string> { "Legs", "Armor" } },
            { "Weapon", new List<string> { "Weapon", "Sword", "Axe", "Bow", "Staff" } },
            { "Offhand", new List<string> { "Shield", "Offhand", "Weapon" } },
            { "Ring", new List<string> { "Ring", "Amulet", "Accessory" } },
            { "Accessory", new List<string> { "Ring", "Amulet", "Accessory" } }
        };

        public EquipmentManager(
            IPlayerEquipmentRepository equipmentRepository,
            IPlayerInventoryRepository inventoryRepository,
            IItemRepository itemRepository,
            ILogger<EquipmentManager> logger)
        {
            _equipmentRepository = equipmentRepository;
            _inventoryRepository = inventoryRepository;
            _itemRepository = itemRepository;
            _logger = logger;

            _logger.LogInformation("EquipmentManager initialized");
        }

        /// <summary>
        /// Get all equipped items for a player
        /// </summary>
        public async Task<List<EquippedItem>> GetPlayerEquipmentAsync(string playerId)
        {
            try
            {
                var equipment = await _equipmentRepository.GetPlayerEquipmentAsync(playerId);
                _logger.LogDebug("Retrieved {Count} equipped items for player {PlayerId}", equipment.Count, playerId);
                return equipment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting equipment for player {PlayerId}", playerId);
                throw;
            }
        }

        /// <summary>
        /// Equip an item from inventory to equipment slot
        /// </summary>
        public async Task<EquipResult> EquipItemAsync(string playerId, int inventorySlotIndex, string targetSlotType)
        {
            try
            {
                _logger.LogInformation("Attempting to equip item from inventory slot {SlotIndex} to {SlotType} for player {PlayerId}", 
                    inventorySlotIndex, targetSlotType, playerId);

                // 1. Get the item from inventory
                var inventoryItem = await _inventoryRepository.GetItemInSlotAsync(playerId, inventorySlotIndex);
                if (inventoryItem == null)
                {
                    return new EquipResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"No item found in inventory slot {inventorySlotIndex}" 
                    };
                }

                // DEBUG: Log inventory item details
                _logger.LogInformation("DEBUG: Inventory item in slot {SlotIndex} - ItemType: '{ItemType}', ItemName: '{ItemName}'", 
                    inventorySlotIndex, inventoryItem.ItemType ?? "NULL", inventoryItem.ItemName ?? "NULL");

                // 2. Validate the item can be equipped to this slot
                var validationResult = await ValidateItemForSlotAsync(inventoryItem.ItemType, targetSlotType);
                if (!validationResult.IsValid)
                {
                    return new EquipResult 
                    { 
                        Success = false, 
                        ErrorMessage = validationResult.ErrorMessage 
                    };
                }

                // 3. Check if slot is already occupied
                var currentEquipped = await _equipmentRepository.GetEquippedItemInSlotAsync(playerId, targetSlotType);
                InventoryItem? replacedItem = null;

                // 4. If slot occupied, unequip current item back to inventory
                if (currentEquipped != null)
                {
                    var unequipResult = await UnequipItemAsync(playerId, targetSlotType);
                    if (!unequipResult.Success)
                    {
                        return new EquipResult 
                        { 
                            Success = false, 
                            ErrorMessage = $"Failed to unequip current item: {unequipResult.ErrorMessage}" 
                        };
                    }
                    replacedItem = unequipResult.UnequippedItem;
                }

                // 5. Remove item from inventory
                var removeSuccess = await _inventoryRepository.RemoveItemFromInventoryAsync(playerId, inventorySlotIndex);
                if (!removeSuccess)
                {
                    return new EquipResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Failed to remove item from inventory" 
                    };
                }

                // 6. Add item to equipment
                var equipSuccess = await _equipmentRepository.EquipItemAsync(playerId, inventoryItem.ItemType, targetSlotType);
                if (!equipSuccess)
                {
                    // Rollback: Add item back to inventory
                    await _inventoryRepository.AddItemToInventoryAsync(playerId, inventoryItem);
                    return new EquipResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Failed to equip item" 
                    };
                }

                // 7. Get the newly equipped item details
                var equippedItem = await _equipmentRepository.GetEquippedItemInSlotAsync(playerId, targetSlotType);

                _logger.LogInformation("Successfully equipped {ItemType} to {SlotType} for player {PlayerId}", 
                    inventoryItem.ItemType, targetSlotType, playerId);


                return new EquipResult
                {
                    Success = true,
                    EquippedItem = equippedItem,
                    ReplacedItem = replacedItem
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error equipping item for player {PlayerId}", playerId);
                return new EquipResult 
                { 
                    Success = false, 
                    ErrorMessage = "Internal error occurred while equipping item" 
                };
            }
        }

        /// <summary>
        /// Unequip an item from equipment slot back to inventory
        /// </summary>
        public async Task<UnequipResult> UnequipItemAsync(string playerId, string slotType)
        {
            try
            {
                _logger.LogInformation("Attempting to unequip item from {SlotType} for player {PlayerId}", slotType, playerId);

                // 1. Get the currently equipped item
                var equippedItem = await _equipmentRepository.GetEquippedItemInSlotAsync(playerId, slotType);
                if (equippedItem == null)
                {
                    return new UnequipResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"No item equipped in {slotType} slot" 
                    };
                }

                // 2. Find an available inventory slot
                var availableSlot = await _inventoryRepository.GetNextAvailableSlotAsync(playerId);
                if (availableSlot == -1)
                {
                    return new UnequipResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Inventory is full" 
                    };
                }

                // 3. Create inventory item from equipped item
                var inventoryItem = new InventoryItem
                {
                    ItemType = equippedItem.ItemType,
                    ItemName = equippedItem.ItemName,
                    ItemDescription = equippedItem.ItemDescription,
                    Quantity = 1,
                    SlotIndex = availableSlot,
                    IconName = equippedItem.IconName,
                    Rarity = equippedItem.Rarity,
                    ItemCategory = equippedItem.ItemCategory,
                    AttackPower = equippedItem.AttackPower,
                    DefensePower = equippedItem.DefensePower,
                    Value = equippedItem.Value
                };

                // 4. Add item back to inventory
                var addSuccess = await _inventoryRepository.AddItemToInventoryAsync(playerId, inventoryItem);
                if (!addSuccess)
                {
                    return new UnequipResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Failed to add item back to inventory" 
                    };
                }

                // 5. Remove item from equipment
                var unequipSuccess = await _equipmentRepository.UnequipItemAsync(playerId, slotType);
                if (!unequipSuccess)
                {
                    // Rollback: Remove from inventory
                    await _inventoryRepository.RemoveItemFromInventoryAsync(playerId, availableSlot);
                    return new UnequipResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Failed to unequip item" 
                    };
                }

                _logger.LogInformation("Successfully unequipped {ItemType} from {SlotType} for player {PlayerId}", 
                    equippedItem.ItemType, slotType, playerId);


                return new UnequipResult
                {
                    Success = true,
                    UnequippedItem = inventoryItem
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unequipping item for player {PlayerId}", playerId);
                return new UnequipResult 
                { 
                    Success = false, 
                    ErrorMessage = "Internal error occurred while unequipping item" 
                };
            }
        }

        /// <summary>
        /// Validate that an item can be equipped to a specific slot
        /// </summary>
        private async Task<ValidationResult> ValidateItemForSlotAsync(string itemType, string slotType)
        {
            try
            {
                // Get item details from repository
                var itemDetails = await _itemRepository.GetItemByIdAsync(itemType);
                if (itemDetails == null)
                {
                    _logger.LogWarning("DEBUG: Item type '{ItemType}' not found in ItemTypes table", itemType ?? "NULL");
                    return new ValidationResult 
                    { 
                        IsValid = false, 
                        ErrorMessage = $"Item type '{itemType}' not found" 
                    };
                }

                // DEBUG: Log what we found in the database
                _logger.LogInformation("DEBUG: Found item in database - ItemType: '{ItemType}', ItemName: '{ItemName}', ItemCategory: '{ItemCategory}'", 
                    itemDetails.ItemType ?? "NULL", itemDetails.ItemName ?? "NULL", itemDetails.ItemCategory ?? "NULL");

                // Check if slot type exists
                if (!_slotItemCategories.ContainsKey(slotType))
                {
                    return new ValidationResult 
                    { 
                        IsValid = false, 
                        ErrorMessage = $"Invalid equipment slot '{slotType}'" 
                    };
                }

                // Check if item category matches slot
                var allowedCategories = _slotItemCategories[slotType];
                var itemCategory = itemDetails.ItemCategory ?? string.Empty;

                // DEBUG: Log the validation details
                _logger.LogInformation($"DEBUG VALIDATION: ItemCategory from DB: '{itemCategory}' (length: {itemCategory.Length})");
                _logger.LogInformation($"DEBUG VALIDATION: SlotType: '{slotType}', Allowed categories: [{string.Join(", ", allowedCategories.Select(c => $"'{c}'"))}]");

                // FALLBACK: If ItemCategory is null/empty, try to infer from item type name
                if (string.IsNullOrEmpty(itemCategory))
                {
                    itemCategory = InferCategoryFromItemType(itemType);
                    _logger.LogWarning($"ItemCategory was null/empty for {itemType}, inferred category: {itemCategory}");
                }

                // DEBUG: Test each allowed category individually
                _logger.LogInformation($"DEBUG VALIDATION: Testing itemCategory '{itemCategory}' against allowed categories:");
                foreach (var allowedCat in allowedCategories)
                {
                    var matches = itemCategory.Equals(allowedCat, StringComparison.OrdinalIgnoreCase);
                    _logger.LogInformation($"  - '{itemCategory}' == '{allowedCat}' ? {matches}");
                }

                var isValidCategory = allowedCategories.Any(category => 
                    itemCategory.Equals(category, StringComparison.OrdinalIgnoreCase));

                if (!isValidCategory)
                {
                    return new ValidationResult 
                    { 
                        IsValid = false, 
                        ErrorMessage = $"Cannot equip {itemCategory} items to {slotType} slot" 
                    };
                }

                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating item {ItemType} for slot {SlotType}", itemType, slotType);
                return new ValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Validation error occurred" 
                };
            }
        }

        /// <summary>
        /// Calculate equipment-derived attack power, defense power, and attack speed for a player
        /// This method does not cache - caching is handled by PlayerState in WebSocketConnectionManager
        /// </summary>
        public async Task<(int AttackPower, int DefensePower, decimal AttackSpeed)> CalculateEquipmentStatsAsync(string playerId)
        {
            try
            {
                // Get all equipped items with their stats from database
                var equippedItems = await GetPlayerEquipmentAsync(playerId);
                
                // Calculate totals from equipped items
                int totalAttackPower = equippedItems.Sum(item => item.AttackPower);
                int totalDefensePower = equippedItems.Sum(item => item.DefensePower);
                
                // Calculate attack speed - use weapon's attack speed, default to 1.0 if no weapon equipped
                decimal totalAttackSpeed = 1.0m; // Default attack speed
                var weaponItem = equippedItems.FirstOrDefault(item => 
                    item.SlotType.Equals("Weapon", StringComparison.OrdinalIgnoreCase));
                
                if (weaponItem != null)
                {
                    totalAttackSpeed = weaponItem.AttackSpeed;
                }
                
                _logger.LogInformation("Calculated equipment stats for player {PlayerId}: ATK +{Attack}, DEF +{Defense}, SPD {Speed}/sec from {ItemCount} items", 
                    playerId, totalAttackPower, totalDefensePower, totalAttackSpeed, equippedItems.Count);
                
                // Debug: Log individual item contributions
                foreach (var item in equippedItems)
                {
                    if (item.AttackPower > 0 || item.DefensePower > 0 || item.AttackSpeed != 1.0m)
                    {
                        _logger.LogDebug("  - {ItemName} ({SlotType}): ATK +{Attack}, DEF +{Defense}, SPD {Speed}", 
                            item.ItemName, item.SlotType, item.AttackPower, item.DefensePower, item.AttackSpeed);
                    }
                }
                
                return (totalAttackPower, totalDefensePower, totalAttackSpeed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating equipment stats for player {PlayerId}", playerId);
                return (0, 0, 1.0m); // Return default values on error
            }
        }

        /// <summary>
        /// Infer item category from item type name when ItemCategory is null/empty in database
        /// This is a fallback mechanism for items with missing category data
        /// </summary>
        private string InferCategoryFromItemType(string itemType)
        {
            var lowerItemType = itemType.ToLower();
            
            // Weapon patterns
            if (lowerItemType.Contains("sword") || lowerItemType.Contains("blade") || 
                lowerItemType.Contains("axe") || lowerItemType.Contains("bow") || 
                lowerItemType.Contains("staff") || lowerItemType.Contains("dagger") ||
                lowerItemType.Contains("weapon"))
            {
                return "Weapon";
            }
            
            // Armor patterns
            if (lowerItemType.Contains("helmet") || lowerItemType.Contains("hat") || 
                lowerItemType.Contains("cap"))
            {
                return "Helmet";
            }
            
            if (lowerItemType.Contains("chest") || lowerItemType.Contains("armor") || 
                lowerItemType.Contains("chestplate") || lowerItemType.Contains("tunic"))
            {
                return "Chest";
            }
            
            if (lowerItemType.Contains("legs") || lowerItemType.Contains("pants") || 
                lowerItemType.Contains("greaves") || lowerItemType.Contains("leggings"))
            {
                return "Legs";
            }
            
            // Shield patterns
            if (lowerItemType.Contains("shield"))
            {
                return "Shield";
            }
            
            // Accessory patterns
            if (lowerItemType.Contains("ring") || lowerItemType.Contains("amulet") || 
                lowerItemType.Contains("accessory") || lowerItemType.Contains("necklace"))
            {
                return "Ring"; // Using Ring as the primary accessory category
            }
            
            // Default fallback - if it's being equipped to a weapon slot, assume it's a weapon
            return "Weapon";
        }
    }

    // Result classes for equipment operations
    public class EquipResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public EquippedItem? EquippedItem { get; set; }
        public InventoryItem? ReplacedItem { get; set; }
    }

    public class UnequipResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public InventoryItem? UnequippedItem { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}