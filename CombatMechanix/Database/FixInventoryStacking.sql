-- Fix inventory stacking issue after updating MaxStackSize to 3
-- This script splits oversized stacks into multiple inventory slots

USE GameDB;
GO

PRINT 'Checking for inventory items exceeding MaxStackSize...'

-- First, let's see what we're dealing with
SELECT 
    pi.PlayerId, 
    pi.ItemTypeId, 
    it.ItemName, 
    pi.Quantity, 
    it.MaxStackSize, 
    pi.SlotIndex,
    (pi.Quantity - 1) / it.MaxStackSize as AdditionalSlotsNeeded
FROM PlayerInventory pi
INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
WHERE pi.Quantity > it.MaxStackSize
ORDER BY pi.PlayerId, pi.SlotIndex;

-- Check if we have any oversized stacks
IF EXISTS (
    SELECT 1 FROM PlayerInventory pi
    INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
    WHERE pi.Quantity > it.MaxStackSize
)
BEGIN
    PRINT 'Found oversized stacks. Beginning correction...'
    
    -- Temporary table to track what needs to be split
    CREATE TABLE #StacksToFix (
        InventoryId INT,
        PlayerId NVARCHAR(50),
        ItemTypeId NVARCHAR(50),
        CurrentQuantity INT,
        MaxStackSize INT,
        SlotIndex INT,
        AdditionalSlotsNeeded INT
    );
    
    INSERT INTO #StacksToFix
    SELECT 
        pi.InventoryId,
        pi.PlayerId, 
        pi.ItemTypeId, 
        pi.Quantity, 
        it.MaxStackSize, 
        pi.SlotIndex,
        (pi.Quantity - 1) / it.MaxStackSize as AdditionalSlotsNeeded
    FROM PlayerInventory pi
    INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
    WHERE pi.Quantity > it.MaxStackSize;
    
    DECLARE @InventoryId INT, @PlayerId NVARCHAR(50), @ItemTypeId NVARCHAR(50)
    DECLARE @CurrentQuantity INT, @MaxStackSize INT, @SlotIndex INT, @AdditionalSlotsNeeded INT
    DECLARE @RemainingQuantity INT, @NextSlot INT
    
    DECLARE stack_cursor CURSOR FOR
        SELECT InventoryId, PlayerId, ItemTypeId, CurrentQuantity, MaxStackSize, SlotIndex, AdditionalSlotsNeeded
        FROM #StacksToFix
        ORDER BY PlayerId, SlotIndex;
    
    OPEN stack_cursor;
    FETCH NEXT FROM stack_cursor INTO @InventoryId, @PlayerId, @ItemTypeId, @CurrentQuantity, @MaxStackSize, @SlotIndex, @AdditionalSlotsNeeded;
    
    WHILE @@FETCH_STATUS = 0
    BEGIN
        PRINT 'Processing oversized stack: Player ' + @PlayerId + ', Item ' + @ItemTypeId + ', Quantity ' + CAST(@CurrentQuantity AS VARCHAR(10))
        
        -- First, reduce the original stack to MaxStackSize
        UPDATE PlayerInventory 
        SET Quantity = @MaxStackSize
        WHERE InventoryId = @InventoryId;
        
        SET @RemainingQuantity = @CurrentQuantity - @MaxStackSize;
        
        -- Create additional stacks for the remaining quantity
        WHILE @RemainingQuantity > 0
        BEGIN
            -- Find next available slot for this player
            WITH SlotNumbers AS (
                SELECT TOP 20 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS SlotIndex
                FROM sys.objects
            )
            SELECT TOP 1 @NextSlot = s.SlotIndex
            FROM SlotNumbers s
            LEFT JOIN PlayerInventory pi ON pi.PlayerId = @PlayerId AND pi.SlotIndex = s.SlotIndex
            WHERE pi.SlotIndex IS NULL
            ORDER BY s.SlotIndex;
            
            -- Create new inventory entry
            DECLARE @NewQuantity INT = CASE WHEN @RemainingQuantity > @MaxStackSize THEN @MaxStackSize ELSE @RemainingQuantity END;
            
            INSERT INTO PlayerInventory (PlayerId, ItemTypeId, Quantity, SlotIndex, ItemCondition, ItemLevel, AcquiredAt)
            VALUES (@PlayerId, @ItemTypeId, @NewQuantity, @NextSlot, 100.0, 1, GETUTCDATE());
            
            PRINT '  Created new stack in slot ' + CAST(@NextSlot AS VARCHAR(10)) + ' with quantity ' + CAST(@NewQuantity AS VARCHAR(10))
            
            SET @RemainingQuantity = @RemainingQuantity - @NewQuantity;
        END
        
        FETCH NEXT FROM stack_cursor INTO @InventoryId, @PlayerId, @ItemTypeId, @CurrentQuantity, @MaxStackSize, @SlotIndex, @AdditionalSlotsNeeded;
    END
    
    CLOSE stack_cursor;
    DEALLOCATE stack_cursor;
    
    DROP TABLE #StacksToFix;
    
    PRINT 'Stack splitting completed!'
END
ELSE
BEGIN
    PRINT 'No oversized stacks found. Inventory is already properly split.'
END

-- Final verification
PRINT 'Final verification - checking for any remaining oversized stacks:'
SELECT 
    pi.PlayerId, 
    pi.ItemTypeId, 
    it.ItemName, 
    pi.Quantity, 
    it.MaxStackSize, 
    pi.SlotIndex
FROM PlayerInventory pi
INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId
WHERE pi.Quantity > it.MaxStackSize
ORDER BY pi.PlayerId, pi.SlotIndex;

IF @@ROWCOUNT = 0
    PRINT 'SUCCESS: All inventory stacks now respect MaxStackSize limits!'
ELSE
    PRINT 'WARNING: Some oversized stacks still remain!'

PRINT 'Inventory correction script completed.'