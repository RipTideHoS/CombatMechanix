# Test Equipment Setup Instructions

Follow these steps to test the Character Panel and equipment system:

## Step 1: Start the Server
```bash
cd CombatMechanix
dotnet run --urls http://localhost:5207
```

## Step 2: Create a Test Player
Open a web browser or use curl to create a test player:

```bash
# Using curl (if available)
curl -X POST "http://localhost:5207/test/createuser?username=testplayer&password=password123&playerName=Test Player"

# Or visit this URL in your browser:
http://localhost:5207/test/createuser?username=testplayer&password=password123&playerName=Test%20Player
```

The response will show the created player ID, something like:
```json
{
  "Success": true,
  "Message": "User created successfully",
  "PlayerId": "test_player_637891234567890123",
  "Username": "testplayer",
  "PlayerName": "Test Player"
}
```

## Step 3: Add Equipment to Database
1. Copy the PlayerId from the response above
2. Open the file `CombatMechanix/Database/AddTestEquipmentForPlayer.sql`
3. Replace `'YOUR_PLAYER_ID_HERE'` with the actual PlayerId (keep the single quotes)
4. Run the SQL script in SQL Server Management Studio or sqlcmd:

```sql
-- Example: If your PlayerId is "test_player_637891234567890123"
DECLARE @PlayerId NVARCHAR(50) = 'test_player_637891234567890123';
-- ... rest of the script
```

Or use sqlcmd:
```bash
sqlcmd -S Beast4070/CM01 -d GameDB -E -i "CombatMechanix/Database/AddTestEquipmentForPlayer.sql"
```

## Step 4: Test the Character Panel
1. Start your Unity client
2. Connect to the server at `ws://localhost:5207/ws`
3. Login with:
   - Username: `testplayer`
   - Password: `password123`
4. Once in game, press the **"C" key** to open the Character Panel
5. You should see 6 equipped items:
   - **Helmet**: Starter Helmet
   - **Chest**: Starter Armor  
   - **Legs**: Starter Pants
   - **Weapon**: Starter Sword
   - **Offhand**: Starter Shield
   - **Accessory**: Starter Amulet

## Step 5: Test Equipment Stats
The Character Panel should show:
- Equipment stats summary (ATK: +0, DEF: +0 since starter items have no stats)
- Item details when hovering over equipment slots
- Right-click to unequip items

## Step 6: Test Inventory Integration
1. Press **"I" key** to open inventory (Character Panel should close)
2. Press **"C" key** to open Character Panel (Inventory should close)
3. Verify mutual exclusion is working

## Alternative: Quick Database Setup
If you prefer to use an existing player, you can find player IDs with:

```sql
-- Find existing players
SELECT TOP 5 PlayerId, PlayerName, DateCreated 
FROM PlayerStats 
ORDER BY DateCreated DESC;
```

Then use one of those PlayerIds in the equipment script.

## Troubleshooting
- If no items appear, check the server logs for equipment request errors
- Verify the database connection is working
- Make sure the ItemTypes table has the starter items
- Check that PlayerEquipment table has the records for your player