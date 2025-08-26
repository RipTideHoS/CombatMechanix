-- Update existing weapons with appropriate attack speeds
-- Generally: faster/lighter weapons = higher attack speed, heavier weapons = lower attack speed

-- Basic/Starter weapons (moderate speed)
UPDATE itemtypes SET AttackSpeed = 1.2 WHERE ItemTypeId = 'basic_sword';
UPDATE itemtypes SET AttackSpeed = 1.5 WHERE ItemTypeId = 'starter_sword';

-- Common tier weapons (varied based on weapon type)
UPDATE itemtypes SET AttackSpeed = 1.4 WHERE ItemTypeId = 'common_iron_sword';
UPDATE itemtypes SET AttackSpeed = 0.8 WHERE ItemTypeId = 'common_bronze_axe';  -- Axes are slower
UPDATE itemtypes SET AttackSpeed = 0.7 WHERE ItemTypeId = 'common_wooden_club'; -- Clubs are slow
UPDATE itemtypes SET AttackSpeed = 2.2 WHERE ItemTypeId = 'common_copper_dagger'; -- Daggers are fast
UPDATE itemtypes SET AttackSpeed = 0.9 WHERE ItemTypeId = 'common_iron_mace';   -- Maces are slow
UPDATE itemtypes SET AttackSpeed = 0.6 WHERE ItemTypeId = 'common_battle_hammer'; -- Hammers are very slow

-- Uncommon tier weapons
UPDATE itemtypes SET AttackSpeed = 1.3 WHERE ItemTypeId = 'uncommon_steel_sword';
UPDATE itemtypes SET AttackSpeed = 1.6 WHERE ItemTypeId = 'uncommon_silver_sword'; -- Silver is lighter
UPDATE itemtypes SET AttackSpeed = 1.1 WHERE ItemTypeId = 'uncommon_steel_spear';  -- Spears moderate speed
UPDATE itemtypes SET AttackSpeed = 0.9 WHERE ItemTypeId = 'uncommon_war_axe';     -- War axes slower
UPDATE itemtypes SET AttackSpeed = 1.8 WHERE ItemTypeId = 'uncommon_curved_blade'; -- Curved blades fast
UPDATE itemtypes SET AttackSpeed = 2.4 WHERE ItemTypeId = 'uncommon_crystal_dagger'; -- Crystal daggers very fast

-- Rare tier weapons (magical properties can affect speed)
UPDATE itemtypes SET AttackSpeed = 1.5 WHERE ItemTypeId = 'rare_enchanted_blade';
UPDATE itemtypes SET AttackSpeed = 1.7 WHERE ItemTypeId = 'rare_flame_sword';     -- Fire magic enhances speed
UPDATE itemtypes SET AttackSpeed = 1.0 WHERE ItemTypeId = 'rare_ice_spear';       -- Ice spears moderate
UPDATE itemtypes SET AttackSpeed = 2.0 WHERE ItemTypeId = 'rare_shadow_blade';    -- Shadow blades are swift
UPDATE itemtypes SET AttackSpeed = 0.8 WHERE ItemTypeId = 'rare_thunder_hammer';  -- Thunder hammers slow but powerful
UPDATE itemtypes SET AttackSpeed = 2.6 WHERE ItemTypeId = 'rare_venomous_dagger'; -- Venomous daggers very fast

-- Epic tier weapons (high-end magical weapons)
UPDATE itemtypes SET AttackSpeed = 1.4 WHERE ItemTypeId = 'epic_dragon_slayer';   -- Heavy but balanced
UPDATE itemtypes SET AttackSpeed = 1.0 WHERE ItemTypeId = 'epic_void_axe';        -- Void axes bend reality, moderate speed
UPDATE itemtypes SET AttackSpeed = 1.2 WHERE ItemTypeId = 'epic_storm_spear';     -- Storm spears moderate-fast
UPDATE itemtypes SET AttackSpeed = 1.6 WHERE ItemTypeId = 'epic_soul_reaper';     -- Soul reapers swift like death
UPDATE itemtypes SET AttackSpeed = 1.8 WHERE ItemTypeId = 'epic_celestial_blade';  -- Divine weapons are graceful

-- Legendary tier weapon (ultimate balance of power and speed)
UPDATE itemtypes SET AttackSpeed = 1.5 WHERE ItemTypeId = 'legendary_world_ender'; -- Perfect balance