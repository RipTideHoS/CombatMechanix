using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NetworkMessages
{
    [System.Serializable]
    public class PlayerMovementMessage
    {
        public string PlayerId;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Rotation;
        public long Timestamp;
    }

    [System.Serializable]
    public class CombatActionMessage
    {
        public string AttackerId;
        public string TargetId;
        public string AttackType;
        public Vector3 Position;
        public float Damage;
        public long Timestamp;
    }

    [System.Serializable]
    public class ChatMessage
    {
        public string SenderId;
        public string SenderName;
        public string Message;
        public string ChannelType; // Global, Local, Private
        public string TargetId;
        public DateTime Timestamp;
    }

    [System.Serializable]
    public class ResourceGatherMessage
    {
        public string PlayerId;
        public string ResourceId;
        public string ResourceType;
        public Vector3 Position;
        public int AmountGathered;
    }

    [System.Serializable]
    public class WorldUpdateMessage
    {
        public List<PlayerState> NearbyPlayers;
        public List<ResourceNode> NearbyResources;
        public List<CombatEffect> ActiveEffects;
        public long ServerTimestamp;
    }

    [System.Serializable]
    public class PlayerJoinNotification
    {
        public string PlayerName;
        public int PlayerLevel;
        public Vector3 JoinLocation;
        public DateTime JoinTime;
        public string NotificationType;
    }

    [System.Serializable]
    public class SystemNotification
    {
        public string Message;
        public string NotificationType;
        public string Priority;
        public DateTime Timestamp;
    }
}

// Supporting data structures that are also used by NetworkMessages
[System.Serializable]
public class PlayerState
{
    public string PlayerId;
    public string PlayerName;
    public Vector3 Position;
    public Vector3 Velocity;
    public float Rotation;
    public float Health;
    public float MaxHealth;
    public int Level;
    public bool IsAlive;
    public long LastUpdateTime;
}

[System.Serializable]
public class ResourceNode
{
    public string ResourceId;
    public string ResourceType;
    public Vector3 Position;
    public int CurrentAmount;
    public int MaxAmount;
    public DateTime LastHarvested;
}

[System.Serializable]
public class CombatEffect
{
    public string EffectId;
    public Vector3 Position;
    public string EffectType;
    public float Duration;
    public DateTime StartTime;
}

[System.Serializable]
public class InventoryItem
{
    public string ItemType;
    public int Quantity;
    public int SlotIndex;
}