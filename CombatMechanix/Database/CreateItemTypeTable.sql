-- Create ItemType table for loot drop system
-- This table stores all available items that can be dropped when enemies are killed

CREATE TABLE ItemType (
    ItemTypeId NVARCHAR(50) PRIMARY KEY,
    ItemName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    Rarity NVARCHAR(50) NOT NULL DEFAULT 'Common',
    ItemCategory NVARCHAR(50),
    BaseValue INT NOT NULL DEFAULT 0,
    IsStackable BIT NOT NULL DEFAULT 0,
    MaxStackSize INT NOT NULL DEFAULT 1,
    AttackPower INT NOT NULL DEFAULT 0,
    DefensePower INT NOT NULL DEFAULT 0,
    IconName NVARCHAR(100)
);

-- Insert sample Common rarity items for testing
INSERT INTO ItemType (ItemTypeId, ItemName, Description, Rarity, ItemCategory, BaseValue, IsStackable, MaxStackSize, AttackPower, DefensePower, IconName) VALUES
('common_health_potion', 'Health Potion', 'A small vial of healing liquid that restores health.', 'Common', 'Consumable', 25, 1, 10, 0, 0, 'health_potion'),
('common_iron_sword', 'Iron Sword', 'A sturdy iron blade forged by skilled blacksmiths.', 'Common', 'Weapon', 100, 0, 1, 15, 0, 'iron_sword'),
('common_leather_armor', 'Leather Armor', 'Basic protective gear made from treated leather.', 'Common', 'Armor', 75, 0, 1, 0, 8, 'leather_armor'),
('common_wooden_shield', 'Wooden Shield', 'A simple shield carved from oak wood.', 'Common', 'Shield', 50, 0, 1, 0, 5, 'wooden_shield'),
('common_bread', 'Bread', 'Fresh baked bread that provides nourishment.', 'Common', 'Food', 5, 1, 20, 0, 0, 'bread'),
('common_copper_coin', 'Copper Coin', 'A small copper coin with modest value.', 'Common', 'Currency', 1, 1, 100, 0, 0, 'copper_coin'),
('common_rope', 'Rope', 'Sturdy hemp rope useful for many purposes.', 'Common', 'Tool', 10, 1, 5, 0, 0, 'rope'),
('common_torch', 'Torch', 'A wooden torch that provides light in dark places.', 'Common', 'Tool', 8, 1, 10, 0, 0, 'torch'),
('common_arrow', 'Arrow', 'A simple wooden arrow with iron tip.', 'Common', 'Ammunition', 2, 1, 50, 0, 0, 'arrow'),
('common_bandage', 'Bandage', 'Clean cloth strips for treating wounds.', 'Common', 'Medical', 15, 1, 15, 0, 0, 'bandage');

-- Insert some Uncommon items for future use
INSERT INTO ItemType (ItemTypeId, ItemName, Description, Rarity, ItemCategory, BaseValue, IsStackable, MaxStackSize, AttackPower, DefensePower, IconName) VALUES
('uncommon_steel_sword', 'Steel Sword', 'A well-crafted steel blade with superior sharpness.', 'Uncommon', 'Weapon', 250, 0, 1, 25, 0, 'steel_sword'),
('uncommon_chainmail', 'Chainmail Armor', 'Interlocked metal rings providing good protection.', 'Uncommon', 'Armor', 200, 0, 1, 0, 15, 'chainmail'),
('uncommon_magic_potion', 'Magic Potion', 'A shimmering potion that restores both health and mana.', 'Uncommon', 'Consumable', 75, 1, 5, 0, 0, 'magic_potion');

-- Insert some Rare items for future use  
INSERT INTO ItemType (ItemTypeId, ItemName, Description, Rarity, ItemCategory, BaseValue, IsStackable, MaxStackSize, AttackPower, DefensePower, IconName) VALUES
('rare_enchanted_blade', 'Enchanted Blade', 'A sword imbued with magical energy that glows with power.', 'Rare', 'Weapon', 500, 0, 1, 40, 0, 'enchanted_blade'),
('rare_plate_armor', 'Plate Armor', 'Heavy metal armor that provides excellent protection.', 'Rare', 'Armor', 400, 0, 1, 0, 25, 'plate_armor');

-- Verify the data was inserted
SELECT 
    COUNT(*) as TotalItems,
    COUNT(CASE WHEN Rarity = 'Common' THEN 1 END) as CommonItems,
    COUNT(CASE WHEN Rarity = 'Uncommon' THEN 1 END) as UncommonItems,
    COUNT(CASE WHEN Rarity = 'Rare' THEN 1 END) as RareItems
FROM ItemType;

-- Show all Common items that will be available for loot drops
SELECT ItemTypeId, ItemName, Description, BaseValue, AttackPower, DefensePower 
FROM ItemType 
WHERE Rarity = 'Common' 
ORDER BY ItemName;