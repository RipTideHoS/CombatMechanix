using CombatMechanix.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSingleton<WebSocketConnectionManager>();
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

// Server status endpoint
app.MapGet("/status", (WebSocketConnectionManager wsManager) => new
{
    Status = "Running",
    Connections = wsManager.GetConnectionCount(),
    Players = wsManager.GetPlayerCount(),
    ServerTime = DateTime.UtcNow
});

app.Run();
