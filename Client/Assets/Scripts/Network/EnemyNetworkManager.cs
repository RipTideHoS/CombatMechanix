using System.Collections.Generic;
using UnityEngine;
using static NetworkMessages;

/// <summary>
/// Client-side enemy network management
/// Handles enemy synchronization from server, creating/updating enemy GameObjects
/// </summary>
public class EnemyNetworkManager : MonoBehaviour
{
    [Header("Enemy Settings")]
    public Material EnemyMaterial;
    public bool EnableDebugLogging = true;
    
    private Dictionary<string, EnemyBase> _networkEnemies = new Dictionary<string, EnemyBase>();
    private NetworkManager _networkManager;
    
    private void Start()
    {
        // Find the NetworkManager to subscribe to enemy events
        _networkManager = FindObjectOfType<NetworkManager>();
        if (_networkManager != null)
        {
            SubscribeToEnemyEvents();
            Debug.Log("[EnemyNetworkManager] Subscribed to enemy network events");
        }
        else
        {
            Debug.LogError("[EnemyNetworkManager] NetworkManager not found!");
        }
        
        // Create default enemy material if not set
        if (EnemyMaterial == null)
        {
            EnemyMaterial = new Material(Shader.Find("Standard"));
            EnemyMaterial.color = Color.red;
        }
    }
    
    private void SubscribeToEnemyEvents()
    {
        // Subscribe to enemy-related network events (static events)
        NetworkManager.OnEnemySpawn += HandleEnemySpawn;
        NetworkManager.OnEnemyUpdate += HandleEnemyUpdate;
        NetworkManager.OnEnemyDamage += HandleEnemyDamage;
        NetworkManager.OnEnemyDeath += HandleEnemyDeath;
        NetworkManager.OnLevelComplete += HandleLevelCleanup;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events (static events)
        NetworkManager.OnEnemySpawn -= HandleEnemySpawn;
        NetworkManager.OnEnemyUpdate -= HandleEnemyUpdate;
        NetworkManager.OnEnemyDamage -= HandleEnemyDamage;
        NetworkManager.OnEnemyDeath -= HandleEnemyDeath;
        NetworkManager.OnLevelComplete -= HandleLevelCleanup;
    }
    
    /// <summary>
    /// Handle enemy spawn messages from server
    /// </summary>
    private void HandleEnemySpawn(EnemySpawnMessage spawnMessage)
    {
        foreach (var enemyState in spawnMessage.Enemies)
        {
            // Skip dead enemies — don't create objects just to immediately deactivate them
            if (!enemyState.IsAlive && !_networkEnemies.ContainsKey(enemyState.EnemyId))
                continue;

            CreateOrUpdateNetworkEnemy(enemyState);
        }
        
        if (EnableDebugLogging)
        {
            Debug.Log($"[EnemyNetworkManager] Spawned/updated {spawnMessage.Enemies.Count} enemies");
        }
    }
    
    /// <summary>
    /// Handle enemy update messages from server
    /// </summary>
    private void HandleEnemyUpdate(EnemyUpdateMessage updateMessage)
    {
        foreach (var enemyState in updateMessage.Enemies)
        {
            UpdateNetworkEnemy(enemyState);
        }
        
        if (EnableDebugLogging)
        {
            Debug.Log($"[EnemyNetworkManager] Updated {updateMessage.Enemies.Count} enemies");
        }
    }
    
    /// <summary>
    /// Handle enemy damage messages from server
    /// </summary>
    private void HandleEnemyDamage(EnemyDamageMessage damageMessage)
    {
        if (_networkEnemies.TryGetValue(damageMessage.EnemyId, out var enemy))
        {
            // Apply visual damage effects
            enemy.TakeDamage(damageMessage.Damage);
            
            if (EnableDebugLogging)
            {
                Debug.Log($"[EnemyNetworkManager] Enemy {damageMessage.EnemyId} took {damageMessage.Damage} damage");
            }
        }
    }
    
    /// <summary>
    /// Handle enemy death messages from server
    /// </summary>
    private void HandleEnemyDeath(EnemyDeathMessage deathMessage)
    {
        Debug.Log($"[EnemyNetworkManager] HandleEnemyDeath called for enemy {deathMessage.EnemyId}");
        
        if (_networkEnemies.TryGetValue(deathMessage.EnemyId, out var enemy))
        {
            Debug.Log($"[EnemyNetworkManager] Found enemy {deathMessage.EnemyId}, applying death");
            
            // Trigger death on the enemy (visual effects, disable collider, etc.)
            enemy.TakeDamage(1000f); // Force death with large damage amount
            
            // Optionally hide the enemy GameObject instead of just marking it dead
            enemy.gameObject.SetActive(false);
            
            if (EnableDebugLogging)
            {
                Debug.Log($"[EnemyNetworkManager] Enemy {deathMessage.EnemyId} was killed by {deathMessage.KillerId}");
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyNetworkManager] Enemy {deathMessage.EnemyId} not found in _networkEnemies for death handling");
        }
    }
    
    /// <summary>
    /// Create or update a network enemy GameObject
    /// </summary>
    private void CreateOrUpdateNetworkEnemy(EnemyState enemyState)
    {
        if (_networkEnemies.TryGetValue(enemyState.EnemyId, out var existingEnemy))
        {
            // Update existing enemy
            UpdateNetworkEnemy(enemyState);
        }
        else
        {
            // Create new enemy
            CreateNetworkEnemy(enemyState);
        }
    }
    
    /// <summary>
    /// Create a new network enemy GameObject
    /// </summary>
    private void CreateNetworkEnemy(EnemyState enemyState)
    {
        // Create the enemy GameObject with die shape based on level
        GameObject enemyObj = EnemyShapeFactory.CreateEnemyShape(enemyState.Level);
        enemyObj.name = $"NetworkEnemy_{enemyState.EnemyName}_{enemyState.EnemyId}";
        
        // Position the enemy (rolling rotation is handled by EnemyBase.Update)
        Vector3 position = enemyState.Position.ToVector3();
        enemyObj.transform.position = position;
        
        // Add EnemyBase component
        EnemyBase enemyBase = enemyObj.AddComponent<EnemyBase>();
        
        // Configure enemy from network state
        enemyBase.EnemyId = enemyState.EnemyId;
        enemyBase.EnemyName = enemyState.EnemyName;
        enemyBase.EnemyType = enemyState.EnemyType;
        enemyBase.Level = enemyState.Level;
        enemyBase.BaseHealth = enemyState.MaxHealth;
        enemyBase.BaseDamage = enemyState.Damage;
        
        // Apply visual styling
        Renderer renderer = enemyObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = EnemyMaterial;
        }
        
        // Ensure collision is enabled for targeting
        Collider collider = enemyObj.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false; // Solid collision for targeting
        }
        
        // Store in dictionary
        _networkEnemies[enemyState.EnemyId] = enemyBase;
        
        if (EnableDebugLogging)
        {
            Debug.Log($"[EnemyNetworkManager] Created network enemy: {enemyState.EnemyName} (ID: {enemyState.EnemyId}) at {position}");
        }
        
        // Update with current state
        UpdateNetworkEnemyState(enemyBase, enemyState);
    }
    
    /// <summary>
    /// Update an existing network enemy
    /// </summary>
    private void UpdateNetworkEnemy(EnemyState enemyState)
    {
        if (_networkEnemies.TryGetValue(enemyState.EnemyId, out var enemy))
        {
            UpdateNetworkEnemyState(enemy, enemyState);
        }
    }
    
    /// <summary>
    /// Update enemy GameObject with server state
    /// </summary>
    private void UpdateNetworkEnemyState(EnemyBase enemy, EnemyState enemyState)
    {
        // Update position (rolling rotation is handled by EnemyBase.Update)
        Vector3 newPosition = enemyState.Position.ToVector3();
        enemy.transform.position = newPosition;
        
        // Update health (this will trigger UI updates and visual effects)
        float currentHealthPercent = enemy.GetHealthPercentage();
        float newHealthPercent = (float)enemyState.Health / enemyState.MaxHealth;
        
        if (Mathf.Abs(currentHealthPercent - newHealthPercent) > 0.01f)
        {
            // Calculate health difference and apply
            float targetHealth = enemyState.Health;
            float currentHealth = enemy.GetComponent<EnemyBase>().GetHealthPercentage() * enemyState.MaxHealth;
            
            if (targetHealth < currentHealth)
            {
                // Enemy took damage
                enemy.TakeDamage(currentHealth - targetHealth);
            }
            else if (targetHealth > currentHealth)
            {
                // Enemy was healed
                enemy.Heal(targetHealth - currentHealth);
            }
        }
        
        // Update alive state
        if (!enemyState.IsAlive && enemy.IsAlive())
        {
            // Force death if server says enemy is dead but client thinks it's alive
            Debug.Log($"[EnemyNetworkManager] Server says {enemyState.EnemyId} is dead, forcing death");
            enemy.TakeDamage(1000f); // Ensure death
            enemy.gameObject.SetActive(false); // Hide the enemy
        }
        else if (enemyState.IsAlive && !enemy.IsAlive())
        {
            // Respawn if server says enemy is alive but client thinks it's dead
            Debug.Log($"[EnemyNetworkManager] Server says {enemyState.EnemyId} is alive, respawning");
            enemy.Reset();
            enemy.gameObject.SetActive(true); // Show the enemy again
        }
        else if (enemyState.IsAlive && !enemy.gameObject.activeInHierarchy)
        {
            // Make sure alive enemies are visible
            Debug.Log($"[EnemyNetworkManager] Ensuring alive enemy {enemyState.EnemyId} is visible");
            enemy.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Remove a network enemy (when it's permanently destroyed)
    /// </summary>
    public void RemoveNetworkEnemy(string enemyId)
    {
        if (_networkEnemies.TryGetValue(enemyId, out var enemy))
        {
            _networkEnemies.Remove(enemyId);
            
            if (enemy != null && enemy.gameObject != null)
            {
                Destroy(enemy.gameObject);
            }
            
            if (EnableDebugLogging)
            {
                Debug.Log($"[EnemyNetworkManager] Removed network enemy: {enemyId}");
            }
        }
    }
    
    /// <summary>
    /// Get all current network enemies
    /// </summary>
    public Dictionary<string, EnemyBase> GetNetworkEnemies()
    {
        return new Dictionary<string, EnemyBase>(_networkEnemies);
    }
    
    /// <summary>
    /// Get network enemy by ID
    /// </summary>
    public EnemyBase GetNetworkEnemy(string enemyId)
    {
        _networkEnemies.TryGetValue(enemyId, out var enemy);
        return enemy;
    }
    
    /// <summary>
    /// Handle level completion by cleaning up dead enemies and stale combat objects
    /// </summary>
    private void HandleLevelCleanup(LevelCompleteMessage message)
    {
        Debug.Log($"[EnemyNetworkManager] Level {message.completedLevel} complete - performing cleanup");

        int destroyed = 0;
        var toRemove = new List<string>();

        // Destroy all inactive (dead) enemy GameObjects and remove from dictionary
        foreach (var kvp in _networkEnemies)
        {
            if (kvp.Value == null || kvp.Value.gameObject == null || !kvp.Value.gameObject.activeInHierarchy)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                    Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
                destroyed++;
            }
        }

        foreach (var id in toRemove)
            _networkEnemies.Remove(id);

        // Clean up stale dynamic combat objects (projectiles, grenades, effects)
        CleanupCombatObjects();

        Debug.Log($"[EnemyNetworkManager] Cleanup complete: destroyed {destroyed} dead enemies, {_networkEnemies.Count} remaining");
    }

    /// <summary>
    /// Destroy leftover dynamic combat objects (projectiles, grenades, swipe effects)
    /// </summary>
    private void CleanupCombatObjects()
    {
        int cleaned = 0;

        // Projectiles
        foreach (var obj in FindObjectsOfType<Projectile>())
        {
            Destroy(obj.gameObject);
            cleaned++;
        }

        // Grenades
        foreach (var obj in FindObjectsOfType<CombatMechanix.Unity.Grenade>())
        {
            Destroy(obj.gameObject);
            cleaned++;
        }

        // Melee swipe effects
        foreach (var obj in FindObjectsOfType<MeleeSwipeEffect>())
        {
            Destroy(obj.gameObject);
            cleaned++;
        }

        if (cleaned > 0)
            Debug.Log($"[EnemyNetworkManager] Cleaned up {cleaned} combat objects");
    }

    /// <summary>
    /// Clear all network enemies (for disconnection/cleanup)
    /// </summary>
    public void ClearAllNetworkEnemies()
    {
        foreach (var kvp in _networkEnemies)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        
        _networkEnemies.Clear();
        
        if (EnableDebugLogging)
        {
            Debug.Log("[EnemyNetworkManager] Cleared all network enemies");
        }
    }
}