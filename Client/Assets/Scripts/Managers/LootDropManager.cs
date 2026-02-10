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
    
    // Constructor for debugging
    public LootDropManager()
    {
        // Debug.Log($"[LootDropManager] DEBUG Constructor called - Component being created");
    }
    
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
    public Transform PlayerTransform => _playerTransform;
    
    // Floating text manager reference
    private LootTextManager _lootTextManager;

    private bool _isSubscribed = false;

    private void OnEnable()
    {
        // Debug.Log($"[LootDropManager] DEBUG OnEnable called - subscribing to NetworkManager events");
        SubscribeToEvents();
    }

    private void Start()
    {
        // Debug.Log($"[LootDropManager] DEBUG Starting LootDropManager initialization");
        
        // Ensure we're subscribed (backup in case OnEnable didn't work)
        SubscribeToEvents();
        
        // Find player transform
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            _playerTransform = player.transform;
            // Debug.Log($"[LootDropManager] DEBUG PlayerController found at position: {_playerTransform.position}");
        }
        else
        {
            // Debug.LogError("[LootDropManager] DEBUG PlayerController not found - loot pickup range validation will be disabled");
        }
        
        // Find loot text manager
        _lootTextManager = FindObjectOfType<LootTextManager>();
        if (_lootTextManager == null)
        {
            // Debug.LogError("[LootDropManager] DEBUG LootTextManager not found - no floating text feedback will be shown");
        }
        else
        {
            // Debug.Log($"[LootDropManager] DEBUG LootTextManager found");
        }

        // Debug.Log($"[LootDropManager] DEBUG Initialization complete. Active loot drops: {_activeLootDrops.Count}");
    }

    private void SubscribeToEvents()
    {
        if (!_isSubscribed)
        {
            // Debug.Log($"[LootDropManager] DEBUG Subscribing to NetworkManager events");
            NetworkManager.OnLootDrop += HandleLootDrop;
            NetworkManager.OnLootPickupResponse += HandleLootPickupResponse;
            _isSubscribed = true;
            // Debug.Log($"[LootDropManager] DEBUG Successfully subscribed to NetworkManager events");
        }
        else
        {
            // Debug.Log($"[LootDropManager] DEBUG Already subscribed to events");
        }
    }

    private void OnDisable()
    {
        // Debug.Log($"[LootDropManager] DEBUG OnDisable called - unsubscribing from events");
        UnsubscribeFromEvents();
    }

    private void OnDestroy()
    {
        // Debug.Log($"[LootDropManager] DEBUG OnDestroy called - unsubscribing from events");
        UnsubscribeFromEvents();
    }

    private void UnsubscribeFromEvents()
    {
        if (_isSubscribed)
        {
            // Debug.Log($"[LootDropManager] DEBUG Unsubscribing from NetworkManager events");
            NetworkManager.OnLootDrop -= HandleLootDrop;
            NetworkManager.OnLootPickupResponse -= HandleLootPickupResponse;
            _isSubscribed = false;
            // Debug.Log($"[LootDropManager] DEBUG Successfully unsubscribed from NetworkManager events");
        }
    }

    /// <summary>
    /// Handle incoming loot drop message from server
    /// </summary>
    private void HandleLootDrop(NetworkMessages.LootDropMessage lootMessage)
    {
        // Debug.Log($"[LootDropManager] DEBUG HandleLootDrop called");
        // Debug.Log($"[LootDropManager] DEBUG Loot details: ID={lootMessage.LootId}, Item={lootMessage.Item?.ItemName}, Position=({lootMessage.Position?.X}, {lootMessage.Position?.Y}, {lootMessage.Position?.Z})");
        // Debug.Log($"[LootDropManager] DEBUG Item rarity: {lootMessage.Item?.Rarity}, Current active loot count: {_activeLootDrops.Count}");
        
        // Validate message data
        if (lootMessage == null)
        {
            Debug.LogError($"[LootDropManager] DEBUG Received null loot message!");
            return;
        }
        
        if (lootMessage.Item == null)
        {
            Debug.LogError($"[LootDropManager] DEBUG Received loot message with null item!");
            return;
        }
        
        if (lootMessage.Position == null)
        {
            Debug.LogError($"[LootDropManager] DEBUG Received loot message with null position!");
            return;
        }
        
        // Debug.Log($"[LootDropManager] DEBUG Message validation passed, calling CreateLootDropVisual");
        
        // Create visual representation of the loot
        CreateLootDropVisual(lootMessage);
        
        // Debug.Log($"[LootDropManager] DEBUG HandleLootDrop completed. New active loot count: {_activeLootDrops.Count}");
    }

    /// <summary>
    /// Handle loot pickup response from server
    /// </summary>
    private void HandleLootPickupResponse(NetworkMessages.LootPickupResponseMessage response)
    {
        Debug.Log($"[LootDropManager] Pickup response: {response.Message}");

        if (response.Success)
        {
            // Server confirmed the pickup - gold awarded
            Debug.Log($"[LootDropManager] Server confirmed pickup, gold awarded: {response.GoldAwarded}");

            // Show gold award floating text (server-authoritative amount)
            if (_lootTextManager != null && _playerTransform != null && response.GoldAwarded > 0)
            {
                _lootTextManager.ShowCustomText($"+{response.GoldAwarded} Gold", _playerTransform.position, new Color(0.8f, 0.6f, 0f));
            }

            // Always remove the loot visual since server confirmed pickup
            RemoveLootDrop(response.LootId);
        }
        else
        {
            // Pickup failed (usually because inventory is full)
            Debug.LogWarning($"[LootDropManager] ❌ Failed to pick up loot: {response.Message}");
            
            // Show appropriate error message
            if (GameManager.Instance?.UIManager != null)
            {
                GameManager.Instance.UIManager.ShowMessage(response.Message ?? "Failed to pick up item");
            }
            
            // Show error floating text (usually "Inventory Full!")
            if (_lootTextManager != null && _playerTransform != null)
            {
                if (response.Message?.Contains("full") == true)
                {
                    _lootTextManager.ShowInventoryFullText(_playerTransform.position);
                }
                else
                {
                    // Generic pickup failure - no floating text needed
                }
            }
            
            // Don't remove loot visual - player can try again or move closer to inventory
        }
    }

    /// <summary>
    /// Create visual representation of a loot drop
    /// </summary>
    private void CreateLootDropVisual(NetworkMessages.LootDropMessage lootMessage)
    {
        // Debug.Log($"[LootDropManager] DEBUG CreateLootDropVisual started for LootId: {lootMessage.LootId}");
        
        // Check if we already have this loot drop (shouldn't happen, but safety check)
        if (_activeLootDrops.ContainsKey(lootMessage.LootId))
        {
            // Debug.LogWarning($"[LootDropManager] DEBUG Loot drop {lootMessage.LootId} already exists, skipping creation");
            return;
        }
        
        // Debug.Log($"[LootDropManager] DEBUG Loot ID unique, proceeding with creation");
        // Debug.Log($"[LootDropManager] DEBUG LootDropPrefab assigned: {LootDropPrefab != null}");

        // Create loot drop object
        GameObject lootObject;
        if (LootDropPrefab != null)
        {
            // Debug.Log($"[LootDropManager] DEBUG Instantiating LootDropPrefab");
            lootObject = Instantiate(LootDropPrefab);
            // Debug.Log($"[LootDropManager] DEBUG LootDropPrefab instantiated successfully: {lootObject != null}");
        }
        else
        {
            // Fallback: create a simple cube if no prefab is assigned
            // Debug.LogWarning($"[LootDropManager] DEBUG No LootDropPrefab assigned, creating default cube");
            lootObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lootObject.transform.localScale = Vector3.one * 0.3f;
            // Debug.Log($"[LootDropManager] DEBUG Default cube created: {lootObject != null}");
        }

        // Position the loot object
        Vector3 worldPosition = new Vector3(lootMessage.Position.X, lootMessage.Position.Y + HoverHeight, lootMessage.Position.Z);
        // Debug.Log($"[LootDropManager] DEBUG Setting loot position to: {worldPosition} (original: {lootMessage.Position.X}, {lootMessage.Position.Y}, {lootMessage.Position.Z}, hover height: {HoverHeight})");
        lootObject.transform.position = worldPosition;
        lootObject.name = $"LootDrop_{lootMessage.Item.ItemName}_{lootMessage.LootId[..8]}";
        // Debug.Log($"[LootDropManager] DEBUG Loot object positioned and named: {lootObject.name}");

        // Add LootDropVisual component
        // Debug.Log($"[LootDropManager] DEBUG Adding LootDropVisual component");
        var lootVisual = lootObject.AddComponent<LootDropVisual>();
        // Debug.Log($"[LootDropManager] DEBUG LootDropVisual component added: {lootVisual != null}");
        
        // Debug.Log($"[LootDropManager] DEBUG Initializing LootDropVisual with message data");
        lootVisual.Initialize(lootMessage, this);
        // Debug.Log($"[LootDropManager] DEBUG LootDropVisual initialized successfully");

        // Set color based on rarity
        // Debug.Log($"[LootDropManager] DEBUG Setting rarity color for: {lootMessage.Item.Rarity}");
        var renderer = lootObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color rarityColor = GetRarityColor(lootMessage.Item.Rarity);
            renderer.material.color = rarityColor;
            // Debug.Log($"[LootDropManager] DEBUG Rarity color set to: {rarityColor} for rarity: {lootMessage.Item.Rarity}");
        }
        else
        {
            // Debug.LogWarning($"[LootDropManager] DEBUG No Renderer component found on loot object!");
        }

        // Add collider for click detection if not already present
        // Debug.Log($"[LootDropManager] DEBUG Checking for collider component");
        if (lootObject.GetComponent<Collider>() == null)
        {
            // Debug.Log($"[LootDropManager] DEBUG No collider found, adding BoxCollider");
            var collider = lootObject.AddComponent<BoxCollider>();
            collider.isTrigger = true; // Make it a trigger for easier interaction
            // Debug.Log($"[LootDropManager] DEBUG BoxCollider added as trigger");
        }
        else
        {
            // Debug.Log($"[LootDropManager] DEBUG Existing collider found, keeping it");
        }

        // Store reference
        // Debug.Log($"[LootDropManager] DEBUG Adding loot to active drops dictionary");
        _activeLootDrops[lootMessage.LootId] = lootVisual;
        // Debug.Log($"[LootDropManager] DEBUG Loot added to dictionary. Total active loot: {_activeLootDrops.Count}");

        // Debug.Log($"[LootDropManager] DEBUG CreateLootDropVisual completed successfully for {lootMessage.Item.ItemName} ({lootMessage.Item.Rarity})");
        // Debug.Log($"[LootDropManager] DEBUG Final loot object state - Position: {lootObject.transform.position}, Active: {lootObject.activeInHierarchy}, Name: {lootObject.name}");
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
    /// Auto-pickup loot when player walks close (magnet pickup)
    /// Awards random gold and sends pickup request to server
    /// </summary>
    public void AutoPickup(string lootId)
    {
        if (!_activeLootDrops.ContainsKey(lootId))
        {
            Debug.LogWarning($"[LootDropManager] AutoPickup: loot {lootId} not found");
            return;
        }

        // Send pickup request to server (server awards gold authoritatively)
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null && _playerTransform != null)
        {
            _ = networkManager.SendLootPickupRequest(lootId, _playerTransform.position);
            Debug.Log($"[LootDropManager] AutoPickup sent for loot: {lootId}");
        }

        // Remove the loot visual immediately
        RemoveLootDrop(lootId);
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

    /// <summary>
    /// Update method to ensure subscription is maintained
    /// </summary>
    private void Update()
    {
        // Periodically check if we're still subscribed (every 5 seconds)
        if (Time.time % 5f < 0.1f && !_isSubscribed)
        {
            // Debug.Log($"[LootDropManager] DEBUG Update detected unsubscribed state, attempting to resubscribe");
            SubscribeToEvents();
        }
    }

    /// <summary>
    /// Public method to check subscription status and force subscription if needed
    /// </summary>
    public void CheckAndForceSubscription()
    {
        // Debug.Log($"[LootDropManager] DEBUG CheckAndForceSubscription called - Current subscription status: {_isSubscribed}");
        // Debug.Log($"[LootDropManager] DEBUG Component enabled: {enabled}, GameObject active: {gameObject.activeInHierarchy}");
        
        if (!_isSubscribed)
        {
            // Debug.Log($"[LootDropManager] DEBUG Not subscribed, forcing subscription");
            SubscribeToEvents();
        }
        
        // Check subscriber count
        var onLootDropEvent = typeof(NetworkManager).GetEvent("OnLootDrop");
        if (onLootDropEvent != null)
        {
            var field = typeof(NetworkManager).GetField("OnLootDrop", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                var eventDelegate = (System.Action<NetworkMessages.LootDropMessage>)field.GetValue(null);
                int subscriberCount = eventDelegate?.GetInvocationList()?.Length ?? 0;
                // Debug.Log($"[LootDropManager] DEBUG NetworkManager.OnLootDrop has {subscriberCount} subscribers");
            }
        }
    }

    /// <summary>
    /// Called from console or other components to test event system
    /// </summary>
    public void TestEventSubscription()
    {
        // Debug.Log($"[LootDropManager] DEBUG TestEventSubscription called");
        CheckAndForceSubscription();
    }
}