-- Add test equipment for the created test player
-- Player ID: e229673f-293a-4b97-8353-ca142b922874
-- Username: testplayer
-- Password: password123

DECLARE @PlayerId NVARCHAR(50) = 'e229673f-293a-4b97-8353-ca142b922874';

-- Verify the player exists
IF NOT EXISTS (SELECT 1 FROM PlayerStats WHERE PlayerId = @PlayerId)
BEGIN
    PRINT 'Error: Player ' + @PlayerId + ' not found in PlayerStats table';
    RETURN;
END
ELSE
BEGIN
    PRINT 'Found test player: ' + @PlayerId;
END

-- Add test equipment items to ItemTypes table if they don't exist
IF NOT EXISTS (SELECT 1 FROM ItemTypes WHERE ItemTypeId = 'starter_sword')
BEGIN
    INSERT INTO ItemTypes (ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize, IconPath)
    VALUES 
    ('starter_sword', 'Starter Sword', 'A basic iron sword for new adventurers', 'Common', 'Weapon', 1, 'sword_basic'),
    ('starter_helmet', 'Iron Helmet', 'Basic iron head protection', 'Common', 'Helmet', 1, 'helmet_basic'),
    ('starter_chestplate', 'Leather Chestplate', 'Simple leather chest armor', 'Common', 'Chest', 1, 'armor_basic'),
    ('starter_leggings', 'Chain Leggings', 'Basic chain leg protection', 'Common', 'Legs', 1, 'pants_basic'),
    ('starter_shield', 'Wooden Shield', 'A sturdy wooden shield', 'Common', 'Shield', 1, 'shield_basic'),
    ('starter_ring', 'Silver Ring', 'A simple silver ring with minor enchantments', 'Uncommon', 'Ring', 1, 'ring_basic');
    
    PRINT 'Added starter equipment items to ItemTypes table';
END
ELSE
BEGIN
    PRINT 'Starter equipment items already exist in ItemTypes table';
END

-- Clear any existing equipment for this player (just in case)
DELETE FROM PlayerEquipment WHERE PlayerId = @PlayerId;
PRINT 'Cleared any existing equipment for test player';

-- Add complete starter equipment set
INSERT INTO PlayerEquipment (PlayerId, ItemTypeId, SlotType, DateEquipped, DateModified)
VALUES 
(@PlayerId, 'starter_sword', 'Weapon', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_helmet', 'Helmet', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_chestplate', 'Chest', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_leggings', 'Legs', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_shield', 'Offhand', GETUTCDATE(), GETUTCDATE()),
(@PlayerId, 'starter_ring', 'Accessory', GETUTCDATE(), GETUTCDATE());

PRINT 'Successfully equipped 6 starter items for test player';

-- Verify the equipment was added correctly
SELECT 
    pe.SlotType,
    it.ItemName,
    it.ItemCategory,
    it.ItemRarity,
    pe.DateEquipped,
    pe.EquipmentId
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
        WHEN 'Accessory' THEN 6
    END;

PRINT '';
PRINT '=== EQUIPMENT SETUP COMPLETE ===';
PRINT 'Test Player Details:';
PRINT '  Username: testplayer';
PRINT '  Password: password123'; 
PRINT '  Player ID: ' + @PlayerId;
PRINT '';
PRINT 'Equipment Added:';
PRINT '  ✅ Weapon: Starter Sword (Iron)';
PRINT '  ✅ Helmet: Iron Helmet'; 
PRINT '  ✅ Chest: Leather Chestplate';
PRINT '  ✅ Legs: Chain Leggings';
PRINT '  ✅ Offhand: Wooden Shield';
PRINT '  ✅ Accessory: Silver Ring (Uncommon)';
PRINT '';
PRINT 'Now you can test the Character Panel:';
PRINT '1. Connect Unity client to ws://localhost:5207/ws';
PRINT '2. Login with testplayer / password123';
PRINT '3. Press "C" key to open Character Panel';
PRINT '4. You should see all 6 equipped items!';
PRINT '';
PRINT 'The server is already running at: http://localhost:5207';