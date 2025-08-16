using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages health bars for all enemies in the scene
/// Automatically creates and destroys health bars as enemies spawn/die
/// Optimizes performance by pooling health bar instances
/// </summary>
public class HealthBarManager : MonoBehaviour
{
    [Header("Prefab References")]
    public GameObject EnemyHealthBarPrefab;
    public GameObject PlayerHealthBarPrefab;

    [Header("Settings")]
    public bool AutoManageEnemyHealthBars = true;
    public bool EnableHealthBarPooling = true;
    public int InitialPoolSize = 10;
    public int MaxPoolSize = 50;
    public float UpdateRate = 0.2f; // How often to check for new enemies (optimized)

    [Header("Visibility Settings")]
    public float MaxViewDistance = 1000f;
    public bool HideWhenBehindObjects = false;
    public LayerMask OcclusionMask = -1;

    private Dictionary<EnemyBase, EnemyHealthBar> _activeHealthBars = new Dictionary<EnemyBase, EnemyHealthBar>();
    private Queue<GameObject> _healthBarPool = new Queue<GameObject>();
    private Camera _mainCamera;
    private Coroutine _updateCoroutine;

    public static HealthBarManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }

        InitializePool();
    }

    private void Start()
    {
        if (AutoManageEnemyHealthBars)
        {
            _updateCoroutine = StartCoroutine(ManageHealthBars());
        }

        Debug.Log("[HealthBarManager] Initialized and started managing health bars");
    }

    private void InitializePool()
    {
        if (!EnableHealthBarPooling || EnemyHealthBarPrefab == null) return;

        for (int i = 0; i < InitialPoolSize; i++)
        {
            CreatePooledHealthBar();
        }

        Debug.Log($"[HealthBarManager] Health bar pool initialized with {InitialPoolSize} instances");
    }

    private GameObject CreatePooledHealthBar()
    {
        var healthBarObj = Instantiate(EnemyHealthBarPrefab, transform);
        healthBarObj.SetActive(false);
        _healthBarPool.Enqueue(healthBarObj);
        return healthBarObj;
    }

    private GameObject GetPooledHealthBar()
    {
        if (_healthBarPool.Count > 0)
        {
            GameObject pooledBar = _healthBarPool.Dequeue();
            Debug.Log($"[HealthBarManager] Retrieved pooled health bar: {pooledBar.name}");
            return pooledBar;
        }
        else if (_healthBarPool.Count + _activeHealthBars.Count < MaxPoolSize)
        {
            Debug.Log("[HealthBarManager] Pool empty, creating new health bar for pool");
            GameObject newBar = Instantiate(EnemyHealthBarPrefab, transform);
            newBar.SetActive(false);
            return newBar;
        }
        else
        {
            Debug.LogWarning("[HealthBarManager] Health bar pool exhausted!");
            return null;
        }
    }

    private void ReturnToPool(GameObject healthBarObj)
    {
        if (healthBarObj == null) return;

        healthBarObj.SetActive(false);
        healthBarObj.transform.SetParent(transform);
        _healthBarPool.Enqueue(healthBarObj);
    }

    private IEnumerator ManageHealthBars()
    {
        while (true)
        {
            yield return new WaitForSeconds(UpdateRate);

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main ?? FindObjectOfType<Camera>();
                continue;
            }

            // Find all enemies in the scene
            EnemyBase[] allEnemies = FindObjectsOfType<EnemyBase>();
            
            // Debug logging for missing health bars
            if (allEnemies.Length != _activeHealthBars.Count)
            {
                Debug.Log($"[HealthBarManager] Mismatch: {allEnemies.Length} enemies found, {_activeHealthBars.Count} health bars active");
                foreach (var enemy in allEnemies)
                {
                    if (!_activeHealthBars.ContainsKey(enemy))
                    {
                        Debug.Log($"[HealthBarManager] Missing health bar for: {enemy.EnemyName} at {enemy.transform.position}");
                    }
                }
            }
            
            // Track which enemies we've processed
            HashSet<EnemyBase> processedEnemies = new HashSet<EnemyBase>();

            // Create health bars for new enemies
            foreach (var enemy in allEnemies)
            {
                processedEnemies.Add(enemy);

                if (!_activeHealthBars.ContainsKey(enemy))
                {
                    CreateHealthBarForEnemy(enemy);
                }
                else
                {
                    // Update visibility for existing health bars
                    UpdateHealthBarVisibility(enemy, _activeHealthBars[enemy]);
                }
            }

            // Remove health bars for destroyed enemies
            var enemiesToRemove = new List<EnemyBase>();
            foreach (var kvp in _activeHealthBars)
            {
                if (kvp.Key == null || !processedEnemies.Contains(kvp.Key))
                {
                    enemiesToRemove.Add(kvp.Key);
                }
            }

            foreach (var enemy in enemiesToRemove)
            {
                RemoveHealthBarForEnemy(enemy);
            }
        }
    }

    private void CreateHealthBarForEnemy(EnemyBase enemy)
    {
        if (enemy == null)
        {
            Debug.LogError("[HealthBarManager] Enemy is null!");
            return;
        }
        
        if (EnemyHealthBarPrefab == null)
        {
            Debug.LogError("[HealthBarManager] EnemyHealthBarPrefab is null! Make sure it's assigned in the inspector or created by AutoSceneSetup.");
            return;
        }

        // Check if this enemy already has a health bar to prevent duplicates
        if (_activeHealthBars.ContainsKey(enemy))
        {
            Debug.LogWarning($"[HealthBarManager] Enemy {enemy.EnemyName} (ID: {enemy.GetInstanceID()}) already has a health bar!");
            return;
        }

        Debug.Log($"[HealthBarManager] Creating health bar for enemy: {enemy.EnemyName} (ID: {enemy.GetInstanceID()})");

        GameObject healthBarObj;
        
        if (EnableHealthBarPooling)
        {
            healthBarObj = GetPooledHealthBar();
            if (healthBarObj == null)
            {
                Debug.LogWarning("[HealthBarManager] Failed to get pooled health bar, creating new one");
                healthBarObj = Instantiate(EnemyHealthBarPrefab);
            }
            else
            {
                Debug.Log($"[HealthBarManager] Got pooled health bar: {healthBarObj.name}");
            }
            healthBarObj.SetActive(true);
        }
        else
        {
            healthBarObj = Instantiate(EnemyHealthBarPrefab);
            Debug.Log($"[HealthBarManager] Instantiated new health bar: {healthBarObj.name}");
        }

        // Give it a unique name for debugging
        healthBarObj.name = $"HealthBar_{enemy.EnemyName}_{enemy.GetInstanceID()}";

        // Get the health bar component
        var healthBar = healthBarObj.GetComponent<EnemyHealthBar>();
        if (healthBar == null)
        {
            Debug.LogError("[HealthBarManager] EnemyHealthBar component not found on prefab!");
            if (EnableHealthBarPooling)
            {
                ReturnToPool(healthBarObj);
            }
            else
            {
                Destroy(healthBarObj);
            }
            return;
        }

        // Initialize the health bar BEFORE adding to dictionary
        Debug.Log($"[HealthBarManager] Initializing health bar for {enemy.EnemyName} (ID: {enemy.GetInstanceID()})");
        healthBar.Initialize(enemy);
        
        // Add to active health bars dictionary
        _activeHealthBars[enemy] = healthBar;

        // Subscribe to enemy death to clean up
        enemy.OnEnemyDeath += OnEnemyDeath;

        // Verify the health bar was properly created and is visible
        bool isActive = healthBarObj.activeSelf;
        bool hasCanvas = healthBar.HealthBarCanvas != null;
        bool hasSlider = healthBar.HealthSlider != null;
        Vector3 position = healthBarObj.transform.position;
        
        Debug.Log($"[HealthBarManager] Health bar created for {enemy.EnemyName} (ID: {enemy.GetInstanceID()}): Active={isActive}, HasCanvas={hasCanvas}, HasSlider={hasSlider}, Position={position}");
        Debug.Log($"[HealthBarManager] Total active health bars: {_activeHealthBars.Count}");
        
        if (!isActive || !hasCanvas || !hasSlider)
        {
            Debug.LogError($"[HealthBarManager] Health bar for {enemy.EnemyName} is not properly configured! Active={isActive}, Canvas={hasCanvas}, Slider={hasSlider}");
        }
    }

    private void RemoveHealthBarForEnemy(EnemyBase enemy)
    {
        if (!_activeHealthBars.TryGetValue(enemy, out EnemyHealthBar healthBar))
        {
            Debug.LogWarning($"[HealthBarManager] Tried to remove health bar for {enemy?.EnemyName} but it wasn't in active dictionary");
            return;
        }

        Debug.Log($"[HealthBarManager] Removing health bar for enemy: {(enemy != null ? enemy.EnemyName : "Destroyed Enemy")} (ID: {enemy?.GetInstanceID()})");

        // Unsubscribe from enemy events
        if (enemy != null)
        {
            enemy.OnEnemyDeath -= OnEnemyDeath;
        }

        // Clear the health bar's target to prevent event issues
        if (healthBar != null && healthBar.gameObject != null)
        {
            // Reset the health bar before returning to pool
            healthBar.ResetHealthBar();
            healthBar.gameObject.name = "EnemyHealthBarPrefab(Clone)";
            
            // Return to pool or destroy
            if (EnableHealthBarPooling)
            {
                ReturnToPool(healthBar.gameObject);
            }
            else
            {
                Destroy(healthBar.gameObject);
            }
        }

        // Remove from active dictionary
        _activeHealthBars.Remove(enemy);

        Debug.Log($"[HealthBarManager] Health bar removed. Total active health bars: {_activeHealthBars.Count}");
    }

    private void UpdateHealthBarVisibility(EnemyBase enemy, EnemyHealthBar healthBar)
    {
        if (enemy == null || healthBar == null || _mainCamera == null) return;

        bool shouldBeVisible = true;

        // Check distance
        float distance = Vector3.Distance(_mainCamera.transform.position, enemy.transform.position);
        if (distance > MaxViewDistance)
        {
            shouldBeVisible = false;
        }

        // Check if behind camera
        Vector3 directionToEnemy = enemy.transform.position - _mainCamera.transform.position;
        if (Vector3.Dot(_mainCamera.transform.forward, directionToEnemy) < 0)
        {
            shouldBeVisible = false;
        }

        // Check occlusion
        if (shouldBeVisible && HideWhenBehindObjects)
        {
            if (Physics.Linecast(_mainCamera.transform.position, enemy.transform.position + healthBar.WorldOffset, OcclusionMask))
            {
                shouldBeVisible = false;
            }
        }

        // Update visibility
        healthBar.SetVisibility(shouldBeVisible);
    }

    private void OnEnemyDeath(EnemyBase enemy)
    {
        // Remove health bar when enemy dies
        RemoveHealthBarForEnemy(enemy);
    }

    public void CreateHealthBarForEnemy(EnemyBase enemy, bool forceCreate = false)
    {
        if (enemy == null) return;

        if (forceCreate || !_activeHealthBars.ContainsKey(enemy))
        {
            CreateHealthBarForEnemy(enemy);
        }
    }

    public void RemoveHealthBarForEnemy(EnemyBase enemy, bool immediate = false)
    {
        if (enemy == null) return;

        if (immediate)
        {
            RemoveHealthBarForEnemy(enemy);
        }
        else
        {
            // Delay removal to allow death animations
            StartCoroutine(DelayedRemoval(enemy, 2f));
        }
    }

    private IEnumerator DelayedRemoval(EnemyBase enemy, float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveHealthBarForEnemy(enemy);
    }

    public void SetHealthBarVisibility(bool visible)
    {
        foreach (var healthBar in _activeHealthBars.Values)
        {
            if (healthBar != null)
            {
                healthBar.SetVisibility(visible);
            }
        }
    }

    public void SetMaxViewDistance(float distance)
    {
        MaxViewDistance = distance;
    }

    public void SetAutoManagement(bool autoManage)
    {
        AutoManageEnemyHealthBars = autoManage;
        
        if (autoManage && _updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(ManageHealthBars());
        }
        else if (!autoManage && _updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
    }

    public int GetActiveHealthBarCount()
    {
        return _activeHealthBars.Count;
    }

    public int GetPooledHealthBarCount()
    {
        return _healthBarPool.Count;
    }

    private void OnDestroy()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
        }

        // Clean up all health bars
        foreach (var healthBar in _activeHealthBars.Values)
        {
            if (healthBar != null)
            {
                Destroy(healthBar.gameObject);
            }
        }
        _activeHealthBars.Clear();

        // Clean up pool
        while (_healthBarPool.Count > 0)
        {
            var pooledObj = _healthBarPool.Dequeue();
            if (pooledObj != null)
            {
                Destroy(pooledObj);
            }
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}