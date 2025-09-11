using System.Text.Json.Serialization;

namespace CombatMechanix.Models
{
    public class NetworkMessages
    {
        public class PlayerMovementMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
            public Vector3Data Velocity { get; set; } = new();
            public float Rotation { get; set; }
            public long Timestamp { get; set; }
        }

        public class CombatActionMessage
        {
            public string AttackerId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public string AttackType { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
            public float Damage { get; set; }
            public long Timestamp { get; set; }
        }

        public class ChatMessage
        {
            public string SenderId { get; set; } = string.Empty;
            public string SenderName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string ChannelType { get; set; } = string.Empty;
            public string? TargetId { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class WorldUpdateMessage
        {
            public List<PlayerState> Players { get; set; } = new();
            public List<PlayerState> NearbyPlayers { get; set; } = new();
            public List<ResourceNode> Resources { get; set; } = new();
            public List<ResourceNode> NearbyResources { get; set; } = new();
            public List<CombatEffect> ActiveEffects { get; set; } = new();
            public long Timestamp { get; set; }
            public long ServerTimestamp { get; set; }
        }

        public class PlayerJoinNotification
        {
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
            public DateTime JoinTime { get; set; }
        }

        public class SystemNotification
        {
            public string Message { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }

        public class ResourceGatherMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string ResourceId { get; set; } = string.Empty;
            public string ResourceType { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
        }

        public class AuthenticationMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
        }

        public class HeartbeatMessage
        {
            public long Timestamp { get; set; }
        }

        public class AuthenticationResponseMessage
        {
            public bool Success { get; set; }
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        public class PlayerStatsUpdateMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public int Level { get; set; }
            public long Experience { get; set; }
            public int Health { get; set; }
            public int MaxHealth { get; set; }
            public int Strength { get; set; }
            public int Defense { get; set; }
            public int Speed { get; set; }
            public long ExperienceToNextLevel { get; set; }
            public int Gold { get; set; } = 100;
        }

        public class ExperienceGainMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public long ExperienceGained { get; set; }
            public string Source { get; set; } = string.Empty; // "Combat", "Quest", "Resource", etc.
        }

        public class LevelUpMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public int NewLevel { get; set; }
            public int StatPointsGained { get; set; }
            public PlayerStatsUpdateMessage NewStats { get; set; } = new();
        }

        public class HealthChangeMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public int NewHealth { get; set; }
            public int HealthChange { get; set; } // Positive for healing, negative for damage
            public string Source { get; set; } = string.Empty; // "Combat", "Potion", "Regeneration", etc.
        }

        public class RespawnRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
        }

        public class RespawnResponseMessage
        {
            public bool Success { get; set; }
            public string PlayerId { get; set; } = string.Empty;
            public int NewHealth { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public class LoginMessage
        {
            public string Username { get; set; } = string.Empty;
            public string ClientHashedPassword { get; set; } = string.Empty;
        }

        public class LoginResponseMessage
        {
            public bool Success { get; set; }
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
            public string? SessionToken { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public PlayerStatsUpdateMessage? PlayerStats { get; set; }
        }

        public class SessionValidationMessage
        {
            public string SessionToken { get; set; } = string.Empty;
        }

        public class LogoutMessage
        {
            public string PlayerId { get; set; } = string.Empty;
        }

        // Enemy-specific network messages
        public class EnemySpawnMessage
        {
            public List<EnemyState> Enemies { get; set; } = new();
        }

        public class EnemyUpdateMessage
        {
            public List<EnemyState> Enemies { get; set; } = new();
        }

        public class EnemyDamageMessage
        {
            public string EnemyId { get; set; } = string.Empty;
            public string AttackerId { get; set; } = string.Empty;
            public float Damage { get; set; }
            public Vector3Data Position { get; set; } = new();
            public long Timestamp { get; set; }
        }

        public class EnemyDeathMessage
        {
            public string EnemyId { get; set; } = string.Empty;
            public string KillerId { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
            public long Timestamp { get; set; }
        }

        // Inventory-specific network messages
        public class InventoryRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
        }

        public class InventoryResponseMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public List<InventoryItem> Items { get; set; } = new();
            public bool Success { get; set; } = true;
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public class InventoryUpdateMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public List<InventoryItem> UpdatedItems { get; set; } = new();
            public string UpdateType { get; set; } = string.Empty; // "Add", "Remove", "Update", "Clear"
        }

        public class ItemUseRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public int SlotIndex { get; set; }
            public string ItemType { get; set; } = string.Empty;
        }

        public class ItemUseResponseMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string ItemType { get; set; } = string.Empty;
            public int RemainingQuantity { get; set; }
        }

        public class ItemSellRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public int SlotIndex { get; set; }
            public string ItemType { get; set; } = string.Empty;
            public int Quantity { get; set; } = 1;
        }

        public class ItemSellResponseMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string ItemType { get; set; } = string.Empty;
            public int GoldEarned { get; set; }
            public int CurrentGold { get; set; } // Player's total gold after the sale
            public int RemainingQuantity { get; set; }
        }

        // Equipment Messages - Following inventory message patterns
        public class EquipmentRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
        }

        public class EquipmentResponseMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public List<EquippedItem> Items { get; set; } = new();
            public bool Success { get; set; } = true;
            public string ErrorMessage { get; set; } = string.Empty;
            
            // Calculated total stats from equipped items
            public int TotalAttackPower { get; set; } = 0;
            public int TotalDefensePower { get; set; } = 0;
        }

        public class ItemEquipRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public int SlotIndex { get; set; } // Inventory slot index of item to equip
            public string ItemType { get; set; } = string.Empty; // ItemTypeId to equip
            public string SlotType { get; set; } = string.Empty; // Target equipment slot
        }

        public class ItemEquipResponseMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public bool Success { get; set; } = false;
            public string ErrorMessage { get; set; } = string.Empty;
            public EquippedItem? EquippedItem { get; set; } // The item that was equipped
            public InventoryItem? UnequippedItem { get; set; } // Item that was replaced (if any)
        }

        public class ItemUnequipRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string SlotType { get; set; } = string.Empty; // Equipment slot to unequip
        }

        public class ItemUnequipResponseMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public bool Success { get; set; } = false;
            public string ErrorMessage { get; set; } = string.Empty;
            public InventoryItem? UnequippedItem { get; set; } // Item moved back to inventory
        }

        public class EquipmentUpdateMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public List<EquippedItem> UpdatedItems { get; set; } = new();
            public string UpdateType { get; set; } = string.Empty; // "Equip", "Unequip", "Replace"
            
            // Include calculated total stats for immediate UI update
            public int TotalAttackPower { get; set; } = 0;
            public int TotalDefensePower { get; set; } = 0;
        }

        // Loot drop system messages
        public class LootDropMessage
        {
            public string LootId { get; set; } = string.Empty; // Unique identifier for this loot drop
            public InventoryItem Item { get; set; } = new(); // The item that was dropped
            public Vector3Data Position { get; set; } = new(); // World position where loot appears
            public string SourceEnemyId { get; set; } = string.Empty; // Enemy that dropped this loot
            public long Timestamp { get; set; } // When the loot was dropped
        }

        public class LootPickupRequestMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string LootId { get; set; } = string.Empty; // ID of the loot to pick up
            public Vector3Data PlayerPosition { get; set; } = new(); // Player position for range validation
        }

        public class LootPickupResponseMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string LootId { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty; // Success message or error reason
            public InventoryItem? Item { get; set; } // The item that was picked up (null if failed)
        }

        /// <summary>
        /// Message sent to client when an attack is rejected due to timing/cooldown
        /// </summary>
        public class AttackRejectedMessage
        {
            /// <summary>
            /// Reason for rejection (e.g., "AttackSpeed", "Dead", "Range")
            /// </summary>
            public string Reason { get; set; } = string.Empty;
            
            /// <summary>
            /// Human-readable message explaining the rejection
            /// </summary>
            public string Message { get; set; } = string.Empty;
            
            /// <summary>
            /// Remaining cooldown time in milliseconds (0 if not applicable)
            /// </summary>
            public int CooldownRemainingMs { get; set; } = 0;
            
            /// <summary>
            /// Player's current attack speed (attacks per second)
            /// </summary>
            public decimal AttackSpeed { get; set; } = 1.0m;
            
            /// <summary>
            /// Timestamp when the rejection occurred
            /// </summary>
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }
    }

    public class PlayerState
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public Vector3Data Position { get; set; } = new();
        public Vector3Data Velocity { get; set; } = new();
        public float Rotation { get; set; }
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;
        public int Strength { get; set; } = 10;
        public int Defense { get; set; } = 10; // Base defense from character stats
        public int Speed { get; set; } = 10;
        public int Gold { get; set; } = 100;
        public bool IsOnline { get; set; } = true;
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        
        // Equipment-derived stat bonuses (cached from equipped items)
        public int EquipmentAttackPower { get; set; } = 0;
        public int EquipmentDefensePower { get; set; } = 0;
        public decimal EquipmentAttackSpeed { get; set; } = 1.0m; // Default 1 attack per second
        
        // Combat timing tracking
        public DateTime LastAttackTime { get; set; } = DateTime.MinValue;
        
        // Computed total values for easy access during combat
        [JsonIgnore]
        public int TotalAttackPower => Strength + EquipmentAttackPower;
        
        [JsonIgnore] 
        public int TotalDefensePower => Defense + EquipmentDefensePower;
        
        [JsonIgnore]
        public decimal TotalAttackSpeed => EquipmentAttackSpeed; // For now, only equipment affects attack speed
    }

    public class EnemyState
    {
        public string EnemyId { get; set; } = string.Empty;
        public string EnemyName { get; set; } = string.Empty;
        public string EnemyType { get; set; } = string.Empty;
        public Vector3Data Position { get; set; } = new();
        public float Rotation { get; set; }
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public int Level { get; set; } = 1;
        public float Damage { get; set; } = 10f;
        public bool IsAlive { get; set; } = true;
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class ResourceNode
    {
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public Vector3Data Position { get; set; } = new();
        public int Amount { get; set; }
        public int CurrentAmount { get; set; }
        public int MaxAmount { get; set; }
        public DateTime LastHarvested { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class MessageWrapper
    {
        public string Type { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    public class ConnectionData
    {
        public string ConnectionId { get; set; } = string.Empty;
    }

    public class Vector3Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3Data() { }

        public Vector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class CombatEffect
    {
        public string EffectId { get; set; } = string.Empty;
        public Vector3Data Position { get; set; } = new();
        public string EffectType { get; set; } = string.Empty;
        public float Duration { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class InventoryItem
    {
        public string ItemId { get; set; } = string.Empty; // Unique item instance ID
        public string ItemType { get; set; } = string.Empty; // "Sword", "Potion", "Shield", etc.
        public string ItemName { get; set; } = string.Empty; // Display name like "Iron Sword"
        public string ItemDescription { get; set; } = string.Empty; // Tooltip description
        public int Quantity { get; set; } = 1;
        public int SlotIndex { get; set; } = -1; // -1 means not equipped/placed
        public string IconName { get; set; } = string.Empty; // Icon file name for UI
        public string Rarity { get; set; } = "Common"; // "Common", "Rare", "Epic", "Legendary"
        public string ItemCategory { get; set; } = string.Empty; // "Consumable", "Weapon", "Armor", etc.
        public int Level { get; set; } = 1; // Item level
        public bool IsStackable { get; set; } = false; // Can multiple items stack in one slot
        public int MaxStackSize { get; set; } = 1; // Maximum stack size
        
        // Item stats (for equipment)
        public int AttackPower { get; set; } = 0;
        public int DefensePower { get; set; } = 0;
        public int Value { get; set; } = 0; // Gold value
        
        // Ranged weapon properties
        public string WeaponType { get; set; } = "Melee"; // "Melee" or "Ranged"
        public float WeaponRange { get; set; } = 0f; // Maximum effective range in Unity units
        public float ProjectileSpeed { get; set; } = 0f; // Projectile speed in Unity units per second
        public float Accuracy { get; set; } = 1.0f; // Base accuracy (0.0-1.0, where 1.0 = perfect)
    }

    public class EquippedItem
    {
        public string EquipmentId { get; set; } = string.Empty; // Unique equipment record ID
        public string ItemType { get; set; } = string.Empty; // "common_sword", "iron_helmet", etc.
        public string SlotType { get; set; } = string.Empty; // "Helmet", "Chest", "Legs", "Weapon", "Offhand", "Accessory"
        public string ItemName { get; set; } = string.Empty; // Display name like "Iron Sword"
        public string ItemDescription { get; set; } = string.Empty; // Tooltip description
        public string IconName { get; set; } = string.Empty; // Icon file name for UI
        public string Rarity { get; set; } = "Common"; // "Common", "Rare", "Epic", "Legendary"
        public string ItemCategory { get; set; } = string.Empty; // "Weapon", "Armor", etc.
        
        // Item stats (for equipment)
        public int AttackPower { get; set; } = 0;
        public int DefensePower { get; set; } = 0;
        public decimal AttackSpeed { get; set; } = 1.0m; // Attacks per second
        public int Value { get; set; } = 0; // Gold value
        
        // Ranged weapon properties
        public string WeaponType { get; set; } = "Melee"; // "Melee" or "Ranged"
        public float WeaponRange { get; set; } = 0f; // Maximum effective range in Unity units
        public float ProjectileSpeed { get; set; } = 0f; // Projectile speed in Unity units per second
        public float Accuracy { get; set; } = 1.0f; // Base accuracy (0.0-1.0, where 1.0 = perfect)
        
        // Equipment tracking
        public DateTime DateEquipped { get; set; } = DateTime.UtcNow;
        public DateTime DateModified { get; set; } = DateTime.UtcNow;
    }

}