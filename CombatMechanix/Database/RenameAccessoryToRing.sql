-- Script to rename Accessory slot to Ring slot

-- First, update any existing Accessory entries to Ring
UPDATE PlayerEquipment 
SET SlotType = 'Ring' 
WHERE SlotType = 'Accessory';

-- Drop the existing CHECK constraint
ALTER TABLE PlayerEquipment 
DROP CONSTRAINT [CK__PlayerEqu__SlotT__6C190EBB];

-- Add the new CHECK constraint with Ring instead of Accessory
ALTER TABLE PlayerEquipment 
ADD CONSTRAINT CK_PlayerEquipment_SlotType 
CHECK (SlotType IN ('Helmet', 'Chest', 'Legs', 'Weapon', 'Offhand', 'Ring'));

PRINT 'Successfully renamed Accessory slot to Ring slot';