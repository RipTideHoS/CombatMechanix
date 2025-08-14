using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CombatMechanix.Models;

namespace CombatMechanix.Services
{
    public class WebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
        private readonly ConcurrentDictionary<string, PlayerState> _players = new();
        private readonly ILogger<WebSocketConnectionManager> _logger;

        public WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
        {
            _logger = logger;
        }

        public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket)
        {
            var connectionId = Guid.NewGuid().ToString();
            var connection = new WebSocketConnection(connectionId, webSocket);
            
            _connections.TryAdd(connectionId, connection);
            _logger.LogInformation($"WebSocket connection established: {connectionId}");

            try
            {
                // Send connection confirmation
                await SendToConnection(connectionId, "ConnectionConfirmed", new ConnectionData { ConnectionId = connectionId });

                // Handle incoming messages
                await ReceiveMessages(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling WebSocket connection {connectionId}");
            }
            finally
            {
                // Clean up connection
                _connections.TryRemove(connectionId, out _);
                _players.TryRemove(connectionId, out _);
                _logger.LogInformation($"WebSocket connection closed: {connectionId}");

                // Notify other players that this player left
                if (_players.ContainsKey(connectionId))
                {
                    await BroadcastToAll("SystemNotification", new NetworkMessages.SystemNotification
                    {
                        Message = $"Player {_players[connectionId].PlayerName} has left the game",
                        Type = "PlayerLeft",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }

        private async Task ReceiveMessages(WebSocketConnection connection)
        {
            var buffer = new byte[1024 * 4];

            while (connection.WebSocket.State == WebSocketState.Open)
            {
                var result = await connection.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessage(connection, message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await connection.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                    break;
                }
            }
        }

        private async Task ProcessMessage(WebSocketConnection connection, string jsonMessage)
        {
            try
            {
                var wrapper = JsonSerializer.Deserialize<MessageWrapper>(jsonMessage);
                if (wrapper?.Type == null) return;

                _logger.LogDebug($"Processing message type: {wrapper.Type} from {connection.ConnectionId}");

                switch (wrapper.Type)
                {
                    case "PlayerMovement":
                        await HandlePlayerMovement(connection, wrapper.Data);
                        break;
                    case "CombatAction":
                        await HandleCombatAction(connection, wrapper.Data);
                        break;
                    case "ChatMessage":
                        await HandleChatMessage(connection, wrapper.Data);
                        break;
                    case "ResourceGather":
                        await HandleResourceGather(connection, wrapper.Data);
                        break;
                    case "Authentication":
                        await HandleAuthentication(connection, wrapper.Data);
                        break;
                    case "Heartbeat":
                        await HandleHeartbeat(connection, wrapper.Data);
                        break;
                    default:
                        _logger.LogWarning($"Unknown message type: {wrapper.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message from {connection.ConnectionId}");
            }
        }

        #region Message Handlers

        private async Task HandlePlayerMovement(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var movementData = JsonSerializer.Deserialize<NetworkMessages.PlayerMovementMessage>(data.ToString()!);
            if (movementData == null) return;

            // Update player state
            if (_players.TryGetValue(connection.ConnectionId, out var player))
            {
                player.Position = movementData.Position;
                player.Velocity = movementData.Velocity;
                player.Rotation = movementData.Rotation;
                player.LastUpdate = DateTime.UtcNow;
            }

            // Broadcast to all other players
            await BroadcastToOthers(connection.ConnectionId, "PlayerMovement", movementData);
        }

        private async Task HandleCombatAction(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var combatData = JsonSerializer.Deserialize<NetworkMessages.CombatActionMessage>(data.ToString()!);
            if (combatData == null) return;

            _logger.LogInformation($"Combat action: {combatData.AttackType} from {combatData.AttackerId} to {combatData.TargetId}");

            // Process combat logic here (damage calculation, etc.)
            // For now, just broadcast the action
            await BroadcastToAll("CombatAction", combatData);
        }

        private async Task HandleChatMessage(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var chatData = JsonSerializer.Deserialize<NetworkMessages.ChatMessage>(data.ToString()!);
            if (chatData == null) return;

            _logger.LogInformation($"Chat message from {chatData.SenderId}: {chatData.Message}");

            // Determine message routing based on channel type
            switch (chatData.ChannelType.ToLower())
            {
                case "global":
                case "all":
                    await BroadcastToAll("ChatMessage", chatData);
                    break;
                case "whisper":
                case "private":
                    if (!string.IsNullOrEmpty(chatData.TargetId))
                    {
                        await SendToConnection(chatData.TargetId, "ChatMessage", chatData);
                        await SendToConnection(connection.ConnectionId, "ChatMessage", chatData); // Echo back to sender
                    }
                    break;
                default:
                    await BroadcastToAll("ChatMessage", chatData);
                    break;
            }
        }

        private async Task HandleResourceGather(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var gatherData = JsonSerializer.Deserialize<NetworkMessages.ResourceGatherMessage>(data.ToString()!);
            if (gatherData == null) return;

            _logger.LogInformation($"Resource gather: {gatherData.ResourceType} by {gatherData.PlayerId}");

            // Process resource gathering logic here
            // For now, just acknowledge the action
            await SendToConnection(connection.ConnectionId, "SystemNotification", new NetworkMessages.SystemNotification
            {
                Message = $"You gathered {gatherData.ResourceType}",
                Type = "ResourceGathered",
                Timestamp = DateTime.UtcNow
            });
        }

        private async Task HandleAuthentication(WebSocketConnection connection, object? data)
        {
            if (data == null) return;
            
            var authData = JsonSerializer.Deserialize<NetworkMessages.AuthenticationMessage>(data.ToString()!);
            if (authData == null) return;

            _logger.LogInformation($"Player authentication: {authData.PlayerName} ({authData.PlayerId})");

            // Create or update player state
            var player = new PlayerState
            {
                PlayerId = connection.ConnectionId, // Use connection ID as player ID for simplicity
                PlayerName = authData.PlayerName,
                Position = System.Numerics.Vector3.Zero,
                Health = 100,
                IsOnline = true,
                LastUpdate = DateTime.UtcNow
            };

            _players.TryAdd(connection.ConnectionId, player);

            // Notify all players about the new player
            await BroadcastToAll("PlayerJoined", new NetworkMessages.PlayerJoinNotification
            {
                PlayerId = player.PlayerId,
                PlayerName = player.PlayerName,
                Position = player.Position,
                JoinTime = DateTime.UtcNow
            });

            // Send current world state to the new player
            await SendWorldUpdate(connection.ConnectionId);
        }

        private async Task HandleHeartbeat(WebSocketConnection connection, object? data)
        {
            // Simple heartbeat acknowledgment
            _logger.LogDebug($"Heartbeat from {connection.ConnectionId}");
            
            if (_players.TryGetValue(connection.ConnectionId, out var player))
            {
                player.LastUpdate = DateTime.UtcNow;
            }
        }

        #endregion

        #region Broadcasting Methods

        private async Task SendToConnection(string connectionId, string messageType, object data)
        {
            if (_connections.TryGetValue(connectionId, out var connection) && 
                connection.WebSocket.State == WebSocketState.Open)
            {
                try
                {
                    var wrapper = new MessageWrapper { Type = messageType, Data = data };
                    var json = JsonSerializer.Serialize(wrapper);
                    var bytes = Encoding.UTF8.GetBytes(json);

                    await connection.WebSocket.SendAsync(
                        new ArraySegment<byte>(bytes), 
                        WebSocketMessageType.Text, 
                        true, 
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error sending message to {connectionId}");
                }
            }
        }

        private async Task BroadcastToAll(string messageType, object data)
        {
            var tasks = _connections.Values.Select(conn => SendToConnection(conn.ConnectionId, messageType, data));
            await Task.WhenAll(tasks);
        }

        private async Task BroadcastToOthers(string excludeConnectionId, string messageType, object data)
        {
            var tasks = _connections.Values
                .Where(conn => conn.ConnectionId != excludeConnectionId)
                .Select(conn => SendToConnection(conn.ConnectionId, messageType, data));
            await Task.WhenAll(tasks);
        }

        private Task SendWorldUpdate(string connectionId)
        {
            var worldUpdate = new NetworkMessages.WorldUpdateMessage
            {
                Players = _players.Values.ToList(),
                Resources = new List<ResourceNode>(), // Add resource nodes here
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            return SendToConnection(connectionId, "WorldUpdate", worldUpdate);
        }

        #endregion

        public int GetConnectionCount() => _connections.Count;
        public int GetPlayerCount() => _players.Count;
    }

    public class WebSocketConnection
    {
        public string ConnectionId { get; }
        public WebSocket WebSocket { get; }
        public DateTime ConnectedAt { get; }

        public WebSocketConnection(string connectionId, WebSocket webSocket)
        {
            ConnectionId = connectionId;
            WebSocket = webSocket;
            ConnectedAt = DateTime.UtcNow;
        }
    }
}