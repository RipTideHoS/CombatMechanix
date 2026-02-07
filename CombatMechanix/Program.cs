using CombatMechanix.Services;
using CombatMechanix.Data;
using CombatMechanix.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<EnemyManager>();
builder.Services.AddSingleton<LootManager>();
builder.Services.AddSingleton<GrenadeManager>();
builder.Services.AddScoped<EquipmentManager>();
builder.Services.AddSingleton<EnemyAIManager>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<EnemyAIManager>>();
    var connectionManager = serviceProvider.GetRequiredService<WebSocketConnectionManager>();
    var enemyManager = serviceProvider.GetRequiredService<EnemyManager>();
    
    // Create delegate functions for AI manager to access enemy/player data
    var getEnemiesFunc = new Func<Task<List<CombatMechanix.Models.EnemyState>>>(async () => 
    {
        return enemyManager.GetAllEnemies();
    });
    
    var getPlayersFunc = new Func<Task<List<CombatMechanix.Models.PlayerState>>>(async () => 
    {
        // Use cached player data from WebSocketConnectionManager instead of creating new objects
        return await connectionManager.GetActivePlayersForAI();
    });
    
    var getEnemyFunc = new Func<string, CombatMechanix.Models.EnemyState?>(enemyId => 
    {
        return enemyManager.GetEnemy(enemyId);
    });
    
    return new EnemyAIManager(logger, connectionManager, serviceProvider, getEnemiesFunc, getPlayersFunc, getEnemyFunc);
});
builder.Services.AddScoped<IPlayerStatsRepository, SqlPlayerStatsRepository>();
builder.Services.AddScoped<IPlayerStatsService, PlayerStatsService>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IPlayerInventoryRepository, PlayerInventoryRepository>();
builder.Services.AddScoped<IPlayerEquipmentRepository, PlayerEquipmentRepository>();
builder.Services.AddScoped<CombatMechanix.Services.IAuthenticationService, CombatMechanix.Services.AuthenticationService>();
builder.Services.AddScoped<IAttackTimingService, AttackTimingService>();
builder.Services.AddSingleton<TerrainService>();
builder.Services.AddLogging();

// Configure CORS for Unity client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUnityClient", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Test database connection
using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IPlayerStatsRepository>();
    try
    {
        // Test connection by checking if we can query top players
        await repository.GetTopByLevelAsync(1);
        app.Logger.LogInformation("Database connection successful");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error connecting to database. Will proceed but functionality may be limited.");
        app.Logger.LogInformation("Check DatabaseInvestigationReport.txt for database structure details");
    }
}

// Initialize enemy, loot, grenade, and terrain systems
var enemyManager = app.Services.GetRequiredService<EnemyManager>();
var wsManager = app.Services.GetRequiredService<WebSocketConnectionManager>();
var lootManager = app.Services.GetRequiredService<LootManager>();
var grenadeManager = app.Services.GetRequiredService<GrenadeManager>();
var aiManager = app.Services.GetRequiredService<EnemyAIManager>();
var terrainService = app.Services.GetRequiredService<TerrainService>();

// Wire up dependencies
wsManager.SetEnemyManager(enemyManager);
wsManager.SetLootManager(lootManager);
wsManager.SetGrenadeManager(grenadeManager);
enemyManager.SetLootManager(lootManager);
enemyManager.SetAIManager(aiManager);
enemyManager.SetTerrainService(terrainService);

// Initialize default content
enemyManager.InitializeDefaultEnemies();
app.Logger.LogInformation("Enemy, loot, grenade, terrain, and AI systems initialized");

// Configure the HTTP request pipeline
app.UseCors("AllowUnityClient");

// Enable WebSocket support
app.UseWebSockets();

// Handle WebSocket connections
app.Map("/ws", async (HttpContext context, WebSocketConnectionManager wsManager) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await wsManager.HandleWebSocketAsync(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
    }
});

// Health check endpoint
app.MapGet("/", () => "Combat Mechanix WebSocket Server Running");

// Test endpoint to create a player manually
app.MapPost("/test/createplayer", async (IPlayerStatsService playerStatsService) =>
{
    var testPlayerId = "test_player_" + DateTime.Now.Ticks;
    var testPlayerName = "TestPlayer_" + DateTime.Now.ToString("HHmmss");
    
    try
    {
        var playerStats = await playerStatsService.CreatePlayerAsync(testPlayerId, testPlayerName);
        return Results.Ok(new { 
            Success = true, 
            Message = "Player created successfully",
            Player = new {
                playerStats.PlayerId,
                playerStats.PlayerName,
                playerStats.Level,
                playerStats.Experience,
                playerStats.Health,
                playerStats.MaxHealth
            }
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { 
            Success = false, 
            Message = $"Failed to create player: {ex.Message}",
            Error = ex.ToString()
        });
    }
});

// Server status endpoint
app.MapGet("/status", (WebSocketConnectionManager wsManager) => new
{
    Status = "Running",
    Connections = wsManager.GetConnectionCount(),
    Players = wsManager.GetPlayerCount(),
    ServerTime = DateTime.UtcNow
});

// Debug endpoint to check player health in database
app.MapGet("/debug/health/{playerId}", async (string playerId, IPlayerStatsRepository repository) =>
{
    try
    {
        var player = await repository.GetPlayerStatsAsync(playerId);
        if (player != null)
        {
            return Results.Ok(new
            {
                PlayerId = player.PlayerId,
                PlayerName = player.PlayerName,
                Health = player.Health,
                MaxHealth = player.MaxHealth,
                LastSave = player.UpdatedAt
            });
        }
        return Results.NotFound("Player not found");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Debug endpoint to check player health by username (login method)
app.MapGet("/debug/healthbyusername/{username}", async (string username, IPlayerStatsRepository repository) =>
{
    try
    {
        var player = await repository.GetByUsernameAsync(username);
        if (player != null)
        {
            return Results.Ok(new
            {
                PlayerId = player.PlayerId,
                PlayerName = player.PlayerName,
                LoginName = player.LoginName,
                Health = player.Health,
                MaxHealth = player.MaxHealth,
                LastSave = player.UpdatedAt
            });
        }
        return Results.NotFound("Player not found");
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Respawn endpoint for dead players
app.MapPost("/respawn/{playerId}", async (string playerId, IPlayerStatsRepository repository) =>
{
    try
    {
        var player = await repository.GetPlayerStatsAsync(playerId);
        if (player == null)
        {
            return Results.NotFound("Player not found");
        }

        if (player.Health > 0)
        {
            return Results.BadRequest(new { Success = false, Message = "Player is not dead" });
        }

        // Respawn player with full health
        await repository.UpdatePlayerHealthAsync(playerId, player.MaxHealth);
        
        return Results.Ok(new
        {
            Success = true,
            Message = "Player respawned successfully",
            PlayerId = playerId,
            Health = player.MaxHealth,
            MaxHealth = player.MaxHealth
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Success = false, Error = ex.Message });
    }
});

// Test endpoint to add equipment to a player
app.MapPost("/test/addequipment", async (IItemRepository itemRepository, IPlayerEquipmentRepository equipmentRepository, IConfiguration configuration, string? playerId) =>
{
    try
    {
        // Validate inputs
        if (string.IsNullOrEmpty(playerId))
        {
            return Results.BadRequest(new { Success = false, Message = "PlayerId is required" });
        }

        // Add test items to ItemTypes table first
        var testItems = new[]
        {
            new { ItemTypeId = "starter_sword", ItemName = "Starter Sword", Description = "A basic iron sword for new adventurers", ItemRarity = "Common", ItemCategory = "Weapon" },
            new { ItemTypeId = "starter_helmet", ItemName = "Iron Helmet", Description = "Basic iron head protection", ItemRarity = "Common", ItemCategory = "Helmet" },
            new { ItemTypeId = "starter_chestplate", ItemName = "Leather Chestplate", Description = "Simple leather chest armor", ItemRarity = "Common", ItemCategory = "Chest" },
            new { ItemTypeId = "starter_leggings", ItemName = "Chain Leggings", Description = "Basic chain leg protection", ItemRarity = "Common", ItemCategory = "Legs" },
            new { ItemTypeId = "starter_shield", ItemName = "Wooden Shield", Description = "A sturdy wooden shield", ItemRarity = "Common", ItemCategory = "Shield" },
            new { ItemTypeId = "starter_ring", ItemName = "Silver Ring", Description = "A simple silver ring with minor enchantments", ItemRarity = "Uncommon", ItemCategory = "Ring" },

            // Phase 3: Multi-projectile weapons for testing
            new { ItemTypeId = "combat_shotgun", ItemName = "Combat Shotgun", Description = "Fires 5 pellets in a cone spread - perfect for close range combat", ItemRarity = "Uncommon", ItemCategory = "Weapon" },
            new { ItemTypeId = "burst_rifle", ItemName = "Burst Rifle", Description = "Fires 3 rounds in quick succession with slight horizontal spread", ItemRarity = "Rare", ItemCategory = "Weapon" },
            new { ItemTypeId = "scatter_gun", ItemName = "Scatter Gun", Description = "Wide spread scatter weapon firing 5 projectiles", ItemRarity = "Common", ItemCategory = "Weapon" },
            new { ItemTypeId = "basic_bow", ItemName = "Basic Bow", Description = "Simple single-shot ranged weapon for testing", ItemRarity = "Common", ItemCategory = "Weapon" },

            // Phase 4: Grenade system items
            new { ItemTypeId = "frag_grenade", ItemName = "Fragmentation Grenade", Description = "High-explosive grenade with wide damage radius", ItemRarity = "Uncommon", ItemCategory = "Grenade" },
            new { ItemTypeId = "smoke_grenade", ItemName = "Smoke Grenade", Description = "Creates smoke cloud that blocks vision", ItemRarity = "Common", ItemCategory = "Grenade" },
            new { ItemTypeId = "flash_grenade", ItemName = "Flash Grenade", Description = "Blinds and disorients enemies in area", ItemRarity = "Rare", ItemCategory = "Grenade" }
        };

        // Add test items to database if they don't exist
        foreach (var item in testItems)
        {
            var existingItem = await itemRepository.GetItemByIdAsync(item.ItemTypeId);
            if (existingItem == null)
            {
                // Add item to ItemTypes table directly since ItemRepository doesn't have an Add method
                // This is acceptable for test data setup
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    INSERT INTO ItemTypes (ItemTypeId, ItemName, Description, ItemRarity, ItemCategory, MaxStackSize, IconPath, AttackPower, DefensePower, BaseValue, WeaponType, WeaponRange, ProjectileSpeed, Accuracy, ExplosionRadius, ExplosionDelay, ThrowRange, AreaDamage, GrenadeType)
                    VALUES (@ItemTypeId, @ItemName, @Description, @ItemRarity, @ItemCategory, @MaxStackSize, @IconPath, @AttackPower, @DefensePower, @BaseValue, @WeaponType, @WeaponRange, @ProjectileSpeed, @Accuracy, @ExplosionRadius, @ExplosionDelay, @ThrowRange, @AreaDamage, @GrenadeType)";

                using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ItemTypeId", item.ItemTypeId);
                command.Parameters.AddWithValue("@ItemName", item.ItemName);
                command.Parameters.AddWithValue("@Description", item.Description);
                command.Parameters.AddWithValue("@ItemRarity", item.ItemRarity);
                command.Parameters.AddWithValue("@ItemCategory", item.ItemCategory);
                command.Parameters.AddWithValue("@MaxStackSize", item.ItemCategory == "Grenade" ? 5 : 1);
                command.Parameters.AddWithValue("@IconPath", $"{item.ItemTypeId}_icon");
                command.Parameters.AddWithValue("@AttackPower", item.ItemCategory == "Weapon" ? 10 : 0);
                command.Parameters.AddWithValue("@DefensePower", item.ItemCategory != "Weapon" && item.ItemCategory != "Ring" ? 5 : 0);
                command.Parameters.AddWithValue("@BaseValue", 10);

                // Phase 3: Set weapon properties for multi-projectile testing
                bool isRangedWeapon = item.ItemTypeId.Contains("shotgun") || item.ItemTypeId.Contains("rifle") ||
                                     item.ItemTypeId.Contains("bow") || item.ItemTypeId.Contains("scatter");
                bool isGrenade = item.ItemCategory == "Grenade";

                command.Parameters.AddWithValue("@WeaponType", isRangedWeapon ? "Ranged" : "Melee");
                command.Parameters.AddWithValue("@WeaponRange", isRangedWeapon ? 30.0f : 2.0f);
                command.Parameters.AddWithValue("@ProjectileSpeed", isRangedWeapon ? 25.0f : 0.0f);
                command.Parameters.AddWithValue("@Accuracy", 0.85f);

                // Grenade properties
                command.Parameters.AddWithValue("@ExplosionRadius", isGrenade ? GetGrenadeRadius(item.ItemTypeId) : 0.0f);
                command.Parameters.AddWithValue("@ExplosionDelay", isGrenade ? GetGrenadeDelay(item.ItemTypeId) : 0.0f);
                command.Parameters.AddWithValue("@ThrowRange", isGrenade ? 25.0f : 0.0f);
                command.Parameters.AddWithValue("@AreaDamage", isGrenade ? GetGrenadeDamage(item.ItemTypeId) : 0.0f);
                command.Parameters.AddWithValue("@GrenadeType", isGrenade ? GetGrenadeType(item.ItemTypeId) : DBNull.Value);
                
                await command.ExecuteNonQueryAsync();
            }
        }

        // Helper methods for grenade properties
        static float GetGrenadeRadius(string grenadeId) => grenadeId switch
        {
            "frag_grenade" => 5.0f,
            "smoke_grenade" => 8.0f,
            "flash_grenade" => 4.0f,
            _ => 0.0f
        };

        static float GetGrenadeDelay(string grenadeId) => grenadeId switch
        {
            "frag_grenade" => 3.0f,
            "smoke_grenade" => 2.0f,
            "flash_grenade" => 2.5f,
            _ => 0.0f
        };

        static float GetGrenadeDamage(string grenadeId) => grenadeId switch
        {
            "frag_grenade" => 75.0f,
            "smoke_grenade" => 0.0f,
            "flash_grenade" => 30.0f,
            _ => 0.0f
        };

        static string GetGrenadeType(string grenadeId) => grenadeId switch
        {
            "frag_grenade" => "Explosive",
            "smoke_grenade" => "Smoke",
            "flash_grenade" => "Flash",
            _ => "Unknown"
        };

        // Clear existing equipment for this player
        await equipmentRepository.UnequipItemAsync(playerId, "Weapon");
        await equipmentRepository.UnequipItemAsync(playerId, "Helmet");
        await equipmentRepository.UnequipItemAsync(playerId, "Chest");
        await equipmentRepository.UnequipItemAsync(playerId, "Legs");
        await equipmentRepository.UnequipItemAsync(playerId, "Offhand");
        await equipmentRepository.UnequipItemAsync(playerId, "Accessory");

        // Add equipment items
        var equipmentSlots = new[]
        {
            new { ItemTypeId = "starter_sword", SlotType = "Weapon" },
            new { ItemTypeId = "starter_helmet", SlotType = "Helmet" },
            new { ItemTypeId = "starter_chestplate", SlotType = "Chest" },
            new { ItemTypeId = "starter_leggings", SlotType = "Legs" },
            new { ItemTypeId = "starter_shield", SlotType = "Offhand" },
            new { ItemTypeId = "starter_ring", SlotType = "Accessory" }
        };

        var equippedItems = new List<object>();
        foreach (var slot in equipmentSlots)
        {
            await equipmentRepository.EquipItemAsync(playerId, slot.ItemTypeId, slot.SlotType);
            equippedItems.Add(new { ItemType = slot.ItemTypeId, SlotType = slot.SlotType });
        }

        return Results.Ok(new { 
            Success = true, 
            Message = "Equipment added successfully",
            PlayerId = playerId,
            EquippedItems = equippedItems,
            Note = "You can now login and press 'C' to view the Character Panel"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { 
            Success = false, 
            Message = $"Failed to add equipment: {ex.Message}",
            Error = ex.ToString()
        });
    }
});

// Test endpoint to create a user account
app.MapPost("/test/createuser", async (IAuthenticationService authService, string? username, string? password, string? playerName) =>
{
    try
    {
        // Validate inputs
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return Results.BadRequest(new { Success = false, Message = "Username and password are required" });
        }

        if (string.IsNullOrEmpty(playerName))
            playerName = username; // Use username as player name if not provided

        // Client-side hash the password (simulate what the client would do)
        string saltedPassword = $"{username.ToLowerInvariant()}:{password}";
        string clientHashedPassword;
        
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(saltedPassword));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hashedBytes.Length; i++)
            {
                sb.Append(hashedBytes[i].ToString("x2"));
            }
            clientHashedPassword = sb.ToString();
        }

        // Create the user using the authentication service
        string playerId = await authService.CreatePlayerWithCredentialsAsync(username, clientHashedPassword, playerName);
        
        return Results.Ok(new { 
            Success = true, 
            Message = "User created successfully",
            PlayerId = playerId,
            Username = username,
            PlayerName = playerName,
            Note = "You can now login with the provided credentials"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { 
            Success = false, 
            Message = $"Failed to create user: {ex.Message}",
            Error = ex.ToString()
        });
    }
});

// Diagnostic endpoint to check equipment database state
app.MapGet("/debug/equipment/{playerId}", async (string playerId, IConfiguration configuration) =>
{
    try
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        
        // Check PlayerEquipment table
        const string equipmentSql = "SELECT * FROM PlayerEquipment WHERE PlayerId = @PlayerId";
        using var equipCmd = new Microsoft.Data.SqlClient.SqlCommand(equipmentSql, connection);
        equipCmd.Parameters.AddWithValue("@PlayerId", playerId);
        
        var equipmentRecords = new List<object>();
        using var equipReader = await equipCmd.ExecuteReaderAsync();
        while (await equipReader.ReadAsync())
        {
            equipmentRecords.Add(new
            {
                EquipmentId = equipReader["EquipmentId"].ToString(),
                ItemTypeId = equipReader["ItemTypeId"].ToString(),
                SlotType = equipReader["SlotType"].ToString(),
                DateEquipped = equipReader["DateEquipped"]
            });
        }
        equipReader.Close();
        
        // Check ItemTypes table for the equipped items
        var itemTypeIds = equipmentRecords.Select(e => ((dynamic)e).ItemTypeId).ToArray();
        var itemDetails = new List<object>();
        
        if (itemTypeIds.Length > 0)
        {
            var itemSql = $"SELECT ItemTypeId, ItemName, AttackPower, DefensePower, ItemCategory FROM ItemTypes WHERE ItemTypeId IN ({string.Join(",", itemTypeIds.Select((_, i) => $"@ItemTypeId{i}"))})";
            using var itemCmd = new Microsoft.Data.SqlClient.SqlCommand(itemSql, connection);
            
            for (int i = 0; i < itemTypeIds.Length; i++)
            {
                itemCmd.Parameters.AddWithValue($"@ItemTypeId{i}", itemTypeIds[i]);
            }
            
            using var itemReader = await itemCmd.ExecuteReaderAsync();
            while (await itemReader.ReadAsync())
            {
                itemDetails.Add(new
                {
                    ItemTypeId = itemReader["ItemTypeId"].ToString(),
                    ItemName = itemReader["ItemName"].ToString(),
                    AttackPower = itemReader["AttackPower"] != DBNull.Value ? Convert.ToInt32(itemReader["AttackPower"]) : 0,
                    DefensePower = itemReader["DefensePower"] != DBNull.Value ? Convert.ToInt32(itemReader["DefensePower"]) : 0,
                    ItemCategory = itemReader["ItemCategory"].ToString()
                });
            }
        }
        
        // Calculate what the totals should be
        int expectedAttackPower = itemDetails.Sum(item => ((dynamic)item).AttackPower);
        int expectedDefensePower = itemDetails.Sum(item => ((dynamic)item).DefensePower);
        
        return Results.Ok(new
        {
            PlayerId = playerId,
            EquipmentRecords = equipmentRecords,
            ItemDetails = itemDetails,
            ExpectedTotals = new { AttackPower = expectedAttackPower, DefensePower = expectedDefensePower },
            Summary = $"Player has {equipmentRecords.Count} equipped items with total ATK +{expectedAttackPower}, DEF +{expectedDefensePower}"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message, StackTrace = ex.ToString() });
    }
});

// Diagnostic endpoint to check server cached PlayerState
app.MapGet("/debug/cache/{playerId}", (string playerId, WebSocketConnectionManager wsManager) =>
{
    try
    {
        var cachedPlayer = wsManager.GetCachedPlayerByPlayerId(playerId);
        
        if (cachedPlayer != null)
        {
            return Results.Ok(new
            {
                PlayerId = cachedPlayer.PlayerId,
                PlayerName = cachedPlayer.PlayerName,
                Level = cachedPlayer.Level,
                Health = cachedPlayer.Health,
                MaxHealth = cachedPlayer.MaxHealth,
                Strength = cachedPlayer.Strength,
                Defense = cachedPlayer.Defense,
                EquipmentAttackPower = cachedPlayer.EquipmentAttackPower,
                EquipmentDefensePower = cachedPlayer.EquipmentDefensePower,
                TotalAttackPower = cachedPlayer.TotalAttackPower,
                TotalDefensePower = cachedPlayer.TotalDefensePower,
                LastUpdate = cachedPlayer.LastUpdate,
                IsOnline = cachedPlayer.IsOnline
            });
        }
        else
        {
            return Results.NotFound(new { Message = "Player not found in server cache" });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message, StackTrace = ex.ToString() });
    }
});

// Terrain API endpoint - serves terrain data to clients
app.MapGet("/api/terrain", (TerrainService terrainService) =>
{
    try
    {
        var terrainData = terrainService.GetTerrainData();
        return Results.Ok(terrainData);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Terrain management endpoints
app.MapPost("/api/terrain/load/{hillSetName}", (string hillSetName, TerrainService terrainService) =>
{
    try
    {
        bool success = terrainService.LoadHillSet(hillSetName);
        return Results.Ok(new { Success = success, HillSet = hillSetName, Message = success ? "Hill set loaded" : "Hill set not found" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

app.MapPost("/api/terrain/unload/{hillSetName}", (string hillSetName, TerrainService terrainService) =>
{
    try
    {
        bool success = terrainService.UnloadHillSet(hillSetName);
        return Results.Ok(new { Success = success, HillSet = hillSetName, Message = success ? "Hill set unloaded" : "Hill set not currently loaded" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

app.MapGet("/api/terrain/hillsets", (TerrainService terrainService) =>
{
    try
    {
        var available = terrainService.GetAvailableHillSets();
        var active = terrainService.GetActiveHillSets();
        return Results.Ok(new { Available = available, Active = active });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

// Database migration endpoint for grenade system
app.MapPost("/migration/add-grenade-columns", async (IConfiguration configuration) =>
{
    try
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            return Results.BadRequest(new { Success = false, Message = "Database connection string not found" });
        }

        // Execute the migration script
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();

        // Check if columns already exist before adding them
        var columnCheckSql = @"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'ItemTypes' AND COLUMN_NAME = 'ExplosionRadius'
        ";

        using var checkCommand = new Microsoft.Data.SqlClient.SqlCommand(columnCheckSql, connection);
        var columnExists = (int)await checkCommand.ExecuteScalarAsync() > 0;

        if (!columnExists)
        {
            var addColumnsSql = @"
                ALTER TABLE ItemTypes ADD ExplosionRadius FLOAT DEFAULT 0.0;
                ALTER TABLE ItemTypes ADD ExplosionDelay FLOAT DEFAULT 0.0;
                ALTER TABLE ItemTypes ADD ThrowRange FLOAT DEFAULT 0.0;
                ALTER TABLE ItemTypes ADD AreaDamage FLOAT DEFAULT 0.0;
                ALTER TABLE ItemTypes ADD GrenadeType VARCHAR(50) DEFAULT NULL;
            ";

            using var addCommand = new Microsoft.Data.SqlClient.SqlCommand(addColumnsSql, connection);
            await addCommand.ExecuteNonQueryAsync();

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

            using var updateCommand = new Microsoft.Data.SqlClient.SqlCommand(updateGrenadesSql, connection);
            var rowsUpdated = await updateCommand.ExecuteNonQueryAsync();

            return Results.Ok(new
            {
                Success = true,
                Message = "Grenade columns added successfully",
                ColumnsAdded = new[] { "ExplosionRadius", "ExplosionDelay", "ThrowRange", "AreaDamage", "GrenadeType" },
                GrenadeItemsUpdated = rowsUpdated
            });
        }
        else
        {
            return Results.Ok(new
            {
                Success = true,
                Message = "Grenade columns already exist. No migration needed.",
                ColumnsAdded = new string[0],
                GrenadeItemsUpdated = 0
            });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new
        {
            Success = false,
            Message = $"Failed to add grenade columns: {ex.Message}",
            Error = ex.ToString()
        });
    }
});

// Debug endpoint to force refresh equipment stats for cached player
app.MapPost("/debug/refresh-equipment/{playerId}", async (string playerId, WebSocketConnectionManager wsManager, IServiceProvider serviceProvider) =>
{
    try
    {
        Console.WriteLine($"[DEBUG] Force refresh equipment stats for player {playerId}");
        
        // Get the cached player
        var cachedPlayer = wsManager.GetCachedPlayerByPlayerId(playerId);
        if (cachedPlayer == null)
        {
            return Results.NotFound(new { Message = "Player not found in server cache" });
        }

        Console.WriteLine($"[DEBUG] Found cached player: {cachedPlayer.PlayerName}");
        Console.WriteLine($"[DEBUG] Before refresh - Equipment stats: ATK +{cachedPlayer.EquipmentAttackPower}, DEF +{cachedPlayer.EquipmentDefensePower}");
        
        // Force recalculate equipment stats
        using var scope = serviceProvider.CreateScope();
        var equipmentManager = scope.ServiceProvider.GetRequiredService<EquipmentManager>();
        var (equipmentAttackPower, equipmentDefensePower, equipmentAttackSpeed) = await equipmentManager.CalculateEquipmentStatsAsync(playerId);
        
        // Update cached player stats
        cachedPlayer.EquipmentAttackPower = equipmentAttackPower;
        cachedPlayer.EquipmentDefensePower = equipmentDefensePower;
        cachedPlayer.EquipmentAttackSpeed = equipmentAttackSpeed;
        cachedPlayer.LastUpdate = DateTime.UtcNow;
        
        Console.WriteLine($"[DEBUG] After refresh - Equipment stats: ATK +{cachedPlayer.EquipmentAttackPower}, DEF +{cachedPlayer.EquipmentDefensePower}");
        Console.WriteLine($"[DEBUG] Total stats now: ATK {cachedPlayer.TotalAttackPower}, DEF {cachedPlayer.TotalDefensePower}");

        return Results.Ok(new
        {
            Success = true,
            Message = "Equipment stats refreshed successfully",
            PlayerId = cachedPlayer.PlayerId,
            PlayerName = cachedPlayer.PlayerName,
            EquipmentAttackPower = cachedPlayer.EquipmentAttackPower,
            EquipmentDefensePower = cachedPlayer.EquipmentDefensePower,
            TotalAttackPower = cachedPlayer.TotalAttackPower,
            TotalDefensePower = cachedPlayer.TotalDefensePower
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG] Error refreshing equipment stats: {ex.Message}");
        return Results.BadRequest(new { Error = ex.Message, StackTrace = ex.ToString() });
    }
});

// Diagnostic endpoint to test AttackTimingService functionality
app.MapGet("/debug/attack-timing", (IServiceProvider serviceProvider) =>
{
    try
    {
        using var scope = serviceProvider.CreateScope();
        var attackTimingService = scope.ServiceProvider.GetRequiredService<IAttackTimingService>();
        
        var basicTestResults = AttackTimingServiceTests.RunBasicTests(attackTimingService);
        var performanceTestResults = AttackTimingServiceTests.RunPerformanceTest(attackTimingService, 1000);
        
        return Results.Ok(new 
        {
            Timestamp = DateTime.UtcNow,
            Service = "AttackTimingService",
            BasicTests = basicTestResults,
            PerformanceTests = performanceTestResults
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new 
        {
            Error = ex.Message,
            StackTrace = ex.StackTrace,
            Timestamp = DateTime.UtcNow
        });
    }
});

// Phase 3: Quick endpoint to equip multi-projectile test weapons
app.MapPost("/test/equipweapon/{playerId}/{weaponId}", async (string playerId, string weaponId, IPlayerEquipmentRepository equipmentRepository) =>
{
    try
    {
        // Valid weapon IDs for testing
        var validWeapons = new[] { "combat_shotgun", "burst_rifle", "scatter_gun", "basic_bow", "starter_sword" };
        if (!validWeapons.Contains(weaponId))
        {
            return Results.BadRequest(new { Success = false, Message = $"Invalid weapon ID. Valid options: {string.Join(", ", validWeapons)}" });
        }

        // Unequip current weapon
        await equipmentRepository.UnequipItemAsync(playerId, "Weapon");

        // Equip new weapon
        bool success = await equipmentRepository.EquipItemAsync(playerId, weaponId, "Weapon");

        if (success)
        {
            return Results.Ok(new
            {
                Success = true,
                Message = $"Successfully equipped {weaponId}",
                PlayerId = playerId,
                WeaponId = weaponId
            });
        }
        else
        {
            return Results.BadRequest(new { Success = false, Message = "Failed to equip weapon" });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Success = false, Message = ex.Message });
    }
});

app.Run();
