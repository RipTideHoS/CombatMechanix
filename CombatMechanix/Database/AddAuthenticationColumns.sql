-- Add Authentication Columns to existing Players table
-- Run this against your GameDB database

USE [GameDB]
GO

-- Add authentication columns to existing Players table (avoiding existing columns)
ALTER TABLE [dbo].[Players] 
ADD 
    [LoginName] nvarchar(50) NULL,               -- Using LoginName instead of Username
    -- [PasswordHash] already exists in table
    [PasswordSalt] nvarchar(32) NULL,            -- Using PasswordSalt instead of Salt
    [SessionToken] nvarchar(128) NULL,
    [SessionExpiry] datetime2 NULL,
    [FailedLoginAttempts] int NOT NULL DEFAULT 0,
    [LastLoginAttempt] datetime2 NULL
GO

-- Create unique index on LoginName (after adding column)
CREATE UNIQUE NONCLUSTERED INDEX [IX_Players_LoginName] ON [dbo].[Players]
(
    [LoginName] ASC
) WHERE [LoginName] IS NOT NULL
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

-- Create index on SessionToken for quick lookups
CREATE NONCLUSTERED INDEX [IX_Players_SessionToken] ON [dbo].[Players]
(
    [SessionToken] ASC
) WHERE [SessionToken] IS NOT NULL
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

-- Update existing players with default login names (temporary - for existing test data)
UPDATE [dbo].[Players] 
SET [LoginName] = 'user_' + SUBSTRING([PlayerId], 1, 8)
WHERE [LoginName] IS NULL
GO

PRINT 'Authentication columns added successfully!'
PRINT 'Players table now supports username/password authentication'
PRINT 'Existing players have been given default usernames'