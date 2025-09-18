using Microsoft.Data.SqlClient;
using System.Configuration;

namespace CombatMechanix.Scripts
{
    /// <summary>
    /// Database migration script to add grenade-specific columns to ItemTypes table
    /// </summary>
    public static class AddGrenadeColumns
    {
        public static async Task ExecuteAsync(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Check if columns already exist before adding them
            var columnCheckSql = @"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'ItemTypes' AND COLUMN_NAME = 'ExplosionRadius'
            ";

            using var checkCommand = new SqlCommand(columnCheckSql, connection);
            var columnExists = (int)await checkCommand.ExecuteScalarAsync() > 0;

            if (!columnExists)
            {
                Console.WriteLine("Adding grenade columns to ItemTypes table...");

                var addColumnsSql = @"
                    ALTER TABLE ItemTypes ADD ExplosionRadius FLOAT DEFAULT 0.0;
                    ALTER TABLE ItemTypes ADD ExplosionDelay FLOAT DEFAULT 0.0;
                    ALTER TABLE ItemTypes ADD ThrowRange FLOAT DEFAULT 0.0;
                    ALTER TABLE ItemTypes ADD AreaDamage FLOAT DEFAULT 0.0;
                    ALTER TABLE ItemTypes ADD GrenadeType VARCHAR(50) DEFAULT NULL;
                ";

                using var addCommand = new SqlCommand(addColumnsSql, connection);
                await addCommand.ExecuteNonQueryAsync();

                Console.WriteLine("Successfully added grenade columns to ItemTypes table.");

                // Update existing grenade items with proper stats
                var updateGrenadesSql = @"
                    UPDATE ItemTypes SET
                        ExplosionRadius = 5.0,
                        ExplosionDelay = 3.0,
                        ThrowRange = 25.0,
                        AreaDamage = 75.0,
                        GrenadeType = 'Explosive'
                    WHERE ItemTypeId = 'frag_grenade';

                    UPDATE ItemTypes SET
                        ExplosionRadius = 8.0,
                        ExplosionDelay = 2.0,
                        ThrowRange = 20.0,
                        AreaDamage = 0.0,
                        GrenadeType = 'Smoke'
                    WHERE ItemTypeId = 'smoke_grenade';

                    UPDATE ItemTypes SET
                        ExplosionRadius = 4.0,
                        ExplosionDelay = 2.5,
                        ThrowRange = 18.0,
                        AreaDamage = 30.0,
                        GrenadeType = 'Flash'
                    WHERE ItemTypeId = 'flash_grenade';
                ";

                using var updateCommand = new SqlCommand(updateGrenadesSql, connection);
                var rowsUpdated = await updateCommand.ExecuteNonQueryAsync();

                Console.WriteLine($"Updated {rowsUpdated} grenade items with proper stats.");

                // Insert grenade items if they don't exist
                await InsertGrenadeItems(connection);
            }
            else
            {
                Console.WriteLine("Grenade columns already exist in ItemTypes table. Skipping migration.");
            }
        }

        private static async Task InsertGrenadeItems(SqlConnection connection)
        {
            Console.WriteLine("Inserting grenade items into ItemTypes table...");

            // Check if grenade items already exist
            var checkSql = @"
                SELECT COUNT(*) FROM ItemTypes WHERE ItemTypeId IN ('frag_grenade', 'smoke_grenade', 'flash_grenade')
            ";

            using var checkCommand = new SqlCommand(checkSql, connection);
            var existingCount = (int)await checkCommand.ExecuteScalarAsync();

            if (existingCount > 0)
            {
                Console.WriteLine($"Found {existingCount} existing grenade items. Skipping insert.");
                return;
            }

            var insertSql = @"
                INSERT INTO ItemTypes (
                    ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize,
                    AttackPower, AreaDamage, ExplosionRadius, ExplosionDelay, ThrowRange, GrenadeType,
                    IconName, Value, Level
                ) VALUES
                ('frag_grenade', 'Fragmentation Grenade', 'High-explosive grenade with wide damage radius', 'Uncommon', 'Grenade', 5, 0, 75, 5.0, 3.0, 25.0, 'Explosive', 'frag_grenade_icon', 50, 1),
                ('smoke_grenade', 'Smoke Grenade', 'Creates smoke cloud that blocks vision', 'Common', 'Grenade', 10, 0, 0, 8.0, 2.0, 20.0, 'Smoke', 'smoke_grenade_icon', 25, 1),
                ('flash_grenade', 'Flash Grenade', 'Blinds and disorients enemies in area', 'Rare', 'Grenade', 3, 0, 30, 4.0, 2.5, 18.0, 'Flash', 'flash_grenade_icon', 75, 1);
            ";

            using var insertCommand = new SqlCommand(insertSql, connection);
            var insertedRows = await insertCommand.ExecuteNonQueryAsync();

            Console.WriteLine($"Successfully inserted {insertedRows} grenade items into ItemTypes table.");
        }
    }
}