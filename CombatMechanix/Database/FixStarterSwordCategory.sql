-- Fix starter_sword ItemCategory for equipment validation
-- This script updates the starter_sword item to have the correct ItemCategory

-- Check current state of starter_sword
SELECT ItemTypeId, ItemName, ItemCategory, ItemRarity 
FROM ItemTypes 
WHERE ItemTypeId = 'starter_sword';

-- Update starter_sword to have correct ItemCategory
UPDATE ItemTypes 
SET ItemCategory = 'Weapon'
WHERE ItemTypeId = 'starter_sword' AND (ItemCategory IS NULL OR ItemCategory = '');

-- Verify the update
SELECT ItemTypeId, ItemName, ItemCategory, ItemRarity 
FROM ItemTypes 
WHERE ItemTypeId = 'starter_sword';

PRINT 'Updated starter_sword ItemCategory to Weapon';