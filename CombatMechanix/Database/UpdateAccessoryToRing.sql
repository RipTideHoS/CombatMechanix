-- Simple script to update existing Accessory slots to Ring slots
-- This works around the constraint issue by updating existing data

USE GameDB;

-- Update any existing Accessory entries to Ring
UPDATE PlayerEquipment 
SET SlotType = 'Ring' 
WHERE SlotType = 'Accessory';

PRINT 'Updated existing Accessory slots to Ring slots';

-- Show the updated data
SELECT PlayerId, ItemTypeId, SlotType, DateEquipped 
FROM PlayerEquipment 
WHERE SlotType = 'Ring'
ORDER BY PlayerId;