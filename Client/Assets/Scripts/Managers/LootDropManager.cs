using System.Collections.Generic;
using UnityEngine;
using System.Collections;

/// <summary>
/// Client-side loot drop management system
/// Handles visualization, positioning, and interaction with loot drops from the server
/// </summary>
public class LootDropManager : MonoBehaviour
{
    [Header("Loot Visualization Settings")]
    public GameObject LootDropPrefab; // Prefab for loot drop visualization
    public float PickupRange = 3f; // Maximum distance for loot pickup
    public float HoverHeight = 0.5f; // Height above ground for loot items
    public float BobSpeed = 2f; // Speed of bobbing animation
    public float BobAmount = 0.2f; // Amount of vertical bobbing
    
    [Header("Rarity Colors")]
    public Color CommonColor = Color.white;
    public Color UncommonColor = Color.green;
    public Color RareColor = Color.blue;
    public Color EpicColor = Color.magenta;
    public Color LegendaryColor = Color.yellow;

    // Active loot drops in the world
    private Dictionary<string, LootDropVisual> _activeLootDrops = new Dictionary<string, LootDropVisual>();
    
    // Player reference for distance calculations
    private Transform _playerTransform;

    private void Start()
    {
        // Subscribe to network events
        NetworkManager.OnLootDrop += HandleLootDrop;
        NetworkManager.OnLootPickupResponse += HandleLootPickupResponse;
        
        // Find player transform
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            _playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("[LootDropManager] PlayerController not found - loot pickup range validation will be disabled");
        }

        Debug.Log("[LootDropManager] Initialized and subscribed to network events");
    }

    private void OnDestroy()
    {
        // Unsubscribe from network events
        NetworkManager.OnLootDrop -= HandleLootDrop;
        NetworkManager.OnLootPickupResponse -= HandleLootPickupResponse;
    }

    /// <summary>
    /// Handle incoming loot drop message from server
    /// </summary>
    private void HandleLootDrop(NetworkMessages.LootDropMessage lootMessage)
    {
        Debug.Log($"[LootDropManager] Creating loot drop: {lootMessage.Item.ItemName} at ({lootMessage.Position.X}, {lootMessage.Position.Y}, {lootMessage.Position.Z})");
        
        // Create visual representation of the loot
        CreateLootDropVisual(lootMessage);
    }

    /// <summary>
    /// Handle loot pickup response from server
    /// </summary>
    private void HandleLootPickupResponse(NetworkMessages.LootPickupResponseMessage response)
    {
        Debug.Log($"[LootDropManager] Pickup response: {response.Message}");

        if (response.Success)
        {
            // Remove the loot visual from the world
            RemoveLootDrop(response.LootId);
            
            // TODO: In Phase 5, we'll add floating text feedback here
            Debug.Log($"Successfully picked up: {response.Item?.ItemName}");
        }
        else
        {
            // Show error message to player
            Debug.LogWarning($"Failed to pick up loot: {response.Message}");
            
            // TODO: In Phase 5, we'll show this as floating text
        }
    }

    /// <summary>
    /// Create visual representation of a loot drop
    /// </summary>
    private void CreateLootDropVisual(NetworkMessages.LootDropMessage lootMessage)
    {
        // Check if we already have this loot drop (shouldn't happen, but safety check)
        if (_activeLootDrops.ContainsKey(lootMessage.LootId))
        {
            Debug.LogWarning($"[LootDropManager] Loot drop {lootMessage.LootId} already exists, skipping creation");
            return;
        }

        // Create loot drop object
        GameObject lootObject;
        if (LootDropPrefab != null)
        {
            lootObject = Instantiate(LootDropPrefab);
        }
        else
        {
            // Fallback: create a simple cube if no prefab is assigned
            lootObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lootObject.transform.localScale = Vector3.one * 0.3f;
            Debug.LogWarning("[LootDropManager] No LootDropPrefab assigned, using default cube");
        }

        // Position the loot object
        Vector3 worldPosition = new Vector3(lootMessage.Position.X, lootMessage.Position.Y + HoverHeight, lootMessage.Position.Z);
        lootObject.transform.position = worldPosition;
        lootObject.name = $"LootDrop_{lootMessage.Item.ItemName}_{lootMessage.LootId[..8]}";

        // Add LootDropVisual component
        var lootVisual = lootObject.AddComponent<LootDropVisual>();
        lootVisual.Initialize(lootMessage, this);

        // Set color based on rarity
        var renderer = lootObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = GetRarityColor(lootMessage.Item.Rarity);
        }

        // Add collider for click detection if not already present
        if (lootObject.GetComponent<Collider>() == null)
        {
            var collider = lootObject.AddComponent<BoxCollider>();
            collider.isTrigger = true; // Make it a trigger for easier interaction
        }

        // Store reference
        _activeLootDrops[lootMessage.LootId] = lootVisual;

        Debug.Log($"[LootDropManager] Created loot visual for {lootMessage.Item.ItemName} ({lootMessage.Item.Rarity})");
    }

    /// <summary>
    /// Remove a loot drop from the world
    /// </summary>
    public void RemoveLootDrop(string lootId)
    {
        if (_activeLootDrops.TryGetValue(lootId, out var lootVisual))
        {
            _activeLootDrops.Remove(lootId);
            
            if (lootVisual != null)
            {
                Destroy(lootVisual.gameObject);
            }
            
            Debug.Log($"[LootDropManager] Removed loot drop: {lootId}");
        }
    }

    /// <summary>
    /// Attempt to pick up a loot drop
    /// </summary>
    public void AttemptPickup(string lootId)
    {
        if (!_activeLootDrops.ContainsKey(lootId))
        {
            Debug.LogWarning($"[LootDropManager] Attempted to pick up non-existent loot: {lootId}");
            return;
        }

        // Validate pickup range if player transform is available
        if (_playerTransform != null)
        {
            var lootVisual = _activeLootDrops[lootId];
            float distance = Vector3.Distance(_playerTransform.position, lootVisual.transform.position);
            
            if (distance > PickupRange)
            {
                Debug.Log($"[LootDropManager] Loot {lootId} is too far away (distance: {distance:F1}m, max: {PickupRange}m)");
                return;
            }
        }

        // Send pickup request to server
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null && _playerTransform != null)
        {
            _ = networkManager.SendLootPickupRequest(lootId, _playerTransform.position);
            Debug.Log($"[LootDropManager] Sent pickup request for loot: {lootId}");
        }
        else
        {
            Debug.LogError("[LootDropManager] Cannot send pickup request - NetworkManager or PlayerTransform not found");
        }
    }

    /// <summary>
    /// Get color for item rarity
    /// </summary>
    private Color GetRarityColor(string rarity)
    {
        return rarity.ToLower() switch
        {
            "common" => CommonColor,
            "uncommon" => UncommonColor,
            "rare" => RareColor,
            "epic" => EpicColor,
            "legendary" => LegendaryColor,
            _ => CommonColor
        };
    }

    /// <summary>
    /// Get all active loot drops (for debugging)
    /// </summary>
    public Dictionary<string, LootDropVisual> GetActiveLootDrops()
    {
        return new Dictionary<string, LootDropVisual>(_activeLootDrops);
    }

    /// <summary>
    /// Clear all loot drops (for cleanup)
    /// </summary>
    public void ClearAllLootDrops()
    {
        foreach (var lootVisual in _activeLootDrops.Values)
        {
            if (lootVisual != null)
            {
                Destroy(lootVisual.gameObject);
            }
        }
        
        _activeLootDrops.Clear();
        Debug.Log("[LootDropManager] Cleared all loot drops");
    }
}