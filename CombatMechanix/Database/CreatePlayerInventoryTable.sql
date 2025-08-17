-- Create PlayerInventory table for persistent inventory storage
-- This table stores each player's inventory items with their quantities and slot positions

CREATE TABLE PlayerInventory (
    InventoryId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlayerId NVARCHAR(50) NOT NULL,
    ItemTypeId NVARCHAR(50) NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), -- Unique instance ID for this stack
    Quantity INT NOT NULL DEFAULT 1,
    SlotIndex INT NOT NULL, -- Position in inventory (0-19 for 20-slot inventory)
    DateCreated DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DateModified DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    -- Foreign key constraints
    FOREIGN KEY (ItemTypeId) REFERENCES ItemTypes(ItemTypeId),
    
    -- Ensure one item per slot per player
    UNIQUE(PlayerId, SlotIndex),
    
    -- Ensure slot index is within valid range (0-19 for 20-slot inventory)
    CHECK (SlotIndex >= 0 AND SlotIndex < 20),
    CHECK (Quantity > 0)
);

-- Create indexes for performance
CREATE INDEX IX_PlayerInventory_PlayerId ON PlayerInventory(PlayerId);
CREATE INDEX IX_PlayerInventory_ItemTypeId ON PlayerInventory(ItemTypeId);
CREATE INDEX IX_PlayerInventory_SlotIndex ON PlayerInventory(PlayerId, SlotIndex);

-- Create view for easy inventory queries with item details
CREATE VIEW PlayerInventoryView AS
SELECT 
    pi.InventoryId,
    pi.PlayerId,
    pi.ItemId,
    pi.ItemTypeId,
    it.ItemName,
    it.Description as ItemDescription,
    it.ItemRarity as Rarity,
    it.ItemCategory,
    pi.Quantity,
    pi.SlotIndex,
    it.MaxStackSize,
    it.AttackPower,
    it.DefensePower,
    it.IconPath as IconName,
    CASE WHEN it.MaxStackSize > 1 THEN 1 ELSE 0 END as IsStackable,
    COALESCE(it.BaseValue, 10) as Value, -- Default value if not set
    1 as Level, -- Default level for inventory items
    pi.DateCreated,
    pi.DateModified
FROM PlayerInventory pi
INNER JOIN ItemTypes it ON pi.ItemTypeId = it.ItemTypeId;

-- Grant permissions (adjust as needed for your security model)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON PlayerInventory TO [YourApplicationUser];
-- GRANT SELECT ON PlayerInventoryView TO [YourApplicationUser];

PRINT 'PlayerInventory table and view created successfully';