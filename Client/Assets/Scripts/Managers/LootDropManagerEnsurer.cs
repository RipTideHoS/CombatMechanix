using UnityEngine;

/// <summary>
/// Ensures LootDropManager component exists and is properly subscribed to events
/// This is a fallback/debugging component to help diagnose LootDropManager issues
/// </summary>
public class LootDropManagerEnsurer : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool EnableDebugLogging = true;
    public bool AutoCreateIfMissing = true;
    public bool ForceSubscriptionCheck = true;
    
    private LootDropManager _lootDropManager;
    private float _lastCheckTime = 0f;
    private float _checkInterval = 2f; // Check every 2 seconds
    
    private void Start()
    {
        if (EnableDebugLogging)
        {
            Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Starting LootDropManager diagnostics");
        }
        
        // Initial check
        CheckLootDropManager();
    }
    
    private void Update()
    {
        // Periodic checks
        if (Time.time - _lastCheckTime > _checkInterval)
        {
            _lastCheckTime = Time.time;
            CheckLootDropManager();
        }
    }
    
    private void CheckLootDropManager()
    {
        if (EnableDebugLogging)
        {
            Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Checking for LootDropManager component");
        }
        
        // Look for LootDropManager in the scene
        _lootDropManager = FindObjectOfType<LootDropManager>();
        
        if (_lootDropManager == null)
        {
            if (EnableDebugLogging)
            {
                Debug.LogWarning($"[LootDropManagerEnsurer] *** LOOT DEBUG *** LootDropManager not found in scene!");
            }
            
            if (AutoCreateIfMissing)
            {
                CreateLootDropManager();
            }
        }
        else
        {
            if (EnableDebugLogging)
            {
                Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** LootDropManager found: {_lootDropManager.name}");
                Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Component enabled: {_lootDropManager.enabled}");
                Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** GameObject active: {_lootDropManager.gameObject.activeInHierarchy}");
            }
            
            if (ForceSubscriptionCheck)
            {
                // Try to call the subscription check method
                try
                {
                    _lootDropManager.CheckAndForceSubscription();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Error calling CheckAndForceSubscription: {ex.Message}");
                }
            }
        }
        
        // Also check for NetworkManager and its events
        CheckNetworkManagerEvents();
    }
    
    private void CreateLootDropManager()
    {
        if (EnableDebugLogging)
        {
            Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Attempting to create LootDropManager component");
        }
        
        // Look for GameManager first
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj == null)
        {
            // Create a dedicated GameObject for LootDropManager
            if (EnableDebugLogging)
            {
                Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** GameManager not found, creating dedicated LootDropManager GameObject");
            }
            
            gameManagerObj = new GameObject("LootDropManager_Fallback");
            DontDestroyOnLoad(gameManagerObj);
        }
        
        // Add the LootDropManager component
        try
        {
            _lootDropManager = gameManagerObj.AddComponent<LootDropManager>();
            
            if (_lootDropManager != null)
            {
                if (EnableDebugLogging)
                {
                    Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** LootDropManager component created successfully!");
                }
            }
            else
            {
                Debug.LogError($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Failed to create LootDropManager component!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Exception creating LootDropManager: {ex.Message}");
        }
    }
    
    private void CheckNetworkManagerEvents()
    {
        // Check NetworkManager events using reflection
        try
        {
            var networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager != null)
            {
                if (EnableDebugLogging)
                {
                    Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** NetworkManager found: {networkManager.name}");
                }
                
                // Check OnLootDrop event subscriber count
                var onLootDropField = typeof(NetworkManager).GetField("OnLootDrop", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (onLootDropField != null)
                {
                    var eventDelegate = (System.Action<NetworkMessages.LootDropMessage>)onLootDropField.GetValue(null);
                    int subscriberCount = eventDelegate?.GetInvocationList()?.Length ?? 0;
                    
                    if (EnableDebugLogging)
                    {
                        Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** NetworkManager.OnLootDrop has {subscriberCount} subscribers");
                    }
                    
                    if (subscriberCount == 0 && _lootDropManager != null)
                    {
                        Debug.LogWarning($"[LootDropManagerEnsurer] *** LOOT DEBUG *** No subscribers to OnLootDrop event, but LootDropManager exists!");
                    }
                }
            }
            else
            {
                if (EnableDebugLogging)
                {
                    Debug.LogWarning($"[LootDropManagerEnsurer] *** LOOT DEBUG *** NetworkManager not found in scene!");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Error checking NetworkManager events: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Manual method to force LootDropManager creation and subscription
    /// Can be called from Unity console or other scripts
    /// </summary>
    [ContextMenu("Force Create LootDropManager")]
    public void ForceCreateLootDropManager()
    {
        Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** ForceCreateLootDropManager called manually");
        CreateLootDropManager();
        CheckLootDropManager();
    }
    
    /// <summary>
    /// Manual method to check subscription status
    /// </summary>
    [ContextMenu("Check Subscription Status")]
    public void CheckSubscriptionStatus()
    {
        Debug.Log($"[LootDropManagerEnsurer] *** LOOT DEBUG *** Manual subscription status check");
        CheckLootDropManager();
    }
}