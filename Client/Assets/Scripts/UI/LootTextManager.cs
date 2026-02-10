using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages floating text for loot pickup feedback with object pooling
/// Shows item names, quantities, and rarity-based colors when items are picked up
/// </summary>
public class LootTextManager : MonoBehaviour
{
    [Header("Pool Settings")]
    public int InitialPoolSize = 10;
    public int MaxPoolSize = 20;
    public bool EnablePooling = true;
    
    [Header("Loot Text Settings")]
    public GameObject FloatingLootTextPrefab;
    public float TextDuration = 3f; // 3 seconds as specified
    
    [Header("Positioning")]
    public Vector3 LootTextOffset = new Vector3(0, 2f, 0); // Above player
    public float RandomSpreadX = 1f; // Random horizontal spread
    public float RandomSpreadY = 0.5f; // Random vertical spread
    
    [Header("Rarity Colors")]
    public Color CommonColor = Color.white;
    public Color UncommonColor = Color.green;
    public Color RareColor = Color.blue;
    public Color EpicColor = Color.magenta;
    public Color LegendaryColor = Color.yellow;
    public Color InventoryFullColor = Color.red;
    
    [Header("Performance")]
    public int MaxActiveTexts = 15;
    public bool EnableDebugLogging = false;
    
    // Object pool
    private Queue<FloatingLootText> _textPool = new Queue<FloatingLootText>();
    private List<FloatingLootText> _activeTexts = new List<FloatingLootText>();
    
    // UI Canvas reference
    private Canvas _uiCanvas;
    
    // Player reference for positioning
    private Transform _playerTransform;
    
    // Stats
    private int _totalTextsCreated = 0;
    private int _poolHits = 0;
    private int _poolMisses = 0;
    
    private void Start()
    {
        InitializeManager();
    }
    
    private void InitializeManager()
    {
        // Find UI Canvas
        _uiCanvas = FindUICanvas();
        if (_uiCanvas == null)
        {
            Debug.LogError("[LootTextManager] No UI Canvas found! Loot text will not be displayed.");
            enabled = false;
            return;
        }
        
        // Find player transform
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            _playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("[LootTextManager] PlayerController not found - using fallback positioning");
        }
        
        // Initialize object pool
        if (EnablePooling)
        {
            InitializePool();
        }
        
        Debug.Log($"[LootTextManager] Initialized with {InitialPoolSize} pooled objects");
    }
    
    private Canvas FindUICanvas()
    {
        // Look for Canvas with name containing "UI"
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (canvas.name.ToLower().Contains("ui") || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvas;
            }
        }
        
        // If no UI canvas found, return the first canvas
        return canvases.Length > 0 ? canvases[0] : null;
    }
    
    private void InitializePool()
    {
        for (int i = 0; i < InitialPoolSize; i++)
        {
            CreatePooledText();
        }
    }
    
    private FloatingLootText CreatePooledText()
    {
        GameObject textObj;
        
        if (FloatingLootTextPrefab != null)
        {
            textObj = Instantiate(FloatingLootTextPrefab, _uiCanvas.transform);
        }
        else
        {
            // Create fallback text object
            textObj = CreateFallbackTextObject();
        }
        
        // Add FloatingLootText component if not present
        var lootText = textObj.GetComponent<FloatingLootText>();
        if (lootText == null)
        {
            lootText = textObj.AddComponent<FloatingLootText>();
        }
        
        textObj.SetActive(false);
        _textPool.Enqueue(lootText);
        _totalTextsCreated++;
        
        return lootText;
    }
    
    private GameObject CreateFallbackTextObject()
    {
        var textObj = new GameObject("FloatingLootText");
        textObj.transform.SetParent(_uiCanvas.transform);
        
        // Add Text component
        var text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        
        // Add ContentSizeFitter
        var sizeFitter = textObj.AddComponent<ContentSizeFitter>();
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        Debug.LogWarning("[LootTextManager] No FloatingLootTextPrefab assigned, using fallback text object");
        return textObj;
    }
    
    /// <summary>
    /// Show floating text for a successful item pickup
    /// </summary>
    /// <param name="item">The item that was picked up</param>
    /// <param name="position">World position where to show the text</param>
    public void ShowItemPickupText(InventoryItem item, Vector3 position)
    {
        if (item == null) return;
        
        string text = $"Picked up {item.ItemName}";
        if (item.Quantity > 1)
        {
            text = $"Picked up {item.ItemName} x{item.Quantity}";
        }
        
        Color textColor = GetRarityColor(item.Rarity);
        ShowLootText(text, position, textColor);
        
        if (EnableDebugLogging)
        {
            Debug.Log($"[LootTextManager] Showing pickup text: {text} ({item.Rarity})");
        }
    }
    
    /// <summary>
    /// Show floating text for inventory full scenario
    /// </summary>
    /// <param name="position">World position where to show the text</param>
    public void ShowInventoryFullText(Vector3 position)
    {
        ShowLootText("Inventory Full!", position, InventoryFullColor);
        
        if (EnableDebugLogging)
        {
            Debug.Log("[LootTextManager] Showing inventory full text");
        }
    }
    
    /// <summary>
    /// Show floating text for "too far away" scenario
    /// </summary>
    /// <param name="position">World position where to show the text</param>
    public void ShowTooFarAwayText(Vector3 position)
    {
        ShowLootText("Too Far Away!", position, InventoryFullColor);
        
        if (EnableDebugLogging)
        {
            Debug.Log("[LootTextManager] Showing too far away text");
        }
    }
    
    /// <summary>
    /// Show floating text for a lost item (inventory full)
    /// </summary>
    /// <param name="item">The item that was lost</param>
    /// <param name="position">World position where to show the text</param>
    public void ShowItemLostText(InventoryItem item, Vector3 position)
    {
        if (item == null) return;
        
        string text = $"Lost {item.ItemName}!";
        ShowLootText(text, position, InventoryFullColor);
        
        if (EnableDebugLogging)
        {
            Debug.Log($"[LootTextManager] Showing item lost text: {text}");
        }
    }
    
    /// <summary>
    /// Show floating text with custom message and color (used for gold pickups, etc.)
    /// </summary>
    public void ShowCustomText(string text, Vector3 position, Color color)
    {
        ShowLootText(text, position, color);
    }

    /// <summary>
    /// Core method to display floating loot text
    /// </summary>
    private void ShowLootText(string text, Vector3 worldPosition, Color color)
    {
        if (_uiCanvas == null) return;
        
        // Use player position if world position is not provided or invalid
        if (worldPosition == Vector3.zero && _playerTransform != null)
        {
            worldPosition = _playerTransform.position;
        }
        
        // Get text object from pool
        FloatingLootText lootText = GetPooledText();
        
        Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** Got pooled text, GameObject active before activation: {lootText.gameObject.activeInHierarchy}");
        
        // IMPORTANT: Activate the GameObject BEFORE calling Initialize() so coroutines can start
        lootText.gameObject.SetActive(true);
        
        Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** GameObject activated, now active: {lootText.gameObject.activeInHierarchy}");
        
        // Convert world position to screen position
        Vector3 screenPosition = GetScreenPosition(worldPosition);
        
        // Add random spread
        screenPosition.x += Random.Range(-RandomSpreadX * 50f, RandomSpreadX * 50f);
        screenPosition.y += Random.Range(-RandomSpreadY * 50f, RandomSpreadY * 50f);
        
        Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** About to call Initialize on active GameObject: {lootText.gameObject.activeInHierarchy}");
        
        // Initialize the floating text (now that GameObject is active)
        lootText.Initialize(text, screenPosition, color, TextDuration, this);
        
        // Track active text
        _activeTexts.Add(lootText);
        
        // Enforce max active texts limit
        EnforceMaxActiveTexts();
        
        if (EnableDebugLogging)
        {
            Debug.Log($"[LootTextManager] Displayed text: {text} at {screenPosition}");
        }
    }
    
    private Vector3 GetScreenPosition(Vector3 worldPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[LootTextManager] No main camera found, using fallback positioning");
            return new Vector3(Screen.width * 0.5f, Screen.height * 0.7f, 0);
        }
        
        // Apply offset
        Vector3 offsetPosition = worldPosition + LootTextOffset;
        return mainCamera.WorldToScreenPoint(offsetPosition);
    }
    
    private FloatingLootText GetPooledText()
    {
        if (EnablePooling && _textPool.Count > 0)
        {
            _poolHits++;
            return _textPool.Dequeue();
        }
        else
        {
            _poolMisses++;
            
            // Create new text if pool is empty or pooling is disabled
            if (_totalTextsCreated < MaxPoolSize)
            {
                return CreatePooledText();
            }
            else
            {
                // Reuse oldest active text
                if (_activeTexts.Count > 0)
                {
                    var oldestText = _activeTexts[0];
                    _activeTexts.RemoveAt(0);
                    return oldestText;
                }
                else
                {
                    // Fallback: create new text
                    return CreatePooledText();
                }
            }
        }
    }
    
    /// <summary>
    /// Return a text object to the pool
    /// </summary>
    public void ReturnToPool(FloatingLootText lootText)
    {
        Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** ReturnToPool called for text: '{lootText?.GetText() ?? "NULL"}'");
        
        if (lootText == null)
        {
            Debug.LogWarning($"[LootTextManager] *** FLOATING TEXT DEBUG *** Attempted to return null lootText to pool");
            return;
        }
        
        Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** Removing from active list. Active count before: {_activeTexts.Count}");
        
        // Remove from active list
        _activeTexts.Remove(lootText);
        
        Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** Active count after removal: {_activeTexts.Count}");
        
        // Reset and return to pool
        lootText.gameObject.SetActive(false);
        
        Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** GameObject deactivated. EnablePooling: {EnablePooling}, Pool count: {_textPool.Count}, Max pool size: {MaxPoolSize}");
        
        if (EnablePooling && _textPool.Count < MaxPoolSize)
        {
            Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** Adding to pool");
            _textPool.Enqueue(lootText);
            Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** Pool count after enqueue: {_textPool.Count}");
        }
        else
        {
            Debug.Log($"[LootTextManager] *** FLOATING TEXT DEBUG *** Destroying GameObject (pool full or pooling disabled)");
            // Destroy if pool is full or pooling is disabled
            Destroy(lootText.gameObject);
        }
    }
    
    private void EnforceMaxActiveTexts()
    {
        while (_activeTexts.Count > MaxActiveTexts)
        {
            var oldestText = _activeTexts[0];
            _activeTexts.RemoveAt(0);
            
            if (oldestText != null)
            {
                oldestText.ForceComplete();
            }
        }
    }
    
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
    /// Get statistics about the text pool
    /// </summary>
    public void LogPoolStats()
    {
        float hitRate = _poolHits + _poolMisses > 0 ? (float)_poolHits / (_poolHits + _poolMisses) * 100f : 0f;
        Debug.Log($"[LootTextManager] Pool Stats - Created: {_totalTextsCreated}, Active: {_activeTexts.Count}, " +
                  $"Pool Size: {_textPool.Count}, Hit Rate: {hitRate:F1}%");
    }
    
    /// <summary>
    /// Clear all active texts (for cleanup)
    /// </summary>
    public void ClearAllTexts()
    {
        foreach (var text in _activeTexts)
        {
            if (text != null)
            {
                text.ForceComplete();
            }
        }
        
        _activeTexts.Clear();
        Debug.Log("[LootTextManager] Cleared all active loot texts");
    }
}