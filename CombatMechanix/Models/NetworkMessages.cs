using System.Text.Json.Serialization;

namespace CombatMechanix.Models
{
    public class NetworkMessages
    {
        public class PlayerMovementMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
            public Vector3Data Velocity { get; set; } = new();
            public float Rotation { get; set; }
            public long Timestamp { get; set; }
        }

        public class CombatActionMessage
        {
            public string AttackerId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public string AttackType { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
            public float Damage { get; set; }
            public long Timestamp { get; set; }
        }

        public class ChatMessage
        {
            public string SenderId { get; set; } = string.Empty;
            public string SenderName { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string ChannelType { get; set; } = string.Empty;
            public string? TargetId { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class WorldUpdateMessage
        {
            public List<PlayerState> Players { get; set; } = new();
            public List<PlayerState> NearbyPlayers { get; set; } = new();
            public List<ResourceNode> Resources { get; set; } = new();
            public List<ResourceNode> NearbyResources { get; set; } = new();
            public List<CombatEffect> ActiveEffects { get; set; } = new();
            public long Timestamp { get; set; }
            public long ServerTimestamp { get; set; }
        }

        public class PlayerJoinNotification
        {
            public string PlayerId { get; set; } = string.Empty;
            public string PlayerName { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
            public DateTime JoinTime { get; set; }
        }

        public class SystemNotification
        {
            public string Message { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }

        public class ResourceGatherMessage
        {
            public string PlayerId { get; set; } = string.Empty;
            public string ResourceId { get; set; } = string.Empty;
            public string ResourceType { get; set; } = string.Empty;
            public Vector3Data Position { get; set; } = new();
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
        public Vector3Data Position { get; set; } = new();
        public Vector3Data Velocity { get; set; } = new();
        public float Rotation { get; set; }
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public int Level { get; set; } = 1;
        public bool IsOnline { get; set; } = true;
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class ResourceNode
    {
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public Vector3Data Position { get; set; } = new();
        public int Amount { get; set; }
        public int CurrentAmount { get; set; }
        public int MaxAmount { get; set; }
        public DateTime LastHarvested { get; set; }
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

    public class Vector3Data
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3Data() { }

        public Vector3Data(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class CombatEffect
    {
        public string EffectId { get; set; } = string.Empty;
        public Vector3Data Position { get; set; } = new();
        public string EffectType { get; set; } = string.Empty;
        public float Duration { get; set; }
        public DateTime StartTime { get; set; }
    }

    public class InventoryItem
    {
        public string ItemType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int SlotIndex { get; set; }
    }
}