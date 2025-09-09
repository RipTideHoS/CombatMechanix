-- Simple script to add test equipment for a specific player
-- Replace 'YOUR_PLAYER_ID_HERE' with the actual player ID you want to test with

DECLARE @PlayerId NVARCHAR(50) = 'YOUR_PLAYER_ID_HERE'; -- CHANGE THIS TO YOUR PLAYER ID

-- First, verify the player exists
IF NOT EXISTS (SELECT 1 FROM PlayerStats WHERE PlayerId = @PlayerId)
BEGIN
    PRINT 'Error: Player ' + @PlayerId + ' not found in PlayerStats table';
    PRINT 'Please create a player first or check the player ID';
    RETURN;
END

-- Add basic test items to ItemTypes if they don't exist
IF NOT EXISTS (SELECT 1 FROM ItemTypes WHERE ItemTypeId = 'starter_sword')
BEGIN
    INSERT INTO ItemTypes (ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize, IconPath)
    VALUES 
    ('starter_sword', 'Starter Sword', 'A basic sword for new adventurers', 'Common', 'Weapon', 1, 'sword_basic'),
    ('starter_helmet', 'Starter Helmet', 'Basic head protection', 'Common', 'Helmet', 1, 'helmet_basic'),
    ('starter_armor', 'Starter Armor', 'Simple chest protection', 'Common', 'Chest', 1, 'armor_basic'),
    ('starter_pants', 'Starter Pants', 'Basic leg protection', 'Common', 'Legs', 1, 'pants_basic'),
    ('starter_shield', 'Starter Shield', 'A simple wooden shield', 'Common', 'Shield', 1, 'shield_basic'),
    ('starter_amulet', 'Starter Amulet', 'A simple protective charm', 'Common', 'Amulet', 1, 'amulet_basic');
END

-- Clear any existing equipment for this player
DELETE FROM PlayerEquipment WHERE PlayerId = @PlayerId;
PRINT 'Cleared existing equipment for player: ' + @PlayerId;

-- Add complete set of starter equipment
INSERT INTO PlayerEquipment (PlayerId, ItemTypeId, SlotType, DateEquipped, DateModified)
VALUES 
(@PlayerId, 'starter_sword', 'Weapon', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_helmet', 'Helmet', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_armor', 'Chest', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_pants', 'Legs', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_shield', 'Offhand', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_amulet', 'Ring', GETUTCDATE(), GETUTCDATE());

PRINT 'Successfully equipped 6 starter items for player: ' + @PlayerId;

-- Show what was equipped
SELECT 
    pe.SlotType,
    it.ItemName,
    it.ItemCategory,
    it.ItemRarity,
    pe.DateEquipped
FROM PlayerEquipment pe
INNER JOIN ItemTypes it ON pe.ItemTypeId = it.ItemTypeId
WHERE pe.PlayerId = @PlayerId
ORDER BY 
    CASE pe.SlotType 
        WHEN 'Helmet' THEN 1
        WHEN 'Chest' THEN 2  
        WHEN 'Legs' THEN 3
        WHEN 'Weapon' THEN 4
        WHEN 'Offhand' THEN 5
        WHEN 'Ring' THEN 6
    END;

PRINT '';
PRINT 'Equipment added successfully!';
PRINT 'Now you can:';
PRINT '1. Start the server: dotnet run --urls http://localhost:5207';
PRINT '2. Connect with the Unity client';
PRINT '3. Login with the player: ' + @PlayerId;
PRINT '4. Press "C" key to open the Character Panel';
PRINT '5. You should see all 6 equipped items displayed';