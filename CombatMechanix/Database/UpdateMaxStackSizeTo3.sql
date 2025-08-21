-- Update all stackable items to have a maximum stack size of 3
-- This will limit inventory stacking for better inventory management

USE CombatMechanixDB;
GO

-- Check which table name exists (ItemType or ItemTypes)
DECLARE @TableName NVARCHAR(50)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ItemTypes')
    SET @TableName = 'ItemTypes'
ELSE IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ItemType')
    SET @TableName = 'ItemType'
ELSE
BEGIN
    PRINT 'ERROR: Neither ItemTypes nor ItemType table found!'
    RETURN
END

PRINT 'Using table: ' + @TableName

-- Show current stackable items and their stack sizes
PRINT 'Current stackable items and their MaxStackSize:'
DECLARE @SQL NVARCHAR(MAX)
SET @SQL = 'SELECT ItemTypeId, ItemName, IsStackable, MaxStackSize FROM ' + @TableName + ' WHERE IsStackable = 1 ORDER BY MaxStackSize DESC, ItemName'
EXEC sp_executesql @SQL

-- Update all stackable items to have MaxStackSize of 3
SET @SQL = 'UPDATE ' + @TableName + ' SET MaxStackSize = 3 WHERE IsStackable = 1'
EXEC sp_executesql @SQL

PRINT 'Updated MaxStackSize to 3 for all stackable items'

-- Show updated results
PRINT 'Updated stackable items:'
SET @SQL = 'SELECT ItemTypeId, ItemName, IsStackable, MaxStackSize FROM ' + @TableName + ' WHERE IsStackable = 1 ORDER BY ItemName'
EXEC sp_executesql @SQL

-- Summary of changes
SET @SQL = 'SELECT COUNT(*) as TotalItemsUpdated, ''MaxStackSize set to 3'' as ChangeDescription FROM ' + @TableName + ' WHERE IsStackable = 1 AND MaxStackSize = 3'
EXEC sp_executesql @SQL

PRINT 'Update completed successfully!'