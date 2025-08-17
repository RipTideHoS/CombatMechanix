using CombatMechanix.Services;
using CombatMechanix.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddSingleton<EnemyManager>();
builder.Services.AddSingleton<LootManager>();
builder.Services.AddScoped<IPlayerStatsRepository, SqlPlayerStatsRepository>();
builder.Services.AddScoped<IPlayerStatsService, PlayerStatsService>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<CombatMechanix.Services.IAuthenticationService, CombatMechanix.Services.AuthenticationService>();
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

// Initialize enemy and loot systems
var enemyManager = app.Services.GetRequiredService<EnemyManager>();
var wsManager = app.Services.GetRequiredService<WebSocketConnectionManager>();
var lootManager = app.Services.GetRequiredService<LootManager>();

// Wire up dependencies
wsManager.SetEnemyManager(enemyManager);
wsManager.SetLootManager(lootManager);
enemyManager.SetLootManager(lootManager);

// Initialize default content
enemyManager.InitializeDefaultEnemies();
app.Logger.LogInformation("Enemy and loot systems initialized");

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

app.Run();
