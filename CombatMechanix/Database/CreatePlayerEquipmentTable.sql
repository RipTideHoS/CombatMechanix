-- Create PlayerEquipment table for persistent equipment storage
-- This table stores each player's equipped items in their 6 equipment slots

CREATE TABLE PlayerEquipment (
    EquipmentId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlayerId NVARCHAR(50) NOT NULL,
    ItemTypeId NVARCHAR(50) NOT NULL,
    ItemId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), -- Unique instance ID for this equipped item
    SlotType NVARCHAR(20) NOT NULL, -- "Helmet", "Chest", "Legs", "Weapon", "Offhand", "Accessory"
    DateEquipped DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DateModified DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    -- Foreign key constraints
    FOREIGN KEY (ItemTypeId) REFERENCES ItemTypes(ItemTypeId),
    
    -- Ensure one item per slot per player
    UNIQUE(PlayerId, SlotType),
    
    -- Ensure slot type is valid
    CHECK (SlotType IN ('Helmet', 'Chest', 'Legs', 'Weapon', 'Offhand', 'Accessory'))
);

-- Create indexes for performance
CREATE INDEX IX_PlayerEquipment_PlayerId ON PlayerEquipment(PlayerId);
CREATE INDEX IX_PlayerEquipment_ItemTypeId ON PlayerEquipment(ItemTypeId);
CREATE INDEX IX_PlayerEquipment_SlotType ON PlayerEquipment(PlayerId, SlotType);
GO

-- Create view for easy equipment queries with item details
CREATE VIEW PlayerEquipmentView AS
SELECT 
    pe.EquipmentId,
    pe.PlayerId,
    pe.ItemId,
    pe.ItemTypeId,
    pe.SlotType,
    it.ItemName,
    it.Description as ItemDescription,
    it.Rarity,
    it.ItemCategory,
    1 as Quantity, -- Equipment items are always quantity 1
    it.AttackPower,
    it.DefensePower,
    it.IconName,
    COALESCE(it.BaseValue, 10) as Value, -- Default value if not set
    1 as Level, -- Default level for equipped items
    pe.DateEquipped,
    pe.DateModified
FROM PlayerEquipment pe
INNER JOIN ItemTypes it ON pe.ItemTypeId = it.ItemTypeId;
GO

-- Grant permissions (adjust as needed for your security model)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON PlayerEquipment TO [YourApplicationUser];
-- GRANT SELECT ON PlayerEquipmentView TO [YourApplicationUser];

PRINT 'PlayerEquipment table and view created successfully';