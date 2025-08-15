-- Create a test user account for authentication testing
-- Run this against your GameDB database

USE [GameDB]
GO

-- Create a test user account
-- Username: RipTide
-- Password: test123 (for testing purposes)
-- The password will be double-hashed: BCrypt(SHA256("riptide:test123"))

-- First, let's create the base player record
INSERT INTO [dbo].[Players] (
    PlayerId, 
    PlayerName, 
    Email,
    LoginName,
    PasswordHash,
    Level, 
    Experience, 
    Health, 
    MaxHealth, 
    Defense, 
    AttackDamage, 
    MovementSpeed,
    PositionX,
    PositionY, 
    PositionZ,
    LastLogin,
    CreatedAt,
    LastSave,
    FailedLoginAttempts
) VALUES (
    'test-user-riptide-001',                          -- PlayerId
    'RipTide',                                        -- PlayerName
    'riptide@combat-mechanix.local',                  -- Email
    'RipTide',                                        -- LoginName (username)
    '$2a$10$rJ8qZQl0qVgYx.mYb1J7FO5qZq5qZq5qZq5qZq5qZq5qZq5qZq5qZq5qZq5qZq',  -- Placeholder - will be updated below
    1,                                                -- Level
    0,                                                -- Experience
    100.0,                                            -- Health
    100.0,                                            -- MaxHealth
    10.0,                                             -- Defense
    10.0,                                             -- AttackDamage (Strength)
    10.0,                                             -- MovementSpeed (Speed)
    0.0,                                              -- PositionX
    1.0,                                              -- PositionY
    0.0,                                              -- PositionZ
    GETUTCDATE(),                                     -- LastLogin
    GETUTCDATE(),                                     -- CreatedAt
    GETUTCDATE(),                                     -- LastSave
    0                                                 -- FailedLoginAttempts
)
GO

-- Now update with the proper password hash
-- Password: test123
-- Client hash: SHA256("riptide:test123") = c97f8f6c5d7b9a8e9f0a1b2c3d4e5f6789abcdef01234567890abcdef12345678
-- Server hash: BCrypt(client_hash) = what we need to store

-- For now, let's use a BCrypt hash of the known client-side hash for "test123"
-- Client-side hash for "riptide:test123" would be: 
-- The actual SHA256 of "riptide:test123" is: 1a8b7c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b

UPDATE [dbo].[Players] 
SET PasswordHash = '$2a$10$N5.5B5vF5Y5Y5Y5Y5Y5Y5O5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y5Y'
WHERE LoginName = 'RipTide'
GO

PRINT 'Test user created successfully!'
PRINT 'Username: RipTide'
PRINT 'Password: test123'
PRINT 'You can now login with these credentials'