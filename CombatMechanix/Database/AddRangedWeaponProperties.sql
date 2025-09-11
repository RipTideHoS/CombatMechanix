-- Add ranged weapon properties to itemtypes table
-- This enables weapons to have range, projectile speed, and accuracy properties

-- Add new columns for ranged weapon properties
ALTER TABLE itemtypes ADD WeaponType VARCHAR(20) DEFAULT 'Melee';
ALTER TABLE itemtypes ADD WeaponRange FLOAT DEFAULT 0;      -- Maximum effective range in Unity units
ALTER TABLE itemtypes ADD ProjectileSpeed FLOAT DEFAULT 0; -- Projectile travel speed in Unity units per second
ALTER TABLE itemtypes ADD Accuracy FLOAT DEFAULT 1.0;      -- Base accuracy (0.0-1.0, where 1.0 = perfect accuracy)
GO

-- Update existing melee weapons to explicitly be 'Melee' type
UPDATE itemtypes 
SET WeaponType = 'Melee' 
WHERE ItemCategory = 'Weapon' AND WeaponType IS NULL;

-- Set reasonable defaults for existing weapons
-- Melee weapons have 0 range and perfect accuracy for close combat
UPDATE itemtypes 
SET WeaponRange = 3.0, Accuracy = 1.0 
WHERE ItemCategory = 'Weapon' AND WeaponType = 'Melee';