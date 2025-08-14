using System.Numerics;

namespace CombatMechanix.Models
{
    public class NetworkMessages
    {
        public class PlayerMovementMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public Vector3 Position { get; set; }
            public Vector3 Velocity { get; set; }
            public float Rotation { get; set; }
            public long Timestamp { get; set; }
        }

        public class CombatActionMessage
        {
            public string AttackerId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public string AttackType { get; set; } = string.Empty;
            public Vector3 Position { get; set; }
            public long Timestamp { get; set; }
        }

        public class ChatMessage
        {
            public string SenderId { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string ChannelType { get; set; } = string.Empty;
            public string? TargetId { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class WorldUpdateMessage
        {
            public List<PlayerState> Players { get; set; } = new();
            public List<ResourceNode> Resources { get; set; } = new();
            public long Timestamp { get; set; }
        }

        public class PlayerJoinNotification
        {
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
            public Vector3 Position { get; set; }
            public DateTime JoinTime { get; set; }
        }

        public class SystemNotification
        {
            public string Message { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }

        public class ResourceGatherMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string ResourceId { get; set; } = string.Empty;
            public string ResourceType { get; set; } = string.Empty;
            public Vector3 Position { get; set; }
        }

        public class AuthenticationMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
        }

        public class HeartbeatMessage
        {
            public long Timestamp { get; set; }
        }
    }

    public class PlayerState
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float Rotation { get; set; }
        public int Health { get; set; } = 100;
        public bool IsOnline { get; set; } = true;
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class ResourceNode
    {
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public Vector3 Position { get; set; }
        public int Amount { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class MessageWrapper
    {
        public string Type { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    public class ConnectionData
    {
        public string ConnectionId { get; set; } = string.Empty;
    }
}