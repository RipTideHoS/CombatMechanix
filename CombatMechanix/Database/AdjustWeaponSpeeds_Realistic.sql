-- Adjust weapon speeds to make cooldowns much more obvious and realistic
-- New philosophy: Make weapon timing a strategic element of combat
-- Fast weapons = 0.8-1.0 attacks/sec (1000-1250ms cooldown)
-- Medium weapons = 0.5-0.7 attacks/sec (1430-2000ms cooldown) 
-- Slow weapons = 0.3-0.4 attacks/sec (2500-3330ms cooldown)
-- Bows = 0.4-0.6 attacks/sec (1670-2500ms cooldown) - no arrow spam!

-- DAGGERS: Fast but low damage weapons
UPDATE itemtypes SET AttackSpeed = 1.0 WHERE ItemTypeId = 'common_copper_dagger';        -- 1000ms cooldown
UPDATE itemtypes SET AttackSpeed = 1.2 WHERE ItemTypeId = 'uncommon_crystal_dagger';     -- 833ms cooldown  
UPDATE itemtypes SET AttackSpeed = 1.1 WHERE ItemTypeId = 'rare_venomous_dagger';        -- 909ms cooldown

-- SWORDS: Medium-fast balanced weapons  
UPDATE itemtypes SET AttackSpeed = 0.8 WHERE ItemTypeId = 'basic_sword';                 -- 1250ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.9 WHERE ItemTypeId = 'starter_sword';               -- 1111ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.7 WHERE ItemTypeId = 'common_iron_sword';           -- 1429ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.6 WHERE ItemTypeId = 'uncommon_steel_sword';        -- 1667ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.7 WHERE ItemTypeId = 'uncommon_silver_sword';       -- 1429ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.8 WHERE ItemTypeId = 'rare_enchanted_blade';        -- 1250ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.9 WHERE ItemTypeId = 'rare_flame_sword';            -- 1111ms cooldown
UPDATE itemtypes SET AttackSpeed = 1.0 WHERE ItemTypeId = 'rare_shadow_blade';           -- 1000ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.8 WHERE ItemTypeId = 'epic_soul_reaper';            -- 1250ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.9 WHERE ItemTypeId = 'epic_celestial_blade';        -- 1111ms cooldown

-- CURVED BLADES: Faster than swords
UPDATE itemtypes SET AttackSpeed = 0.9 WHERE ItemTypeId = 'uncommon_curved_blade';       -- 1111ms cooldown

-- SPEARS: Medium speed, good reach
UPDATE itemtypes SET AttackSpeed = 0.6 WHERE ItemTypeId = 'uncommon_steel_spear';        -- 1667ms cooldown  
UPDATE itemtypes SET AttackSpeed = 0.5 WHERE ItemTypeId = 'rare_ice_spear';              -- 2000ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.6 WHERE ItemTypeId = 'epic_storm_spear';            -- 1667ms cooldown

-- AXES: Slow but powerful
UPDATE itemtypes SET AttackSpeed = 0.4 WHERE ItemTypeId = 'common_bronze_axe';           -- 2500ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.3 WHERE ItemTypeId = 'uncommon_war_axe';            -- 3333ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.35 WHERE ItemTypeId = 'epic_void_axe';              -- 2857ms cooldown

-- MACES & CLUBS: Very slow but devastating
UPDATE itemtypes SET AttackSpeed = 0.35 WHERE ItemTypeId = 'common_wooden_club';         -- 2857ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.4 WHERE ItemTypeId = 'common_iron_mace';            -- 2500ms cooldown

-- HAMMERS: Extremely slow but highest damage
UPDATE itemtypes SET AttackSpeed = 0.25 WHERE ItemTypeId = 'common_battle_hammer';       -- 4000ms cooldown!
UPDATE itemtypes SET AttackSpeed = 0.3 WHERE ItemTypeId = 'rare_thunder_hammer';         -- 3333ms cooldown

-- LEGENDARY MELEE: Balanced but still respects timing
UPDATE itemtypes SET AttackSpeed = 0.7 WHERE ItemTypeId = 'legendary_world_ender';       -- 1429ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.7 WHERE ItemTypeId = 'epic_dragon_slayer';          -- 1429ms cooldown

-- BOWS: Slow and deliberate - no more arrow spam!
-- Each shot should be strategic and well-aimed
UPDATE itemtypes SET AttackSpeed = 0.6 WHERE ItemTypeId = 'common_wooden_bow';           -- 1667ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.65 WHERE ItemTypeId = 'common_short_bow';           -- 1538ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.5 WHERE ItemTypeId = 'uncommon_hunting_bow';        -- 2000ms cooldown  
UPDATE itemtypes SET AttackSpeed = 0.45 WHERE ItemTypeId = 'uncommon_composite_bow';     -- 2222ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.4 WHERE ItemTypeId = 'rare_elven_bow';              -- 2500ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.35 WHERE ItemTypeId = 'rare_longbow';               -- 2857ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.5 WHERE ItemTypeId = 'epic_storm_bow';              -- 2000ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.45 WHERE ItemTypeId = 'epic_void_bow';              -- 2222ms cooldown
UPDATE itemtypes SET AttackSpeed = 0.4 WHERE ItemTypeId = 'legendary_world_piercer';     -- 2500ms cooldown

-- Display the new cooldown times for reference:
-- Fast Daggers: 833-1000ms (0.8-1.2 second cooldown)
-- Medium Swords/Blades: 1000-1667ms (1.0-1.7 second cooldown)  
-- Slow Spears: 1667-2000ms (1.7-2.0 second cooldown)
-- Heavy Axes/Maces: 2500-3333ms (2.5-3.3 second cooldown)
-- Massive Hammers: 3333-4000ms (3.3-4.0 second cooldown!)
-- Strategic Bows: 1538-2857ms (1.5-2.9 second cooldown)

PRINT 'Weapon speeds adjusted for realistic combat timing!';
PRINT 'Cooldown ranges:';
PRINT '  - Fast Daggers: 0.8-1.2 seconds';
PRINT '  - Medium Swords: 1.0-1.7 seconds'; 
PRINT '  - Slow Spears: 1.7-2.0 seconds';
PRINT '  - Heavy Axes: 2.5-3.3 seconds';
PRINT '  - Massive Hammers: 3.3-4.0 seconds';
PRINT '  - Strategic Bows: 1.5-2.9 seconds';
PRINT 'Players will now need to time their attacks carefully!';