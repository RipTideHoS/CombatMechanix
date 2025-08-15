# GameDB Players Table Mapping

## Existing Players Table Schema
The existing `Players` table in GameDB contains the following relevant columns:

| GameDB Column     | Data Type      | PlayerStats Model | Notes |
|------------------|----------------|-------------------|--------|
| PlayerId         | nvarchar(50)   | PlayerId         | ✅ Perfect match |
| PlayerName       | nvarchar(100)  | PlayerName       | ✅ Perfect match |
| Level            | int            | Level            | ✅ Perfect match |
| Experience       | int            | Experience       | ⚠️ Type difference (int vs long) |
| Health           | float          | Health           | ⚠️ Type difference (float vs int) |
| MaxHealth        | float          | MaxHealth        | ⚠️ Type difference (float vs int) |
| Defense          | float          | Defense          | ⚠️ Type difference (float vs int) |
| PositionX        | float          | LastPosition.X   | ✅ Can combine into Vector3Data |
| PositionY        | float          | LastPosition.Y   | ✅ Can combine into Vector3Data |
| PositionZ        | float          | LastPosition.Z   | ✅ Can combine into Vector3Data |
| CreatedAt        | datetime2      | CreatedAt        | ✅ Perfect match |
| LastLogin        | datetime2      | LastLogin        | ✅ Perfect match |
| LastSave         | datetime2      | UpdatedAt        | ✅ Can map to UpdatedAt |

## Missing Columns in GameDB (need to calculate or default)
- **Strength**: Not in GameDB, will use AttackDamage as basis or default to 10
- **Speed**: Not in GameDB, will use MovementSpeed as basis or default to 10

## Additional Columns in GameDB (not used in PlayerStats)
- Email, PasswordHash (authentication)
- Mana, MaxMana (magic system)
- AttackDamage, AttackRange, CriticalChance (combat stats)
- IsAlive, IsBanned (status flags)
- Inventory (JSON data)
- TotalPlayTime, PlayerKills, PlayerDeaths, ResourcesGathered (statistics)

## Mapping Strategy
1. **Use existing Players table** instead of creating PlayerStats table
2. **Map closely matching fields** directly
3. **Handle type conversions** safely (float ↔ int, int ↔ long)
4. **Derive missing stats** from existing columns where possible
5. **Store position as separate X,Y,Z columns** instead of JSON

## Implementation Notes
- Experience: Convert between int (DB) and long (model)
- Health/MaxHealth/Defense: Convert between float (DB) and int (model)
- Strength: Map from AttackDamage or default to 10
- Speed: Map from MovementSpeed or default to 10
- LastPosition: Combine PositionX/Y/Z into Vector3Data
- UpdatedAt: Use LastSave column