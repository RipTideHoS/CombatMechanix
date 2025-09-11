-- Add bow weapons with varying accuracy and range
-- These will be the first ranged weapons in Combat Mechanix

INSERT INTO itemtypes (
    ItemTypeId, ItemName, ItemCategory, ItemRarity, MaxStackSize, ItemStats, 
    Description, IconPath, IsTradeble, IsDroppable, CreatedAt, 
    AttackPower, DefensePower, BaseValue, IsStackable, AttackSpeed,
    WeaponType, WeaponRange, ProjectileSpeed, Accuracy
)
VALUES 
-- Common Bows (Lower accuracy, shorter range)
('common_wooden_bow', 'Wooden Bow', 'Weapon', 'Common', 1, '{"attack": 18, "range": 25}', 
 'A simple wooden bow good for hunting small game.', '/icons/weapons/wooden_bow.png', 
 1, 1, GETDATE(), 18, 0, 180, 0, 1.2,
 'Ranged', 25.0, 20.0, 0.70),

('common_short_bow', 'Short Bow', 'Weapon', 'Common', 1, '{"attack": 22, "range": 30}', 
 'A compact bow that trades power for portability.', '/icons/weapons/short_bow.png', 
 1, 1, GETDATE(), 22, 0, 220, 0, 1.4,
 'Ranged', 30.0, 22.0, 0.75),

-- Uncommon Bows (Better accuracy, longer range)
('uncommon_hunting_bow', 'Hunting Bow', 'Weapon', 'Uncommon', 1, '{"attack": 28, "range": 35}', 
 'A well-crafted bow designed for precise hunting.', '/icons/weapons/hunting_bow.png', 
 1, 1, GETDATE(), 28, 0, 350, 0, 1.3,
 'Ranged', 35.0, 25.0, 0.80),

('uncommon_composite_bow', 'Composite Bow', 'Weapon', 'Uncommon', 1, '{"attack": 34, "range": 40}', 
 'A bow made from multiple materials for superior performance.', '/icons/weapons/composite_bow.png', 
 1, 1, GETDATE(), 34, 0, 450, 0, 1.1,
 'Ranged', 40.0, 28.0, 0.82),

-- Rare Bows (High accuracy, long range)
('rare_elven_bow', 'Elven Bow', 'Weapon', 'Rare', 1, '{"attack": 42, "range": 50}', 
 'An elegant bow crafted by elven masters with supernatural precision.', '/icons/weapons/elven_bow.png', 
 1, 1, GETDATE(), 42, 0, 800, 0, 1.0,
 'Ranged', 50.0, 30.0, 0.90),

('rare_longbow', 'Longbow', 'Weapon', 'Rare', 1, '{"attack": 38, "range": 55}', 
 'A powerful longbow capable of striking distant targets.', '/icons/weapons/longbow.png', 
 1, 1, GETDATE(), 38, 0, 750, 0, 0.9,
 'Ranged', 55.0, 32.0, 0.85),

-- Epic Bows (Exceptional accuracy and range)
('epic_storm_bow', 'Storm Bow', 'Weapon', 'Epic', 1, '{"attack": 52, "range": 60}', 
 'A magical bow that channels the power of storms into every shot.', '/icons/weapons/storm_bow.png', 
 1, 1, GETDATE(), 52, 0, 1500, 0, 1.2,
 'Ranged', 60.0, 35.0, 0.93),

('epic_void_bow', 'Void Bow', 'Weapon', 'Epic', 1, '{"attack": 58, "range": 65}', 
 'A bow that seems to bend space itself around its arrows.', '/icons/weapons/void_bow.png', 
 1, 1, GETDATE(), 58, 0, 1700, 0, 1.1,
 'Ranged', 65.0, 40.0, 0.95),

-- Legendary Bow (Perfect accuracy)
('legendary_world_piercer', 'World Piercer', 'Weapon', 'Legendary', 1, '{"attack": 65, "range": 80}', 
 'A legendary bow said to be able to pierce the very fabric of reality.', '/icons/weapons/world_piercer.png', 
 1, 1, GETDATE(), 65, 0, 2500, 0, 1.0,
 'Ranged', 80.0, 45.0, 0.98);