-- Migration script to rename SlotPosition column to SlotIndex
-- This fixes the inventory issue where server code uses SlotIndex but database might have SlotPosition

USE CombatMechanixDB;
GO

-- Check if the column SlotPosition exists and SlotIndex doesn't exist
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'PlayerInventory' AND COLUMN_NAME = 'SlotPosition'
) AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'PlayerInventory' AND COLUMN_NAME = 'SlotIndex'
)
BEGIN
    PRINT 'Migrating SlotPosition column to SlotIndex...'
    
    -- First drop any constraints/indexes on SlotPosition
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PlayerInventory_SlotIndex')
    BEGIN
        DROP INDEX IX_PlayerInventory_SlotIndex ON PlayerInventory;
        PRINT 'Dropped existing IX_PlayerInventory_SlotIndex index'
    END
    
    -- Drop unique constraint if it exists with SlotPosition
    DECLARE @ConstraintName NVARCHAR(128)
    SELECT @ConstraintName = name 
    FROM sys.key_constraints 
    WHERE parent_object_id = OBJECT_ID('PlayerInventory') 
    AND type = 'UQ'
    
    IF @ConstraintName IS NOT NULL
    BEGIN
        DECLARE @SQL NVARCHAR(MAX) = 'ALTER TABLE PlayerInventory DROP CONSTRAINT ' + @ConstraintName
        EXEC sp_executesql @SQL
        PRINT 'Dropped unique constraint on SlotPosition'
    END
    
    -- Rename the column using sp_rename
    EXEC sp_rename 'PlayerInventory.SlotPosition', 'SlotIndex', 'COLUMN';
    PRINT 'Renamed SlotPosition column to SlotIndex'
    
    -- Recreate the unique constraint with the new column name
    ALTER TABLE PlayerInventory ADD CONSTRAINT UC_PlayerInventory_PlayerSlot UNIQUE(PlayerId, SlotIndex);
    PRINT 'Recreated unique constraint on PlayerId, SlotIndex'
    
    -- Recreate the index
    CREATE INDEX IX_PlayerInventory_SlotIndex ON PlayerInventory(PlayerId, SlotIndex);
    PRINT 'Recreated IX_PlayerInventory_SlotIndex index'
    
    PRINT 'Migration completed successfully!'
END
ELSE IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'PlayerInventory' AND COLUMN_NAME = 'SlotIndex'
)
BEGIN
    PRINT 'Column SlotIndex already exists. No migration needed.'
END
ELSE
BEGIN
    PRINT 'ERROR: PlayerInventory table structure is unexpected. Please check the table schema.'
END
GO