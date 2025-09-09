-- Add Test Equipment Items to PlayerEquipment Table
-- Run this script to add sample equipped items for testing the Character Panel

-- First, let's check what players exist (replace with actual player IDs from your testing)
-- You'll need to replace 'test_player_id' with an actual player ID from your PlayerStats table

-- Check if we have any test items in ItemTypes table
IF NOT EXISTS (SELECT 1 FROM ItemTypes WHERE ItemTypeId = 'test_iron_sword')
BEGIN
    -- Add some test equipment items to ItemTypes table first
    INSERT INTO ItemTypes (ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize, IconPath)
    VALUES 
    ('test_iron_sword', 'Iron Sword', 'A sturdy iron sword for combat', 'Common', 'Weapon', 1, 'sword_iron'),
    ('test_iron_helmet', 'Iron Helmet', 'Protective iron helmet', 'Common', 'Helmet', 1, 'helmet_iron'),
    ('test_leather_chest', 'Leather Chestpiece', 'Basic leather armor for the chest', 'Common', 'Chest', 1, 'chest_leather'),
    ('test_iron_legs', 'Iron Leggings', 'Protective iron leg armor', 'Uncommon', 'Legs', 1, 'legs_iron'),
    ('test_wooden_shield', 'Wooden Shield', 'Simple wooden shield for defense', 'Common', 'Shield', 1, 'shield_wooden'),
    ('test_silver_ring', 'Silver Ring', 'A silver ring with minor magical properties', 'Uncommon', 'Ring', 1, 'ring_silver');
    
    PRINT 'Added test equipment items to ItemTypes table';
END

-- Get the first available player ID for testing
DECLARE @TestPlayerId NVARCHAR(50);
SELECT TOP 1 @TestPlayerId = PlayerId FROM PlayerStats ORDER BY DateCreated;

IF @TestPlayerId IS NOT NULL
BEGIN
    PRINT 'Using player ID: ' + @TestPlayerId + ' for test equipment';
    
    -- Clear any existing equipment for this test player
    DELETE FROM PlayerEquipment WHERE PlayerId = @TestPlayerId;
    
    -- Add test equipment items for each slot type
    INSERT INTO PlayerEquipment (PlayerId, ItemTypeId, SlotType, DateEquipped, DateModified)
    VALUES 
    (@TestPlayerId, 'test_iron_sword', 'Weapon', GETUTCDATE(), GETUTCDATE()),
    (@TestPlayerId, 'test_iron_helmet', 'Helmet', GETUTCDATE(), GETUTCDATE()),
    (@TestPlayerId, 'test_leather_chest', 'Chest', GETUTCDATE(), GETUTCDATE()),
    (@TestPlayerId, 'test_iron_legs', 'Legs', GETUTCDATE(), GETUTCDATE()),
    (@TestPlayerId, 'test_wooden_shield', 'Offhand', GETUTCDATE(), GETUTCDATE()),
    (@TestPlayerId, 'test_silver_ring', 'Accessory', GETUTCDATE(), GETUTCDATE());
    
    PRINT 'Added 6 test equipment items for player: ' + @TestPlayerId;
    
    -- Display what was added
    SELECT 
        pe.SlotType,
        it.ItemName,
        it.ItemCategory,
        it.ItemRarity,
        pe.DateEquipped
    FROM PlayerEquipment pe
    INNER JOIN ItemTypes it ON pe.ItemTypeId = it.ItemTypeId
    WHERE pe.PlayerId = @TestPlayerId
    ORDER BY pe.SlotType;
    
END
ELSE
BEGIN
    PRINT 'No players found in PlayerStats table. Please create a test player first.';
    PRINT 'You can use the /test/createplayer endpoint on the server to create a test player.';
END

-- If you want to manually specify a player ID, uncomment and modify this section:
/*
DECLARE @ManualPlayerId NVARCHAR(50) = 'your_player_id_here';

-- Clear existing equipment
DELETE FROM PlayerEquipment WHERE PlayerId = @ManualPlayerId;

-- Add test equipment
INSERT INTO PlayerEquipment (PlayerId, ItemTypeId, SlotType, DateEquipped, DateModified)
VALUES 
(@ManualPlayerId, 'test_iron_sword', 'Weapon', GETUTCDATE(), GETUTCDATE()),
(@ManualPlayerId, 'test_iron_helmet', 'Helmet', GETUTCDATE(), GETUTCDATE()),
(@ManualPlayerId, 'test_leather_chest', 'Chest', GETUTCDATE(), GETUTCDATE()),
(@ManualPlayerId, 'test_iron_legs', 'Legs', GETUTCDATE(), GETUTCDATE()),
(@ManualPlayerId, 'test_wooden_shield', 'Offhand', GETUTCDATE(), GETUTCDATE()),
(@ManualPlayerId, 'test_silver_ring', 'Accessory', GETUTCDATE(), GETUTCDATE());

PRINT 'Manually added equipment for player: ' + @ManualPlayerId;
*/