using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages floating damage text for enemies with object pooling
/// Integrates with the existing enemy damage system and provides performance optimization
/// </summary>
public class EnemyDamageTextManager : MonoBehaviour
{
    [Header("Pool Settings")]
    public int InitialPoolSize = 20;
    public int MaxPoolSize = 50;
    public bool EnablePooling = true;
    
    [Header("Damage Text Settings")]
    public GameObject FloatingDamageTextPrefab;
    public float CriticalDamageThreshold = 0.25f; // 25% of max health = critical
    
    [Header("Positioning")]
    public Vector3 DamageTextOffset = new Vector3(0, 4f, 0); // Above health bar
    public float RandomSpreadX = 1f; // Random horizontal spread
    public float RandomSpreadY = 0.5f; // Random vertical spread
    
    [Header("Performance")]
    public int MaxActiveTexts = 30;
    public bool EnableDebugLogging = false;
    
    // Object pool
    private Queue<FloatingDamageText> _textPool = new Queue<FloatingDamageText>();
    private List<FloatingDamageText> _activeTexts = new List<FloatingDamageText>();
    
    // UI Canvas reference
    private Canvas _uiCanvas;
    
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
        Debug.Log("[EnemyDamageTextManager] Initializing damage text system");
        
        // Find the main UI canvas
        _uiCanvas = FindMainUICanvas();
        if (_uiCanvas == null)
        {
            Debug.LogError("[EnemyDamageTextManager] No UI Canvas found! Damage text will not work properly.");
            return;
        }
        
        // Create floating damage text prefab if not assigned
        if (FloatingDamageTextPrefab == null)
        {
            CreateDefaultFloatingDamageTextPrefab();
        }
        
        // Initialize object pool
        if (EnablePooling)
        {
            InitializePool();
        }
        
        // Subscribe to enemy damage events
        SubscribeToEnemyEvents();
        
        Debug.Log($"[EnemyDamageTextManager] Initialized with pool size: {InitialPoolSize}, Canvas: {_uiCanvas.name}");
    }
    
    private Canvas FindMainUICanvas()
    {
        // Find Screen Space Overlay canvas (main UI)
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                if (EnableDebugLogging)
                    Debug.Log($"[EnemyDamageTextManager] Found UI Canvas: {canvas.name}");
                return canvas;
            }
        }
        
        Debug.LogWarning("[EnemyDamageTextManager] No ScreenSpaceOverlay canvas found");
        return null;
    }
    
    private void CreateDefaultFloatingDamageTextPrefab()
    {
        Debug.Log("[EnemyDamageTextManager] Creating default floating damage text prefab");
        
        // Create a simple prefab for floating damage text
        GameObject prefab = new GameObject("FloatingDamageText");
        
        // Add RectTransform
        RectTransform rectTransform = prefab.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(100, 30);
        
        // Add Text component
        Text textComponent = prefab.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComponent.fontSize = 24;
        textComponent.fontStyle = FontStyle.Bold;
        textComponent.alignment = TextAnchor.MiddleCenter;
        textComponent.color = Color.red;
        
        // Add CanvasGroup for alpha animation
        prefab.AddComponent<CanvasGroup>();
        
        // Add FloatingDamageText component
        prefab.AddComponent<FloatingDamageText>();
        
        // Set as prefab reference
        FloatingDamageTextPrefab = prefab;
        
        // Deactivate the original
        prefab.SetActive(false);
        
        Debug.Log("[EnemyDamageTextManager] Default floating damage text prefab created");
    }
    
    private void InitializePool()
    {
        if (FloatingDamageTextPrefab == null)
        {
            Debug.LogError("[EnemyDamageTextManager] Cannot initialize pool: FloatingDamageTextPrefab is null");
            return;
        }
        
        for (int i = 0; i < InitialPoolSize; i++)
        {
            CreatePooledText();
        }
        
        Debug.Log($"[EnemyDamageTextManager] Pool initialized with {_textPool.Count} objects");
    }
    
    private FloatingDamageText CreatePooledText()
    {
        GameObject textObj = Instantiate(FloatingDamageTextPrefab, _uiCanvas.transform);
        FloatingDamageText floatingText = textObj.GetComponent<FloatingDamageText>();
        
        if (floatingText == null)
        {
            floatingText = textObj.AddComponent<FloatingDamageText>();
        }
        
        textObj.SetActive(false);
        _totalTextsCreated++;
        
        return floatingText;
    }
    
    private void SubscribeToEnemyEvents()
    {
        // We'll hook into the existing enemy damage system in Phase 4
        // For now, create the subscription mechanism
        Debug.Log("[EnemyDamageTextManager] Ready to subscribe to enemy damage events");
    }
    
    /// <summary>
    /// Show floating damage text for an enemy
    /// </summary>
    /// <param name="enemy">Enemy that took damage</param>
    /// <param name="damage">Amount of damage</param>
    /// <param name="isHealing">Whether this is healing instead of damage</param>
    public void ShowDamageText(EnemyBase enemy, float damage, bool isHealing = false)
    {
        if (enemy == null || _uiCanvas == null)
        {
            if (EnableDebugLogging)
                Debug.LogWarning("[EnemyDamageTextManager] Cannot show damage text: enemy or canvas is null");
            return;
        }
        
        // Check if we've hit the max active texts limit
        if (_activeTexts.Count >= MaxActiveTexts)
        {
            if (EnableDebugLogging)
                Debug.LogWarning($"[EnemyDamageTextManager] Max active texts reached ({MaxActiveTexts}), skipping");
            return;
        }
        
        // Determine damage type
        DamageType damageType = DetermineDamageType(enemy, damage, isHealing);
        
        // Calculate world position above enemy
        Vector3 worldPosition = CalculateDamageTextPosition(enemy);
        
        // Get or create floating text
        FloatingDamageText floatingText = GetFloatingText();
        if (floatingText == null)
        {
            Debug.LogError("[EnemyDamageTextManager] Failed to get floating text object");
            return;
        }
        
        // Initialize and start animation
        floatingText.gameObject.SetActive(true);
        floatingText.Initialize(damage, damageType, worldPosition);
        
        // Add to active list
        _activeTexts.Add(floatingText);
        
        if (EnableDebugLogging)
        {
            Debug.Log($"[EnemyDamageTextManager] Showing {damageType} damage text: {damage:F1} for {enemy.EnemyName}");
        }
    }
    
    private DamageType DetermineDamageType(EnemyBase enemy, float damage, bool isHealing)
    {
        if (isHealing)
        {
            return DamageType.Healing;
        }
        
        // Calculate if this is critical damage (> 25% of max health)
        float maxHealth = enemy.BaseHealth + (enemy.HealthPerLevel * (enemy.Level - 1));
        float criticalThreshold = maxHealth * CriticalDamageThreshold;
        
        if (damage >= criticalThreshold)
        {
            return DamageType.Critical;
        }
        
        return DamageType.Regular;
    }
    
    private Vector3 CalculateDamageTextPosition(EnemyBase enemy)
    {
        // Start with enemy position
        Vector3 basePosition = enemy.transform.position;
        
        // Add base offset (above health bar)
        Vector3 worldPosition = basePosition + DamageTextOffset;
        
        // Add random spread to prevent overlap
        worldPosition.x += Random.Range(-RandomSpreadX, RandomSpreadX);
        worldPosition.y += Random.Range(0, RandomSpreadY);
        
        return worldPosition;
    }
    
    private FloatingDamageText GetFloatingText()
    {
        FloatingDamageText floatingText = null;
        
        if (EnablePooling && _textPool.Count > 0)
        {
            // Get from pool
            floatingText = _textPool.Dequeue();
            _poolHits++;
            
            if (EnableDebugLogging)
                Debug.Log($"[EnemyDamageTextManager] Got text from pool. Pool size: {_textPool.Count}");
        }
        else
        {
            // Create new one
            floatingText = CreatePooledText();
            _poolMisses++;
            
            if (EnableDebugLogging)
                Debug.Log($"[EnemyDamageTextManager] Created new floating text. Total created: {_totalTextsCreated}");
        }
        
        return floatingText;
    }
    
    /// <summary>
    /// Return a floating text object to the pool
    /// </summary>
    public void ReturnToPool(FloatingDamageText floatingText)
    {
        if (floatingText == null) return;
        
        // Remove from active list
        _activeTexts.Remove(floatingText);
        
        // Reset for reuse
        floatingText.ResetForReuse();
        
        // Return to pool if not full
        if (EnablePooling && _textPool.Count < MaxPoolSize)
        {
            _textPool.Enqueue(floatingText);
            
            if (EnableDebugLogging)
                Debug.Log($"[EnemyDamageTextManager] Returned text to pool. Pool size: {_textPool.Count}");
        }
        else
        {
            // Pool is full, destroy
            Destroy(floatingText.gameObject);
            
            if (EnableDebugLogging)
                Debug.Log("[EnemyDamageTextManager] Pool full, destroyed floating text");
        }
    }
    
    /// <summary>
    /// Public method for external systems to show damage text
    /// </summary>
    public void ShowDamageAtWorldPosition(Vector3 worldPosition, float damage, DamageType damageType)
    {
        if (_uiCanvas == null) return;
        
        // Check active text limit
        if (_activeTexts.Count >= MaxActiveTexts) return;
        
        FloatingDamageText floatingText = GetFloatingText();
        if (floatingText == null) return;
        
        floatingText.gameObject.SetActive(true);
        floatingText.Initialize(damage, damageType, worldPosition);
        _activeTexts.Add(floatingText);
    }
    
    /// <summary>
    /// Clean up active texts that have finished animating
    /// </summary>
    private void Update()
    {
        // Clean up finished animations
        for (int i = _activeTexts.Count - 1; i >= 0; i--)
        {
            if (_activeTexts[i] == null || !_activeTexts[i].IsAnimating)
            {
                _activeTexts.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Get statistics about the damage text system
    /// </summary>
    public void LogStatistics()
    {
        Debug.Log($"[EnemyDamageTextManager] Stats - Total Created: {_totalTextsCreated}, Pool Hits: {_poolHits}, Pool Misses: {_poolMisses}, Active: {_activeTexts.Count}, Pooled: {_textPool.Count}");
    }
    
    private void OnDestroy()
    {
        // Clean up
        _activeTexts.Clear();
        _textPool.Clear();
    }
}