using UnityEngine;

/// <summary>
/// Debug utility to help troubleshoot health bar system
/// </summary>
public class HealthBarDebugger : MonoBehaviour
{
    [Header("Debug Controls")]
    public bool EnableDebugLogs = false; // Disabled by default for production
    public bool ShowGUI = false; // Disabled by default for production
    public KeyCode ToggleDebugKey = KeyCode.F1;
    public KeyCode CreateTestEnemyKey = KeyCode.E;
    public KeyCode DamageEnemyKey = KeyCode.D;
    public KeyCode DamagePlayerKey = KeyCode.P;

    private void Update()
    {
        if (Input.GetKeyDown(ToggleDebugKey))
        {
            ShowGUI = !ShowGUI;
            EnableDebugLogs = ShowGUI;
        }

        if (ShowGUI)
        {
            if (Input.GetKeyDown(CreateTestEnemyKey))
            {
                CreateTestEnemy();
            }

            if (Input.GetKeyDown(DamageEnemyKey))
            {
                DamageRandomEnemy();
            }

            if (Input.GetKeyDown(DamagePlayerKey))
            {
                DamagePlayer();
            }
        }
    }

    private void Start()
    {
        if (EnableDebugLogs)
        {
            Debug.Log("=== Health Bar Debug Info ===");
            
            // Check for HealthBarManager
            var healthBarManager = FindObjectOfType<HealthBarManager>();
            Debug.Log($"HealthBarManager found: {healthBarManager != null}");
            if (healthBarManager != null)
            {
                Debug.Log($"HealthBarManager EnemyHealthBarPrefab assigned: {healthBarManager.EnemyHealthBarPrefab != null}");
                Debug.Log($"HealthBarManager PlayerHealthBarPrefab assigned: {healthBarManager.PlayerHealthBarPrefab != null}");
                Debug.Log($"HealthBarManager AutoManageEnemyHealthBars: {healthBarManager.AutoManageEnemyHealthBars}");
                Debug.Log($"HealthBarManager active health bars: {healthBarManager.GetActiveHealthBarCount()}");
                Debug.Log($"HealthBarManager pooled health bars: {healthBarManager.GetPooledHealthBarCount()}");
                
                if (healthBarManager.EnemyHealthBarPrefab == null)
                {
                    Debug.LogError("HealthBarManager.EnemyHealthBarPrefab is NULL! This is why health bars aren't appearing.");
                }
            }

            // Check for enemies
            var enemies = FindObjectsOfType<EnemyBase>();
            Debug.Log($"Enemies in scene: {enemies.Length}");
            foreach (var enemy in enemies)
            {
                Debug.Log($"Enemy: {enemy.EnemyName} at {enemy.transform.position}, Health: {enemy.GetHealthPercentage():P0}");
            }

            // Check for PlayerHealthUI
            var playerHealthUI = FindObjectOfType<PlayerHealthUI>();
            Debug.Log($"PlayerHealthUI found: {playerHealthUI != null}");

            // Check for UIManager
            var uiManager = FindObjectOfType<UIManager>();
            Debug.Log($"UIManager found: {uiManager != null}");

            Debug.Log("=== Controls ===");
            Debug.Log($"Press '{CreateTestEnemyKey}' to create test enemy");
            Debug.Log($"Press '{DamageEnemyKey}' to damage random enemy");
            Debug.Log($"Press '{DamagePlayerKey}' to damage player");
        }
    }

    private void CreateTestEnemy()
    {
        Debug.Log("[HealthBarDebugger] Creating test enemy...");

        // Create enemy GameObject
        GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemyObj.name = "DebugTestEnemy";
        
        // Position it near the player
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            Vector3 spawnPos = player.transform.position + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
            spawnPos.y = 0.5f; // Ground level
            enemyObj.transform.position = spawnPos;
        }
        else
        {
            enemyObj.transform.position = new Vector3(Random.Range(-10f, 10f), 0.5f, Random.Range(-10f, 10f));
        }

        // Add EnemyBase component
        var enemyBase = enemyObj.AddComponent<EnemyBase>();
        enemyBase.EnemyName = "Debug Enemy";
        enemyBase.EnemyType = "Debug";
        enemyBase.Level = Random.Range(1, 5);
        enemyBase.BaseHealth = 100f;
        enemyBase.BaseDamage = 15f;

        // Make it red for visibility
        var renderer = enemyObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.red;
        }

        Debug.Log($"[HealthBarDebugger] Created test enemy at {enemyObj.transform.position}");
    }

    private void DamageRandomEnemy()
    {
        var enemies = FindObjectsOfType<EnemyBase>();
        if (enemies.Length == 0)
        {
            Debug.Log("[HealthBarDebugger] No enemies found to damage");
            return;
        }

        var randomEnemy = enemies[Random.Range(0, enemies.Length)];
        float damage = Random.Range(10f, 30f);
        
        Debug.Log($"[HealthBarDebugger] Damaging {randomEnemy.EnemyName} for {damage} damage");
        randomEnemy.TakeDamage(damage, gameObject);
    }

    private void DamagePlayer()
    {
        var playerStats = FindObjectOfType<ClientPlayerStats>();
        if (playerStats != null)
        {
            int damage = Random.Range(5, 25);
            Debug.Log($"[HealthBarDebugger] Damaging player for {damage} damage");
            playerStats.TestHealthChange(-damage, "Debug Damage");
        }
        else
        {
            Debug.LogWarning("[HealthBarDebugger] ClientPlayerStats not found");
        }
    }

    private void OnGUI()
    {
        if (!ShowGUI) return;

        GUILayout.BeginArea(new Rect(Screen.width - 350, 10, 340, 300));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("Health Bar Debugger");
        
        if (GUILayout.Button("Create Test Enemy"))
        {
            CreateTestEnemy();
        }
        
        if (GUILayout.Button("Damage Random Enemy"))
        {
            DamageRandomEnemy();
        }
        
        if (GUILayout.Button("Damage Player"))
        {
            DamagePlayer();
        }

        GUILayout.Space(10);

        var healthBarManager = FindObjectOfType<HealthBarManager>();
        if (healthBarManager != null)
        {
            GUILayout.Label($"Active Health Bars: {healthBarManager.GetActiveHealthBarCount()}");
            GUILayout.Label($"Pooled Health Bars: {healthBarManager.GetPooledHealthBarCount()}");
            GUILayout.Label($"Enemy Prefab: {(healthBarManager.EnemyHealthBarPrefab != null ? "✓" : "✗")}");
            GUILayout.Label($"Auto Management: {(healthBarManager.AutoManageEnemyHealthBars ? "✓" : "✗")}");
        }
        else
        {
            GUILayout.Label("HealthBarManager: NOT FOUND");
        }

        var enemies = FindObjectsOfType<EnemyBase>();
        GUILayout.Label($"Enemies in Scene: {enemies.Length}");
        
        if (GUILayout.Button("Debug Health System"))
        {
            DebugHealthSystem();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DebugHealthSystem()
    {
        Debug.Log("=== HEALTH SYSTEM DEBUG ===");
        
        var healthBarManager = FindObjectOfType<HealthBarManager>();
        if (healthBarManager != null)
        {
            Debug.Log($"HealthBarManager found: {healthBarManager.name}");
            Debug.Log($"EnemyHealthBarPrefab: {healthBarManager.EnemyHealthBarPrefab}");
            Debug.Log($"AutoManageEnemyHealthBars: {healthBarManager.AutoManageEnemyHealthBars}");
            Debug.Log($"EnableHealthBarPooling: {healthBarManager.EnableHealthBarPooling}");
            Debug.Log($"Active Health Bars: {healthBarManager.GetActiveHealthBarCount()}");
            Debug.Log($"Pooled Health Bars: {healthBarManager.GetPooledHealthBarCount()}");
            
            if (healthBarManager.EnemyHealthBarPrefab != null)
            {
                var enemyHealthBarComponent = healthBarManager.EnemyHealthBarPrefab.GetComponent<EnemyHealthBar>();
                Debug.Log($"Enemy Prefab has EnemyHealthBar component: {enemyHealthBarComponent != null}");
            }

            // Check each health bar individually
            var allHealthBars = FindObjectsOfType<EnemyHealthBar>();
            Debug.Log($"Found {allHealthBars.Length} EnemyHealthBar components in scene:");
            foreach (var healthBar in allHealthBars)
            {
                if (healthBar.gameObject.activeSelf)
                {
                    var canvasGroup = healthBar.GetComponent<CanvasGroup>();
                    float alpha = canvasGroup != null ? canvasGroup.alpha : 1f;
                    Debug.Log($"  HealthBar: {healthBar.name}, Active: {healthBar.gameObject.activeSelf}, Alpha: {alpha}, Position: {healthBar.transform.position}");
                }
            }
        }
        else
        {
            Debug.LogError("HealthBarManager NOT FOUND!");
        }
        
        var enemies = FindObjectsOfType<EnemyBase>();
        Debug.Log($"Enemies in scene: {enemies.Length}");
        
        foreach (var enemy in enemies)
        {
            Debug.Log($"Enemy: {enemy.EnemyName} at {enemy.transform.position}, Health: {enemy.GetHealthPercentage():P0}");
        }
        
        Debug.Log("=== END DEBUG ===");
    }
}