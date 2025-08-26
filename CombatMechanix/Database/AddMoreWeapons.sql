-- Add more weapons with varying attack power in range 20-80
-- This script adds diverse weapon types with different rarities and attack power values

INSERT INTO itemtypes (ItemTypeId, ItemName, ItemCategory, ItemRarity, MaxStackSize, ItemStats, Description, IconPath, IsTradeble, IsDroppable, CreatedAt, AttackPower, DefensePower, BaseValue, IsStackable)
VALUES 
-- Attack Power 20-30 (Common tier)
('common_bronze_axe', 'Bronze Axe', 'Weapon', 'Common', 1, '{"attack": 20}', 'A sturdy bronze axe good for both combat and utility.', '/icons/weapons/bronze_axe.png', 1, 1, GETDATE(), 20, 0, 150, 0),
('common_wooden_club', 'Wooden Club', 'Weapon', 'Common', 1, '{"attack": 22}', 'A heavy wooden club that packs a punch.', '/icons/weapons/wooden_club.png', 1, 1, GETDATE(), 22, 0, 120, 0),
('common_copper_dagger', 'Copper Dagger', 'Weapon', 'Common', 1, '{"attack": 24}', 'A quick copper dagger ideal for swift strikes.', '/icons/weapons/copper_dagger.png', 1, 1, GETDATE(), 24, 0, 180, 0),
('common_iron_mace', 'Iron Mace', 'Weapon', 'Common', 1, '{"attack": 26}', 'An iron mace that can crush through light armor.', '/icons/weapons/iron_mace.png', 1, 1, GETDATE(), 26, 0, 200, 0),
('common_battle_hammer', 'Battle Hammer', 'Weapon', 'Common', 1, '{"attack": 28}', 'A two-handed hammer for devastating blows.', '/icons/weapons/battle_hammer.png', 1, 1, GETDATE(), 28, 0, 250, 0),

-- Attack Power 30-45 (Uncommon tier)
('uncommon_silver_sword', 'Silver Sword', 'Weapon', 'Uncommon', 1, '{"attack": 32}', 'A gleaming silver sword with enhanced sharpness.', '/icons/weapons/silver_sword.png', 1, 1, GETDATE(), 32, 0, 350, 0),
('uncommon_steel_spear', 'Steel Spear', 'Weapon', 'Uncommon', 1, '{"attack": 35}', 'A long steel spear perfect for keeping enemies at bay.', '/icons/weapons/steel_spear.png', 1, 1, GETDATE(), 35, 0, 400, 0),
('uncommon_war_axe', 'War Axe', 'Weapon', 'Uncommon', 1, '{"attack": 38}', 'A fearsome war axe designed for battlefield combat.', '/icons/weapons/war_axe.png', 1, 1, GETDATE(), 38, 0, 450, 0),
('uncommon_curved_blade', 'Curved Blade', 'Weapon', 'Uncommon', 1, '{"attack": 41}', 'An exotic curved blade with superior cutting power.', '/icons/weapons/curved_blade.png', 1, 1, GETDATE(), 41, 0, 500, 0),
('uncommon_crystal_dagger', 'Crystal Dagger', 'Weapon', 'Uncommon', 1, '{"attack": 44}', 'A mystical dagger infused with crystal energy.', '/icons/weapons/crystal_dagger.png', 1, 1, GETDATE(), 44, 0, 550, 0),

-- Attack Power 45-60 (Rare tier)
('rare_flame_sword', 'Flame Sword', 'Weapon', 'Rare', 1, '{"attack": 47}', 'A magical sword wreathed in eternal flames.', '/icons/weapons/flame_sword.png', 1, 1, GETDATE(), 47, 0, 800, 0),
('rare_ice_spear', 'Ice Spear', 'Weapon', 'Rare', 1, '{"attack": 50}', 'A spear forged from eternal ice that never melts.', '/icons/weapons/ice_spear.png', 1, 1, GETDATE(), 50, 0, 850, 0),
('rare_shadow_blade', 'Shadow Blade', 'Weapon', 'Rare', 1, '{"attack": 53}', 'A blade that seems to bend light around its edge.', '/icons/weapons/shadow_blade.png', 1, 1, GETDATE(), 53, 0, 900, 0),
('rare_thunder_hammer', 'Thunder Hammer', 'Weapon', 'Rare', 1, '{"attack": 56}', 'A hammer that crackles with electrical energy.', '/icons/weapons/thunder_hammer.png', 1, 1, GETDATE(), 56, 0, 950, 0),
('rare_venomous_dagger', 'Venomous Dagger', 'Weapon', 'Rare', 1, '{"attack": 59}', 'A dagger coated with a deadly, ever-fresh poison.', '/icons/weapons/venomous_dagger.png', 1, 1, GETDATE(), 59, 0, 1000, 0),

-- Attack Power 60-80 (Epic tier)
('epic_dragon_slayer', 'Dragon Slayer', 'Weapon', 'Epic', 1, '{"attack": 62}', 'A legendary sword forged to slay dragons.', '/icons/weapons/dragon_slayer.png', 1, 1, GETDATE(), 62, 0, 1500, 0),
('epic_void_axe', 'Void Axe', 'Weapon', 'Epic', 1, '{"attack": 66}', 'An axe that seems to cut through reality itself.', '/icons/weapons/void_axe.png', 1, 1, GETDATE(), 66, 0, 1600, 0),
('epic_storm_spear', 'Storm Spear', 'Weapon', 'Epic', 1, '{"attack": 70}', 'A spear that commands the fury of storms.', '/icons/weapons/storm_spear.png', 1, 1, GETDATE(), 70, 0, 1700, 0),
('epic_soul_reaper', 'Soul Reaper', 'Weapon', 'Epic', 1, '{"attack": 74}', 'A scythe that can harvest the essence of enemies.', '/icons/weapons/soul_reaper.png', 1, 1, GETDATE(), 74, 0, 1800, 0),
('epic_celestial_blade', 'Celestial Blade', 'Weapon', 'Epic', 1, '{"attack": 78}', 'A blade blessed by the gods themselves.', '/icons/weapons/celestial_blade.png', 1, 1, GETDATE(), 78, 0, 1900, 0),

-- Top tier weapon at 80 attack power
('legendary_world_ender', 'World Ender', 'Weapon', 'Legendary', 1, '{"attack": 80}', 'The ultimate weapon capable of ending worlds.', '/icons/weapons/world_ender.png', 1, 1, GETDATE(), 80, 0, 2500, 0);