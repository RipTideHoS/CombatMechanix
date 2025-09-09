-- Add Gold column to Players table
-- This migration adds a Gold column with a default starting value

-- Check if Gold column doesn't exist before adding it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Players' AND COLUMN_NAME = 'Gold')
BEGIN
    -- Add Gold column with default value of 100 starting gold
    ALTER TABLE Players 
    ADD Gold INT NOT NULL DEFAULT 100;
    
    PRINT 'Gold column added to Players table with default value of 100';
END
ELSE
BEGIN
    PRINT 'Gold column already exists in Players table';
END

-- Update existing players to have starting gold if they have 0 (only if column exists)
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Players' AND COLUMN_NAME = 'Gold')
BEGIN
    UPDATE Players 
    SET Gold = 100 
    WHERE Gold = 0 OR Gold IS NULL;
    
    PRINT 'Updated existing players to have starting gold of 100';
END