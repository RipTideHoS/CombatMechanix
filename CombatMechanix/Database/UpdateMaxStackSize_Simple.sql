-- Simple script to update MaxStackSize to 3 for all stackable items
-- Run this against your CombatMechanixDB database

-- For ItemTypes table (if it exists)
UPDATE ItemTypes SET MaxStackSize = 3 WHERE IsStackable = 1;

-- For ItemType table (if it exists instead)
UPDATE ItemType SET MaxStackSize = 3 WHERE IsStackable = 1;

-- Check results - run whichever table exists
SELECT ItemTypeId, ItemName, IsStackable, MaxStackSize 
FROM ItemTypes 
WHERE IsStackable = 1 
ORDER BY ItemName;

-- OR if using ItemType table:
-- SELECT ItemTypeId, ItemName, IsStackable, MaxStackSize 
-- FROM ItemType 
-- WHERE IsStackable = 1 
-- ORDER BY ItemName;