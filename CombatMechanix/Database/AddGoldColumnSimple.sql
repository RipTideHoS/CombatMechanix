-- Simple Gold column addition script
-- Check if Gold column exists first
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Players' AND COLUMN_NAME = 'Gold')
BEGIN
    -- Add Gold column with default value of 100
    ALTER TABLE Players 
    ADD Gold INT NOT NULL DEFAULT 100;
    
    PRINT 'Gold column added successfully with default value of 100';
END
ELSE
BEGIN
    PRINT 'Gold column already exists in Players table';
END

-- Show the table structure
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Players' AND COLUMN_NAME = 'Gold';