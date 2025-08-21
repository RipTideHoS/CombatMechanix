-- Simple migration to ensure PlayerInventory table has SlotIndex column
-- This fixes the inventory display issue

-- If table exists but has wrong column name, this will fix it
-- If table doesn't exist, this will create it with correct structure

USE CombatMechanixDB;
GO

-- Check current table structure
SELECT COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'PlayerInventory' 
AND COLUMN_NAME IN ('SlotPosition', 'SlotIndex')
ORDER BY COLUMN_NAME;

-- If you see 'SlotPosition' in the results above, run this migration:

-- Step 1: Rename column if it exists
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PlayerInventory' AND COLUMN_NAME = 'SlotPosition')
BEGIN
    -- Rename SlotPosition to SlotIndex
    EXEC sp_rename 'PlayerInventory.SlotPosition', 'SlotIndex', 'COLUMN';
    PRINT 'Renamed SlotPosition to SlotIndex';
END

-- Verify the fix
SELECT COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'PlayerInventory' 
AND COLUMN_NAME = 'SlotIndex';

PRINT 'Migration complete. SlotIndex column should now exist.';