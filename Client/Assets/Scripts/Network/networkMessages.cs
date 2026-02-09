using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity client-side network message classes that match the server structure
/// but use UnityEngine.Vector3 and provide conversion utilities
/// </summary>
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
        public string TargetId { get; set; }
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

    public class AuthenticationResponseMessage
    {
        public bool Success { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class HeartbeatMessage
    {
        public long Timestamp { get; set; }
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
        public string Source { get; set; } = string.Empty;
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
        public int HealthChange { get; set; }
        public string Source { get; set; } = string.Empty;
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
        public EquippedItem EquippedItem { get; set; } = new(); // The item that was equipped
        public InventoryItem UnequippedItem { get; set; } = new(); // Item that was replaced (if any)
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
        public InventoryItem UnequippedItem { get; set; } = new(); // Item moved back to inventory
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

    public class LoginResponseMessage
    {
        public bool Success { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string SessionToken { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public PlayerStatsUpdateMessage PlayerStats { get; set; } = new();
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
        public InventoryItem Item { get; set; } = new(); // The item that was picked up (null if failed)
    }

    // Phase 1: New projectile collision system messages
    
    /// <summary>
    /// Server authorizes projectile launch (no damage predetermined)
    /// Supports both single and multi-projectile weapons
    /// </summary>
    public class ProjectileLaunchMessage
    {
        /// <summary>
        /// Unique projectile identifier for tracking (for single projectile weapons)
        /// </summary>
        public string ProjectileId { get; set; } = string.Empty;

        /// <summary>
        /// Phase 3: Array of projectile data for multi-projectile weapons
        /// If this is populated, ignore single ProjectileId and use this array instead
        /// </summary>
        public List<ProjectileData> Projectiles { get; set; } = new();

        /// <summary>
        /// Player who fired the projectile(s)
        /// </summary>
        public string ShooterId { get; set; } = string.Empty;

        /// <summary>
        /// Initial intended target (may miss and hit something else)
        /// </summary>
        public string IntendedTargetId { get; set; } = string.Empty;

        /// <summary>
        /// Launch position (base position for all projectiles)
        /// </summary>
        public Vector3Data LaunchPosition { get; set; } = new();

        /// <summary>
        /// Target position (where player aimed - center aim point)
        /// </summary>
        public Vector3Data TargetPosition { get; set; } = new();

        /// <summary>
        /// Weapon stats for projectile physics
        /// </summary>
        public ProjectileWeaponData WeaponData { get; set; } = new();

        /// <summary>
        /// Server timestamp of launch authorization
        /// </summary>
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// Phase 3: Individual projectile data for multi-projectile weapons
    /// </summary>
    [System.Serializable]
    public class ProjectileData
    {
        /// <summary>
        /// Unique identifier for this specific projectile
        /// </summary>
        public string ProjectileId { get; set; } = string.Empty;

        /// <summary>
        /// Launch position for this specific projectile (may be offset from base)
        /// </summary>
        public Vector3Data LaunchPosition { get; set; } = new();

        /// <summary>
        /// Target direction for this specific projectile (with spread applied)
        /// </summary>
        public Vector3Data TargetPosition { get; set; } = new();

        /// <summary>
        /// Individual projectile modifiers (for variation in multi-shot)
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1.0f;
        public float AccuracyMultiplier { get; set; } = 1.0f;
    }
    
    /// <summary>
    /// Client reports projectile collision
    /// </summary>
    public class ProjectileHitMessage
    {
        /// <summary>
        /// Projectile that hit something
        /// </summary>
        public string ProjectileId { get; set; } = string.Empty;
        
        /// <summary>
        /// What was hit (enemy ID, player ID, or "terrain"/"obstacle")
        /// </summary>
        public string TargetId { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of target hit
        /// </summary>
        public string TargetType { get; set; } = string.Empty; // "Enemy", "Player", "Terrain", "Obstacle"
        
        /// <summary>
        /// Exact collision position
        /// </summary>
        public Vector3Data HitPosition { get; set; } = new();
        
        /// <summary>
        /// Client timestamp when collision occurred
        /// </summary>
        public long ClientTimestamp { get; set; }
        
        /// <summary>
        /// Additional context about the collision
        /// </summary>
        public string CollisionContext { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Server confirms damage after validating projectile hit
    /// </summary>
    public class DamageConfirmationMessage
    {
        /// <summary>
        /// Projectile that caused the damage
        /// </summary>
        public string ProjectileId { get; set; } = string.Empty;
        
        /// <summary>
        /// Attacker who fired the projectile
        /// </summary>
        public string AttackerId { get; set; } = string.Empty;
        
        /// <summary>
        /// Target that took damage
        /// </summary>
        public string TargetId { get; set; } = string.Empty;
        
        /// <summary>
        /// Actual damage dealt (after defense calculations)
        /// </summary>
        public float ActualDamage { get; set; }
        
        /// <summary>
        /// Position where damage was dealt
        /// </summary>
        public Vector3Data DamagePosition { get; set; } = new();
        
        /// <summary>
        /// Type of damage/attack
        /// </summary>
        public string DamageType { get; set; } = "Projectile";
        
        /// <summary>
        /// Server timestamp of damage confirmation
        /// </summary>
        public long Timestamp { get; set; }
        
        /// <summary>
        /// Whether this was a critical hit, headshot, etc.
        /// </summary>
        public bool IsCritical { get; set; } = false;
    }
    
    /// <summary>
    /// Weapon data for projectile physics calculations
    /// </summary>
    [System.Serializable]
    public class ProjectileWeaponData
    {
        public float ProjectileSpeed { get; set; } = 20f;
        public float WeaponRange { get; set; } = 25f;
        public float Accuracy { get; set; } = 1.0f;
        public int BaseDamage { get; set; } = 10;
        public string WeaponType { get; set; } = "Ranged";
        public string WeaponName { get; set; } = string.Empty;

        // Phase 3: Multi-projectile support
        public int ProjectileCount { get; set; } = 1;  // Number of projectiles per shot
        public float SpreadAngle { get; set; } = 0f;   // Spread cone angle in degrees (0 = no spread)
        public string SpreadPattern { get; set; } = "None"; // "None", "Cone", "Horizontal", "Circle"
    }

    // Grenade system messages
    public class GrenadeThrowMessage
    {
        public string PlayerId { get; set; } = string.Empty;
        public Vector3Data ThrowPosition { get; set; } = new();
        public Vector3Data TargetPosition { get; set; } = new();
        public string GrenadeType { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    public class GrenadeSpawnMessage
    {
        public string GrenadeId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public Vector3Data StartPosition { get; set; } = new();
        public Vector3Data TargetPosition { get; set; } = new();
        public string GrenadeType { get; set; } = string.Empty;
        public float ExplosionDelay { get; set; }
        public long Timestamp { get; set; }
    }

    public class GrenadeWarningMessage
    {
        public string GrenadeId { get; set; } = string.Empty;
        public Vector3Data ExplosionPosition { get; set; } = new();
        public float ExplosionRadius { get; set; }
        public float TimeToExplosion { get; set; }
        public long Timestamp { get; set; }
    }

    public class GrenadeExplosionMessage
    {
        public string GrenadeId { get; set; } = string.Empty;
        public Vector3Data ExplosionPosition { get; set; } = new();
        public float ExplosionRadius { get; set; }
        public float Damage { get; set; }
        public List<DamageTarget> DamagedTargets { get; set; } = new();
        public long Timestamp { get; set; }
    }

    public class GrenadeErrorMessage
    {
        public string PlayerId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }

    public class GrenadeCountUpdateMessage
    {
        public string PlayerId { get; set; } = string.Empty;
        public int FragGrenades { get; set; }
        public int SmokeGrenades { get; set; }
        public int FlashGrenades { get; set; }
        public long Timestamp { get; set; }
    }

    public class DamageTarget
    {
        public string TargetId { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty; // "Player", "Enemy"
        public float DamageDealt { get; set; }
        public Vector3Data Position { get; set; } = new();
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
    public int Defense { get; set; } = 10;
    public int Speed { get; set; } = 10;
    public bool IsOnline { get; set; } = true;
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
}

[Serializable]
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

[Serializable]
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

    public Vector3Data(Vector3 vector)
    {
        X = vector.x;
        Y = vector.y;
        Z = vector.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }

    public static implicit operator Vector3Data(Vector3 vector)
    {
        return new Vector3Data(vector);
    }

    public static implicit operator Vector3(Vector3Data data)
    {
        return data?.ToVector3() ?? Vector3.zero;
    }
}

[Serializable]
public class CombatEffect
{
    public string EffectId { get; set; } = string.Empty;
    public Vector3Data Position { get; set; } = new();
    public string EffectType { get; set; } = string.Empty;
    public float Duration { get; set; }
    public DateTime StartTime { get; set; }
}

[Serializable]
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
    
    // Weapon properties for ranged combat
    public string WeaponType { get; set; } = "Melee"; // "Melee", "Ranged"
    public float WeaponRange { get; set; } = 0f; // Maximum effective range
    public float ProjectileSpeed { get; set; } = 0f; // Projectile travel speed
    public float Accuracy { get; set; } = 1.0f; // Base accuracy (0.0-1.0)
}

[System.Serializable]
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
    public int Value { get; set; } = 0; // Gold value
    
    // Weapon properties for ranged combat
    public string WeaponType { get; set; } = "Melee"; // "Melee", "Ranged"
    public float WeaponRange { get; set; } = 0f; // Maximum effective range
    public float ProjectileSpeed { get; set; } = 0f; // Projectile travel speed
    public float Accuracy { get; set; } = 1.0f; // Base accuracy (0.0-1.0)
    
    // Equipment tracking
    public System.DateTime DateEquipped { get; set; } = System.DateTime.UtcNow;
    public System.DateTime DateModified { get; set; } = System.DateTime.UtcNow;
}

// Weapon timing information for client-side cooldown validation
[System.Serializable]
public class WeaponTimingMessage
{
    public string PlayerId { get; set; } = string.Empty;
    public decimal AttackSpeed { get; set; } = 1.0m; // Attacks per second
    public int CooldownMs { get; set; } = 1000; // Milliseconds between attacks
    public long ServerTime { get; set; } = 0; // Server timestamp for sync
    public string WeaponType { get; set; } = "Melee"; // "Melee", "Ranged"
    public string WeaponName { get; set; } = ""; // For debugging/display
    public bool HasWeaponEquipped { get; set; } = false; // If no weapon, use default timing
}

// Terrain system data structures
[Serializable]
public class ColorData
{
    public float r;
    public float g;
    public float b;
    public float a = 1f;

    public Color ToColor() => new Color(r, g, b, a);
}

[Serializable]
public class TerrainHill
{
    public string id;
    public string name;
    public string hillSet;
    public Vector3Data position;
    public Vector3Data scale;
    public ColorData color;
}

// ===== TERRAIN SYSTEM MESSAGES =====

/// <summary>
/// Server notification that terrain has changed (e.g., level complete, new area)
/// Sent via WebSocket to trigger client terrain refresh
/// </summary>
[Serializable]
public class TerrainChangeMessage
{
    /// <summary>
    /// Reason for terrain change (e.g., "LevelComplete", "AreaTransition")
    /// </summary>
    public string reason;

    /// <summary>
    /// Current level/wave number
    /// </summary>
    public int currentLevel;

    /// <summary>
    /// The new terrain data to apply
    /// </summary>
    public ServerTerrainData terrainData;

    /// <summary>
    /// Server timestamp when terrain changed
    /// </summary>
    public long timestamp;
}

[Serializable]
public class ServerTerrainData
{
    public float baseGroundLevel;
    public List<TerrainHill> hills;
    public List<string> activeHillSets;
}

// ===== LEVEL SYSTEM MESSAGES =====

/// <summary>
/// Server notification that a level/wave has been completed
/// </summary>
[Serializable]
public class LevelCompleteMessage
{
    public int completedLevel;
    public int nextLevel;
    public int enemiesKilled;
    public long experienceEarned;
    public float damageDealt;
    public float timeTaken;
    public long timestamp;
}

/// <summary>
/// Client request to continue to the next level
/// </summary>
[Serializable]
public class LevelContinueMessage
{
    public string playerId;
    public int nextLevel;
    public long timestamp;
}

/// <summary>
/// Server tells client to reposition player (e.g., after level transition to avoid spawning inside terrain)
/// </summary>
[Serializable]
public class PlayerRepositionMessage
{
    public string playerId;
    public Vector3Data position;
    public string reason;
    public long timestamp;
}
