-- Test what the server sees when retrieving inventory
USE GameDB;

PRINT 'Testing inventory retrieval for RipTide player...'

-- Test the exact query the server uses
SELECT 
    pi.InventoryId,
    pi.ItemTypeId,
    it.ItemName,
    it.Description as ItemDescription,
    it.ItemRarity as Rarity,
    it.ItemCategory,
    pi.Quantity,
    pi.SlotIndex,
    it.MaxStackSize,
    0 as AttackPower,
    0 as DefensePower,
    COALESCE(it.IconPath, '') as IconName,
    CASE WHEN it.MaxStackSize > 1 THEN 1 ELSE 0 END as IsStackable,
    10 as Value,
    pi.ItemLevel as Level
FROM PlayerInventory pi
INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
WHERE pi.PlayerId = '388f0290-65b4-4a9d-83c9-a6e31d91b6d4'
ORDER BY pi.SlotIndex;

PRINT 'Row count check:'
SELECT COUNT(*) as ItemCount FROM PlayerInventory 
WHERE PlayerId = '388f0290-65b4-4a9d-83c9-a6e31d91b6d4';

PRINT 'Checking for NULL values that might cause issues:'
SELECT 
    COUNT(*) as TotalItems,
    COUNT(pi.InventoryId) as NonNullInventoryId,
    COUNT(pi.ItemTypeId) as NonNullItemTypeId,
    COUNT(it.ItemName) as NonNullItemName,
    COUNT(pi.Quantity) as NonNullQuantity,
    COUNT(pi.SlotIndex) as NonNullSlotIndex,
    COUNT(pi.ItemLevel) as NonNullItemLevel
FROM PlayerInventory pi
INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
WHERE pi.PlayerId = '388f0290-65b4-4a9d-83c9-a6e31d91b6d4';