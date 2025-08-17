-- Add NextLevelExp column to Players table and populate with calculated values
-- This script preserves existing data while adding the new level progression tracking

USE GameDB;
GO

-- Step 1: Add the NextLevelExp column if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Players' AND COLUMN_NAME = 'NextLevelExp')
BEGIN
    ALTER TABLE Players 
    ADD NextLevelExp BIGINT NOT NULL DEFAULT 0;
    PRINT 'Added NextLevelExp column to Players table';
END
ELSE
BEGIN
    PRINT 'NextLevelExp column already exists';
END

-- Step 2: Create a function to calculate experience for next level
-- Using the same formula as in C# code: (level^2 * 100)
IF OBJECT_ID('dbo.CalculateNextLevelExp', 'FN') IS NOT NULL
    DROP FUNCTION dbo.CalculateNextLevelExp;
GO

CREATE FUNCTION dbo.CalculateNextLevelExp(@currentLevel INT, @currentExp BIGINT)
RETURNS BIGINT
AS
BEGIN
    DECLARE @nextLevelTotalExp BIGINT;
    DECLARE @remainingExp BIGINT;
    
    -- Calculate total experience needed for next level
    SET @nextLevelTotalExp = (@currentLevel * @currentLevel * 100);
    
    -- Calculate remaining experience needed
    SET @remainingExp = @nextLevelTotalExp - @currentExp;
    
    -- Ensure we don't return negative values
    IF @remainingExp < 0
        SET @remainingExp = 0;
        
    RETURN @remainingExp;
END
GO

-- Step 3: Update all existing players with correct NextLevelExp values
UPDATE Players 
SET NextLevelExp = dbo.CalculateNextLevelExp(Level, Experience)
WHERE NextLevelExp = 0 OR NextLevelExp IS NULL;

-- Step 4: Display updated player information
SELECT 
    PlayerId,
    PlayerName,
    LoginName,
    Level,
    Experience,
    NextLevelExp,
    (Level * Level * 100) AS TotalExpForNextLevel,
    CASE 
        WHEN NextLevelExp <= 0 THEN 'Max Level or Ready to Level Up'
        ELSE CAST(NextLevelExp AS VARCHAR(20)) + ' XP needed'
    END AS Status
FROM Players
ORDER BY Level DESC, Experience DESC;

PRINT 'Successfully updated NextLevelExp for all players';

-- Clean up the function (optional - remove if you want to keep it)
-- DROP FUNCTION dbo.CalculateNextLevelExp;